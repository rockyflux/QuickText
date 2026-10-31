using System.Windows;
using System.Windows.Threading;

namespace QuickText.App.Interop;

/// <summary>Run a paste only once the target app actually holds the foreground. At the timeout
/// it runs the fallback instead of blindly sending Ctrl+V into whatever happens to be focused.
/// Retries ForceForeground on every poll — a single attempt right after Hide() is often refused
/// by the foreground lock and used to make every send fall through to "copy only".</summary>
internal static class ForegroundTarget
{
    // ~1s total: Hide → focus handoff is often slower than the old 240ms window on Chromium /
    // Electron / UWP hosts, and giving up early was indistinguishable from "copy only".
    private const int DefaultTries = 50;
    private const int PollMs = 20;

    public static void WhenForeground(IntPtr target, Action onReady, Action onTimeout, int triesLeft = DefaultTries)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null) { onTimeout(); return; }

        try
        {
            if (IsReady(target))
            {
                dispatcher.BeginInvoke(onReady, DispatcherPriority.Background);
                return;
            }
            if (triesLeft <= 0)
            {
                dispatcher.BeginInvoke(onTimeout, DispatcherPriority.Background);
                return;
            }

            // Re-assert every tick: the first ForceForeground after Hide is the one most often
            // refused; later ticks run once some other window has settled as the grant holder.
            NativeMethods.ForceForeground(target);

            // ContinueWith + Invoke (not BeginInvoke): ordered polls, one ForceForeground at a time.
            System.Threading.Tasks.Task.Delay(PollMs).ContinueWith(_ =>
            {
                try { dispatcher.Invoke(() => WhenForeground(target, onReady, onTimeout, triesLeft - 1)); }
                catch { /* shutting down */ }
            });
        }
        catch { onTimeout(); }
    }

    /// <summary>Exact HWND match, or any live window of the same foreign process. Apps (Chrome,
    /// WeChat, …) often replace or re-parent the top-level HWND between summon and send; requiring
    /// pointer equality then timed out into copy even though the user was back in the right app.</summary>
    private static bool IsReady(IntPtr target)
    {
        if (target == IntPtr.Zero || !NativeMethods.IsWindow(target)) return false;
        var fg = NativeMethods.GetForegroundWindow();
        if (fg == IntPtr.Zero) return false;
        if (fg == target) return true;

        NativeMethods.GetWindowThreadProcessId(target, out uint targetPid);
        NativeMethods.GetWindowThreadProcessId(fg, out uint fgPid);
        return targetPid != 0 && targetPid == fgPid
               && fgPid != (uint)Environment.ProcessId;
    }
}
