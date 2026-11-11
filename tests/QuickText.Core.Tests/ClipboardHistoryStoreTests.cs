using QuickText.Core.Clipboard;

namespace QuickText.Core.Tests;

public class ClipboardHistoryStoreTests
{
    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), "qt-clip-" + Guid.NewGuid().ToString("N") + ".json");

    [Fact]
    public void Record_appends_newest_first()
    {
        var path = TempFile();
        try
        {
            var store = new ClipboardHistoryStore(path);
            store.Record("a", new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc));
            store.Record("b", new DateTime(2026, 11, 11, 12, 1, 0, DateTimeKind.Utc));
            Assert.Equal(2, store.Items.Count);
            Assert.Equal("b", store.Items[0].Text);
            Assert.Equal("a", store.Items[1].Text);
        }
        finally { TryDelete(path); }
    }

    [Fact]
    public void Record_ignores_whitespace_only()
    {
        var path = TempFile();
        try
        {
            var store = new ClipboardHistoryStore(path);
            store.Record("   \t\n");
            store.Record("");
            Assert.Empty(store.Items);
        }
        finally { TryDelete(path); }
    }

    [Fact]
    public void Record_consecutive_duplicate_refreshes_timestamp_without_growing()
    {
        var path = TempFile();
        try
        {
            var store = new ClipboardHistoryStore(path);
            var t1 = new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);
            var t2 = new DateTime(2026, 11, 11, 12, 5, 0, DateTimeKind.Utc);
            store.Record("x", t1);
            store.Record("x", t2);
            Assert.Single(store.Items);
            Assert.Equal(t2, store.Items[0].CreatedUtc);
        }
        finally { TryDelete(path); }
    }

    [Fact]
    public void Record_prunes_older_than_3_days()
    {
        var path = TempFile();
        try
        {
            var store = new ClipboardHistoryStore(path);
            var now = new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);
            store.Record("old", now.AddDays(-4));
            store.Record("keep", now.AddDays(-1));
            store.Record("fresh", now);
            Assert.Equal(2, store.Items.Count);
            Assert.DoesNotContain(store.Items, i => i.Text == "old");
            Assert.Contains(store.Items, i => i.Text == "keep");
            Assert.Contains(store.Items, i => i.Text == "fresh");
        }
        finally { TryDelete(path); }
    }

    [Fact]
    public void Record_caps_at_500()
    {
        var path = TempFile();
        try
        {
            var store = new ClipboardHistoryStore(path);
            var t0 = new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc);
            for (int i = 0; i < 501; i++)
                store.Record($"item-{i}", t0.AddSeconds(i));
            Assert.Equal(500, store.Items.Count);
            Assert.Equal("item-500", store.Items[0].Text);
            Assert.DoesNotContain(store.Items, i => i.Text == "item-0");
        }
        finally { TryDelete(path); }
    }

    [Fact]
    public void Record_truncates_oversized_text()
    {
        var path = TempFile();
        try
        {
            var store = new ClipboardHistoryStore(path);
            var huge = new string('a', ClipboardHistoryStore.MaxTextChars + 50);
            store.Record(huge);
            Assert.Single(store.Items);
            Assert.Equal(ClipboardHistoryStore.MaxTextChars, store.Items[0].Text.Length);
        }
        finally { TryDelete(path); }
    }

    [Fact]
    public void Clear_empties_file_and_items()
    {
        var path = TempFile();
        try
        {
            var store = new ClipboardHistoryStore(path);
            store.Record("keep-me");
            store.Clear();
            Assert.Empty(store.Items);
            var again = new ClipboardHistoryStore(path);
            Assert.Empty(again.Items);
        }
        finally { TryDelete(path); }
    }

    [Fact]
    public void Load_corrupt_file_yields_empty()
    {
        var path = TempFile();
        try
        {
            File.WriteAllText(path, "{ not valid json");
            var store = new ClipboardHistoryStore(path);
            Assert.Empty(store.Items);
        }
        finally { TryDelete(path); }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { }
        try { File.Delete(path + ".tmp"); } catch { }
    }
}
