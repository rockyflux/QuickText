namespace QuickText.App.Interop;

/// <summary>Single place for per-snippet OutputMode vs global paste/copy + AutoSend.
/// Search panel and abbreviation expand must stay in lockstep.</summary>
internal static class OutputModeRules
{
    /// <param name="outputMode">Snippet override: "" follow global, "paste", "paste-enter", "copy".</param>
    /// <param name="copyToClipboardOnly">Global "仅复制".</param>
    /// <param name="autoSend">Global "粘贴后自动回车".</param>
    /// <param name="hasCursor">Body has a {光标} token — never auto-Enter.</param>
    public static (bool CopyOnly, bool AutoSend) Resolve(
        string? outputMode, bool copyToClipboardOnly, bool autoSend, bool hasCursor)
    {
        var mode = outputMode ?? "";
        bool copyOnly = mode == "copy" || (mode.Length == 0 && copyToClipboardOnly);
        bool enter = mode switch
        {
            "paste-enter" => true,
            "paste" => false,
            _ => autoSend,
        } && !hasCursor;
        return (copyOnly, enter);
    }
}
