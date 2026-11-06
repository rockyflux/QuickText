using System.Text.Json;
using QuickText.Core.Models;

namespace QuickText.Core.Persistence;

/// <summary>Parse / validate a category's snippet JSON file (array of <see cref="Snippet"/>).</summary>
public static class CategoryJson
{
    public static readonly HashSet<string> AllowedOutputModes = new(StringComparer.Ordinal)
    {
        "", "paste", "paste-enter", "copy",
    };

    public enum IssueKind
    {
        InvalidJson,
        NotArray,
        EmptyName,
        EmptyId,
        BadId,
        BadOutputMode,
    }

    /// <param name="Index">0 for document-level issues; 1-based item index otherwise.</param>
    public sealed record Issue(IssueKind Kind, int Index, string? Detail);

    /// <summary>Validate and deserialize category JSON. On success <paramref name="snippets"/> is
    /// non-null (possibly empty); on failure it is null and <paramref name="issue"/> explains why.</summary>
    public static bool TryValidate(string? text, out List<Snippet>? snippets, out Issue? issue)
    {
        snippets = null;
        issue = null;
        var raw = text ?? "";
        List<Snippet>? list;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                issue = new Issue(IssueKind.NotArray, 0, null);
                return false;
            }
            list = JsonSerializer.Deserialize<List<Snippet>>(raw, JsonConfig.Read);
        }
        catch (JsonException ex)
        {
            issue = new Issue(IssueKind.InvalidJson, 0, ex.Message);
            return false;
        }

        list ??= new List<Snippet>();
        for (int i = 0; i < list.Count; i++)
        {
            var sn = list[i];
            int n = i + 1;
            if (string.IsNullOrWhiteSpace(sn.Name))
            {
                issue = new Issue(IssueKind.EmptyName, n, null);
                return false;
            }
            if (string.IsNullOrWhiteSpace(sn.Id))
            {
                issue = new Issue(IssueKind.EmptyId, n, null);
                return false;
            }
            if (!Guid.TryParse(sn.Id, out _))
            {
                issue = new Issue(IssueKind.BadId, n, sn.Id);
                return false;
            }
            var mode = sn.OutputMode ?? "";
            if (!AllowedOutputModes.Contains(mode))
            {
                issue = new Issue(IssueKind.BadOutputMode, n, mode);
                return false;
            }
        }

        snippets = list;
        return true;
    }

    public static string Format(IEnumerable<Snippet> snippets) =>
        JsonSerializer.Serialize(snippets, JsonConfig.Write);
}
