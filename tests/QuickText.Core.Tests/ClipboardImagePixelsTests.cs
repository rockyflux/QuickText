using QuickText.Core;

namespace QuickText.Core.Tests;

public class ClipboardImagePixelsTests
{
    [Fact]
    public void FixFullyTransparentRgb_forces_opaque_when_alpha_all_zero_but_rgb_present()
    {
        // Two BGRA pixels: light gray + blue, alpha cleared the way Clipboard.GetImage often does.
        byte[] pixels =
        [
            246, 246, 246, 0,
            200, 40, 10, 0,
        ];

        Assert.True(ClipboardImagePixels.FixFullyTransparentRgb(pixels));
        Assert.Equal(255, pixels[3]);
        Assert.Equal(255, pixels[7]);
        Assert.Equal(246, pixels[0]);
        Assert.Equal(200, pixels[4]);
    }

    [Fact]
    public void FixFullyTransparentRgb_leaves_real_transparency_alone()
    {
        byte[] pixels =
        [
            10, 20, 30, 0,
            40, 50, 60, 128, // intentional partial alpha
        ];

        Assert.False(ClipboardImagePixels.FixFullyTransparentRgb(pixels));
        Assert.Equal(0, pixels[3]);
        Assert.Equal(128, pixels[7]);
    }

    [Fact]
    public void FixFullyTransparentRgb_leaves_empty_buffer_alone()
    {
        byte[] pixels =
        [
            0, 0, 0, 0,
            0, 0, 0, 0,
        ];

        Assert.False(ClipboardImagePixels.FixFullyTransparentRgb(pixels));
        Assert.Equal(0, pixels[3]);
    }

    [Fact]
    public void FixFullyTransparentRgb_rejects_truncated_buffers()
    {
        byte[] pixels = [1, 2, 3, 0, 4]; // not a multiple of 4
        Assert.False(ClipboardImagePixels.FixFullyTransparentRgb(pixels));
        Assert.Equal(0, pixels[3]);
    }
}
