# BrighterTools Modular Localization Integration

This guide shows how to integrate the split packages:

- `BrighterTools.ModularLocalization` (core abstractions + localizer runtime)
- `BrighterTools.ModularLocalization.EntityFrameworkCore` (EF Core store, entities, model config, migration helpers)

## 1. Install packages

```bash
dotnet add package BrighterTools.ModularLocalization
dotnet add package BrighterTools.ModularLocalization.EntityFrameworkCore
```

## 2. Implement `ILocalizationDbContext` on your DbContext

```csharp
using BrighterTools.ModularLocalization.Abstractions;
using BrighterTools.ModularLocalization.EF;
using BrighterTools.ModularLocalization.EF.Entities;
using Microsoft.EntityFrameworkCore;

public sealed class AppDbContext : DbContext, ILocalizationDbContext
{
    public DbSet<TranslationKey> TranslationKeys => Set<TranslationKey>();
    public DbSet<TranslationValue> TranslationValues => Set<TranslationValue>();
    public DbSet<SupportedCulture> SupportedCultures => Set<SupportedCulture>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Optional schema name: "localization", "i18n", etc.
        modelBuilder.ApplyModularLocalization(schemaName: "localization");
    }
}
```

## 3. Register services

```csharp
using BrighterTools.ModularLocalization.Abstractions;
using BrighterTools.ModularLocalization;

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// ILocalizationDbContext is required by the EF adapter.
builder.Services.AddScoped<ILocalizationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

// Core package registration
builder.Services.AddModularLocalization(options =>
{
    options.DefaultCulture = "en";
    options.AutoRegisterMissingKeys = true;
    options.BaseCultureValueSyncMode = BaseCultureValueSyncMode.IfMissing;
    options.EnableTenantSupport = true;
    options.EnableAutoMigrate = true;
    options.StrictMode = false;
    options.WarmupOnStartup = true;
    options.WarmupCultures = new() { "en", "fr" };
});

// EF adapter registration
builder.Services.AddModularLocalizationEntityFramework();

// Optional: OpenAI machine-translation generator
builder.Services.AddModularLocalizationOpenAiTranslation(options =>
{
    options.ApiKey = builder.Configuration["OpenAI:ApiKey"] ?? "";
    options.Model = "gpt-4.1-mini";
    options.BaseUrl = "https://api.openai.com/v1/";
    options.PromptContext = "B2B SaaS dashboard UI";
    options.MaxCandidatesPerRun = 2000;
    options.MaxRetryAttempts = 2;
});
```

`BaseCultureValueSyncMode` controls how code defaults sync into the default-culture `TranslationValue` row:

- `Never`: do not create/update base-culture value from code defaults.
- `IfMissing`: create base-culture value only when missing (recommended).
- `Always`: overwrite existing base-culture value with the code default each time key/default is seen.

## 4. Run localization migrations at startup (optional but recommended)

```csharp
using BrighterTools.ModularLocalization;

await app.Services.UseModularLocalizationMigrationsAsync();
```

Behavior:

- If `EnableAutoMigrate = false`, no migration is run.
- If `EnableAutoMigrate = true` and `StrictMode = true`, startup fails on migration/connectivity errors.
- If `EnableAutoMigrate = true` and `StrictMode = false`, failures are logged and startup continues.

## 5. Consume the localizer

Inject `IModularLocalizer` into services/controllers:

```csharp
public sealed class GreetingService
{
    private readonly IModularLocalizer _localizer;

    public GreetingService(IModularLocalizer localizer)
    {
        _localizer = localizer;
    }

    public string Hello() => _localizer.Get("Greeting.Hello", "Hello");
}
```

## 6. Generate missing translations with OpenAI

Inject `ILocalizationTranslationGenerator` and trigger generation when needed (admin endpoint, background job, CLI, etc.):

```csharp
using BrighterTools.ModularLocalization.Abstractions;

public sealed class LocalizationAdminService
{
    private readonly ILocalizationTranslationGenerator _generator;

    public LocalizationAdminService(ILocalizationTranslationGenerator generator)
    {
        _generator = generator;
    }

    public Task<LocalizationTranslationGenerationResult> GenerateLoginTranslationsAsync(CancellationToken ct)
    {
        return _generator.GenerateAsync(new LocalizationTranslationGenerationRequest
        {
            SourceCulture = "en",
            TargetCultures = new[] { "fr", "de" },
            KeyStartsWith = "Login.",
            OnlyMissing = true,
            OverwriteMachineTranslatedValues = false,
            DryRun = false
        }, ct);
    }
}
```

Request filters supported:

- `TargetCultures`
- `SourceCulture`
- `TenantId`
- `KeyStartsWith` (e.g. `Login.`)
- `OnlyMissing`
- `OverwriteMachineTranslatedValues`
- `BatchSize`
- `DryRun`

`OverwriteMachineTranslatedValues` only applies to existing machine-translated values.
Human-edited values (`IsMachineTranslated == false`) are never overwritten by the generator.

## Notes for custom persistence

If you do not want EF Core:

1. Register core only (`AddModularLocalization(...)`).
2. Provide your own `ILocalizationStore` implementation.
3. Do not call `AddModularLocalizationEntityFramework()`.
