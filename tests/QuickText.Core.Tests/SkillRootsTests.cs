using QuickText.Core.Skills;

namespace QuickText.Core.Tests;

public class SkillRootsTests
{
    [Fact]
    public void Resolve_joins_user_profile()
    {
        var path = SkillRoots.Resolve(@".claude\skills");
        Assert.Equal(
            Path.GetFullPath(Path.Combine(SkillRoots.UserProfile, @".claude\skills")),
            path);
    }

    [Fact]
    public void NormalizeKey_maps_known_aliases()
    {
        Assert.Equal(@".agents\skills", SkillRoots.NormalizeKey("agents/skills"));
        Assert.Equal(@".claude\skills", SkillRoots.NormalizeKey(@".claude\skills"));
        Assert.Equal(SkillRoots.DefaultKey, SkillRoots.NormalizeKey(null));
    }

    [Fact]
    public void Catalog_includes_requested_roots()
    {
        Assert.Contains(@".cursor\skills", SkillRoots.RelativeKeys);
        Assert.Contains(@".qoder\skills", SkillRoots.RelativeKeys);
        Assert.Contains(@".kiro\skills", SkillRoots.RelativeKeys);
        Assert.Contains(@".pi\agent\skills", SkillRoots.RelativeKeys);
    }
}
