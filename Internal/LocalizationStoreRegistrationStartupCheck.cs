using BrighterTools.ModularLocalization.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BrighterTools.ModularLocalization.Internal;

internal sealed class LocalizationStoreRegistrationStartupCheck : IHostedService
{
    private readonly IServiceProvider _services;

    public LocalizationStoreRegistrationStartupCheck(IServiceProvider services)
    {
        _services = services;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        _ = scope.ServiceProvider.GetRequiredService<ILocalizationStore>();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
