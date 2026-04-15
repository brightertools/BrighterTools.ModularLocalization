using BrighterTools.ModularLocalization.Abstractions;
using BrighterTools.ModularLocalization.Caching;
using BrighterTools.ModularLocalization.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace BrighterTools.ModularLocalization;

public static class LocalizationServiceCollectionExtensions
{
    public static IServiceCollection AddModularLocalization(
        this IServiceCollection services,
        Action<LocalizationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.AddMemoryCache();

        services.AddSingleton<ILocalizationLoadCoordinator, LocalizationLoadCoordinator>();
        services.AddSingleton<ILocalizationCache, MemoryLocalizationCache>();
        services.AddSingleton<IPluralRuleProvider, DefaultPluralRuleProvider>();
        services.AddSingleton<IMissingTranslationLogger, MissingTranslationLogger>();
        services.AddSingleton<CacheExpiryTracker>();

        services.AddScoped<ICultureResolver, DefaultCultureResolver>();
        services.AddScoped<IModularLocalizer, ModularLocalizer>();

        services.AddHostedService<LocalizationStoreRegistrationStartupCheck>();
        services.AddHostedService<LocalizationWarmupHostedService>();

        return services;
    }
}
