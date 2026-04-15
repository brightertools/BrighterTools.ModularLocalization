using System.Collections.Concurrent;
using System.Threading;

namespace BrighterTools.ModularLocalization.Internal;

internal sealed class LocalizationLoadCoordinator : ILocalizationLoadCoordinator
{
    private sealed class Gate
    {
        public readonly SemaphoreSlim Semaphore = new(1, 1);
        public int RefCount;
    }

    private readonly ConcurrentDictionary<string, Gate> _gates = new();

    public async Task<T> RunOncePerKeyAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> action,
        CancellationToken ct)
    {
        // Gate is per dictionary key (tenant+culture)

        if (!_gates.TryGetValue(key, out var gate))
        {
            gate = _gates.GetOrAdd(key, _ => new Gate());
        }

        Interlocked.Increment(ref gate.RefCount);

        try
        {
            await gate.Semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                return await action(ct).ConfigureAwait(false);
            }
            finally
            {
                gate.Semaphore.Release();
            }
        }
        finally
        {
            // Cleanup when nobody is using this gate anymore.
            if (Interlocked.Decrement(ref gate.RefCount) == 0)
            {
                // Only remove if the same instance is still in dictionary
                _gates.TryRemove(new KeyValuePair<string, Gate>(key, gate));
            }
        }
    }
}