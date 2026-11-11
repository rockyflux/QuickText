namespace QuickText.Core.Settings;

/// <summary>One AI provider endpoint the user configured. The first item in
/// <see cref="AppSettings.AiConfigs"/> is the default used by AI features;
/// "switch default" means move another item to index 0.</summary>
public sealed class AiProviderConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    /// <summary>One of <see cref="AiProviders"/> ids (magpie, openai, anthropic, azure, deepseek, custom).</summary>
    public string Provider { get; set; } = AiProviders.OpenAi;
    public string ApiKey { get; set; } = "";
    /// <summary>Optional base URL. Required for azure/custom; overrides the provider default when set.</summary>
    public string BaseUrl { get; set; } = "";
    public string Model { get; set; } = "";
}

/// <summary>Known provider ids stored in <see cref="AiProviderConfig.Provider"/>.</summary>
public static class AiProviders
{
    public const string Magpie = "magpie";
    public const string OpenAi = "openai";
    public const string Anthropic = "anthropic";
    public const string Azure = "azure";
    public const string DeepSeek = "deepseek";
    public const string Custom = "custom";

    public static readonly string[] All =
    {
        Magpie, OpenAi, Anthropic, Azure, DeepSeek, Custom,
    };

    // Built-in endpoints / offline fallback catalogs. Live lists come from AiModelListClient.
    // Azure / custom stay blank — the user must supply the host.
    public const string MagpieBaseUrl = "http://127.0.0.1:3425/v1";
    public const string OpenAiBaseUrl = "https://api.openai.com/v1";
    public const string AnthropicBaseUrl = "https://api.anthropic.com";
    public const string DeepSeekBaseUrl = "https://api.deepseek.com/v1";
    public const string AzureBaseUrlExample = "https://YOUR_RESOURCE.openai.azure.com/";
    public const string CustomBaseUrlExample = "https://api.example.com/v1";

    private static readonly string[] OpenAiModels =
    {
        "gpt-4o-mini", "gpt-4o", "gpt-4.1-mini", "gpt-4.1", "o4-mini", "o3-mini",
    };
    private static readonly string[] AnthropicModels =
    {
        "claude-sonnet-4-5", "claude-opus-4-5", "claude-haiku-4-5",
        "claude-sonnet-4-0", "claude-opus-4-0", "claude-3-5-haiku-latest",
    };
    private static readonly string[] DeepSeekModels =
    {
        "deepseek-flash", "deepseek-reasoner",
    };
    private static readonly string[] AzureModels = OpenAiModels;

    public static bool IsKnown(string? id) =>
        id is Magpie or OpenAi or Anthropic or Azure or DeepSeek or Custom;

    /// <summary>Normalize a stored/typed id; unknown values become <see cref="Custom"/>.</summary>
    public static string Normalize(string? id) => IsKnown(id) ? id! : Custom;

    /// <summary>Default Base URL for known hosts; empty for Azure/custom (user must fill).</summary>
    public static string DefaultBaseUrl(string? provider) => Normalize(provider) switch
    {
        Magpie => MagpieBaseUrl,
        OpenAi => OpenAiBaseUrl,
        Anthropic => AnthropicBaseUrl,
        DeepSeek => DeepSeekBaseUrl,
        _ => "",
    };

    /// <summary>Placeholder example shown when the Base URL box is empty.</summary>
    public static string BaseUrlPlaceholder(string? provider) => Normalize(provider) switch
    {
        Magpie => MagpieBaseUrl,
        OpenAi => OpenAiBaseUrl,
        Anthropic => AnthropicBaseUrl,
        DeepSeek => DeepSeekBaseUrl,
        Azure => AzureBaseUrlExample,
        _ => CustomBaseUrlExample,
    };

    /// <summary>Offline fallback models when the live <c>/models</c> fetch fails or is skipped.</summary>
    public static IReadOnlyList<string> KnownModels(string? provider) => Normalize(provider) switch
    {
        OpenAi => OpenAiModels,
        Anthropic => AnthropicModels,
        DeepSeek => DeepSeekModels,
        Azure => AzureModels,
        _ => Array.Empty<string>(),
    };

    public static string DefaultModel(string? provider)
    {
        var list = KnownModels(provider);
        return list.Count > 0 ? list[0] : "";
    }

    /// <summary>Resolve the URL a future caller should hit: stored value, else the provider default.</summary>
    public static string EffectiveBaseUrl(AiProviderConfig cfg)
    {
        var typed = (cfg.BaseUrl ?? "").Trim().TrimEnd('/');
        if (typed.Length > 0) return typed;
        return DefaultBaseUrl(cfg.Provider).TrimEnd('/');
    }

    public static bool IsDefaultBaseUrl(string? provider, string? url)
    {
        var def = DefaultBaseUrl(provider);
        if (def.Length == 0) return string.IsNullOrWhiteSpace(url);
        var a = (url ?? "").Trim().TrimEnd('/');
        var b = def.TrimEnd('/');
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>List helpers: index 0 is the default; missing/empty list = no AI configured (old configs).</summary>
public static class AiConfigList
{
    public static AiProviderConfig? DefaultOf(IList<AiProviderConfig>? list) =>
        list is { Count: > 0 } ? list[0] : null;

    /// <summary>Move the item with <paramref name="id"/> to index 0. No-op if missing or already first.</summary>
    public static bool SetDefault(IList<AiProviderConfig> list, string id)
    {
        if (list.Count == 0 || string.IsNullOrEmpty(id)) return false;
        var i = -1;
        for (var n = 0; n < list.Count; n++)
        {
            if (list[n].Id == id) { i = n; break; }
        }
        if (i <= 0) return false;
        var item = list[i];
        list.RemoveAt(i);
        list.Insert(0, item);
        return true;
    }

    public static AiProviderConfig Clone(AiProviderConfig src) => new()
    {
        Id = src.Id,
        Name = src.Name,
        Provider = src.Provider,
        ApiKey = src.ApiKey,
        BaseUrl = src.BaseUrl,
        Model = src.Model,
    };

    public static List<AiProviderConfig> CloneAll(IEnumerable<AiProviderConfig>? src) =>
        src?.Select(Clone).ToList() ?? new List<AiProviderConfig>();

    /// <summary>Stable fingerprint for dirty-form comparison (includes secrets — settings stay local).</summary>
    public static string Fingerprint(IEnumerable<AiProviderConfig>? list) =>
        list == null
            ? ""
            : string.Join("\n", list.Select(c =>
                string.Join("\t", c.Id, c.Name, c.Provider, c.ApiKey, c.BaseUrl, c.Model)));
}
