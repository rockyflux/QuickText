using QuickText.Core.Settings;

namespace QuickText.Core.Tests;

public class AiConfigListTests
{
    [Fact]
    public void DefaultOf_empty_or_null_is_null()
    {
        Assert.Null(AiConfigList.DefaultOf(null));
        Assert.Null(AiConfigList.DefaultOf(new List<AiProviderConfig>()));
    }

    [Fact]
    public void DefaultOf_returns_first()
    {
        var a = new AiProviderConfig { Name = "A" };
        var b = new AiProviderConfig { Name = "B" };
        Assert.Same(a, AiConfigList.DefaultOf(new List<AiProviderConfig> { a, b }));
    }

    [Fact]
    public void SetDefault_moves_item_to_front()
    {
        var a = new AiProviderConfig { Name = "A" };
        var b = new AiProviderConfig { Name = "B" };
        var c = new AiProviderConfig { Name = "C" };
        var list = new List<AiProviderConfig> { a, b, c };

        Assert.True(AiConfigList.SetDefault(list, b.Id));
        Assert.Equal(new[] { b, a, c }, list);
        Assert.False(AiConfigList.SetDefault(list, b.Id));   // already first
        Assert.False(AiConfigList.SetDefault(list, "missing"));
    }

    [Fact]
    public void AiConfigs_round_trip_and_absent_field_stays_empty()
    {
        var path = Path.Combine(Path.GetTempPath(), "qt-ai-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new SettingsStore(path);
            store.Save(new AppSettings
            {
                AiConfigs =
                {
                    new AiProviderConfig
                    {
                        Name = "Work",
                        Provider = AiProviders.DeepSeek,
                        ApiKey = "sk-test",
                        BaseUrl = "https://api.deepseek.com",
                        Model = "deepseek-chat",
                    },
                    new AiProviderConfig
                    {
                        Name = "Home",
                        Provider = AiProviders.OpenAi,
                        ApiKey = "sk-home",
                        Model = "gpt-4o-mini",
                    },
                },
            });
            var back = store.Load();
            Assert.Equal(2, back.AiConfigs.Count);
            Assert.Equal("Work", back.AiConfigs[0].Name);
            Assert.Equal(AiProviders.DeepSeek, back.AiConfigs[0].Provider);
            Assert.Equal("sk-test", back.AiConfigs[0].ApiKey);
            Assert.Equal("gpt-4o-mini", back.AiConfigs[1].Model);
            Assert.Same(back.AiConfigs[0], AiConfigList.DefaultOf(back.AiConfigs));

            // Old settings.json without AiConfigs must keep previous behaviour (empty list).
            File.WriteAllText(path, """{"Hotkey":"Ctrl+Shift+8"}""");
            var old = store.Load();
            Assert.Empty(old.AiConfigs);
            Assert.Null(AiConfigList.DefaultOf(old.AiConfigs));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Normalize_unknown_provider_becomes_custom()
    {
        Assert.Equal(AiProviders.OpenAi, AiProviders.Normalize("openai"));
        Assert.Equal(AiProviders.Custom, AiProviders.Normalize("moonshot"));
        Assert.Equal(AiProviders.Custom, AiProviders.Normalize(null));
    }

    [Fact]
    public void DeepSeek_Magpie_and_OpenAi_have_default_base_url_and_models()
    {
        Assert.Equal("http://127.0.0.1:3425/v1", AiProviders.DefaultBaseUrl(AiProviders.Magpie));
        Assert.Equal("https://api.deepseek.com/v1", AiProviders.DefaultBaseUrl(AiProviders.DeepSeek));
        Assert.Equal("https://api.openai.com/v1", AiProviders.DefaultBaseUrl(AiProviders.OpenAi));
        Assert.Equal("", AiProviders.DefaultBaseUrl(AiProviders.Azure));
        Assert.Equal("", AiProviders.DefaultBaseUrl(AiProviders.Custom));
        Assert.Equal("https://YOUR_RESOURCE.openai.azure.com/", AiProviders.BaseUrlPlaceholder(AiProviders.Azure));
        Assert.Equal("https://api.example.com/v1", AiProviders.BaseUrlPlaceholder(AiProviders.Custom));

        Assert.Contains("deepseek-flash", AiProviders.KnownModels(AiProviders.DeepSeek));
        Assert.DoesNotContain("deepseek-chat", AiProviders.KnownModels(AiProviders.DeepSeek));
        Assert.Equal("deepseek-flash", AiProviders.DefaultModel(AiProviders.DeepSeek));
        Assert.Contains("gpt-4o-mini", AiProviders.KnownModels(AiProviders.OpenAi));
        Assert.Empty(AiProviders.KnownModels(AiProviders.Custom));
        Assert.Empty(AiProviders.KnownModels(AiProviders.Magpie));   // live list from localhost

        var cfg = new AiProviderConfig { Provider = AiProviders.DeepSeek, BaseUrl = "" };
        Assert.Equal("https://api.deepseek.com/v1", AiProviders.EffectiveBaseUrl(cfg));
        Assert.True(AiProviders.IsDefaultBaseUrl(AiProviders.DeepSeek, "https://api.deepseek.com/v1/"));
        Assert.True(AiProviders.IsDefaultBaseUrl(AiProviders.Magpie, "http://127.0.0.1:3425/v1"));
    }

    [Fact]
    public void Model_list_client_builds_urls_and_parses_openai_shape()
    {
        Assert.Equal(
            "http://127.0.0.1:3425/v1/models",
            AiModelListClient.ModelsUrl(AiProviders.Magpie, AiProviders.MagpieBaseUrl));
        Assert.Equal(
            "https://api.deepseek.com/v1/models",
            AiModelListClient.ModelsUrl(AiProviders.DeepSeek, AiProviders.DeepSeekBaseUrl));
        Assert.Equal(
            "https://api.anthropic.com/v1/models",
            AiModelListClient.ModelsUrl(AiProviders.Anthropic, AiProviders.AnthropicBaseUrl));

        using var doc = System.Text.Json.JsonDocument.Parse(
            """{"data":[{"id":"deepseek-flash"},{"id":"b"},{"id":"deepseek-flash"}]}""");
        var ids = AiModelListClient.ParseModelIds(doc.RootElement);
        Assert.Equal(new[] { "b", "deepseek-flash" }, ids);   // sorted unique
    }
}
