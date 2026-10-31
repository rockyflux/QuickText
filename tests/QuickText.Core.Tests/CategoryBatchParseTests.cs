namespace QuickText.Core.Tests;

/// <summary>Mirrors <c>CategoryBatchDialog.ParseNames</c> rules so Core tests catch regressions
/// without referencing the WPF project. Keep both in sync when changing the line-split rules.</summary>
public class CategoryBatchParseTests
{
    private static List<string> ParseNames(string text)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();
        foreach (var raw in (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var name = raw.Trim();
            if (name.Length == 0 || !seen.Add(name)) continue;
            list.Add(name);
        }
        return list;
    }

    [Fact]
    public void ParseNames_trims_skips_blanks_and_case_insensitive_dupes()
    {
        var names = ParseNames("  常用 \n\n模板\n常用\nCOMMON\ncommon ");
        Assert.Equal(new[] { "常用", "模板", "COMMON" }, names);
    }
}
