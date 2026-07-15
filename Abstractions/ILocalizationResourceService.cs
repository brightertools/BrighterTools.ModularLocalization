namespace BrighterTools.ModularLocalization.Abstractions;

public interface ILocalizationResourceService
{
    Task<IReadOnlyDictionary<string, string>> GetResourcesAsync(
        string culture,
        string? namespaceName = null,
        CancellationToken ct = default);

    Task RegisterResourceAsync(
        string key,
        string defaultValue,
        CancellationToken ct = default);

    Task<LocalizationResourceSyncResult> RegisterResourcesAsync(
        IReadOnlyCollection<LocalizationResourceDefinition> resources,
        CancellationToken ct = default);
}
