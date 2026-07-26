using VisualStates.Core;

namespace VisualStates.Tests;

public sealed class BoxColorPaletteTests
{
    [Fact]
    public void PickForId_IsStable()
    {
        var first = BoxColorPalette.PickForId("box-42");
        var second = BoxColorPalette.PickForId("box-42");

        Assert.Equal(first, second);
        Assert.Contains(first, BoxColorPalette.Colors);
    }

    [Fact]
    public void PickNext_SkipsUsedColors()
    {
        var used = BoxColorPalette.Colors.Take(3).Cast<string?>();
        var next = BoxColorPalette.PickNext(used);

        Assert.Equal(BoxColorPalette.Colors[3], next);
    }

    [Fact]
    public void PickNext_FallsBackToRandom_WhenAllUsed()
    {
        var next = BoxColorPalette.PickNext(BoxColorPalette.Colors);

        Assert.Contains(next, BoxColorPalette.Colors);
    }

    [Fact]
    public void Normalize_ReturnsTrimmedColor_WhenProvided()
    {
        Assert.Equal("#E74C3C", BoxColorPalette.Normalize("  #E74C3C  ", "id"));
    }

    [Fact]
    public void Normalize_FallsBackToPickForId_WhenBlank()
    {
        var expected = BoxColorPalette.PickForId("fallback");
        Assert.Equal(expected, BoxColorPalette.Normalize(null, "fallback"));
        Assert.Equal(expected, BoxColorPalette.Normalize("   ", "fallback"));
    }

    [Fact]
    public void ParseRgb_ParsesValidHex()
    {
        var (r, g, b) = BoxColorPalette.ParseRgb("#E74C3C", "id");

        Assert.Equal(0xE7, r);
        Assert.Equal(0x4C, g);
        Assert.Equal(0x3C, b);
    }

    [Fact]
    public void ParseRgb_ReturnsDefaultBlue_WhenInvalidAfterNormalizeFallbackFails()
    {
        // Normalize will pick a palette color for blank input, so use a malformed
        // non-blank value that survives Normalize but fails length check.
        var (r, g, b) = BoxColorPalette.ParseRgb("#XYZ", "id");

        // "#XYZ" has length 3 after stripping # → falls through to default blue
        Assert.Equal(45, r);
        Assert.Equal(110, g);
        Assert.Equal(180, b);
    }
}
