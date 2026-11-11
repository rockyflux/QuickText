using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QuickText.Core.Settings;

/// <summary>Builds prompts and parses model replies into <see cref="AiSnippetDraft"/>.
/// Network I/O lives in <see cref="AiChatClient"/>; this type stays pure for unit tests.</summary>
public static class AiSnippetGenerator
{
    private static readonly Regex Fence = new(
        @"^```(?:json)?\s*|\s*```$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Rough match for QuickText <c>{token}</c> placeholders (not JSON braces).</summary>
    private static readonly Regex PlaceholderToken = new(
        @"\{[^{}\n]{1,64}\}",
        RegexOptions.CultureInvariant);

    /// <summary>System instruction: JSON-only reply shaped for QuickText's expander + placeholder syntax.</summary>
    public static string SystemPrompt(string uiLanguage)
    {
        var lang = string.IsNullOrWhiteSpace(uiLanguage) ? "zh-Hans" : uiLanguage.Trim();
        return
            "You create snippets for QuickText, a Windows text-expander / snippet manager.\n" +
            "Users expand short abbreviations into paste-ready text (chat, IDE, email, AI tools).\n" +
            "Reply with ONLY one JSON object (no markdown, no commentary) with keys " +
            "\"name\", \"abbr\", \"body\", \"useVariables\".\n" +
            "- name: short human title\n" +
            "- abbr: 2–8 lowercase latin letters/digits, no spaces (pinyin initials OK)\n" +
            "- body: paste text; keep intentional newlines\n" +
            "- useVariables: true if body contains QuickText placeholders, else false\n" +
            "\n" +
            "QuickText placeholder syntax (use when the snippet should be reused with changing fields):\n" +
            "- {变量名} — prompt the user when expanding\n" +
            "- {变量名:默认值} — with default\n" +
            "- {变量名|选项A|选项B} — dropdown choices\n" +
            "- {剪贴板} — insert current clipboard (great for code/logs/notes already copied)\n" +
            "- {日期} / {时间} — current date/time\n" +
            "- {光标} — caret position after paste\n" +
            "Examples of good bodies:\n" +
            "- 帮我重构下面代码，语言：{语言}\\n代码：\\n{剪贴板}\n" +
            "- 日报，汇报人：{姓名}\\n日期：{日期}\\n今日完成：\\n{工作内容}\n" +
            "- Plain facts (phone/email) should NOT use placeholders.\n" +
            $"Write name and body in the UI language ({lang}). Abbr stays ASCII.";
    }

    /// <summary>User message: scenario guidance + free-form description.</summary>
    public static string UserPrompt(string? templateId, string userInput)
    {
        var input = (userInput ?? "").Trim();
        var sb = new StringBuilder();
        sb.AppendLine(AiSnippetTemplates.Guidance(templateId));
        sb.AppendLine();
        sb.Append("User request:");
        sb.AppendLine();
        sb.Append(input.Length > 0 ? input : "(empty — invent a useful QuickText snippet for this scenario)");
        return sb.ToString();
    }

    /// <summary>Parse model text into a draft. Throws <see cref="FormatException"/> when JSON is missing or invalid.</summary>
    public static AiSnippetDraft ParseResponse(string? content)
    {
        var raw = (content ?? "").Trim();
        if (raw.Length == 0) throw new FormatException("Empty model response.");

        // Strip a single ```json … ``` wrapper some models still emit despite instructions.
        if (raw.StartsWith("```", StringComparison.Ordinal))
        {
            var end = raw.LastIndexOf("```", StringComparison.Ordinal);
            if (end > 3)
            {
                var start = raw.IndexOf('\n');
                if (start > 0 && start < end)
                    raw = raw[(start + 1)..end].Trim();
                else
                    raw = Fence.Replace(raw, "").Trim();
            }
        }

        // If the model wrapped JSON in prose, take the outermost object.
        var objStart = raw.IndexOf('{');
        var objEnd = raw.LastIndexOf('}');
        if (objStart < 0 || objEnd <= objStart)
            throw new FormatException("Model response is not JSON.");
        raw = raw[objStart..(objEnd + 1)];

        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new FormatException("Model JSON root is not an object.");

        var name = ReadString(root, "name");
        var abbr = ReadString(root, "abbr");
        var body = ReadString(root, "body");
        if (name.Length == 0 && abbr.Length == 0 && body.Length == 0)
            throw new FormatException("Model JSON has empty name, abbr, and body.");

        abbr = SanitizeAbbr(abbr);
        var useVars = ReadBool(root, "useVariables") ?? LooksLikePlaceholders(body);
        // Placeholders only work when UseVariables is on — force true if body clearly has tokens.
        if (LooksLikePlaceholders(body)) useVars = true;
        return new AiSnippetDraft(name, abbr, body, useVars);
    }

    public static bool LooksLikePlaceholders(string? body) =>
        !string.IsNullOrEmpty(body) && PlaceholderToken.IsMatch(body);

    private static string ReadString(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var el)) return "";
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString()?.Trim() ?? "",
            JsonValueKind.Number => el.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => "",
        };
    }

    private static bool? ReadBool(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var el)) return null;
        return el.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(el.GetString(), out var b) ? b : null,
            _ => null,
        };
    }

    /// <summary>Drop whitespace and keep a compact trigger; empty stays empty.</summary>
    public static string SanitizeAbbr(string? abbr)
    {
        if (string.IsNullOrWhiteSpace(abbr)) return "";
        var sb = new StringBuilder(abbr.Length);
        foreach (var ch in abbr.Trim())
        {
            if (char.IsWhiteSpace(ch)) continue;
            // Strip common punctuation that would make expansion awkward to type.
            if (ch is '/' or '\\' or '.' or ',' or ';' or ':' or '!' or '?' or '"' or '\'' or '`'
                or '(' or ')' or '[' or ']' or '{' or '}' or '<' or '>')
                continue;
            sb.Append(ch);
        }
        return sb.ToString();
    }
}
