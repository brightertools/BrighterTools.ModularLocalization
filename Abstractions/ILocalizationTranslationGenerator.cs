namespace BrighterTools.ModularLocalization.Abstractions;

public interface ILocalizationTranslationGenerator
{
    Task<LocalizationTranslationGenerationResult> GenerateAsync(
        LocalizationTranslationGenerationRequest request,
        CancellationToken ct = default);
}
