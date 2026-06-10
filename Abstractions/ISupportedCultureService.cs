namespace BrighterTools.ModularLocalization.Abstractions;

public interface ISupportedCultureService
{
    Task<IReadOnlyList<SupportedCultureDto>> GetEnabledCulturesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<SupportedCultureDto>> GetCulturesAsync(CancellationToken ct = default);

    Task<SupportedCultureDto> UpsertCultureAsync(
        UpsertSupportedCultureRequest request,
        CancellationToken ct = default);
}
