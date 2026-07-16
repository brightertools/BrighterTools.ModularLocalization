# BrighterTools.ModularLocalization Publishing

This guide is for maintainers packaging and publishing the BrighterTools.ModularLocalization NuGet packages and `@brightertools/modular-localization-react` npm package.

## Package Pages

- NuGet: https://www.nuget.org/packages/BrighterTools.ModularLocalization
- NuGet: https://www.nuget.org/packages/BrighterTools.ModularLocalization.EntityFrameworkCore
- NuGet: https://www.nuget.org/packages/BrighterTools.ModularLocalization.Redis
- npm: https://www.npmjs.com/package/@brightertools/modular-localization-react

## Local Packaging

NuGet validation:

```powershell
dotnet restore .\BrighterTools.ModularLocalization.sln --configfile .\NuGet.config
dotnet build .\BrighterTools.ModularLocalization.sln -c Release --no-restore
dotnet test .\BrighterTools.ModularLocalization.sln -c Release --no-build --verbosity normal
dotnet pack .\BrighterTools.ModularLocalization.csproj -c Release --no-build --output .\artifacts\nuget --configfile .\NuGet.config
dotnet pack .\EntityFrameworkCore\BrighterTools.ModularLocalization.EntityFrameworkCore.csproj -c Release --no-build --output .\artifacts\nuget --configfile .\NuGet.config
dotnet pack .\ModularLocalization.Redis\BrighterTools.ModularLocalization.Redis.csproj -c Release --no-build --output .\artifacts\nuget --configfile .\NuGet.config
```

React package validation:

```powershell
cd .\react\brightertools-modular-localization-react
npm install
npm test
npm run build
npm run pack:dry-run
```

Expected `1.0.0` NuGet artifacts:

- `artifacts/nuget/BrighterTools.ModularLocalization.1.0.0.nupkg`
- `artifacts/nuget/BrighterTools.ModularLocalization.1.0.0.snupkg`
- `artifacts/nuget/BrighterTools.ModularLocalization.EntityFrameworkCore.1.0.0.nupkg`
- `artifacts/nuget/BrighterTools.ModularLocalization.EntityFrameworkCore.1.0.0.snupkg`
- `artifacts/nuget/BrighterTools.ModularLocalization.Redis.1.0.0.nupkg`
- `artifacts/nuget/BrighterTools.ModularLocalization.Redis.1.0.0.snupkg`

## GitHub Actions Publishing

Publishing is handled by `.github/workflows/publish-tool.yml`.

Workflow inputs:

- `version` optionally overrides package versions.
- `publish_to_nuget` controls whether `.nupkg` files are pushed to nuget.org.
- `publish_to_npm` controls whether the React package is published to npm.

NuGet uses `NuGet/login@v1` and GitHub OIDC. npm uses trusted publishing from GitHub Actions with provenance. No long-lived NuGet or npm publish token is required after registry policies are configured.

## Registry Checklist

NuGet Trusted Publishing policies should point to repository owner `brightertools`, repository `BrighterTools.ModularLocalization`, workflow `publish-tool.yml`, and environment `production` for these package IDs:

- `BrighterTools.ModularLocalization`
- `BrighterTools.ModularLocalization.EntityFrameworkCore`
- `BrighterTools.ModularLocalization.Redis`

npm Trusted Publisher should point to the same repository, workflow, and environment for:

- `@brightertools/modular-localization-react`

Package metadata uses the `MIT-0` license. Version is `1.0.0` for the first stable publish.

## Related Docs

- [README.md](./README.md) for overview and package index
- [usage.md](./usage.md) for consuming application guidance
- [docs/integration.md](./docs/integration.md) for integration details
- [RELEASE_NOTES.md](./RELEASE_NOTES.md) for release history