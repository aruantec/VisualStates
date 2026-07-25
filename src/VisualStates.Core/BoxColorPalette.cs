namespace VisualStates.Core;

public static class BoxColorPalette
{
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

    public static string PickRandom(Random? random = null)
    {
        var rng = random ?? Random.Shared;
        return Colors[rng.Next(Colors.Length)];
    }

    public static string PickForId(string id)
    {
        var hash = unchecked((uint)id.GetHashCode());
        return Colors[hash % Colors.Length];
    }

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

    public static string Normalize(string? color, string fallbackId) =>
        string.IsNullOrWhiteSpace(color) ? PickForId(fallbackId) : color.Trim();

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
