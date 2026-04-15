namespace BrighterTools.ModularLocalization.EF;

internal static class EfConventions
{
    public static void StampUtcOnAddOrUpdate(object entity)
    {
        var now = DateTime.UtcNow;
        var type = entity.GetType();

        var created = type.GetProperty("CreatedAtUtc");
        var updated = type.GetProperty("UpdatedAtUtc");

        if (created != null && (DateTime)created.GetValue(entity)! == default)
            created.SetValue(entity, now);

        updated?.SetValue(entity, now);
    }
}
