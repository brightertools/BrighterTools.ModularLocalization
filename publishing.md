# BrighterTools.ModularLocalization Publishing

This guide is for maintainers packaging and publishing `BrighterTools.ModularLocalization` and `BrighterTools.ModularLocalization.EntityFrameworkCore` to NuGet.

Package pages:

- [BrighterTools.ModularLocalization](https://www.nuget.org/packages/BrighterTools.ModularLocalization)
- [BrighterTools.ModularLocalization.EntityFrameworkCore](https://www.nuget.org/packages/BrighterTools.ModularLocalization.EntityFrameworkCore)

## Prerequisites

- NuGet Trusted Publishing policies are active for both package IDs.
- Both policies point to repository owner `brightertools`, repository `BrighterTools.ModularLocalization`, workflow `publish-tool.yml`, and environment `production`.
- The GitHub workflow uses `NuGet/login@v1` and GitHub OIDC.

## Local Packaging

```powershell
dotnet restore .\BrighterTools.ModularLocalization.sln --configfile .\NuGet.config
dotnet build .\BrighterTools.ModularLocalization.sln -c Release --no-restore
dotnet test .\BrighterTools.ModularLocalization.sln -c Release --no-build --verbosity normal
dotnet pack .\BrighterTools.ModularLocalization.csproj -c Release --no-build --output .\artifacts\nuget --configfile .\NuGet.config
dotnet pack .\EntityFrameworkCore\BrighterTools.ModularLocalization.EntityFrameworkCore.csproj -c Release --no-build --output .\artifacts\nuget --configfile .\NuGet.config
```

Expected `1.0.1` artifacts:

- `artifacts/nuget/BrighterTools.ModularLocalization.1.0.1.nupkg`
- `artifacts/nuget/BrighterTools.ModularLocalization.1.0.1.snupkg`
- `artifacts/nuget/BrighterTools.ModularLocalization.EntityFrameworkCore.1.0.1.nupkg`
- `artifacts/nuget/BrighterTools.ModularLocalization.EntityFrameworkCore.1.0.1.snupkg`

## GitHub Actions Publishing

Publishing is handled by `.github/workflows/publish-tool.yml`.

Workflow inputs:

- `publish_to_nuget`
- `version` as an optional override

The workflow restores, builds, tests, packs both packages, uploads artifacts, and publishes with `dotnet nuget push --skip-duplicate`.

## Related Docs

- [README.md](./README.md) for overview and quick start
- [usage.md](./usage.md) for consuming application guidance
- [docs/integration.md](./docs/integration.md) for integration details
- [RELEASE_NOTES.md](./RELEASE_NOTES.md) for release history