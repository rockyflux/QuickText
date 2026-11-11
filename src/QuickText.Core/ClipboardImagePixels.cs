namespace QuickText.Core;

/// <summary>
/// Pixel-buffer helpers for clipboard bitmaps that WPF surfaces as BGRA with a broken alpha
/// channel. Kept free of WPF so the rule can be unit-tested without a UI harness.
/// </summary>
public static class ClipboardImagePixels
{
    /// <summary>
    /// WPF's <c>Clipboard.GetImage()</c> often yields a 32bpp buffer whose alpha bytes are all
    /// zero even though RGB is intact (CF_DIB has no alpha; WPF still tags the buffer as BGRA).
    /// Encoding that buffer to PNG produces a fully transparent — visually blank — image.
    /// When every alpha byte is 0 and at least one RGB channel is non-zero, force alpha to 255.
    /// Leaves genuine transparent images (any non-zero alpha) and empty buffers alone.
    /// </summary>
    /// <returns>True when alpha was rewritten.</returns>
    public static bool FixFullyTransparentRgb(Span<byte> bgraPixels)
    {
        if (bgraPixels.Length < 4 || (bgraPixels.Length & 3) != 0)
            return false;

        bool anyAlpha = false;
        bool anyColor = false;
        for (int i = 0; i < bgraPixels.Length; i += 4)
        {
            if (bgraPixels[i + 3] != 0)
            {
                anyAlpha = true;
                break;
            }
            if (bgraPixels[i] != 0 || bgraPixels[i + 1] != 0 || bgraPixels[i + 2] != 0)
                anyColor = true;
        }

        if (anyAlpha || !anyColor)
            return false;

        for (int i = 3; i < bgraPixels.Length; i += 4)
            bgraPixels[i] = 255;
        return true;
    }
}
