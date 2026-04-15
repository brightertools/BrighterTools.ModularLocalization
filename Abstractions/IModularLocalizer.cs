namespace BrighterTools.ModularLocalization.Abstractions;

public interface IModularLocalizer
{
    string Get(string key, string defaultValue);

    string GetPlural(string key, int count, string defaultOne, string defaultOther);

    string Format(string key, string defaultValue, object values);

    Task<IReadOnlyDictionary<string, string>> GetCultureDictionaryAsync(
        string culture,
        Guid? tenantId,
        CancellationToken ct = default);
}
