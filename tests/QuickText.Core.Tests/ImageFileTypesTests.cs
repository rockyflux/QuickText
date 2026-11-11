using QuickText.Core;

namespace QuickText.Core.Tests;

public class ImageFileTypesTests
{
    [Theory]
    [InlineData(".png", true)]
    [InlineData(".PNG", true)]
    [InlineData(".jpg", true)]
    [InlineData(".jpeg", true)]
    [InlineData(".gif", true)]
    [InlineData(".bmp", true)]
    [InlineData(".webp", true)]
    [InlineData(".txt", false)]
    [InlineData(".exe", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Supported_extensions_match_the_file_picker(string? ext, bool expected)
        => Assert.Equal(expected, ImageFileTypes.IsSupportedExtension(ext));
}
