using BrighterTools.ModularLocalization.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace BrighterTools.ModularLocalization.Internal;

internal sealed class MissingTranslationLogger : IMissingTranslationLogger
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<MissingTranslationLogger> _logger;

    private static readonly TimeSpan LogThrottleDuration = TimeSpan.FromHours(1);

    public MissingTranslationLogger(
        IMemoryCache cache,
        ILogger<MissingTranslationLogger> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public void LogMissingOnce(Guid? tenantId, string culture, string key)
    {
        var normalizedTenant = tenantId?.ToString() ?? "global";
        var cacheKey = $"missing:{normalizedTenant}:{culture}:{key}";

        if (_cache.TryGetValue(cacheKey, out _))
            return;

        _cache.Set(cacheKey, true, LogThrottleDuration);

        _logger.LogWarning(
            "Missing translation for Key={Key}, Culture={Culture}, Tenant={Tenant}",
            key,
            culture,
            normalizedTenant);
    }
}