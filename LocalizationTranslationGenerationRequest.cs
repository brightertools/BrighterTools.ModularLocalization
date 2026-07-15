namespace BrighterTools.ModularLocalization;

public sealed class LocalizationTranslationGenerationRequest
{
    public string? SourceCulture { get; set; }
    public IReadOnlyList<string> TargetCultures { get; set; } = Array.Empty<string>();
    public IReadOnlyList<Guid> TranslationKeyIds { get; set; } = Array.Empty<Guid>();
    public Guid? TenantId { get; set; }
    public string? KeyStartsWith { get; set; }
    public bool OnlyMissing { get; set; } = true;
    public bool OverwriteMachineTranslatedValues { get; set; } = true;
    public int BatchSize { get; set; } = 50;
    public bool DryRun { get; set; } = false;
}
