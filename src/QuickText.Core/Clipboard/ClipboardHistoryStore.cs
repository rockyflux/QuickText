using System.Text.Json;
using QuickText.Core.Persistence;

namespace QuickText.Core.Clipboard;

/// <summary>Persists recent text clipboard entries next to machine settings.
/// Bounds memory with a 3-day window, a 500-row cap, and a per-entry char ceiling.</summary>
public sealed class ClipboardHistoryStore
{
    public const int MaxEntries = 500;
    /// <summary>Hard ceiling per entry so a huge paste cannot blow RAM or the JSON file.</summary>
    public const int MaxTextChars = 32 * 1024;
    private static readonly TimeSpan Retention = TimeSpan.FromDays(3);

    private readonly string _path;
    private List<ClipboardHistoryEntry> _items;

    public ClipboardHistoryStore(string path)
    {
        _path = path;
        _items = Load();
        Prune(DateTime.UtcNow);
    }

    public IReadOnlyList<ClipboardHistoryEntry> Items => _items;

    public void Record(string text, DateTime? utcNow = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var now = utcNow ?? DateTime.UtcNow;
        if (now.Kind == DateTimeKind.Unspecified)
            now = DateTime.SpecifyKind(now, DateTimeKind.Utc);
        else if (now.Kind == DateTimeKind.Local)
            now = now.ToUniversalTime();

        text = text.Length > MaxTextChars ? text[..MaxTextChars] : text;

        if (_items.Count > 0 && string.Equals(_items[0].Text, text, StringComparison.Ordinal))
        {
            _items[0].CreatedUtc = now;
            Prune(now);
            Save();
            return;
        }

        _items.Insert(0, new ClipboardHistoryEntry
        {
            Id = Guid.NewGuid().ToString("N"),
            Text = text,
            CreatedUtc = now,
        });
        Prune(now);
        Save();
    }

    public void Clear()
    {
        _items = new List<ClipboardHistoryEntry>();
        Save();
    }

    private void Prune(DateTime utcNow)
    {
        var cutoff = utcNow - Retention;
        _items.RemoveAll(i => i.CreatedUtc < cutoff);
        if (_items.Count > MaxEntries)
            _items.RemoveRange(MaxEntries, _items.Count - MaxEntries);
    }

    private List<ClipboardHistoryEntry> Load()
    {
        try
        {
            if (!File.Exists(_path)) return new List<ClipboardHistoryEntry>();
            var file = JsonSerializer.Deserialize<ClipboardHistoryFile>(File.ReadAllText(_path), JsonConfig.Read);
            return file?.Items ?? new List<ClipboardHistoryEntry>();
        }
        catch
        {
            return new List<ClipboardHistoryEntry>();
        }
    }

    private void Save()
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(new ClipboardHistoryFile { Items = _items }, JsonConfig.Write);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, json);
        if (File.Exists(_path)) File.Replace(tmp, _path, null);
        else File.Move(tmp, _path);
    }

    private sealed class ClipboardHistoryFile
    {
        public List<ClipboardHistoryEntry> Items { get; set; } = new();
    }
}
