namespace BrighterTools.ModularLocalization.Abstractions;

public interface ILocalizationStore
{
    Task<IReadOnlyDictionary<string, string>> LoadCultureDictionaryAsync(
        string requestedCulture,
        Guid? tenantId,
        CancellationToken ct);

    Task TryAutoRegisterKeyAsync(Guid? tenantId, string key, string defaultValue, CancellationToken ct);
}
