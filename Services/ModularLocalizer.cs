using BrighterTools.ModularLocalization;
using BrighterTools.ModularLocalization.Abstractions;
using BrighterTools.ModularLocalization.Caching;
using BrighterTools.ModularLocalization.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;

internal sealed class ModularLocalizer : IModularLocalizer
{
    private readonly ILocalizationLoadCoordinator _loadCoordinator;
    private readonly ILocalizationStore _store;
    private readonly ILocalizationCache _cache;
    private readonly ICultureResolver _cultureResolver;
    private readonly IPluralRuleProvider _pluralRules;
    private readonly IMissingTranslationLogger _missingLogger;
    private readonly LocalizationOptions _opt;
    private readonly ILogger<ModularLocalizer> _logger;
    private readonly CacheExpiryTracker _expiryTracker;

    public ModularLocalizer(
        ILocalizationLoadCoordinator loadCoordinator,
        ILocalizationStore store,
        ILocalizationCache cache,
        ICultureResolver cultureResolver,
        IPluralRuleProvider pluralRules,
        IMissingTranslationLogger missingLogger,
        IOptions<LocalizationOptions> opt,
        ILogger<ModularLocalizer> logger,
        CacheExpiryTracker expiryTracker)
    {
        _loadCoordinator = loadCoordinator;
        _store = store;
        _cache = cache;
        _cultureResolver = cultureResolver;
        _pluralRules = pluralRules;
        _missingLogger = missingLogger;
        _opt = opt.Value;
        _logger = logger;
        _expiryTracker = expiryTracker;

        if (_opt.EnableRefreshAhead &&
            _opt.RefreshAheadWindow >= _opt.CacheTtl)
        {
            throw new InvalidOperationException(
                "RefreshAheadWindow must be less than CacheTtl.");
        }
    }

    public string Get(string key, string defaultValue)
        => GetAsyncInternal(key, defaultValue).GetAwaiter().GetResult();

    public string GetPlural(string key, int count, string defaultOne, string defaultOther)
        => GetPluralAsyncInternal(key, count, defaultOne, defaultOther).GetAwaiter().GetResult();

    private async Task<string> GetAsyncInternal(string key, string defaultValue)
    {
        var culture = NormalizeCulture(_cultureResolver.GetCurrentCulture().Name);

        try
        {
            var dict = await EnsureDictionaryLoadedAsync(culture);

            if (dict.TryGetValue(key, out var value))
                return value;

            _missingLogger.LogMissingOnce(null, culture, key);

            if (_opt.AutoRegisterMissingKeys)
            {
                await _store.TryAutoRegisterKeyAsync(null, key, defaultValue, CancellationToken.None);
                await _cache.InvalidateAsync(null, culture, CancellationToken.None);
            }

            return defaultValue;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Localization Get failed.");
            return defaultValue;
        }
    }

    private async Task<string> GetPluralAsyncInternal(
        string key,
        int count,
        string defaultOne,
        string defaultOther)
    {
        var culture = NormalizeCulture(_cultureResolver.GetCurrentCulture().Name);
        var category = _pluralRules.GetPluralCategory(culture, count);

        var suffixKey = $"{key}_{category}";
        var fallbackOtherKey = $"{key}_other";
        var defaultValue = category == "one" ? defaultOne : defaultOther;

        try
        {
            var dict = await EnsureDictionaryLoadedAsync(culture);

            string template;

            if (dict.TryGetValue(suffixKey, out var v))
                template = v;
            else if (dict.TryGetValue(fallbackOtherKey, out var other))
                template = other;
            else
            {
                _missingLogger.LogMissingOnce(null, culture, suffixKey);

                if (_opt.AutoRegisterMissingKeys)
                {
                    await _store.TryAutoRegisterKeyAsync(null, key, defaultOther, CancellationToken.None);
                    await _cache.InvalidateAsync(null, culture, CancellationToken.None);
                }

                template = defaultValue;
            }

            return template.Replace("{count}", count.ToString());
        }
        catch
        {
            return defaultValue.Replace("{count}", count.ToString());
        }
    }

    public async Task<IReadOnlyDictionary<string, string>> GetCultureDictionaryAsync(
        string culture,
        Guid? tenantId,
        CancellationToken ct = default)
    {
        culture = NormalizeCulture(culture);
        var gateKey = LocalizationCacheKeys.Build(tenantId, culture);

        var cached = await _cache.TryGetAsync(tenantId, culture, ct).ConfigureAwait(false);

        if (cached != null)
        {
            TriggerRefreshAheadIfNeeded(gateKey, culture, tenantId);
            return cached;
        }

        return await LoadAndCacheAsync(gateKey, culture, tenantId, ct).ConfigureAwait(false);
    }

    private async Task<IReadOnlyDictionary<string, string>> EnsureDictionaryLoadedAsync(string culture)
    {
        culture = NormalizeCulture(culture);
        Guid? tenantId = null;
        var gateKey = LocalizationCacheKeys.Build(tenantId, culture);

        var cached = await _cache.TryGetAsync(tenantId, culture, CancellationToken.None)
            .ConfigureAwait(false);

        if (cached != null)
        {
            TriggerRefreshAheadIfNeeded(gateKey, culture, tenantId);
            return cached;
        }

        return await LoadAndCacheAsync(gateKey, culture, tenantId, CancellationToken.None)
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadAndCacheAsync(
        string gateKey,
        string culture,
        Guid? tenantId,
        CancellationToken ct)
    {
        return await _loadCoordinator.RunOncePerKeyAsync(
            gateKey,
            async innerCt =>
            {
                var cachedInside = await _cache.TryGetAsync(tenantId, culture, innerCt)
                    .ConfigureAwait(false);

                if (cachedInside != null)
                    return cachedInside;

                var dict = await _store.LoadCultureDictionaryAsync(culture, tenantId, innerCt)
                    .ConfigureAwait(false);

                await _cache.SetAsync(tenantId, culture, dict, _opt.CacheTtl, innerCt)
                    .ConfigureAwait(false);

                _expiryTracker.Set(gateKey, _opt.CacheTtl);

                return dict;
            },
            ct).ConfigureAwait(false);
    }

    private void TriggerRefreshAheadIfNeeded(string gateKey, string culture, Guid? tenantId)
    {
        if (!_opt.EnableRefreshAhead)
            return;

        if (!_expiryTracker.ShouldRefresh(gateKey, _opt.RefreshAheadWindow))
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await _loadCoordinator.RunOncePerKeyAsync(
                    gateKey,
                    async ct =>
                    {
                        var dict = await _store.LoadCultureDictionaryAsync(culture, tenantId, ct)
                            .ConfigureAwait(false);

                        await _cache.SetAsync(tenantId, culture, dict, _opt.CacheTtl, ct)
                            .ConfigureAwait(false);

                        _expiryTracker.Set(gateKey, _opt.CacheTtl);

                        return dict;
                    },
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Localization background refresh failed.");
            }
        });
    }

    public string Format(string key, string defaultValue, object values)
    {
        var template = Get(key, defaultValue);
        return PlaceholderFormatter.Format(template, values);
    }

    private static string NormalizeCulture(string culture)
        => CultureInfo.GetCultureInfo(culture).Name;
}