# BrighterTools.ModularLocalization Usage

This guide is for applications consuming `BrighterTools.ModularLocalization` and `BrighterTools.ModularLocalization.EntityFrameworkCore`.

Install the core runtime package:

```powershell
dotnet add package BrighterTools.ModularLocalization
```

Install the EF Core adapter when translations are stored in the application database:

```powershell
dotnet add package BrighterTools.ModularLocalization.EntityFrameworkCore
```

## Package Roles

- `BrighterTools.ModularLocalization` provides runtime abstractions, caching, pluralization, culture fallback, resource sync contracts, and localizer services.
- `BrighterTools.ModularLocalization.EntityFrameworkCore` provides EF Core storage, supported-culture management, translation administration, and optional OpenAI translation generation.
- The consuming app owns DbContext registration, migrations, authorization, translation review workflow, and any admin UI.

## Integration Shape

Register the core services with `AddModularLocalization`, then add the EF Core adapter with `AddModularLocalizationEntityFramework` when database persistence is used. Implement `ILocalizationDbContext` on the application DbContext and apply the model configuration/migration helpers described in the integration guide.

Use the resource sync contracts when external libraries expose localization manifests and the application needs to persist missing or updated localization keys. Use the translation administration service for admin workflows that inspect, update, or generate translation values.

For a complete setup walkthrough, see [docs/integration.md](./docs/integration.md).