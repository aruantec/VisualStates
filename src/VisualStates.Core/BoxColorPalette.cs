namespace VisualStates.Core;

/// <summary>
/// Shared accent-color palette for box headers and zone borders.
/// Provides deterministic and least-used color selection helpers.
/// </summary>
public static class BoxColorPalette
{
    /// <summary>Fixed set of hex colors used across the editor.</summary>
    public static readonly string[] Colors =
    [
        "#E74C3C",
        "#E67E22",
        "#D4AC0D",
        "#58D68D",
        "#1ABC9C",
        "#3498DB",
        "#5B6EE1",
        "#9B59B6",
        "#E056A0",
        "#EC7063",
        "#48C9B0",
        "#5DADE2",
        "#AF7AC5",
        "#F1948A",
        "#52BE80",
        "#5499C7"
    ];

    /// <summary>Picks a random color from <see cref="Colors"/>.</summary>
    /// <param name="random">Optional RNG; defaults to <see cref="Random.Shared"/>.</param>
    public static string PickRandom(Random? random = null)
    {
        var rng = random ?? Random.Shared;
        return Colors[rng.Next(Colors.Length)];
    }

    /// <summary>
    /// Picks a stable color for an entity id by hashing it into the palette.
    /// </summary>
    /// <param name="id">Entity id (box or zone).</param>
    public static string PickForId(string id)
    {
        var hash = unchecked((uint)id.GetHashCode());
        return Colors[hash % Colors.Length];
    }

    /// <summary>
    /// Picks the first palette color not already present in
    /// <paramref name="usedColors"/>, or a random color when every slot is taken.
    /// </summary>
    /// <param name="usedColors">Colors already assigned to siblings.</param>
    public static string PickNext(IEnumerable<string?> usedColors)
    {
        var used = new HashSet<string>(
            usedColors.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!.ToUpperInvariant()));

        foreach (var color in Colors)
        {
            if (!used.Contains(color.ToUpperInvariant()))
                return color;
        }

        return PickRandom();
    }

    /// <summary>
    /// Returns <paramref name="color"/> trimmed when non-empty; otherwise a
    /// deterministic color derived from <paramref name="fallbackId"/>.
    /// </summary>
    /// <param name="color">Stored color, which may be null or blank.</param>
    /// <param name="fallbackId">Id used when no color is stored.</param>
    public static string Normalize(string? color, string fallbackId) =>
        string.IsNullOrWhiteSpace(color) ? PickForId(fallbackId) : color.Trim();

    /// <summary>
    /// Parses a hex color into RGB bytes, falling back to a palette color for
    /// <paramref name="fallbackId"/> (or a default blue) when the value is invalid.
    /// </summary>
    /// <param name="color">Hex string such as <c>#E74C3C</c>.</param>
    /// <param name="fallbackId">Id used when parsing fails.</param>
    public static (byte R, byte G, byte B) ParseRgb(string? color, string fallbackId)
    {
        var hex = Normalize(color, fallbackId);
        if (hex.StartsWith('#'))
            hex = hex[1..];

        if (hex.Length != 6)
            return (45, 110, 180);

        return (
            Convert.ToByte(hex[..2], 16),
            Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..6], 16));
    }
}
