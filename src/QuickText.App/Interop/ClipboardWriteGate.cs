using static QuickText.App.Interop.NativeMethods;

namespace QuickText.App.Interop;

/// <summary>Marks clipboard writes that QuickText itself performed so the history watcher
/// can ignore them. Depth covers nested writes; the sequence number covers the async
/// <c>WM_CLIPBOARDUPDATE</c> that arrives after <see cref="Suppress"/> has already disposed.</summary>
public static class ClipboardWriteGate
{
    private static int _depth;
    private static uint _ignoredSeq;

    public static bool IsSuppressed => _depth > 0;

    public static IDisposable Suppress()
    {
        _depth++;
        return new Releaser();
    }

    /// <summary>Call immediately after a successful clipboard write made under <see cref="Suppress"/>.</summary>
    public static void NoteOwnWrite() => _ignoredSeq = GetClipboardSequenceNumber();

    public static bool ShouldIgnore()
    {
        if (_depth > 0) return true;
        return _ignoredSeq != 0 && GetClipboardSequenceNumber() == _ignoredSeq;
    }

    private sealed class Releaser : IDisposable
    {
        private bool _done;
        public void Dispose()
        {
            if (_done) return;
            _done = true;
            if (_depth > 0) _depth--;
        }
    }
}
