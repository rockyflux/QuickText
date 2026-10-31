using QuickText.Core.Persistence;
using QuickText.Core.Seed;

namespace QuickText.Core.Tests;

public class BuiltinSeedTests
{
    private static readonly string[] ZhHansCategories =
    {
        "资料信息",
        "开发类提示词",
        "其他类提示词",
        "Skills",
    };

    [Fact]
    public void CategoriesFor_ZhHans_HasPromptCategoriesWithVariables()
    {
        var cats = BuiltinSeed.CategoriesFor("zh-Hans");
        Assert.Equal(ZhHansCategories, cats.Select(c => c.Name));
        Assert.Equal(
            new[] { "手机号码", "邮箱", "住址", "身份证号码", "公司名称" },
            cats[0].Snippets.Select(s => s.Name));
        Assert.Equal(8, cats[1].Snippets.Count);
        Assert.Equal(15, cats[2].Snippets.Count);
        Assert.All(cats[1].Snippets, s => Assert.True(s.UseVariables));
        Assert.All(cats[2].Snippets, s => Assert.True(s.UseVariables));
        Assert.Contains("{技术栈}", cats[1].Snippets[0].Body);
        Assert.Contains("{剪贴板}", cats[1].Snippets[1].Body);
        Assert.Equal("周报", cats[2].Snippets[0].Name);
    }

    [Fact]
    public void CategoryNames_En_MatchPromptSet()
    {
        Assert.Equal(
            new[] { "Profile", "Dev prompts", "Other prompts", "Skills" },
            BuiltinSeed.CategoryNames("en"));
    }

    [Fact]
    public void WriteMissing_SkipsExistingNames()
    {
        var root = Path.Combine(Path.GetTempPath(), "qt-seed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new Store(root);
            store.SaveCategory(new Models.Category { Name = "资料信息" });

            var (added, skipped) = BuiltinSeed.WriteMissing(store, "zh-Hans");
            Assert.Equal(ZhHansCategories.Length - 1, added);
            Assert.Equal(1, skipped);

            var all = store.LoadAll();
            Assert.Equal(ZhHansCategories.Length, all.Count);
            Assert.Empty(all.First(c => c.Name == "资料信息").Snippets);
            Assert.Equal(8, all.First(c => c.Name == "开发类提示词").Snippets.Count);
            Assert.Equal(15, all.First(c => c.Name == "其他类提示词").Snippets.Count);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* temp cleanup */ }
        }
    }

    [Theory]
    [InlineData("zh-CN", "zh-Hans")]
    [InlineData("zh-TW", "zh-Hant")]
    [InlineData("ja-JP", "ja")]
    [InlineData("en-US", "en")]
    [InlineData("de-DE", "en")]
    public void LangFor_MapsCulture(string culture, string expected) =>
        Assert.Equal(expected, BuiltinSeed.LangFor(new System.Globalization.CultureInfo(culture)));
}
