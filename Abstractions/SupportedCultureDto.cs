namespace BrighterTools.ModularLocalization.Abstractions;

public sealed class SupportedCultureDto
{
    public string CultureCode { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string NativeName { get; set; } = null!;
    public bool IsEnabled { get; set; }
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }
}
