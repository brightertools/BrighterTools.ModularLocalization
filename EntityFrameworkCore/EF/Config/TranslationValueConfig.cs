using BrighterTools.ModularLocalization.EF.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrighterTools.ModularLocalization.EF.Config;

public sealed class TranslationValueConfig : IEntityTypeConfiguration<TranslationValue>
{
    private readonly string? _schema;
    public TranslationValueConfig(string? schema) => _schema = schema;

    public void Configure(EntityTypeBuilder<TranslationValue> b)
    {
        b.ToTable("TranslationValues", _schema);

        b.HasKey(x => x.Id);

        b.Property(x => x.Culture).IsRequired().HasMaxLength(10);
        b.Property(x => x.PluralCategory).HasMaxLength(20);
        b.Property(x => x.Value).IsRequired();

        b.Property(x => x.TenantId).IsRequired();

        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();

        b.HasIndex(x => new { x.TranslationKeyId, x.Culture, x.PluralCategory, x.TenantId }).IsUnique();

        b.HasIndex(x => new { x.Culture, x.TenantId });
    }
}
