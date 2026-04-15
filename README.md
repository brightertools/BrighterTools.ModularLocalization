# BrighterTools.ModularLocalization

React/C# modular localization with a split architecture:

- `BrighterTools.ModularLocalization`: core runtime + abstractions
- `BrighterTools.ModularLocalization.EntityFrameworkCore`: EF Core storage adapter
- Optional built-in OpenAI translation generator (filterable by culture/key prefix)

## Quick Start

1. Register core:

```csharp
builder.Services.AddModularLocalization(options =>
{
    options.DefaultCulture = "en";
    options.BaseCultureValueSyncMode = BaseCultureValueSyncMode.IfMissing;
});
```

2. If using EF Core, register adapter:

```csharp
builder.Services.AddModularLocalizationEntityFramework();
```

3. Implement and register `ILocalizationDbContext` in your app DbContext.

4. Optional: register OpenAI translation generator.

```csharp
builder.Services.AddModularLocalizationOpenAiTranslation(options =>
{
    options.ApiKey = configuration["OpenAI:ApiKey"] ?? "";
    options.Model = "gpt-4.1-mini";
    options.MaxCandidatesPerRun = 2000;
});
```

## Integration Guide

Full setup instructions (DbContext, model config, migrations, and usage):

- [docs/integration.md](docs/integration.md)
