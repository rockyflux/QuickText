namespace QuickText.Core;

/// <summary>Extensions the manager accepts for snippet images (file picker and clipboard file drops).</summary>
public static class ImageFileTypes
{
    public const string OpenFileFilter =
        "Image (*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp";

    public static bool IsSupportedExtension(string? extension)
    {
        if (string.IsNullOrEmpty(extension)) return false;
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".gif", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }
}
