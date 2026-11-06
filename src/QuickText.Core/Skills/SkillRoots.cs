namespace QuickText.Core.Skills;

/// <summary>Known per-user Agent Skill directories under the profile home.</summary>
public static class SkillRoots
{
    /// <summary>Relative keys shown in the UI; resolved under the user profile.</summary>
    public static readonly string[] RelativeKeys =
    {
        @".agents\skills",
        @".cursor\skills",
        @".codex\skills",
        @".claude\skills",
        @".dsh\skills",
        @".pi\agent\skills",
        @".config\opencode\skills",
        @".zcode\skills",
        @".kiro\skills",
        @".codebuddy\skills",
        @".trae\skills",
        @".qoder\skills",
    };

    public const string DefaultKey = @".agents\skills";

    public static string UserProfile =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static string Resolve(string? relativeKey)
    {
        var key = NormalizeKey(relativeKey);
        return Path.GetFullPath(Path.Combine(UserProfile, key));
    }

    public static string NormalizeKey(string? relativeKey)
    {
        if (string.IsNullOrWhiteSpace(relativeKey)) return DefaultKey;
        var key = relativeKey.Trim().Replace('/', '\\').TrimStart('\\');
        if (!key.StartsWith('.')) key = "." + key;
        foreach (var known in RelativeKeys)
        {
            if (string.Equals(known, key, StringComparison.OrdinalIgnoreCase)
                || string.Equals(known.TrimStart('.'), key.TrimStart('.'), StringComparison.OrdinalIgnoreCase))
                return known;
        }
        // Unknown but still under profile — keep as-is with leading dot folder form.
        return key.StartsWith('.') ? key : "." + key;
    }

    public static bool IsKnown(string? relativeKey) =>
        RelativeKeys.Any(k => string.Equals(k, NormalizeKey(relativeKey), StringComparison.OrdinalIgnoreCase));
}
