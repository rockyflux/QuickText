using System.Reflection;
using System.Text;

namespace QuickText.Core.Updates;

/// <summary>
/// Ships the published <c>docs/release-notes/v*.md</c> notes inside the assembly so Settings can
/// show "what's new in this build" without going online. Missing notes (a source zip, a tag with
/// no file yet) return null — the UI falls back to a localized empty message.
/// </summary>
public static class ReleaseNotes
{
    /// <summary>
    /// Load the release notes for <paramref name="version"/> (with or without a leading 'v').
    /// Returns null when no matching embedded file exists.
    /// </summary>
    public static string? TryLoad(string? version)
    {
        var normalized = UpdateCheck.NormalizeVersion(version);
        if (normalized.Length == 0) return null;

        var asm = typeof(ReleaseNotes).Assembly;
        var needle = $".v{normalized}.md";
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(needle, StringComparison.OrdinalIgnoreCase));
        if (name is null) return null;

        using var stream = asm.GetManifestResourceStream(name);
        if (stream is null) return null;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var text = reader.ReadToEnd().Trim();
        return text.Length == 0 ? null : text;
    }
}
