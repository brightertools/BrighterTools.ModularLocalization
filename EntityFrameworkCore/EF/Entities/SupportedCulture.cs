namespace BrighterTools.ModularLocalization.EF.Entities;

public sealed class SupportedCulture
{
    public Guid Id { get; set; }

    public string CultureCode { get; set; } = null!;       // max 10
    public string DisplayName { get; set; } = null!;       // max 100
    public string NativeName { get; set; } = null!;        // max 100
    public bool IsEnabled { get; set; }
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }

    public Guid TenantId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
