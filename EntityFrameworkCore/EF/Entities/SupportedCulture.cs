using System;
using System.Collections.Generic;
using System.Text;

namespace BrighterTools.ModularLocalization.EF.Entities;

public sealed class SupportedCulture
{
    public Guid Id { get; set; }

    public string CultureCode { get; set; } = null!;       // max 10
    public bool IsEnabled { get; set; }
    public bool IsDefault { get; set; }

    public Guid TenantId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
