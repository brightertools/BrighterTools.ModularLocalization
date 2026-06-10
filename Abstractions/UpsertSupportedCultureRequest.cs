namespace BrighterTools.ModularLocalization.Abstractions;

public sealed class UpsertSupportedCultureRequest
{
    public string CultureCode { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string NativeName { get; set; } = null!;
    public bool IsEnabled { get; set; } = true;
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }
}
