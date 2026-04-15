using BrighterTools.ModularLocalization.Abstractions;
using Microsoft.Extensions.Caching.Memory;

namespace BrighterTools.ModularLocalization.Caching;

internal sealed class MemoryLocalizationCache : ILocalizationCache
{
    private readonly IMemoryCache _cache;

    public MemoryLocalizationCache(IMemoryCache cache) => _cache = cache;

    public Task<IReadOnlyDictionary<string, string>?> TryGetAsync(Guid? tenantId, string culture, CancellationToken ct)
    {
        var key = LocalizationCacheKeys.Build(tenantId, culture);
        _cache.TryGetValue(key, out IReadOnlyDictionary<string, string>? dict);
        return Task.FromResult(dict);
    }

    public Task SetAsync(Guid? tenantId, string culture, IReadOnlyDictionary<string, string> dictionary, TimeSpan ttl, CancellationToken ct)
    {
        var key = LocalizationCacheKeys.Build(tenantId, culture);
        _cache.Set(key, dictionary, ttl);
        return Task.CompletedTask;
    }

    public Task InvalidateAsync(Guid? tenantId, string culture, CancellationToken ct)
    {
        var key = LocalizationCacheKeys.Build(tenantId, culture);
        _cache.Remove(key);
        return Task.CompletedTask;
    }

    
}
