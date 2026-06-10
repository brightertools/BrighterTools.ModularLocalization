using BrighterTools.ModularLocalization.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BrighterTools.ModularLocalization.Services;

internal sealed class EfSupportedCultureService : ISupportedCultureService
{
    private readonly ILocalizationDbContext _db;
    private readonly LocalizationOptions _options;

    public EfSupportedCultureService(ILocalizationDbContext db, IOptions<LocalizationOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<SupportedCultureDto>> GetEnabledCulturesAsync(CancellationToken ct = default)
        => (await GetCulturesQuery()
            .Where(x => x.IsEnabled)
            .ToListAsync(ct)
            .ConfigureAwait(false))
            .Select(Map)
            .ToList();

    public async Task<IReadOnlyList<SupportedCultureDto>> GetCulturesAsync(CancellationToken ct = default)
        => (await GetCulturesQuery()
            .ToListAsync(ct)
            .ConfigureAwait(false))
            .Select(Map)
            .ToList();

    public async Task<SupportedCultureDto> UpsertCultureAsync(
        UpsertSupportedCultureRequest request,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CultureCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.NativeName);

        var cultureCode = request.CultureCode.Trim();
        var tenantId = GetTenantId();
        var now = DateTime.UtcNow;

        var entity = await _db.SupportedCultures
            .FirstOrDefaultAsync(x => x.CultureCode == cultureCode && x.TenantId == tenantId, ct)
            .ConfigureAwait(false);

        if (entity == null)
        {
            entity = new EF.Entities.SupportedCulture
            {
                Id = Guid.NewGuid(),
                CultureCode = cultureCode,
                TenantId = tenantId,
                CreatedAtUtc = now
            };

            _db.SupportedCultures.Add(entity);
        }

        entity.DisplayName = request.DisplayName.Trim();
        entity.NativeName = request.NativeName.Trim();
        entity.IsEnabled = request.IsEnabled;
        entity.IsDefault = request.IsDefault;
        entity.SortOrder = request.SortOrder;

        if (entity.IsDefault)
        {
            var otherDefaults = await _db.SupportedCultures
                .Where(x => x.TenantId == tenantId && x.CultureCode != cultureCode && x.IsDefault)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            foreach (var culture in otherDefaults)
            {
                culture.IsDefault = false;
            }
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Map(entity);
    }

    private IQueryable<EF.Entities.SupportedCulture> GetCulturesQuery()
    {
        var tenantId = GetTenantId();

        return _db.SupportedCultures
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.DisplayName);
    }

    private Guid GetTenantId()
    {
        return _options.EnableTenantSupport ? Guid.Empty : Guid.Empty;
    }

    private static SupportedCultureDto Map(EF.Entities.SupportedCulture culture)
    {
        return new SupportedCultureDto
        {
            CultureCode = culture.CultureCode,
            DisplayName = culture.DisplayName,
            NativeName = culture.NativeName,
            IsEnabled = culture.IsEnabled,
            IsDefault = culture.IsDefault,
            SortOrder = culture.SortOrder
        };
    }
}
