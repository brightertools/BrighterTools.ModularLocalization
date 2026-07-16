using BrighterTools.ModularLocalization;
using BrighterTools.ModularLocalization.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

public static class RedisLocalizationServiceCollectionExtensions
{
    public static IServiceCollection AddModularLocalizationRedis(this IServiceCollection services)
    {
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var opt = sp.GetRequiredService<IOptions<LocalizationOptions>>().Value;
            if (string.IsNullOrWhiteSpace(opt.RedisConnectionString))
                throw new InvalidOperationException("RedisConnectionString is required when Redis is enabled.");

            return ConnectionMultiplexer.Connect(opt.RedisConnectionString);
        });

        services.AddSingleton<ILocalizationCache>(sp =>
        {
            var opt = sp.GetRequiredService<IOptions<LocalizationOptions>>().Value;
            var mux = sp.GetRequiredService<IConnectionMultiplexer>();
            return new BrighterTools.ModularLocalization.Redis.RedisLocalizationCache(mux, opt.RedisInstanceName ?? "localization");
        });

        return services;
    }
}