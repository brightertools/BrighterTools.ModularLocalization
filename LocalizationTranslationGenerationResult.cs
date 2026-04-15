namespace BrighterTools.ModularLocalization;

public sealed class LocalizationTranslationGenerationResult
{
    public int CandidateEntries { get; set; }
    public int GeneratedEntries { get; set; }
    public int UpdatedEntries { get; set; }
    public int SkippedEntries { get; set; }
    public int FailedEntries { get; set; }
    public List<string> Errors { get; } = new();
}
