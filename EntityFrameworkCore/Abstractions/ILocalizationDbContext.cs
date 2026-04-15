using BrighterTools.ModularLocalization.EF.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace BrighterTools.ModularLocalization.Abstractions;

public interface ILocalizationDbContext
{
    DbSet<TranslationKey> TranslationKeys { get; }
    DbSet<TranslationValue> TranslationValues { get; }
    DbSet<SupportedCulture> SupportedCultures { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    DatabaseFacade Database { get; }
    ChangeTracker ChangeTracker { get; }
}