using QuickText.Core.Models;

namespace QuickText.Core.Snippets;

/// <summary>Display order for snippets inside one category. Manual keeps the library array
/// order; the other modes are view-only and must not rewrite the category JSON.</summary>
public static class SnippetOrder
{
    public const string Manual = "manual";
    public const string Created = "created";
    public const string Name = "name";
    public const string Usage = "usage";
    public const string LastUsed = "lastused";

    public static string Normalize(string? mode) =>
        string.IsNullOrWhiteSpace(mode)
            ? Manual
            : mode.Trim().ToLowerInvariant() switch
            {
                Manual or "" => Manual,
                Created => Created,
                Name => Name,
                Usage => Usage,
                LastUsed => LastUsed,
                _ => Manual,
            };

    public static bool IsManual(string? mode) => Normalize(mode) == Manual;

    /// <summary>Return a new list in display order. <paramref name="countOf"/> /
    /// <paramref name="lastUsed"/> are read for <see cref="Usage"/> / <see cref="LastUsed"/>.</summary>
    public static List<Snippet> Apply(
        IEnumerable<Snippet> snippets,
        string? sortMode,
        Func<string, int>? countOf = null,
        Func<string, DateTimeOffset?>? lastUsed = null)
    {
        var list = snippets as List<Snippet> ?? snippets.ToList();
        return Normalize(sortMode) switch
        {
            Created => list
                .OrderByDescending(s => s.CreatedAt != default ? s.CreatedAt : s.UpdatedAt)
                .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Name => list
                .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Usage => list
                .OrderByDescending(s => countOf?.Invoke(s.Id) ?? 0)
                .ThenByDescending(s => lastUsed?.Invoke(s.Id) ?? DateTimeOffset.MinValue)
                .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            LastUsed => list
                .OrderByDescending(s => lastUsed?.Invoke(s.Id) ?? DateTimeOffset.MinValue)
                .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            _ => list.ToList(),
        };
    }

    /// <summary>Older libraries omit CreatedAt; treat default as UpdatedAt (or now).</summary>
    public static void BackfillCreatedAt(IEnumerable<Snippet> snippets)
    {
        foreach (var s in snippets)
        {
            if (s.CreatedAt != default) continue;
            s.CreatedAt = s.UpdatedAt != default ? s.UpdatedAt : DateTimeOffset.UtcNow;
        }
    }
}
