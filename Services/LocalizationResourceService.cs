using BrighterTools.ModularLocalization.Abstractions;
using Microsoft.Extensions.Options;

namespace BrighterTools.ModularLocalization.Services;

internal sealed class LocalizationResourceService : ILocalizationResourceService
{
    private readonly IModularLocalizer _localizer;
    private readonly ILocalizationStore _store;
    private readonly ILocalizationCache _cache;
    private readonly LocalizationOptions _options;

    public LocalizationResourceService(
        IModularLocalizer localizer,
        ILocalizationStore store,
        ILocalizationCache cache,
        IOptions<LocalizationOptions> options)
    {
        _localizer = localizer;
        _store = store;
        _cache = cache;
        _options = options.Value;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetResourcesAsync(
        string culture,
        string? namespaceName = null,
        CancellationToken ct = default)
    {
        var dictionary = await _localizer.GetCultureDictionaryAsync(culture, null, ct).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(namespaceName))
        {
            return dictionary;
        }

        var prefix = namespaceName.Trim() + ".";
        return dictionary
            .Where(x => x.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                x => x.Key[prefix.Length..],
                x => x.Value,
                StringComparer.OrdinalIgnoreCase);
    }

    public async Task RegisterResourceAsync(
        string key,
        string defaultValue,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var normalizedKey = key.Trim();
        var normalizedDefaultValue = string.IsNullOrWhiteSpace(defaultValue)
            ? normalizedKey
            : defaultValue;

        await _store.TryAutoRegisterKeyAsync(null, normalizedKey, normalizedDefaultValue, ct)
            .ConfigureAwait(false);

        var culturesToInvalidate = _options.WarmupCultures
            .Append(_options.DefaultCulture)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var culture in culturesToInvalidate)
        {
            await _cache.InvalidateAsync(null, culture, ct).ConfigureAwait(false);
        }
    }
}
