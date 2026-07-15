using BrighterTools.ModularLocalization.Abstractions;
using BrighterTools.ModularLocalization.EF.Entities;
using Microsoft.EntityFrameworkCore;

namespace BrighterTools.ModularLocalization.Services;

internal sealed class EfLocalizationTranslationAdminService : ILocalizationTranslationAdminService
{
    private static readonly Guid GlobalTenantId = Guid.Empty;

    private readonly ILocalizationDbContext _db;
    private readonly ILocalizationCache _cache;
    private readonly ILocalizationTranslationGenerator? _translationGenerator;

    public EfLocalizationTranslationAdminService(
        ILocalizationDbContext db,
        ILocalizationCache cache,
        IEnumerable<ILocalizationTranslationGenerator>? translationGenerators = null)
    {
        _db = db;
        _cache = cache;
        _translationGenerator = translationGenerators?.FirstOrDefault();
    }

    public bool CanGenerateTranslations => _translationGenerator != null;

    public async Task<LocalizationTranslationListResponse> GetTranslationsAsync(
        LocalizationTranslationListRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 250);
        var search = request.Search?.Trim();
        var keyPrefix = request.KeyPrefix?.Trim();
        var exactKey = request.ExactKey?.Trim();
        var cultureCodes = await LoadCultureCodesAsync(ct).ConfigureAwait(false);

        var query = _db.TranslationKeys
            .AsNoTracking()
            .Where(x => x.TenantId == GlobalTenantId);

        if (!string.IsNullOrWhiteSpace(exactKey))
        {
            query = query.Where(x => x.Key == exactKey);
        }
        else if (!string.IsNullOrWhiteSpace(keyPrefix))
        {
            var prefixWithSeparator = $"{keyPrefix}.";
            query = query.Where(x => x.Key == keyPrefix || x.Key.StartsWith(prefixWithSeparator));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                x.Key.Contains(search) ||
                x.DefaultValue.Contains(search) ||
                x.Values.Any(v =>
                    v.TenantId == GlobalTenantId &&
                    v.PluralCategory == null &&
                    v.Value.Contains(search)));
        }

        var totalCount = await query.CountAsync(ct).ConfigureAwait(false);

        var keys = await query
            .Include(x => x.Values)
            .OrderBy(x => x.Key)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new LocalizationTranslationListResponse
        {
            Items = keys.Select(key => MapTranslation(key, cultureCodes)).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            CanGenerateTranslations = CanGenerateTranslations
        };
    }

    public async Task<IReadOnlyList<LocalizationTranslationTreeNodeDto>> GetTranslationTreeAsync(CancellationToken ct = default)
    {
        var keys = await _db.TranslationKeys
            .AsNoTracking()
            .Where(x => x.TenantId == GlobalTenantId)
            .OrderBy(x => x.Key)
            .Select(x => x.Key)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return BuildTree(keys);
    }

    public async Task<LocalizationTranslationDto?> UpsertTranslationAsync(
        Guid translationKeyId,
        UpsertLocalizationTranslationRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var key = await _db.TranslationKeys
            .Include(x => x.Values)
            .FirstOrDefaultAsync(x => x.Id == translationKeyId && x.TenantId == GlobalTenantId, ct)
            .ConfigureAwait(false);

        if (key == null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var invalidatedCultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in request.Values)
        {
            var culture = NormalizeCulture(item.Key);
            var value = key.Values.FirstOrDefault(v =>
                v.TenantId == GlobalTenantId &&
                v.Culture == culture &&
                v.PluralCategory == null);

            if (value == null)
            {
                value = new TranslationValue
                {
                    Id = Guid.NewGuid(),
                    TranslationKeyId = key.Id,
                    Culture = culture,
                    PluralCategory = null,
                    TenantId = GlobalTenantId,
                    CreatedAtUtc = now
                };

                _db.TranslationValues.Add(value);
                key.Values.Add(value);
            }

            value.Value = item.Value?.Trim() ?? string.Empty;
            value.IsMachineTranslated = false;
            value.UpdatedAtUtc = now;
            invalidatedCultures.Add(culture);
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        foreach (var culture in invalidatedCultures)
        {
            await _cache.InvalidateAsync(null, culture, ct).ConfigureAwait(false);
        }

        var cultureCodes = await LoadCultureCodesAsync(ct).ConfigureAwait(false);
        return MapTranslation(key, cultureCodes, now);
    }

    public async Task<LocalizationTranslationDto?> GenerateTranslationAsync(
        Guid translationKeyId,
        GenerateLocalizationTranslationRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_translationGenerator == null)
        {
            throw new InvalidOperationException("AI translation generation is not configured.");
        }

        if (string.IsNullOrWhiteSpace(request.CultureCode))
        {
            throw new InvalidOperationException("A target culture code is required.");
        }

        var targetCulture = NormalizeCulture(request.CultureCode);
        var keyExists = await _db.TranslationKeys
            .AsNoTracking()
            .AnyAsync(x => x.Id == translationKeyId && x.TenantId == GlobalTenantId, ct)
            .ConfigureAwait(false);

        if (!keyExists)
        {
            return null;
        }

        var generationResult = await _translationGenerator.GenerateAsync(
            new LocalizationTranslationGenerationRequest
            {
                SourceCulture = NormalizeOptionalCulture(request.SourceCulture),
                TargetCultures = [targetCulture],
                TranslationKeyIds = [translationKeyId],
                TenantId = GlobalTenantId,
                OnlyMissing = false,
                OverwriteMachineTranslatedValues = true,
                BatchSize = 1
            },
            ct).ConfigureAwait(false);

        if (generationResult.Errors.Count > 0 &&
            generationResult.GeneratedEntries == 0 &&
            generationResult.UpdatedEntries == 0)
        {
            throw new InvalidOperationException(string.Join(" ", generationResult.Errors));
        }

        await _cache.InvalidateAsync(null, targetCulture, ct).ConfigureAwait(false);

        var updatedKey = await _db.TranslationKeys
            .AsNoTracking()
            .Include(x => x.Values)
            .FirstOrDefaultAsync(x => x.Id == translationKeyId && x.TenantId == GlobalTenantId, ct)
            .ConfigureAwait(false);

        if (updatedKey == null)
        {
            return null;
        }

        var cultureCodes = await LoadCultureCodesAsync(ct).ConfigureAwait(false);
        return MapTranslation(updatedKey, cultureCodes);
    }

    private async Task<IReadOnlyList<string>> LoadCultureCodesAsync(CancellationToken ct)
    {
        return await _db.SupportedCultures
            .AsNoTracking()
            .Where(x => x.TenantId == GlobalTenantId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.DisplayName)
            .Select(x => x.CultureCode)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    private static LocalizationTranslationDto MapTranslation(
        TranslationKey key,
        IReadOnlyList<string> cultureCodes,
        DateTime? updatedAtOverride = null)
    {
        var entries = cultureCodes.ToDictionary(
            cultureCode => cultureCode,
            cultureCode =>
            {
                var value = key.Values
                    .Where(v => v.TenantId == GlobalTenantId && v.Culture == cultureCode && v.PluralCategory == null)
                    .OrderByDescending(v => v.UpdatedAtUtc)
                    .FirstOrDefault();

                return new LocalizationTranslationValueDto
                {
                    Value = value?.Value ?? string.Empty,
                    IsMachineTranslated = value?.IsMachineTranslated ?? false,
                    UpdatedAtUtc = value?.UpdatedAtUtc
                };
            },
            StringComparer.OrdinalIgnoreCase);

        return new LocalizationTranslationDto
        {
            TranslationKeyId = key.Id,
            Key = key.Key,
            DefaultValue = key.DefaultValue,
            Values = entries.ToDictionary(
                static entry => entry.Key,
                static entry => entry.Value.Value,
                StringComparer.OrdinalIgnoreCase),
            Entries = entries,
            UpdatedAtUtc = updatedAtOverride ?? key.Values
                .Where(v => v.TenantId == GlobalTenantId && v.PluralCategory == null)
                .Select(v => (DateTime?)v.UpdatedAtUtc)
                .Max() ?? key.UpdatedAtUtc
        };
    }

    private static string NormalizeCulture(string? culture)
    {
        return string.IsNullOrWhiteSpace(culture)
            ? "en"
            : culture.Trim().ToLowerInvariant();
    }

    private static string? NormalizeOptionalCulture(string? culture)
    {
        return string.IsNullOrWhiteSpace(culture) ? null : NormalizeCulture(culture);
    }

    private static IReadOnlyList<LocalizationTranslationTreeNodeDto> BuildTree(IEnumerable<string> keys)
    {
        var roots = new Dictionary<string, TranslationTreeNodeBuilder>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in keys.Where(static key => !string.IsNullOrWhiteSpace(key)))
        {
            var segments = key.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                continue;
            }

            var currentChildren = roots;
            var currentPath = string.Empty;
            TranslationTreeNodeBuilder? currentNode = null;

            foreach (var segment in segments)
            {
                currentPath = string.IsNullOrEmpty(currentPath)
                    ? segment
                    : $"{currentPath}.{segment}";

                if (!currentChildren.TryGetValue(segment, out currentNode))
                {
                    currentNode = new TranslationTreeNodeBuilder(segment, currentPath);
                    currentChildren[segment] = currentNode;
                }

                currentChildren = currentNode.Children;
            }

            if (currentNode != null)
            {
                currentNode.IsLeaf = true;
            }
        }

        return roots.Values
            .OrderBy(node => node.FullKey, StringComparer.OrdinalIgnoreCase)
            .Select(ToTreeNodeDto)
            .ToList();
    }

    private static LocalizationTranslationTreeNodeDto ToTreeNodeDto(TranslationTreeNodeBuilder node)
    {
        return new LocalizationTranslationTreeNodeDto
        {
            Label = node.Label,
            FullKey = node.FullKey,
            IsLeaf = node.IsLeaf,
            Children = node.Children.Values
                .OrderBy(child => child.FullKey, StringComparer.OrdinalIgnoreCase)
                .Select(ToTreeNodeDto)
                .ToList()
        };
    }

    private sealed class TranslationTreeNodeBuilder
    {
        public TranslationTreeNodeBuilder(string label, string fullKey)
        {
            Label = label;
            FullKey = fullKey;
        }

        public string Label { get; }

        public string FullKey { get; }

        public bool IsLeaf { get; set; }

        public Dictionary<string, TranslationTreeNodeBuilder> Children { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
