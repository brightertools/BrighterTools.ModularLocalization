using BrighterTools.ModularLocalization.EF.Entities;

namespace BrighterTools.ModularLocalization.Internal;

internal static class DictionaryAssembler
{
    public static IReadOnlyDictionary<string, string> AssembleI18NextDictionary(
        string requestedCulture,
        IReadOnlyList<string> cultureChain,
        string defaultCulture,
        Guid? tenantId,
        IReadOnlyList<TranslationKey> keys,
        IReadOnlyList<TranslationValue> values)
    {
        // final output: Dictionary<string, string> where keys include plural suffix like i18next:
        // "Cart.Items_one", "Cart.Items_other", and non-plural just "Cart.Title"
        var output = new Dictionary<string, string>(StringComparer.Ordinal);

        // Pre-load key defaults (tenant-specific can override global by having same Key with tenantId)
        // Precedence for TranslationKey itself: tenant -> global
        var keyByName = new Dictionary<string, TranslationKey>(StringComparer.Ordinal);
        foreach (var k in keys
            .OrderByDescending(k => k.TenantId == tenantId) // tenant first
            .ThenBy(k => k.TenantId == Guid.Empty))              // then global
        {
            if (!keyByName.ContainsKey(k.Key))
                keyByName[k.Key] = k;
        }

        // Sort translation values by precedence:
        // tenant before global; culture chain earlier before later; plural exact before null? (we store plural and non-plural separately anyway)
        int CultureRank(string c)
        {
            for (int i = 0; i < cultureChain.Count; i++)
                if (string.Equals(cultureChain[i], c, StringComparison.OrdinalIgnoreCase))
                    return i;
            return int.MaxValue;
        }

        var ordered = values
            .OrderByDescending(v => v.TenantId == tenantId)  // tenant first
            .ThenBy(v => v.TenantId == Guid.Empty)                // then global
            .ThenBy(v => CultureRank(v.Culture));            // exact -> parent -> default

        // Apply first-win semantics with correct precedence order.
        foreach (var v in ordered)
        {
            var baseKey = v.TranslationKey.Key;
            var outKey = v.PluralCategory is null
                ? baseKey
                : $"{baseKey}_{v.PluralCategory}";

            if (!output.ContainsKey(outKey))
                output[outKey] = v.Value;
        }

        // Ensure non-plural keys exist at least with DefaultValue if missing (useful for frontend export)
        foreach (var kvp in keyByName)
        {
            if (!output.ContainsKey(kvp.Key))
                output[kvp.Key] = kvp.Value.DefaultValue;
        }

        return output;
    }
}