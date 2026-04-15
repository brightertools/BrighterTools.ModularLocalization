using BrighterTools.ModularLocalization.EF.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrighterTools.ModularLocalization.EF.Config;

public sealed class TranslationKeyConfig : IEntityTypeConfiguration<TranslationKey>
{
    private readonly string? _schema;
    public TranslationKeyConfig(string? schema) => _schema = schema;

    public void Configure(EntityTypeBuilder<TranslationKey> b)
    {
        b.ToTable("TranslationKeys", _schema);

        b.HasKey(x => x.Id);

        b.HasIndex(x => x.TenantId);

        b.Property(x => x.Key).IsRequired().HasMaxLength(300);
        b.Property(x => x.DefaultValue).IsRequired();
        b.Property(x => x.LastSeenDefaultValue);

        b.Property(x => x.TenantId).IsRequired();

        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();

        b.HasIndex(x => new { x.Key, x.TenantId }).IsUnique();

        b.HasMany(x => x.Values)
         .WithOne(v => v.TranslationKey)
         .HasForeignKey(v => v.TranslationKeyId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}