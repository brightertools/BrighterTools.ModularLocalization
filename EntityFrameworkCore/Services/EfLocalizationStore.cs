using BrighterTools.ModularLocalization.Abstractions;
using BrighterTools.ModularLocalization.EF.Entities;
using BrighterTools.ModularLocalization.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BrighterTools.ModularLocalization.Services;

internal sealed class EfLocalizationStore : ILocalizationStore
{
    private readonly ILocalizationDbContext _db;
    private readonly LocalizationOptions _opt;
    private readonly ILogger<EfLocalizationStore> _logger;

    public EfLocalizationStore(
        ILocalizationDbContext db,
        IOptions<LocalizationOptions> opt,
        ILogger<EfLocalizationStore> logger)
    {
        _db = db;
        _opt = opt.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<string, string>> LoadCultureDictionaryAsync(
        string requestedCulture,
        Guid? tenantId,
        CancellationToken ct)
    {
        var cultureChain = CultureFallback.BuildChain(requestedCulture, _opt.DefaultCulture);
        var effectiveTenant = NormalizeTenant(tenantId);

        var values = await _db.TranslationValues
            .AsNoTracking()
            .Include(v => v.TranslationKey)
            .Where(v =>
                cultureChain.Contains(v.Culture) &&
                (
                    (v.TenantId == effectiveTenant && v.TranslationKey.TenantId == effectiveTenant) ||
                    (v.TenantId == Guid.Empty && v.TranslationKey.TenantId == Guid.Empty)
                ))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var keys = await _db.TranslationKeys
            .AsNoTracking()
            .Where(k => k.TenantId == effectiveTenant || k.TenantId == Guid.Empty)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return DictionaryAssembler.AssembleI18NextDictionary(
            requestedCulture,
            cultureChain,
            _opt.DefaultCulture,
            effectiveTenant,
            keys,
            values);
    }

    public Task TryAutoRegisterKeyAsync(Guid? tenantId, string key, string defaultValue, CancellationToken ct)
        => SyncAutoRegisterKeysAsync(
            tenantId,
            [
                new LocalizationResourceDefinition
                {
                    Key = key,
                    DefaultValue = defaultValue
                }
            ],
            ct);

    public async Task<LocalizationResourceSyncResult> SyncAutoRegisterKeysAsync(
        Guid? tenantId,
        IReadOnlyCollection<LocalizationResourceDefinition> resources,
        CancellationToken ct)
    {
        var requestedCount = resources?.Count ?? 0;
        if (!_opt.AutoRegisterMissingKeys)
        {
            return new LocalizationResourceSyncResult
            {
                RequestedCount = requestedCount,
                SkippedCount = requestedCount
            };
        }

        var normalizedResources = NormalizeResources(resources);
        var result = new LocalizationResourceSyncResult
        {
            RequestedCount = requestedCount,
            ProcessedCount = normalizedResources.Count,
            SkippedCount = Math.Max(0, requestedCount - normalizedResources.Count)
        };

        if (normalizedResources.Count == 0)
        {
            return result;
        }

        var effectiveTenant = NormalizeTenant(tenantId);
        var now = DateTime.UtcNow;
        var resourceKeys = normalizedResources.Select(x => x.Key).ToList();
        var initialExistingKeys = await LoadExistingKeySetAsync(resourceKeys, effectiveTenant, ct).ConfigureAwait(false);

        await EnsureKeysExistAsync(normalizedResources, initialExistingKeys, effectiveTenant, now, ct).ConfigureAwait(false);

        var trackedKeys = await LoadTrackedKeysAsync(resourceKeys, effectiveTenant, ct).ConfigureAwait(false);
        result.CreatedCount = resourceKeys.Count(key => !initialExistingKeys.Contains(key) && trackedKeys.ContainsKey(key));
        result.ExistingCount = Math.Max(0, result.ProcessedCount - result.CreatedCount);

        var syncOutcome = await ApplyDefaultSyncAsync(normalizedResources, trackedKeys, effectiveTenant, now, ct).ConfigureAwait(false);
        result.UpdatedDefaultsCount = syncOutcome.UpdatedDefaultsCount;
        result.InvalidatedCultures = syncOutcome.InvalidatedCultures;
        return result;
    }

    private async Task<HashSet<string>> LoadExistingKeySetAsync(
        IReadOnlyCollection<string> resourceKeys,
        Guid effectiveTenant,
        CancellationToken ct)
    {
        var existingKeys = await _db.TranslationKeys
            .AsNoTracking()
            .Where(k => k.TenantId == effectiveTenant && resourceKeys.Contains(k.Key))
            .Select(k => k.Key)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new HashSet<string>(existingKeys, StringComparer.OrdinalIgnoreCase);
    }

    private async Task EnsureKeysExistAsync(
        IReadOnlyCollection<LocalizationResourceDefinition> resources,
        HashSet<string> existingKeys,
        Guid effectiveTenant,
        DateTime now,
        CancellationToken ct)
    {
        var missingResources = resources
            .Where(resource => !existingKeys.Contains(resource.Key))
            .ToList();

        if (missingResources.Count == 0)
        {
            return;
        }

        foreach (var resource in missingResources)
        {
            _db.TranslationKeys.Add(new TranslationKey
            {
                Id = Guid.NewGuid(),
                Key = resource.Key,
                DefaultValue = resource.DefaultValue,
                LastSeenDefaultValue = resource.DefaultValue,
                TenantId = effectiveTenant,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        try
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogDebug(ex,
                "Auto-register race for keys {Keys} tenant {TenantId}",
                string.Join(", ", missingResources.Select(resource => resource.Key)),
                effectiveTenant);
            ClearChangeTracker();
        }
    }

    private async Task<Dictionary<string, TranslationKey>> LoadTrackedKeysAsync(
        IReadOnlyCollection<string> resourceKeys,
        Guid effectiveTenant,
        CancellationToken ct)
    {
        var trackedKeys = await _db.TranslationKeys
            .Where(k => k.TenantId == effectiveTenant && resourceKeys.Contains(k.Key))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return trackedKeys.ToDictionary(key => key.Key, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<LocalizationResourceSyncResult> ApplyDefaultSyncAsync(
        IReadOnlyCollection<LocalizationResourceDefinition> resources,
        Dictionary<string, TranslationKey> trackedKeys,
        Guid effectiveTenant,
        DateTime now,
        CancellationToken ct)
    {
        var baseCulture = _opt.DefaultCulture.Trim();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            trackedKeys = await LoadTrackedKeysAsync(resources.Select(resource => resource.Key).ToList(), effectiveTenant, ct)
                .ConfigureAwait(false);

            var trackedKeyIds = trackedKeys.Values.Select(key => key.Id).ToList();
            var trackedBaseValues = await _db.TranslationValues
                .Where(value =>
                    trackedKeyIds.Contains(value.TranslationKeyId) &&
                    value.TenantId == effectiveTenant &&
                    value.PluralCategory == null &&
                    value.Culture == baseCulture)
                .ToDictionaryAsync(value => value.TranslationKeyId, ct)
                .ConfigureAwait(false);

            var hasChanges = false;
            var updatedDefaultsCount = 0;
            var invalidatedCultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var resource in resources)
            {
                if (!trackedKeys.TryGetValue(resource.Key, out var trackedKey))
                {
                    continue;
                }

                var resourceChanged = false;

                if (!string.Equals(trackedKey.LastSeenDefaultValue, resource.DefaultValue, StringComparison.Ordinal))
                {
                    trackedKey.LastSeenDefaultValue = resource.DefaultValue;
                    trackedKey.UpdatedAtUtc = now;
                    hasChanges = true;
                    resourceChanged = true;
                }

                if (SyncBaseCultureValue(trackedKey, trackedBaseValues, resource.DefaultValue, effectiveTenant, baseCulture, now))
                {
                    hasChanges = true;
                    resourceChanged = true;
                    invalidatedCultures.Add(baseCulture);
                }

                if (resourceChanged)
                {
                    updatedDefaultsCount++;
                }
            }

            if (!hasChanges)
            {
                return new LocalizationResourceSyncResult
                {
                    InvalidatedCultures = []
                };
            }

            try
            {
                await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                return new LocalizationResourceSyncResult
                {
                    UpdatedDefaultsCount = updatedDefaultsCount,
                    InvalidatedCultures = invalidatedCultures.ToList()
                };
            }
            catch (DbUpdateException ex)
            {
                _logger.LogDebug(ex,
                    "Concurrent localization sync race for keys {Keys} tenant {TenantId}",
                    string.Join(", ", resources.Select(resource => resource.Key)),
                    effectiveTenant);

                ClearChangeTracker();

                if (attempt == 1)
                {
                    return new LocalizationResourceSyncResult();
                }
            }
        }

        return new LocalizationResourceSyncResult();
    }

    private bool SyncBaseCultureValue(
        TranslationKey key,
        IDictionary<Guid, TranslationValue> trackedBaseValues,
        string defaultValue,
        Guid effectiveTenant,
        string baseCulture,
        DateTime now)
    {
        if (_opt.BaseCultureValueSyncMode == BaseCultureValueSyncMode.Never)
            return false;

        if (!trackedBaseValues.TryGetValue(key.Id, out var existing))
        {
            var value = new TranslationValue
            {
                Id = Guid.NewGuid(),
                TranslationKeyId = key.Id,
                Culture = baseCulture,
                PluralCategory = null,
                Value = defaultValue,
                IsMachineTranslated = false,
                TenantId = effectiveTenant,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            _db.TranslationValues.Add(value);
            trackedBaseValues[key.Id] = value;
            return true;
        }

        if (_opt.BaseCultureValueSyncMode != BaseCultureValueSyncMode.Always)
            return false;

        if (string.Equals(existing.Value, defaultValue, StringComparison.Ordinal))
            return false;

        existing.Value = defaultValue;
        existing.IsMachineTranslated = false;
        existing.UpdatedAtUtc = now;
        return true;
    }

    private static List<LocalizationResourceDefinition> NormalizeResources(
        IReadOnlyCollection<LocalizationResourceDefinition>? resources)
    {
        if (resources == null || resources.Count == 0)
        {
            return [];
        }

        var normalized = new Dictionary<string, LocalizationResourceDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var resource in resources)
        {
            if (resource == null || string.IsNullOrWhiteSpace(resource.Key))
            {
                continue;
            }

            var normalizedKey = resource.Key.Trim();
            var normalizedDefaultValue = string.IsNullOrWhiteSpace(resource.DefaultValue)
                ? normalizedKey
                : resource.DefaultValue.Trim();

            normalized[normalizedKey] = new LocalizationResourceDefinition
            {
                Key = normalizedKey,
                DefaultValue = normalizedDefaultValue
            };
        }

        return normalized.Values.ToList();
    }

    private void ClearChangeTracker()
    {
        if (_db is DbContext dbContext)
        {
            dbContext.ChangeTracker.Clear();
        }
    }

    private Guid NormalizeTenant(Guid? tenantId)
    {
        if (!_opt.EnableTenantSupport)
            return Guid.Empty;

        return tenantId ?? Guid.Empty;
    }
}
