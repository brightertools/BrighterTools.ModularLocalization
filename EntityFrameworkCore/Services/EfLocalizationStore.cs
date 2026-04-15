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

    public async Task TryAutoRegisterKeyAsync(
        Guid? tenantId,
        string key,
        string defaultValue,
        CancellationToken ct)
    {
        if (!_opt.AutoRegisterMissingKeys)
            return;

        var effectiveTenant = NormalizeTenant(tenantId);
        var now = DateTime.UtcNow;

        var entity = new TranslationKey
        {
            Id = Guid.NewGuid(),
            Key = key,
            DefaultValue = defaultValue,
            LastSeenDefaultValue = defaultValue,
            TenantId = effectiveTenant,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.TranslationKeys.Add(entity);

        try
        {
            await SyncBaseCultureValueAsync(entity, defaultValue, effectiveTenant, now, ct)
                .ConfigureAwait(false);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogDebug(ex,
                "Auto-register race for key {Key} tenant {TenantId}",
                key, effectiveTenant);

            if (_db is DbContext efContext)
            {
                efContext.Entry(entity).State = EntityState.Detached;
            }
        }

        var existing = await _db.TranslationKeys
            .FirstOrDefaultAsync(k =>
                k.Key == key &&
                k.TenantId == effectiveTenant,
                ct)
            .ConfigureAwait(false);

        if (existing == null)
            return;

        var hasChanges = false;

        if (!string.Equals(existing.LastSeenDefaultValue, defaultValue, StringComparison.Ordinal))
        {
            existing.LastSeenDefaultValue = defaultValue;
            existing.UpdatedAtUtc = now;
            hasChanges = true;
        }

        hasChanges |= await SyncBaseCultureValueAsync(existing, defaultValue, effectiveTenant, now, ct)
            .ConfigureAwait(false);

        if (!hasChanges)
            return;

        try
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogDebug(ex,
                "Concurrent update race for key {Key} tenant {TenantId}",
                key, effectiveTenant);
        }
    }

    private async Task<bool> SyncBaseCultureValueAsync(
        TranslationKey key,
        string defaultValue,
        Guid effectiveTenant,
        DateTime now,
        CancellationToken ct)
    {
        if (_opt.BaseCultureValueSyncMode == BaseCultureValueSyncMode.Never)
            return false;

        var baseCulture = _opt.DefaultCulture.Trim();

        var existing = await _db.TranslationValues
            .FirstOrDefaultAsync(v =>
                v.TranslationKeyId == key.Id &&
                v.TenantId == effectiveTenant &&
                v.PluralCategory == null &&
                v.Culture == baseCulture,
                ct)
            .ConfigureAwait(false);

        if (existing == null)
        {
            _db.TranslationValues.Add(new TranslationValue
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
            });

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

    private Guid NormalizeTenant(Guid? tenantId)
    {
        if (!_opt.EnableTenantSupport)
            return Guid.Empty;

        return tenantId ?? Guid.Empty;
    }
}
