namespace QuickText.Core.Clipboard;

public sealed class ClipboardHistoryEntry
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
}
