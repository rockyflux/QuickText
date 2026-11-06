using QuickText.Core.Skills;

namespace QuickText.Core.Tests;

public class AgentSkillScannerTests
{
    private static string TempRoot()
    {
        var dir = Path.Combine(Path.GetTempPath(), "qt_skills_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Scan_builds_relative_paths_and_display_names()
    {
        var root = TempRoot();
        try
        {
            WriteSkill(Path.Combine(root, "brainstorming"), "brainstorming",
                "You MUST use this before any creative work - creating features.");
            WriteSkill(Path.Combine(root, "birdview", "compatibility-audit"), "compatibility-audit",
                "Audit compatibility.");

            var skills = AgentSkillScanner.Scan(root);
            Assert.Equal(2, skills.Count);

            var top = skills.Single(s => s.FolderName == "brainstorming");
            Assert.Equal("brainstorming", top.RelativePath);
            Assert.Equal("brainstorming", top.SkillName);
            Assert.Contains("You MUST use this", top.Description, StringComparison.Ordinal);
            Assert.True(File.Exists(top.SkillFilePath));

            var nested = skills.Single(s => s.FolderName == "compatibility-audit");
            Assert.Equal("birdview/compatibility-audit", nested.RelativePath.Replace('\\', '/'));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void FormatDisplayName_truncates_long_description()
    {
        var longDesc = new string('x', 80);
        var title = AgentSkillScanner.FormatDisplayName("demo", longDesc);
        Assert.StartsWith("demo — ", title, StringComparison.Ordinal);
        Assert.EndsWith("…", title, StringComparison.Ordinal);
        Assert.True(title.Length < 80);
    }

    [Fact]
    public void Missing_root_returns_empty()
    {
        var missing = Path.Combine(Path.GetTempPath(), "qt_skills_missing_" + Guid.NewGuid().ToString("N"));
        Assert.Empty(AgentSkillScanner.Scan(missing));
    }

    private static void WriteSkill(string folder, string name, string description)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "SKILL.md"),
            $"---\nname: {name}\ndescription: \"{description}\"\n---\n\n# {name}\n");
    }
}
