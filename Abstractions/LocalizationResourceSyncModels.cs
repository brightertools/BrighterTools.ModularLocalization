namespace BrighterTools.ModularLocalization.Abstractions;

public sealed class LocalizationResourceDefinition
{
    public string Key { get; set; } = string.Empty;

    public string DefaultValue { get; set; } = string.Empty;
}

public sealed class LocalizationResourceSyncResult
{
    public int RequestedCount { get; set; }

    public int ProcessedCount { get; set; }

    public int CreatedCount { get; set; }

    public int ExistingCount { get; set; }

    public int UpdatedDefaultsCount { get; set; }

    public int SkippedCount { get; set; }

    public IReadOnlyList<string> InvalidatedCultures { get; set; } = [];
}
