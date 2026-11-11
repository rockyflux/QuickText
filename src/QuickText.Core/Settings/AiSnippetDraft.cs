namespace QuickText.Core.Settings;

/// <summary>One AI-generated snippet draft ready to fill the Manager editor.</summary>
public sealed record AiSnippetDraft(string Name, string Abbr, string Body, bool UseVariables = false);

/// <summary>Built-in scenario presets for the AI generate dialog (dropdown). Ids are stable;
/// labels are localized. Shaped like the app's built-in categories (资料信息 / 开发类提示词 /
/// 其他类提示词 / Skills), not generic "email/chat" copywriting.</summary>
public static class AiSnippetTemplates
{
    public const string General = "general";
    public const string Profile = "profile";
    public const string Dev = "dev";
    public const string Office = "office";
    public const string Agent = "agent";

    public static readonly string[] All =
    {
        General, Profile, Dev, Office, Agent,
    };

    /// <summary>English guidance embedded in the model prompt (not user-facing).</summary>
    public static string Guidance(string? id) => (id ?? General) switch
    {
        Profile =>
            "Scenario: personal/profile facts for a text expander (phone, email, address, company, ID placeholder). " +
            "Body is usually plain paste text WITHOUT {placeholders}. useVariables=false.",
        Dev =>
            "Scenario: a reusable AI/coding prompt the user pastes into ChatGPT/Cursor/IDE. " +
            "Prefer QuickText placeholders: {剪贴板} for code/logs to analyze, {技术栈}, {语言}, " +
            "{选项名|A|B} for choices, {字段:默认} for defaults. useVariables=true when any {…} is used.",
        Office =>
            "Scenario: office collaboration expander — weekly/daily report, meeting notes, formal email, " +
            "requirement write-up. Use {姓名}, {日期}, {剪贴板}, {事项1}, option lists where they help reuse. " +
            "useVariables=true when placeholders appear.",
        Agent =>
            "Scenario: an Agent Skill / system-style prompt (role, task, constraints, output format). " +
            "Often ends with material from {剪贴板}. useVariables=true when placeholders appear.",
        _ =>
            "Scenario: a general QuickText expander snippet. Use {placeholders} only when the same " +
            "snippet will be reused with changing fields; otherwise plain text. Set useVariables accordingly.",
    };
}
