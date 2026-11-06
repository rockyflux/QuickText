namespace QuickText.Core.Skills;

/// <summary>One discovered Agent Skill (a directory that contains <c>SKILL.md</c>).</summary>
public sealed class AgentSkillInfo
{
    public required string FolderPath { get; init; }
    public required string SkillFilePath { get; init; }
    public required string RelativePath { get; init; }
    public required string FolderName { get; init; }
    /// <summary>Frontmatter <c>name</c>, or the folder name when missing.</summary>
    public required string SkillName { get; init; }
    public string Description { get; init; } = "";
    /// <summary>List label: alias if set, else <see cref="SkillName"/>.</summary>
    public string DisplayName => SkillName;
}

/// <summary>Persisted favorite: import writes <see cref="Name"/>, <see cref="Body"/>, and
/// <see cref="Description"/> into a snippet.</summary>
public sealed class SkillFavoriteItem
{
    public string Name { get; set; } = "";
    /// <summary>Absolute path of the skill's <c>SKILL.md</c>.</summary>
    public string Body { get; set; } = "";
    /// <summary>Skill frontmatter description; stored for import, not edited in the Skills UI.</summary>
    public string? Description { get; set; }
    /// <summary>Optional UI-only label; never copied into the snippet body.</summary>
    public string? Alias { get; set; }
}

public sealed class SkillFavoritesFile
{
    /// <summary>Last browsed root key, e.g. <c>.agents\skills</c> (see <see cref="SkillRoots"/>).</summary>
    public string? ActiveRoot { get; set; }
    public List<SkillFavoriteItem> Items { get; set; } = new();
}
