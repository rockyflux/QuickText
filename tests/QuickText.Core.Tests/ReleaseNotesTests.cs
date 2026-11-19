using QuickText.Core.Updates;

namespace QuickText.Core.Tests;

public class ReleaseNotesTests
{
    [Theory]
    [InlineData("1.1.0")]
    [InlineData("v1.1.0")]
    [InlineData("1.0.0")]
    [InlineData("1.1.3")]
    public void TryLoad_returns_published_notes_for_known_versions(string version)
    {
        var notes = ReleaseNotes.TryLoad(version);
        Assert.False(string.IsNullOrWhiteSpace(notes));
        Assert.Contains('-', notes);
    }

    [Fact]
    public void TryLoad_returns_null_for_unknown_version()
    {
        Assert.Null(ReleaseNotes.TryLoad("9.9.9"));
        Assert.Null(ReleaseNotes.TryLoad(""));
        Assert.Null(ReleaseNotes.TryLoad(null));
    }

    [Fact]
    public void FormatForDisplay_turns_markdown_list_markers_into_bullets()
    {
        var formatted = ReleaseNotes.FormatForDisplay("- first\n* second\nplain");
        Assert.Equal("• first\n• second\nplain", formatted);
    }
}
