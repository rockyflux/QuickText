using System.Runtime.InteropServices;

namespace QuickText.App.Interop;

internal static class NativeMethods
{
    public const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    public const uint MSGFLT_ALLOW = 1;

    /// <summary>Per-window UIPI exemption: lets a LOWER-integrity process post the given message to
    /// this window. Without it Windows silently drops such posts to an elevated process's windows.</summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool ChangeWindowMessageFilterEx(IntPtr hWnd, uint msg, uint action, IntPtr pChangeFilterStruct);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    /// <summary>True when <paramref name="hwnd"/> is a live window owned by another process —
    /// the only kind of paste target that makes sense (never ourselves, never a stale HWND).</summary>
    public static bool IsForeignWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return false;
        GetWindowThreadProcessId(hwnd, out uint pid);
        return pid != 0 && pid != (uint)Environment.ProcessId;
    }

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    // --- foreground-change hook: reliably auto-hide the panel when another app takes the
    // foreground, monitor-agnostic (WPF's Window.Deactivated can miss cross-monitor changes and
    // never fires if the panel was shown without truly activating). ---
    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    public const uint WINEVENT_OUTOFCONTEXT = 0x0000;   // callback delivered on our thread's message loop
    public delegate void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);
    [DllImport("user32.dll")]
    public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventProc lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);
    [DllImport("user32.dll")]
    public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    public const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern bool AllowSetForegroundWindow(uint dwProcessId);

    private const uint ASFW_ANY = 0xFFFFFFFF;

    /// <summary>
    /// Give the foreground to <paramref name="hwnd"/> reliably. Windows' foreground lock only
    /// yields when we attach to the thread that CURRENTLY holds the grant (same trick as
    /// <see cref="StealForeground"/>). Attaching to the *target* thread — the old approach —
    /// does nothing once our panel has Hidden and some other app holds the lock, so
    /// SetForegroundWindow was refused and every send timed out into "copy only".
    /// </summary>
    public static void ForceForeground(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return;
        if (GetForegroundWindow() == hwnd) return;

        if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);

        uint self = GetCurrentThreadId();
        IntPtr fg = GetForegroundWindow();
        uint fgThread = fg != IntPtr.Zero ? GetWindowThreadProcessId(fg, out _) : 0;
        uint targetThread = GetWindowThreadProcessId(hwnd, out uint targetPid);

        // Unlock for the target process when we still can (hotkey / recent input grant).
        if (targetPid != 0) AllowSetForegroundWindow(targetPid);
        else AllowSetForegroundWindow(ASFW_ANY);

        // We already hold the grant — a bare set is enough (and AttachThreadInput to ourselves
        // is a no-op that some Windows builds dislike).
        if (fgThread == 0 || fgThread == self)
        {
            SetForegroundWindow(hwnd);
            BringWindowToTop(hwnd);
            return;
        }

        AttachThreadInput(self, fgThread, true);
        bool linkedTarget = false;
        try
        {
            // Bridge the grant-holder's queue to the target's when they differ, so focus can land
            // on a control inside the target process rather than just raising its top-level frame.
            if (targetThread != 0 && targetThread != self && targetThread != fgThread)
                linkedTarget = AttachThreadInput(fgThread, targetThread, true);
            SetForegroundWindow(hwnd);
            BringWindowToTop(hwnd);
        }
        finally
        {
            if (linkedTarget) AttachThreadInput(fgThread, targetThread, false);
            AttachThreadInput(self, fgThread, false);
        }
    }

    /// <summary>
    /// Bring one of OUR OWN windows (<paramref name="ownHwnd"/>) to the foreground from a
    /// background context — e.g. a low-level-hook summon that holds no WM_HOTKEY foreground
    /// grant. Attaches to the CURRENTLY-foreground thread (it holds the grant) then sets
    /// foreground. Prefer this over <see cref="ForceForeground"/> when raising ourselves: that
    /// helper also restores minimized targets and bridges input queues for a foreign paste host,
    /// which is unnecessary overhead for our own tool windows.
    /// </summary>
    public static void StealForeground(IntPtr ownHwnd)
    {
        if (ownHwnd == IntPtr.Zero) return;
        IntPtr fg = GetForegroundWindow();
        uint self = GetCurrentThreadId();
        uint fgThread = fg != IntPtr.Zero ? GetWindowThreadProcessId(fg, out _) : 0;
        if (fgThread == 0 || fgThread == self) { SetForegroundWindow(ownHwnd); return; }
        AttachThreadInput(self, fgThread, true);
        try { SetForegroundWindow(ownHwnd); }
        finally { AttachThreadInput(self, fgThread, false); }
    }

    // --- low-level keyboard hook ---
    public const int WH_KEYBOARD_LL = 13;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_KEYUP = 0x0101;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int WM_SYSKEYUP = 0x0105;

    // --- low-level mouse hook (clicks break the abbreviation token) ---
    public const int WH_MOUSE_LL = 14;
    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_RBUTTONDOWN = 0x0204;
    public const int WM_MBUTTONDOWN = 0x0207;
    public const int WM_XBUTTONDOWN = 0x020B;
    public const int WM_MOUSEWHEEL = 0x020A;
    public const int WM_MOUSEHWHEEL = 0x020E;

    public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    // --- SendInput for Ctrl+V and backspaces ---
    public const int INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint KEYEVENTF_UNICODE = 0x0004;

    /// <summary>Tag on every synthesized key event. Low-level hooks check it instead of relying
    /// only on the suppression flag: SendInput can deliver the hook callback after the flag is
    /// cleared, which used to let our own Ctrl+V consume the one-shot expansion undo.</summary>
    public static readonly IntPtr SelfInputMarker = (IntPtr)0x51545831;
    public const ushort VK_CONTROL = 0x11;
    public const ushort VK_V = 0x56;
    public const ushort VK_BACK = 0x08;
    public const ushort VK_RETURN = 0x0D;
    public const ushort VK_LEFT = 0x25;
    public const ushort VK_SHIFT = 0x10;
    public const ushort VK_CAPITAL = 0x14;
    public const ushort VK_LSHIFT = 0xA0;
    public const ushort VK_RSHIFT = 0xA1;

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT { public int type; public InputUnion U; }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HARDWAREINPUT
    {
        public uint uMsg; public ushort wParamL; public ushort wParamH;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    public static extern short GetKeyState(int nVirtKey);

    // Increments on EVERY clipboard change (including a re-copy of identical bytes) — lets us tell
    // "our own paste still sitting on the clipboard" from "the user copied something".
    [DllImport("user32.dll")]
    public static extern uint GetClipboardSequenceNumber();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int ToUnicodeEx(uint wVirtKey, uint wScanCode, byte[] lpKeyState,
        [Out] System.Text.StringBuilder pwszBuff, int cchBuff, uint wFlags, IntPtr dwhkl);

    [DllImport("user32.dll")]
    public static extern IntPtr GetKeyboardLayout(uint idThread);

    [DllImport("user32.dll")]
    public static extern bool GetKeyboardState(byte[] lpKeyState);

    // --- monitors / caret (panel placement) ---
    public const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GUITHREADINFO
    {
        public int cbSize;
        public uint flags;
        public IntPtr hwndActive, hwndFocus, hwndCapture, hwndMenuOwner, hwndMoveSize, hwndCaret;
        public RECT rcCaret;
    }

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    public static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

    [DllImport("user32.dll")]
    public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    // --- per-monitor DPI (PerMonitorV2, see app.manifest) ---
    public const int MDT_EFFECTIVE_DPI = 0;

    [DllImport("Shcore.dll")]
    public static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    // --- dark window chrome (Win10 2004+/Win11) ---
    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    /// <summary>Hide a window from DWM composition entirely while it is "shown" — the same flag the
    /// OS uses for windows on inactive virtual desktops. Used to keep a window invisible between
    /// ShowWindow and WPF's first presented frame (see WindowTheming.UseDarkChrome).</summary>
    public const int DWMWA_CLOAK = 13;

    // --- z-order fallback when the foreground grant is refused ---
    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new(-2);
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOACTIVATE = 0x0010;

    // Floating ball: click without taking activation (so the search-panel summon does not fight
    // the ball for foreground and flash Hide/Show via Deactivated).
    public const int WM_MOUSEACTIVATE = 0x0021;
    public const int MA_NOACTIVATE = 3;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
}
