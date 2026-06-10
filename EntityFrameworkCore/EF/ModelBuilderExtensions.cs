using BrighterTools.ModularLocalization.EF.Config;
using Microsoft.EntityFrameworkCore;

namespace BrighterTools.ModularLocalization.EF;

public static class ModelBuilderExtensions
{
    public static ModelBuilder ApplyModularLocalization(this ModelBuilder modelBuilder, string? schemaName)
    {
        var schema = string.IsNullOrWhiteSpace(schemaName) ? null : schemaName;

        modelBuilder.ApplyConfiguration(new TranslationKeyConfig(schema));
        modelBuilder.ApplyConfiguration(new TranslationValueConfig(schema));
        modelBuilder.ApplyConfiguration(new SupportedCultureConfig(schema));

        return modelBuilder;
    }
}

