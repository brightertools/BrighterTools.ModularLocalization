namespace BrighterTools.ModularLocalization.Abstractions;

public sealed class LocalizationTranslationListRequest
{
    public string? Search { get; set; }

    public string? KeyPrefix { get; set; }

    public string? ExactKey { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}

public sealed class LocalizationTranslationListResponse
{
    public IReadOnlyList<LocalizationTranslationDto> Items { get; set; } = [];

    public int TotalCount { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }

    public bool CanGenerateTranslations { get; set; }
}

public sealed class LocalizationTranslationDto
{
    public Guid TranslationKeyId { get; set; }

    public string Key { get; set; } = string.Empty;

    public string DefaultValue { get; set; } = string.Empty;

    public IReadOnlyDictionary<string, string> Values { get; set; } = new Dictionary<string, string>();

    public IReadOnlyDictionary<string, LocalizationTranslationValueDto> Entries { get; set; } = new Dictionary<string, LocalizationTranslationValueDto>();

    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class LocalizationTranslationValueDto
{
    public string Value { get; set; } = string.Empty;

    public bool IsMachineTranslated { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}

public sealed class LocalizationTranslationTreeNodeDto
{
    public string Label { get; set; } = string.Empty;

    public string FullKey { get; set; } = string.Empty;

    public bool IsLeaf { get; set; }

    public IReadOnlyList<LocalizationTranslationTreeNodeDto> Children { get; set; } = [];
}

public sealed class UpsertLocalizationTranslationRequest
{
    public Dictionary<string, string?> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class GenerateLocalizationTranslationRequest
{
    public string CultureCode { get; set; } = string.Empty;

    public string? SourceCulture { get; set; }
}
