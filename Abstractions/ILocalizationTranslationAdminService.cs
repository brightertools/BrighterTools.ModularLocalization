namespace BrighterTools.ModularLocalization.Abstractions;

public interface ILocalizationTranslationAdminService
{
    bool CanGenerateTranslations { get; }

    Task<LocalizationTranslationListResponse> GetTranslationsAsync(
        LocalizationTranslationListRequest request,
        CancellationToken ct = default);

    Task<IReadOnlyList<LocalizationTranslationTreeNodeDto>> GetTranslationTreeAsync(
        CancellationToken ct = default);

    Task<LocalizationTranslationDto?> UpsertTranslationAsync(
        Guid translationKeyId,
        UpsertLocalizationTranslationRequest request,
        CancellationToken ct = default);

    Task<LocalizationTranslationDto?> GenerateTranslationAsync(
        Guid translationKeyId,
        GenerateLocalizationTranslationRequest request,
        CancellationToken ct = default);
}
