using Microsoft.EntityFrameworkCore.Migrations;

namespace BrighterTools.ModularLocalization.EF.Migrations;

public sealed record SupportedCultureSeed(
    string CultureCode,
    string DisplayName,
    string NativeName,
    bool IsEnabled,
    bool IsDefault,
    int SortOrder);

public static class SupportedCultureMigrationBuilderExtensions
{
    public static void UpsertSupportedCultures(
        this MigrationBuilder migrationBuilder,
        IEnumerable<SupportedCultureSeed> cultures)
    {
        foreach (var culture in cultures)
        {
            migrationBuilder.Sql($"""
DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000000';
DECLARE @CultureCode nvarchar(10) = N'{Escape(culture.CultureCode)}';

IF EXISTS (SELECT 1 FROM [SupportedCultures] WHERE [TenantId] = @TenantId AND [CultureCode] = @CultureCode)
BEGIN
    UPDATE [SupportedCultures]
    SET [DisplayName] = N'{Escape(culture.DisplayName)}',
        [NativeName] = N'{Escape(culture.NativeName)}',
        [IsEnabled] = {(culture.IsEnabled ? 1 : 0)},
        [IsDefault] = {(culture.IsDefault ? 1 : 0)},
        [SortOrder] = {culture.SortOrder}
    WHERE [TenantId] = @TenantId AND [CultureCode] = @CultureCode;
END
ELSE
BEGIN
    INSERT INTO [SupportedCultures] ([Id], [CultureCode], [DisplayName], [NativeName], [IsEnabled], [IsDefault], [SortOrder], [TenantId], [CreatedAtUtc])
    VALUES (NEWID(), @CultureCode, N'{Escape(culture.DisplayName)}', N'{Escape(culture.NativeName)}', {(culture.IsEnabled ? 1 : 0)}, {(culture.IsDefault ? 1 : 0)}, {culture.SortOrder}, @TenantId, SYSUTCDATETIME());
END
""");
        }
    }

    private static string Escape(string value) => value.Replace("'", "''");
}
