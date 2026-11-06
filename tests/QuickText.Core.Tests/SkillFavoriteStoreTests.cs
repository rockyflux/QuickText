using QuickText.Core.Skills;

namespace QuickText.Core.Tests;

public class SkillFavoriteStoreTests
{
    private static string TempFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "qt_fav_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "skills-favorites.json");
    }

    [Fact]
    public void Favorite_persists_name_body_and_alias()
    {
        var path = TempFile();
        try
        {
            var store = new SkillFavoriteStore(path);
            store.Favorite("demo", @"C:\skills\demo\SKILL.md", "short blurb", "演示");

            var reloaded = new SkillFavoriteStore(path);
            var item = Assert.Single(reloaded.Items);
            Assert.Equal("demo", item.Name);
            Assert.Equal(@"C:\skills\demo\SKILL.md", item.Body);
            Assert.Equal("short blurb", item.Description);
            Assert.Equal("演示", item.Alias);
            Assert.True(reloaded.IsFavorite(@"C:\skills\demo\SKILL.md"));
        }
        finally
        {
            var dir = Path.GetDirectoryName(path)!;
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Unfavorite_and_alias_updates_file()
    {
        var path = TempFile();
        try
        {
            var store = new SkillFavoriteStore(path);
            store.Favorite("a", @"D:\a\SKILL.md");
            store.SetAlias(@"D:\a\SKILL.md", "别名");
            Assert.Equal("别名", store.FindByBody(@"D:\a\SKILL.md")!.Alias);

            Assert.True(store.Unfavorite(@"D:\a\SKILL.md"));
            Assert.Empty(new SkillFavoriteStore(path).Items);
        }
        finally
        {
            var dir = Path.GetDirectoryName(path)!;
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
