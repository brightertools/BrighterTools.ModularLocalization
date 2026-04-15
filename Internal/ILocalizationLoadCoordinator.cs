using System;

namespace BrighterTools.ModularLocalization.Internal;

internal interface ILocalizationLoadCoordinator
{
    Task<T> RunOncePerKeyAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> action,
        CancellationToken ct);
}