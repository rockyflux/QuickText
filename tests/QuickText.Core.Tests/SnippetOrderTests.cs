using QuickText.Core.Models;
using QuickText.Core.Snippets;

namespace QuickText.Core.Tests;

public class SnippetOrderTests
{
    [Theory]
    [InlineData(null, SnippetOrder.Manual)]
    [InlineData("", SnippetOrder.Manual)]
    [InlineData("manual", SnippetOrder.Manual)]
    [InlineData("CREATED", SnippetOrder.Created)]
    [InlineData("lastused", SnippetOrder.LastUsed)]
    [InlineData("weird", SnippetOrder.Manual)]
    public void Normalize_maps_known_and_legacy_values(string? raw, string expected) =>
        Assert.Equal(expected, SnippetOrder.Normalize(raw));

    [Fact]
    public void Apply_manual_keeps_input_order()
    {
        var a = Sn("a"); var b = Sn("b"); var c = Sn("c");
        var ordered = SnippetOrder.Apply(new[] { b, a, c }, SnippetOrder.Manual);
        Assert.Equal(new[] { b, a, c }, ordered);
    }

    [Fact]
    public void Apply_name_sorts_ordinal_ignore_case()
    {
        var a = Sn("beta"); var b = Sn("Alpha"); var c = Sn("gamma");
        var ordered = SnippetOrder.Apply(new[] { a, b, c }, SnippetOrder.Name);
        Assert.Equal(new[] { b, a, c }, ordered);
    }

    [Fact]
    public void Apply_created_newest_first()
    {
        var old = Sn("old", created: DateTimeOffset.Parse("2020-01-01Z"));
        var mid = Sn("mid", created: DateTimeOffset.Parse("2022-01-01Z"));
        var neu = Sn("new", created: DateTimeOffset.Parse("2024-01-01Z"));
        var ordered = SnippetOrder.Apply(new[] { old, neu, mid }, SnippetOrder.Created);
        Assert.Equal(new[] { neu, mid, old }, ordered);
    }

    [Fact]
    public void Apply_created_falls_back_to_UpdatedAt_when_CreatedAt_missing()
    {
        var older = new Snippet { Name = "a", UpdatedAt = DateTimeOffset.Parse("2020-01-01Z") };
        var newer = new Snippet { Name = "b", UpdatedAt = DateTimeOffset.Parse("2024-01-01Z") };
        var ordered = SnippetOrder.Apply(new[] { older, newer }, SnippetOrder.Created);
        Assert.Equal(new[] { newer, older }, ordered);
    }

    [Fact]
    public void Apply_usage_count_then_last_used_then_name()
    {
        var a = Sn("a", id: "1");
        var b = Sn("b", id: "2");
        var c = Sn("c", id: "3");
        var counts = new Dictionary<string, int> { ["1"] = 5, ["2"] = 5, ["3"] = 1 };
        var last = new Dictionary<string, DateTimeOffset?>
        {
            ["1"] = DateTimeOffset.Parse("2024-01-01Z"),
            ["2"] = DateTimeOffset.Parse("2024-06-01Z"),
            ["3"] = DateTimeOffset.Parse("2025-01-01Z"),
        };
        var ordered = SnippetOrder.Apply(new[] { a, b, c }, SnippetOrder.Usage,
            id => counts.GetValueOrDefault(id),
            id => last.GetValueOrDefault(id));
        Assert.Equal(new[] { b, a, c }, ordered);
    }

    [Fact]
    public void Apply_last_used_newest_first_never_used_last()
    {
        var a = Sn("a", id: "1");
        var b = Sn("b", id: "2");
        var c = Sn("c", id: "3");
        var last = new Dictionary<string, DateTimeOffset?>
        {
            ["1"] = DateTimeOffset.Parse("2024-01-01Z"),
            ["2"] = DateTimeOffset.Parse("2024-06-01Z"),
            // 3 never used
        };
        var ordered = SnippetOrder.Apply(new[] { a, c, b }, SnippetOrder.LastUsed,
            lastUsed: id => last.GetValueOrDefault(id));
        Assert.Equal(new[] { b, a, c }, ordered);
    }

    [Fact]
    public void BackfillCreatedAt_uses_UpdatedAt_when_missing()
    {
        var updated = DateTimeOffset.Parse("2023-05-01Z");
        var s = new Snippet { Name = "旧", UpdatedAt = updated, CreatedAt = default };
        SnippetOrder.BackfillCreatedAt(new[] { s });
        Assert.Equal(updated, s.CreatedAt);
    }

    private static Snippet Sn(string name, string? id = null, DateTimeOffset? created = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid().ToString("N"),
            Name = name,
            CreatedAt = created ?? DateTimeOffset.UtcNow,
            UpdatedAt = created ?? DateTimeOffset.UtcNow,
        };
}
