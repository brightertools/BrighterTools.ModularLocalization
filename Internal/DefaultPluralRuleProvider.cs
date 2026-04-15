using BrighterTools.ModularLocalization.Abstractions;

namespace BrighterTools.ModularLocalization.Internal;

internal sealed class DefaultPluralRuleProvider : IPluralRuleProvider
{
    public string GetPluralCategory(string culture, int count)
    {
        // count handling
        if (count < 0) count = -count;

        // Normalize to language subtag for many rules
        var lang = GetLanguage(culture);

        // Skeleton: implement real rules via generated code table.
        // The key point: the rest of your system does not care how rules are computed.
        return lang switch
        {
            "en" => count == 1 ? PluralCategories.One : PluralCategories.Other,
            "fr" => (count == 0 || count == 1) ? PluralCategories.One : PluralCategories.Other,
            "ru" => GetRussian(count),
            _ => count == 1 ? PluralCategories.One : PluralCategories.Other
        };
    }

    private static string GetLanguage(string culture)
    {
        if (string.IsNullOrWhiteSpace(culture)) return "en";
        var dash = culture.IndexOf('-');
        return (dash > 0 ? culture[..dash] : culture).ToLowerInvariant();
    }

    private static string GetRussian(int n)
    {
        var mod10 = n % 10;
        var mod100 = n % 100;

        if (mod10 == 1 && mod100 != 11) return PluralCategories.One;
        if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return PluralCategories.Few;
        if (mod10 == 0 || (mod10 >= 5 && mod10 <= 9) || (mod100 >= 11 && mod100 <= 14)) return PluralCategories.Many;
        return PluralCategories.Other;
    }
}
