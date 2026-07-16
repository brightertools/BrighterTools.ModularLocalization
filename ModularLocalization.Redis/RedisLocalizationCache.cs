using System.Text.Json;
using BrighterTools.ModularLocalization.Abstractions;
using StackExchange.Redis;

namespace BrighterTools.ModularLocalization.Redis;

public sealed class RedisLocalizationCache : ILocalizationCache
{
    private readonly IDatabase _db;
    private readonly string _instanceName;

    public RedisLocalizationCache(IConnectionMultiplexer mux, string instanceName)
    {
        _db = mux.GetDatabase();
        _instanceName = string.IsNullOrWhiteSpace(instanceName) ? "localization" : instanceName;
    }

    public async Task<IReadOnlyDictionary<string, string>?> TryGetAsync(Guid? tenantId, string culture, CancellationToken ct)
    {
        var key = BuildKey(_instanceName, tenantId, culture);

        var redisValue = await _db.StringGetAsync(key).ConfigureAwait(false);
        if (redisValue.IsNullOrEmpty) return null;

        // Avoid overload ambiguity by forcing string
        var json = redisValue.ToString();
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json);
    }

    public Task SetAsync(Guid? tenantId, string culture, IReadOnlyDictionary<string, string> value, TimeSpan ttl, CancellationToken ct)
    {
        var key = BuildKey(_instanceName, tenantId, culture);
        var json = JsonSerializer.Serialize(value);
        return _db.StringSetAsync(key, json, ttl);
    }

    public Task InvalidateAsync(Guid? tenantId, string culture, CancellationToken ct)
    {
        var key = BuildKey(_instanceName, tenantId, culture);
        return _db.KeyDeleteAsync(key);
    }

    private static string BuildKey(string instanceName, Guid? tenantId, string culture)
    {
        // Keep key format stable across all methods
        var tenantPart = tenantId?.ToString() ?? "global";
        return $"{instanceName}:localization:{tenantPart}:{culture}";
    }
}