using BrighterTools.ModularLocalization.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BrighterTools.ModularLocalization.Internal;

internal sealed class LocalizationStrictModeStartupCheck : IHostedService
{
    private readonly IServiceProvider _sp;
    private readonly LocalizationOptions _opt;
    private readonly ILogger<LocalizationStrictModeStartupCheck> _logger;

    public LocalizationStrictModeStartupCheck(
        IServiceProvider sp,
        IOptions<LocalizationOptions> opt,
        ILogger<LocalizationStrictModeStartupCheck> logger)
    {
        _sp = sp;
        _opt = opt.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_opt.StrictMode)
            return;

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ILocalizationDbContext>();

        if (db is not DbContext ef)
            throw new InvalidOperationException("StrictMode enabled: ILocalizationDbContext must inherit from DbContext.");

        try
        {
            // This ensures we actually validate connectivity immediately.
            // For SQLite, this will open the connection; for server DBs, it validates reachability.
            var canConnect = await ef.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
            if (!canConnect)
                throw new InvalidOperationException("StrictMode enabled: cannot connect to localization database.");

            if ((await ef.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).Any())
            {
                await ef.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // If no migrations exist, ensure schema exists.
                await ef.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StrictMode enabled: localization database startup check failed.");
            throw new InvalidOperationException("StrictMode enabled: localization database startup check failed.", ex);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}