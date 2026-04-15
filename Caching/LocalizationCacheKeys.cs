namespace BrighterTools.ModularLocalization.Caching;

public static class LocalizationCacheKeys
{
    public static string Build(Guid? tenantId, string culture)
        => $"localization:{tenantId?.ToString() ?? "global"}:{culture}";
}
