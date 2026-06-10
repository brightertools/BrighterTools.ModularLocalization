using BrighterTools.ModularLocalization.Abstractions;
using BrighterTools.ModularLocalization.Internal;
using BrighterTools.ModularLocalization.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BrighterTools.ModularLocalization;

public static class LocalizationEntityFrameworkServiceCollectionExtensions
{
    public static IServiceCollection AddModularLocalizationEntityFramework(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ILocalizationStore, EfLocalizationStore>();
        services.AddScoped<ISupportedCultureService, EfSupportedCultureService>();
        services.AddHostedService<LocalizationStrictModeStartupCheck>();

        return services;
    }

    public static IServiceCollection AddModularLocalizationOpenAiTranslation(
        this IServiceCollection services,
        Action<OpenAiTranslationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.AddHttpClient<OpenAiLocalizationTranslationGenerator>();
        services.AddScoped<ILocalizationTranslationGenerator, OpenAiLocalizationTranslationGenerator>();

        return services;
    }
}
