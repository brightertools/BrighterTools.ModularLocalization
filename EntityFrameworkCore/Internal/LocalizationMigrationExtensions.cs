using BrighterTools.ModularLocalization.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BrighterTools.ModularLocalization;

public static class LocalizationMigrationExtensions
{
    public static async Task UseModularLocalizationMigrationsAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var opt = scope.ServiceProvider.GetRequiredService<IOptions<LocalizationOptions>>().Value;
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("ModularLocalization.Migrations");

        if (!opt.EnableAutoMigrate)
            return;

        try
        {
            var db = scope.ServiceProvider.GetRequiredService<ILocalizationDbContext>();

            if (opt.StrictMode)
            {
                await db.Database.MigrateAsync(ct).ConfigureAwait(false);
            }
            else
            {
                try
                {
                    await db.Database.MigrateAsync(ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Auto-migrate failed; continuing because StrictMode=false.");
                }
            }
        }
        catch (Exception ex)
        {
            if (opt.StrictMode) throw;
            logger.LogWarning(ex, "Localization DB unreachable; continuing because StrictMode=false.");
        }
    }
}
