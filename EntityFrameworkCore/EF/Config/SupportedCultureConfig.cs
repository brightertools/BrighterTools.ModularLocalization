using BrighterTools.ModularLocalization.EF.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrighterTools.ModularLocalization.EF.Config;

public sealed class SupportedCultureConfig : IEntityTypeConfiguration<SupportedCulture>
{
    private readonly string? _schema;
    public SupportedCultureConfig(string? schema) => _schema = schema;

    public void Configure(EntityTypeBuilder<SupportedCulture> b)
    {
        b.ToTable("SupportedCultures", _schema);

        b.HasKey(x => x.Id);

        b.Property(x => x.CultureCode).IsRequired().HasMaxLength(10);
        b.Property(x => x.DisplayName).IsRequired().HasMaxLength(100);
        b.Property(x => x.NativeName).IsRequired().HasMaxLength(100);
        b.Property(x => x.IsEnabled).IsRequired();
        b.Property(x => x.IsDefault).IsRequired();
        b.Property(x => x.SortOrder).IsRequired();
        b.Property(x => x.TenantId);

        b.Property(x => x.CreatedAtUtc).IsRequired();

        b.HasIndex(x => new { x.CultureCode, x.TenantId }).IsUnique();
    }
}
