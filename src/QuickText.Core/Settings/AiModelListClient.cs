using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace QuickText.Core.Settings;

/// <summary>Fetches model ids from an OpenAI-compatible (or Anthropic) <c>/models</c> endpoint.
/// Used by the settings dialog so the picker reflects what the server currently exposes.</summary>
public static class AiModelListClient
{
    // Shared client: short timeout so a dead localhost (magpie off) fails fast in the dialog.
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(6) };

    /// <summary>GET models; returns sorted unique ids. Throws on HTTP/parse failure (caller falls back).</summary>
    public static async Task<IReadOnlyList<string>> FetchAsync(
        string provider, string baseUrl, string? apiKey, CancellationToken ct = default)
    {
        var url = ModelsUrl(provider, baseUrl);
        if (url == null) throw new InvalidOperationException("Base URL is empty.");

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyAuth(req, provider, apiKey);

        using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        return ParseModelIds(doc.RootElement);
    }

    public static string? ModelsUrl(string? provider, string? baseUrl)
    {
        var b = (baseUrl ?? "").Trim().TrimEnd('/');
        if (b.Length == 0) return null;
        if (AiProviders.Normalize(provider) == AiProviders.Anthropic)
        {
            // Stored Anthropic base is https://api.anthropic.com (no /v1).
            if (b.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)) return b + "/models";
            return b + "/v1/models";
        }
        return b + "/models";
    }

    private static void ApplyAuth(HttpRequestMessage req, string? provider, string? apiKey)
    {
        var key = (apiKey ?? "").Trim();
        if (AiProviders.Normalize(provider) == AiProviders.Anthropic)
        {
            if (key.Length > 0) req.Headers.TryAddWithoutValidation("x-api-key", key);
            req.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
            return;
        }
        if (key.Length > 0)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
    }

    /// <summary>OpenAI / Anthropic list shape: <c>{ "data": [ { "id": "…" }, … ] }</c>.</summary>
    public static IReadOnlyList<string> ParseModelIds(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in data.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            if (!item.TryGetProperty("id", out var idEl)) continue;
            var id = idEl.GetString()?.Trim();
            if (!string.IsNullOrEmpty(id)) set.Add(id);
        }
        return set.ToList();
    }
}
