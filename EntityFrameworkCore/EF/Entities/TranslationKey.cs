namespace BrighterTools.ModularLocalization.EF.Entities;

public sealed class TranslationKey
{
    public Guid Id { get; set; }

    public string Key { get; set; } = null!;               // required, max 300
    public string DefaultValue { get; set; } = null!;      // required
    public string? LastSeenDefaultValue { get; set; }      // nullable

    public Guid TenantId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<TranslationValue> Values { get; set; } = new List<TranslationValue>();
}
