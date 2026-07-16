# BrighterTools.ModularLocalization Usage

This guide is for applications consuming the BrighterTools ModularLocalization packages and the optional React administration companion package.

## Install

```powershell
dotnet add package BrighterTools.ModularLocalization
dotnet add package BrighterTools.ModularLocalization.EntityFrameworkCore
dotnet add package BrighterTools.ModularLocalization.Redis
npm install @brightertools/modular-localization-react
```

Use only the packages your application needs. Core is required, EF Core is used when translations live in the application database, Redis is used when localized resources should be cached in Redis, and the React package is used when the app wants the reusable administration UI.

## Package Roles

- `BrighterTools.ModularLocalization` provides runtime abstractions, caching contracts, pluralization, culture fallback, resource sync contracts, and localizer services.
- `BrighterTools.ModularLocalization.EntityFrameworkCore` provides EF Core storage, supported-culture management, translation administration, and optional OpenAI translation generation.
- `BrighterTools.ModularLocalization.Redis` provides Redis cache integration for deployments that need distributed localization cache behavior.
- `@brightertools/modular-localization-react` provides reusable React administration components and typed API adapter contracts.

## Integration Shape

Register the core services with `AddModularLocalization`, then add `AddModularLocalizationEntityFramework` when database persistence is used. Implement `ILocalizationDbContext` on the application DbContext and apply the model configuration/migration helpers described in the integration guide.

Use the resource sync contracts when external libraries expose localization manifests and the application needs to persist missing or updated localization keys. Use the translation administration service and React companion package for admin workflows that inspect, update, or generate translation values.

The host app remains responsible for authorization, API endpoint shape, translation review process, OpenAI/provider credentials, Redis connection configuration, migrations, and environment-specific operations.

For a complete setup walkthrough, see [docs/integration.md](./docs/integration.md).