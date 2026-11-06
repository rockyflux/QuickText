using System.Text.RegularExpressions;

namespace QuickText.Core.Skills;

/// <summary>Reads Agent Skill folders under a user skills root (see <see cref="SkillRoots"/>).</summary>
public static class AgentSkillScanner
{
    public const int DescriptionMaxChars = 48;

    /// <summary>Default root: <c>%USERPROFILE%\.agents\skills</c>.</summary>
    public static string DefaultSkillsRoot() => SkillRoots.Resolve(SkillRoots.DefaultKey);

    public static IReadOnlyList<AgentSkillInfo> Scan(string? root = null)
    {
        root ??= DefaultSkillsRoot();
        if (!Directory.Exists(root)) return Array.Empty<AgentSkillInfo>();

        var list = new List<AgentSkillInfo>();
        foreach (var skillFile in Directory.EnumerateFiles(root, "SKILL.md", SearchOption.AllDirectories))
        {
            var folder = Path.GetDirectoryName(skillFile)!;
            var rel = Path.GetRelativePath(root, folder);
            if (rel.StartsWith("..", StringComparison.Ordinal)) continue;
            var folderName = Path.GetFileName(folder);
            var (name, description) = ReadFrontmatter(skillFile);
            if (string.IsNullOrWhiteSpace(name)) name = folderName;
            list.Add(new AgentSkillInfo
            {
                FolderPath = folder,
                SkillFilePath = skillFile,
                RelativePath = rel.Replace('\\', '/'),
                FolderName = folderName,
                SkillName = name.Trim(),
                Description = description.Trim(),
            });
        }

        return list
            .OrderBy(s => s.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string FormatDisplayName(string name, string description)
    {
        if (string.IsNullOrWhiteSpace(description)) return name;
        var desc = TruncateOneLine(description, DescriptionMaxChars);
        return string.IsNullOrEmpty(desc) ? name : $"{name} — {desc}";
    }

    public static (string Name, string Description) ReadFrontmatter(string skillFilePath)
    {
        try
        {
            // Only the header is needed; huge skill bodies must not be fully loaded.
            using var reader = new StreamReader(skillFilePath);
            var first = reader.ReadLine();
            if (first is null || !first.TrimStart().StartsWith("---", StringComparison.Ordinal))
                return ("", "");

            var sb = new System.Text.StringBuilder();
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (line.TrimStart().StartsWith("---", StringComparison.Ordinal)) break;
                sb.AppendLine(line);
                if (sb.Length > 16_384) break;
            }
            return ParseYamlScalars(sb.ToString());
        }
        catch
        {
            return ("", "");
        }
    }

    // Minimal frontmatter: top-level `name:` / `description:` scalars (quoted or plain).
    private static (string Name, string Description) ParseYamlScalars(string yaml)
    {
        string name = "", description = "";
        foreach (var raw in yaml.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0 || line[0] is ' ' or '\t' or '#' or '-') continue;
            var m = Regex.Match(line, @"^(name|description)\s*:\s*(.*)$", RegexOptions.IgnoreCase);
            if (!m.Success) continue;
            var key = m.Groups[1].Value.ToLowerInvariant();
            var val = Unquote(m.Groups[2].Value.Trim());
            if (key == "name" && name.Length == 0) name = val;
            else if (key == "description" && description.Length == 0) description = val;
        }
        return (name, description);
    }

    private static string Unquote(string s)
    {
        if (s.Length >= 2)
        {
            char a = s[0], b = s[^1];
            if ((a == '"' && b == '"') || (a == '\'' && b == '\''))
                return s[1..^1].Replace("\\\"", "\"").Replace("\\'", "'");
        }
        return s;
    }

    private static string TruncateOneLine(string text, int maxChars)
    {
        var one = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        while (one.Contains("  ", StringComparison.Ordinal))
            one = one.Replace("  ", " ", StringComparison.Ordinal);
        if (one.Length <= maxChars) return one;
        if (maxChars <= 1) return "…";
        return one[..(maxChars - 1)].TrimEnd() + "…";
    }
}
