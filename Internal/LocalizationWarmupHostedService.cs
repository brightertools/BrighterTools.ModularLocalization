using BrighterTools.ModularLocalization.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BrighterTools.ModularLocalization.Internal;

internal sealed class LocalizationWarmupHostedService : IHostedService
{
    private readonly IServiceProvider _sp;
    private readonly LocalizationOptions _options;
    private readonly ILogger<LocalizationWarmupHostedService> _logger;

    public LocalizationWarmupHostedService(
        IServiceProvider sp,
        IOptions<LocalizationOptions> options,
        ILogger<LocalizationWarmupHostedService> logger)
    {
        _sp = sp;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.WarmupOnStartup)
            return;

        var cultures = _options.WarmupCultures?.Any() == true
            ? _options.WarmupCultures
            : new List<string> { _options.DefaultCulture };

        using var scope = _sp.CreateScope();
        var localizer = scope.ServiceProvider.GetRequiredService<IModularLocalizer>();

        foreach (var culture in cultures)
        {
            try
            {
                await localizer.GetCultureDictionaryAsync(culture, null, cancellationToken)
                    .ConfigureAwait(false);

                _logger.LogInformation(
                    "Localization warmup completed for culture {Culture}",
                    culture);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Localization warmup failed for culture {Culture}",
                    culture);

                if (_options.StrictMode)
                    throw;
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}