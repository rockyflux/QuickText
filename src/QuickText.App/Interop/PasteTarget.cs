using System.Text;

namespace QuickText.App.Interop;

/// <summary>
/// Remembers the last window we can safely paste into. Summoning from the tray / a second
/// launch / our own UI often makes GetForegroundWindow return the taskbar or ourselves — neither
/// is a paste target — so a raw capture at panel-show time silently degrades every send to
/// "copy only". This tracker keeps the last real app window and skips shell chrome.
/// </summary>
internal static class PasteTarget
{
    private static IntPtr _last;
    private static IntPtr _hook;
    private static NativeMethods.WinEventProc? _proc;   // keep alive for the native callback

    public static void StartTracking()
    {
        if (_hook != IntPtr.Zero) return;
        _proc = OnForeground;
        _hook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _proc, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);
        // Seed from whatever is focused at startup (often nothing useful yet).
        Remember(NativeMethods.GetForegroundWindow());
    }

    public static void StopTracking()
    {
        if (_hook == IntPtr.Zero) return;
        NativeMethods.UnhookWinEvent(_hook);
        _hook = IntPtr.Zero;
        _proc = null;
    }

    private static void OnForeground(IntPtr hook, uint ev, IntPtr hwnd, int idObj, int idChild, uint thread, uint time) =>
        Remember(hwnd);

    /// <summary>Record <paramref name="hwnd"/> when it is a live foreign app window (not us, not
    /// the taskbar/desktop). Safe to call from any thread that may touch HWND state.</summary>
    public static void Remember(IntPtr hwnd)
    {
        if (!IsPasteable(hwnd)) return;
        _last = hwnd;
    }

    /// <summary>Best paste destination right now: the current foreground if it is pasteable,
    /// otherwise the last remembered one. Zero when nothing usable is known.</summary>
    public static IntPtr Capture()
    {
        var fg = NativeMethods.GetForegroundWindow();
        if (IsPasteable(fg))
        {
            _last = fg;
            return fg;
        }
        // Re-validate: the window may have been destroyed or turned into shell chrome since we
        // remembered it (IsPasteable includes IsWindow).
        if (!IsPasteable(_last)) _last = IntPtr.Zero;
        return _last;
    }

    /// <summary>True when Ctrl+V into this HWND is meaningful — another process's live window,
    /// and not shell chrome (taskbar / desktop / notification flyout) that only eats the paste.</summary>
    public static bool IsPasteable(IntPtr hwnd)
    {
        if (!NativeMethods.IsForeignWindow(hwnd)) return false;
        if (IsShellChrome(hwnd)) return false;
        return true;
    }

    private static bool IsShellChrome(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        if (NativeMethods.GetClassName(hwnd, sb, sb.Capacity) <= 0) return false;
        // Taskbar / desktop only — do NOT blanket-reject Windows.UI.Core.CoreWindow: that class
        // is also the top-level frame of many UWP/WinUI apps, and rejecting it sent paste to the
        // previous Win32 window (or to copy-only) whenever the user was in Settings / Store apps.
        return sb.ToString() switch
        {
            "Shell_TrayWnd" => true,
            "Shell_SecondaryTrayWnd" => true,
            "NotifyIconOverflowWindow" => true,
            "Progman" => true,
            "WorkerW" => true,
            "ForegroundStaging" => true,
            _ => false,
        };
    }
}
