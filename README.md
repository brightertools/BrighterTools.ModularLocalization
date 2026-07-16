# BrighterTools.ModularLocalization

`BrighterTools.ModularLocalization` provides modular localization building blocks for BrighterTools-style applications: runtime localization services, EF Core persistence, optional Redis-backed caching, and a React companion administration package.

The host application owns translation persistence, localization discovery, admin endpoint authorization, provider credentials, migrations, and deployment-specific cache configuration. The libraries own the reusable contracts, orchestration, persistence helpers, cache integration, and typed React administration surface.

## Packages

```powershell
dotnet add package BrighterTools.ModularLocalization
dotnet add package BrighterTools.ModularLocalization.EntityFrameworkCore
dotnet add package BrighterTools.ModularLocalization.Redis
npm install @brightertools/modular-localization-react
```

## Repository Layout

- `BrighterTools.ModularLocalization.csproj` - core runtime package and service contracts
- `EntityFrameworkCore` - EF Core storage and administration services
- `ModularLocalization.Redis` - Redis cache integration package
- `react/brightertools-modular-localization-react` - React companion admin package
- `docs/integration.md` - end-to-end host application integration guide

## Documentation

- [usage.md](./usage.md) for consuming application guidance
- [publishing.md](./publishing.md) for maintainer release steps
- [docs/integration.md](./docs/integration.md) for DbContext, model, migration, and runtime integration
- [RELEASE_NOTES.md](./RELEASE_NOTES.md) for release history

## Validation

```powershell
dotnet test .\BrighterTools.ModularLocalization.sln -c Release
cd .\react\brightertools-modular-localization-react
npm install
npm test
npm run build
npm run pack:dry-run
```