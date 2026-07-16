# BrighterTools.ModularLocalization.Redis

Redis cache integration for `BrighterTools.ModularLocalization`.

## Install

```powershell
dotnet add package BrighterTools.ModularLocalization.Redis
```

## Usage

Register the core ModularLocalization package first, then register Redis-specific caching services from this package where your application wants distributed cache-backed localization behavior.

The host application owns Redis connection configuration, environment-specific secret management, and operational monitoring.
