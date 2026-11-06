using QuickText.Core.Models;
using QuickText.Core.Persistence;

namespace QuickText.Core.Tests;

public class CategoryJsonTests
{
    private static string ValidItem(string? id = null, string name = "问候", string mode = "") =>
        $$"""
        {
          "id": "{{id ?? Guid.NewGuid().ToString("N")}}",
          "name": "{{name}}",
          "abbr": "hw",
          "body": "你好",
          "useVariables": false,
          "outputMode": "{{mode}}"
        }
        """;

    [Fact]
    public void Empty_array_is_valid()
    {
        Assert.True(CategoryJson.TryValidate("[]", out var list, out var issue));
        Assert.Null(issue);
        Assert.NotNull(list);
        Assert.Empty(list!);
    }

    [Fact]
    public void Valid_snippet_array_parses()
    {
        var json = $"[{ValidItem()}]";
        Assert.True(CategoryJson.TryValidate(json, out var list, out var issue));
        Assert.Null(issue);
        Assert.NotNull(list);
        Assert.Single(list);
        Assert.Equal("问候", list[0].Name);
    }

    [Fact]
    public void Invalid_json_is_rejected()
    {
        Assert.False(CategoryJson.TryValidate("{ broken", out var list, out var issue));
        Assert.Null(list);
        Assert.Equal(CategoryJson.IssueKind.InvalidJson, issue!.Kind);
    }

    [Fact]
    public void Object_root_is_rejected()
    {
        Assert.False(CategoryJson.TryValidate("""{"name":"x"}""", out _, out var issue));
        Assert.Equal(CategoryJson.IssueKind.NotArray, issue!.Kind);
    }

    [Fact]
    public void Empty_name_is_rejected()
    {
        var json = $"[{ValidItem(name: "  ")}]";
        Assert.False(CategoryJson.TryValidate(json, out _, out var issue));
        Assert.Equal(CategoryJson.IssueKind.EmptyName, issue!.Kind);
        Assert.Equal(1, issue.Index);
    }

    [Fact]
    public void Bad_id_is_rejected()
    {
        var json = $"[{ValidItem(id: "not-a-guid")}]";
        Assert.False(CategoryJson.TryValidate(json, out _, out var issue));
        Assert.Equal(CategoryJson.IssueKind.BadId, issue!.Kind);
    }

    [Fact]
    public void Empty_id_is_rejected()
    {
        var json = """[{"id":"","name":"x","body":""}]""";
        Assert.False(CategoryJson.TryValidate(json, out _, out var issue));
        Assert.Equal(CategoryJson.IssueKind.EmptyId, issue!.Kind);
    }

    [Theory]
    [InlineData("paste")]
    [InlineData("paste-enter")]
    [InlineData("copy")]
    [InlineData("")]
    public void Allowed_output_modes_pass(string mode)
    {
        var json = $"[{ValidItem(mode: mode)}]";
        Assert.True(CategoryJson.TryValidate(json, out _, out var issue));
        Assert.Null(issue);
    }

    [Fact]
    public void Unknown_output_mode_is_rejected()
    {
        var json = $"[{ValidItem(mode: "send")}]";
        Assert.False(CategoryJson.TryValidate(json, out _, out var issue));
        Assert.Equal(CategoryJson.IssueKind.BadOutputMode, issue!.Kind);
        Assert.Equal("send", issue.Detail);
    }

    [Fact]
    public void Format_round_trips_through_validate()
    {
        var list = new List<Snippet>
        {
            new() { Name = "A", Body = "1", OutputMode = "copy" },
        };
        var json = CategoryJson.Format(list);
        Assert.True(CategoryJson.TryValidate(json, out var back, out _));
        Assert.Equal("A", back![0].Name);
        Assert.Equal("copy", back[0].OutputMode);
        Assert.True(Guid.TryParse(back[0].Id, out _));
    }
}
