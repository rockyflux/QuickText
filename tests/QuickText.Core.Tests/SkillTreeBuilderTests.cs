using QuickText.Core.Skills;

namespace QuickText.Core.Tests;

public class SkillTreeBuilderTests
{
    [Fact]
    public void Build_keeps_nested_folders_and_skill_labels()
    {
        var skills = new[]
        {
            new AgentSkillInfo
            {
                FolderPath = @"C:\s\birdview",
                SkillFilePath = @"C:\s\birdview\SKILL.md",
                RelativePath = "birdview",
                FolderName = "birdview",
                SkillName = "birdview",
                Description = "top",
            },
            new AgentSkillInfo
            {
                FolderPath = @"C:\s\birdview\compatibility-audit",
                SkillFilePath = @"C:\s\birdview\compatibility-audit\SKILL.md",
                RelativePath = "birdview/compatibility-audit",
                FolderName = "compatibility-audit",
                SkillName = "compatibility-audit",
                Description = "nested",
            },
        };

        var roots = SkillTreeBuilder.Build(skills);
        var bird = Assert.Single(roots);
        Assert.Equal("birdview", bird.Label);
        Assert.True(bird.IsSkill);
        var child = Assert.Single(bird.Children);
        Assert.Equal("compatibility-audit", child.Label);
    }
}
