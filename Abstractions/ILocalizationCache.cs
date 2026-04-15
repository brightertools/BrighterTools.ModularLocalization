namespace BrighterTools.ModularLocalization.Abstractions;

public interface ILocalizationCache
{
    Task<IReadOnlyDictionary<string, string>?> TryGetAsync(
        Guid? tenantId,
        string culture,
        CancellationToken ct);

    Task SetAsync(
        Guid? tenantId,
        string culture,
        IReadOnlyDictionary<string, string> value,
        TimeSpan ttl,
        CancellationToken ct);

    Task InvalidateAsync(
        Guid? tenantId,
        string culture,
        CancellationToken ct);
}