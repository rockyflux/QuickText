using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace QuickText.Core.Settings;

/// <summary>One-shot chat completion against an OpenAI-compatible, Anthropic, or Azure endpoint.
/// Mirrors <see cref="AiModelListClient"/> auth/URL conventions so settings profiles work as-is.</summary>
public static class AiChatClient
{
    // Longer than /models: generation can take a while on local or slow hosts.
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(90) };

    /// <summary>POST a system+user turn; returns the assistant text. Throws on HTTP/parse failure.</summary>
    public static async Task<string> CompleteAsync(
        AiProviderConfig cfg, string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(cfg);
        var provider = AiProviders.Normalize(cfg.Provider);
        var baseUrl = AiProviders.EffectiveBaseUrl(cfg);
        if (baseUrl.Length == 0)
            throw new InvalidOperationException("Base URL is empty.");
        var model = (cfg.Model ?? "").Trim();
        if (model.Length == 0)
            throw new InvalidOperationException("Model is empty.");

        var url = CompletionsUrl(provider, baseUrl, model);
        if (url == null) throw new InvalidOperationException("Could not build completions URL.");

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        ApplyAuth(req, provider, cfg.ApiKey);
        req.Content = new StringContent(
            BuildRequestBody(provider, model, systemPrompt, userPrompt),
            Encoding.UTF8,
            "application/json");

        using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"HTTP {(int)resp.StatusCode}: {Truncate(body, 240)}");

        return ExtractAssistantText(provider, body);
    }

    /// <summary>OpenAI-compatible <c>/chat/completions</c>, Anthropic <c>/v1/messages</c>, or Azure deployment URL.</summary>
    public static string? CompletionsUrl(string? provider, string? baseUrl, string? model)
    {
        var b = (baseUrl ?? "").Trim().TrimEnd('/');
        if (b.Length == 0) return null;
        var p = AiProviders.Normalize(provider);
        if (p == AiProviders.Anthropic)
        {
            if (b.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)) return b + "/messages";
            return b + "/v1/messages";
        }
        if (p == AiProviders.Azure)
        {
            var dep = (model ?? "").Trim().Trim('/');
            if (dep.Length == 0) return null;
            // Deployment name is the Azure "model"; api-version is required by the service.
            return $"{b}/openai/deployments/{Uri.EscapeDataString(dep)}/chat/completions?api-version=2024-06-01";
        }
        return b + "/chat/completions";
    }

    public static string BuildRequestBody(
        string? provider, string model, string systemPrompt, string userPrompt)
    {
        if (AiProviders.Normalize(provider) == AiProviders.Anthropic)
        {
            // Anthropic uses top-level "system" + messages[]; max_tokens is required.
            using var stream = new MemoryStream();
            using (var w = new Utf8JsonWriter(stream))
            {
                w.WriteStartObject();
                w.WriteString("model", model);
                w.WriteNumber("max_tokens", 2048);
                w.WriteString("system", systemPrompt ?? "");
                w.WritePropertyName("messages");
                w.WriteStartArray();
                w.WriteStartObject();
                w.WriteString("role", "user");
                w.WriteString("content", userPrompt ?? "");
                w.WriteEndObject();
                w.WriteEndArray();
                w.WriteEndObject();
            }
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            writer.WriteStartObject();
            // Azure puts the deployment in the URL; body model is still accepted by many gateways.
            writer.WriteString("model", model);
            writer.WriteNumber("temperature", 0.4);
            writer.WritePropertyName("messages");
            writer.WriteStartArray();
            WriteMessage(writer, "system", systemPrompt);
            WriteMessage(writer, "user", userPrompt);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void WriteMessage(Utf8JsonWriter w, string role, string? content)
    {
        w.WriteStartObject();
        w.WriteString("role", role);
        w.WriteString("content", content ?? "");
        w.WriteEndObject();
    }

    public static string ExtractAssistantText(string? provider, string responseJson)
    {
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;
        if (AiProviders.Normalize(provider) == AiProviders.Anthropic)
        {
            if (!root.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                throw new FormatException("Anthropic response missing content[].");
            var sb = new StringBuilder();
            foreach (var part in content.EnumerateArray())
            {
                if (part.ValueKind != JsonValueKind.Object) continue;
                if (part.TryGetProperty("type", out var t) &&
                    !string.Equals(t.GetString(), "text", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (part.TryGetProperty("text", out var text))
                    sb.Append(text.GetString());
            }
            return sb.ToString();
        }

        // OpenAI / Azure / Magpie / DeepSeek / custom: choices[0].message.content
        if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array
            || choices.GetArrayLength() == 0)
            throw new FormatException("OpenAI response missing choices[].");
        var first = choices[0];
        if (first.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var c))
            return c.GetString() ?? "";
        if (first.TryGetProperty("text", out var legacy))
            return legacy.GetString() ?? "";
        throw new FormatException("OpenAI choice has no message.content.");
    }

    private static void ApplyAuth(HttpRequestMessage req, string? provider, string? apiKey)
    {
        var key = (apiKey ?? "").Trim();
        var p = AiProviders.Normalize(provider);
        if (p == AiProviders.Anthropic)
        {
            if (key.Length > 0) req.Headers.TryAddWithoutValidation("x-api-key", key);
            req.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
            return;
        }
        if (p == AiProviders.Azure)
        {
            // Azure accepts api-key; Bearer also works for some Entra setups — prefer api-key for keys.
            if (key.Length > 0) req.Headers.TryAddWithoutValidation("api-key", key);
            return;
        }
        if (key.Length > 0)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
