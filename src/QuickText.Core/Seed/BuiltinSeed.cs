using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using QuickText.Core.Models;
using QuickText.Core.Persistence;

namespace QuickText.Core.Seed;

/// <summary>Built-in starter categories/snippets shipped as embedded JSON.
/// First-run seeding and the empty-panel / category-manager "initialize" actions all read this.</summary>
public static class BuiltinSeed
{
    private const string ResourceSuffix = "Seed.builtin-categories.json";

    private static readonly Lazy<IReadOnlyDictionary<string, SeedLocale>> Locales = new(Load);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Which of the seeded languages fits a UI culture (non-CJK → English).</summary>
    public static string LangFor(System.Globalization.CultureInfo c)
    {
        if (c.TwoLetterISOLanguageName == "ja") return "ja";
        if (c.TwoLetterISOLanguageName != "zh") return "en";
        var n = c.Name;
        bool traditional = n.Contains("Hant", StringComparison.OrdinalIgnoreCase)
            || n.Equals("zh-TW", StringComparison.OrdinalIgnoreCase)
            || n.Equals("zh-HK", StringComparison.OrdinalIgnoreCase)
            || n.Equals("zh-MO", StringComparison.OrdinalIgnoreCase)
            || n.Equals("zh-CHT", StringComparison.OrdinalIgnoreCase);
        return traditional ? "zh-Hant" : "zh-Hans";
    }

    public static IReadOnlyList<string> CategoryNames(string lang) =>
        Locale(lang).Categories.Select(c => c.Name).ToList();

    /// <summary>Materialize categories for <paramref name="lang"/> (fallback zh-Hans → en).</summary>
    public static IReadOnlyList<Category> CategoriesFor(string lang)
    {
        var now = DateTimeOffset.UtcNow;
        return Locale(lang).Categories.Select(c =>
        {
            var cat = new Category { Name = c.Name };
            foreach (var s in c.Snippets)
            {
                cat.Snippets.Add(new Snippet
                {
                    Name = s.Name ?? "",
                    Abbr = s.Abbr ?? "",
                    Body = s.Body ?? "",
                    UseVariables = s.UseVariables,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            }
            return cat;
        }).ToList();
    }

    /// <summary>Write built-in categories that are not already present (by name). Returns how many
    /// were added vs skipped as duplicates.</summary>
    public static (int Added, int Skipped) WriteMissing(Store store, string lang)
    {
        var known = new HashSet<string>(
            store.LoadAll().Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
        int added = 0, skipped = 0;
        foreach (var cat in CategoriesFor(lang))
        {
            if (!known.Add(cat.Name))
            {
                skipped++;
                continue;
            }
            store.SaveCategory(cat);
            added++;
        }
        return (added, skipped);
    }

    private static SeedLocale Locale(string lang)
    {
        var map = Locales.Value;
        if (map.TryGetValue(lang, out var hit)) return hit;
        if (map.TryGetValue("zh-Hans", out hit)) return hit;
        if (map.TryGetValue("en", out hit)) return hit;
        throw new InvalidOperationException("builtin-categories.json has no usable locale.");
    }

    private static IReadOnlyDictionary<string, SeedLocale> Load()
    {
        var asm = typeof(BuiltinSeed).Assembly;
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(ResourceSuffix, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"Embedded resource ending with '{ResourceSuffix}' was not found.");
        using var stream = asm.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Could not open embedded resource '{name}'.");
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        var map = JsonSerializer.Deserialize<Dictionary<string, SeedLocale>>(json, JsonOptions)
            ?? throw new InvalidOperationException("builtin-categories.json deserialized to null.");
        if (map.Count == 0)
            throw new InvalidOperationException("builtin-categories.json has no locales.");
        return map;
    }

    private sealed class SeedLocale
    {
        [JsonPropertyName("categories")]
        public List<SeedCategory> Categories { get; set; } = new();
    }

    private sealed class SeedCategory
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("snippets")]
        public List<SeedSnippet> Snippets { get; set; } = new();
    }

    private sealed class SeedSnippet
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("abbr")]
        public string? Abbr { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("useVariables")]
        public bool UseVariables { get; set; }
    }
}
