using System.Windows;
using System.Windows.Threading;
using QuickText.Core.Clipboard;
using static QuickText.App.Interop.NativeMethods;

namespace QuickText.App.Interop;

/// <summary>Listens for clipboard text changes and records them when enabled.
/// Debounced so a burst of format updates (or IME churn) collapses to one read.</summary>
public sealed class ClipboardHistoryWatcher : IDisposable
{
    // Short coalesce window: cheap on quiet desktops, avoids N disk writes when an app
    // sets several clipboard formats in rapid succession.
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(75);

    private readonly IntPtr _hwnd;
    private readonly Func<bool> _isEnabled;
    private readonly ClipboardHistoryStore _store;
    private readonly DispatcherTimer _timer;
    private bool _listening;

    public ClipboardHistoryWatcher(IntPtr hwnd, Func<bool> isEnabled, ClipboardHistoryStore store)
    {
        _hwnd = hwnd;
        _isEnabled = isEnabled;
        _store = store;
        _timer = new DispatcherTimer { Interval = Debounce };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            CaptureIfNeeded();
        };
    }

    public ClipboardHistoryStore Store => _store;

    public void Sync()
    {
        if (_isEnabled()) Start();
        else Stop();
    }

    public void Start()
    {
        if (_listening || _hwnd == IntPtr.Zero) return;
        if (AddClipboardFormatListener(_hwnd))
            _listening = true;
    }

    public void Stop()
    {
        _timer.Stop();
        if (!_listening) return;
        RemoveClipboardFormatListener(_hwnd);
        _listening = false;
    }

    /// <summary>Return true when the message was a clipboard update (caller may mark handled).</summary>
    public bool HandleMessage(int msg)
    {
        if (msg != WM_CLIPBOARDUPDATE || !_listening) return false;
        if (!_isEnabled() || ClipboardWriteGate.ShouldIgnore()) return true;
        _timer.Stop();
        _timer.Start();
        return true;
    }

    private void CaptureIfNeeded()
    {
        if (!_isEnabled() || ClipboardWriteGate.ShouldIgnore()) return;
        string? text = null;
        try
        {
            if (Clipboard.ContainsText())
                text = Clipboard.GetText();
        }
        catch { /* locked / unavailable */ }
        if (!string.IsNullOrWhiteSpace(text))
            _store.Record(text);
    }

    public void Dispose() => Stop();
}
