using System;
using System.Collections.Generic;
using System.Text;

namespace BrighterTools.ModularLocalization.EF.Entities;

public sealed class TranslationValue
{
    public Guid Id { get; set; }

    public Guid TranslationKeyId { get; set; }
    public TranslationKey TranslationKey { get; set; } = null!;

    public string Culture { get; set; } = null!;           // required, max 10 (e.g. "fr", "fr-CA")
    public string? PluralCategory { get; set; }            // null for non-plural; max 20
    public string Value { get; set; } = null!;             // required

    public bool IsMachineTranslated { get; set; }

    public Guid TenantId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}