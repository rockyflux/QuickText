namespace QuickText.Core.Skills;

/// <summary>Folder / skill node for a TreeView over scanned Agent Skills.</summary>
public sealed class SkillTreeNode
{
    public string Label { get; set; } = "";
    public string RelativeFolder { get; set; } = "";
    public AgentSkillInfo? Skill { get; set; }
    public List<SkillTreeNode> Children { get; } = new();
    public bool IsSkill => Skill is not null;
}

public static class SkillTreeBuilder
{
    /// <summary>Build a forest from scanned skills. Optional <paramref name="predicate"/> filters skills
    /// (ancestor folders of kept skills are still shown).</summary>
    public static List<SkillTreeNode> Build(
        IEnumerable<AgentSkillInfo> skills,
        Func<AgentSkillInfo, bool>? predicate = null)
    {
        var kept = predicate is null ? skills.ToList() : skills.Where(predicate).ToList();
        var folders = new Dictionary<string, SkillTreeNode>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new SkillTreeNode { Label = "", RelativeFolder = "" },
        };

        SkillTreeNode EnsureFolder(string relFolder)
        {
            relFolder = Normalize(relFolder);
            if (folders.TryGetValue(relFolder, out var existing)) return existing;

            var name = relFolder.Contains('/')
                ? relFolder[(relFolder.LastIndexOf('/') + 1)..]
                : relFolder;
            var node = new SkillTreeNode { Label = name, RelativeFolder = relFolder };
            folders[relFolder] = node;
            EnsureFolder(ParentPath(relFolder)).Children.Add(node);
            return node;
        }

        foreach (var skill in kept.OrderBy(s => s.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            var folderNode = EnsureFolder(skill.RelativePath);
            folderNode.Skill = skill;
            folderNode.Label = skill.SkillName;
        }

        return folders[""].Children;
    }

    private static string Normalize(string path) =>
        string.IsNullOrEmpty(path) ? "" : path.Replace('\\', '/').Trim('/');

    private static string ParentPath(string rel)
    {
        rel = Normalize(rel);
        var i = rel.LastIndexOf('/');
        return i < 0 ? "" : rel[..i];
    }
}
