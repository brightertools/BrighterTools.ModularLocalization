using System.Collections.Concurrent;

internal sealed class CacheExpiryTracker
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _expirations = new();

    public void Set(string key, TimeSpan ttl)
        => _expirations[key] = DateTimeOffset.UtcNow.Add(ttl);

    public bool ShouldRefresh(string key, TimeSpan refreshWindow)
    {
        if (!_expirations.TryGetValue(key, out var expiry))
            return false;

        return expiry - DateTimeOffset.UtcNow <= refreshWindow;
    }

    public void Remove(string key)
        => _expirations.TryRemove(key, out _);
}