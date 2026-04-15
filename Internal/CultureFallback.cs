namespace BrighterTools.ModularLocalization.Internal;

internal static class CultureFallback
{
    public static IReadOnlyList<string> BuildChain(string requestedCulture, string defaultCulture)
    {
        var list = new List<string>();

        static void AddIfValid(List<string> l, string? c)
        {
            if (string.IsNullOrWhiteSpace(c)) return;
            if (!l.Contains(c, StringComparer.OrdinalIgnoreCase)) l.Add(c);
        }

        AddIfValid(list, Normalize(requestedCulture));

        // parent: "fr-CA" -> "fr"
        var dash = requestedCulture.IndexOf('-');
        if (dash > 0)
            AddIfValid(list, Normalize(requestedCulture[..dash]));

        AddIfValid(list, Normalize(defaultCulture));

        return list;
    }

    private static string Normalize(string culture)
        => culture.Trim();
}
