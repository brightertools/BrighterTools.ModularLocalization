namespace BrighterTools.ModularLocalization;

public sealed class LocalizationOptions
{
    public string DefaultCulture { get; set; } = "en";
    public bool AutoRegisterMissingKeys { get; set; } = true;
    public BaseCultureValueSyncMode BaseCultureValueSyncMode { get; set; } = BaseCultureValueSyncMode.IfMissing;
    public bool StrictMode { get; set; } = false;

    public string? SchemaName { get; set; } = null;

    public bool UseRedis { get; set; } = false;
    public string? RedisConnectionString { get; set; }
    public string? RedisInstanceName { get; set; }

    public bool EnableTenantSupport { get; set; } = true;

    public bool EnableAutoMigrate { get; set; } = false;

    public TimeSpan CacheTtl
    {
        get => _cacheTtl;
        set => _cacheTtl = value <= TimeSpan.Zero
            ? throw new ArgumentOutOfRangeException(nameof(CacheTtl))
            : value;
    }

    private TimeSpan _cacheTtl = TimeSpan.FromMinutes(15);

    public bool WarmupOnStartup { get; set; } = false;

    public List<string> WarmupCultures { get; set; } = new();

    public bool EnableRefreshAhead { get; set; } = false;

    public TimeSpan RefreshAheadWindow { get; set; } = TimeSpan.FromMinutes(1);
}
