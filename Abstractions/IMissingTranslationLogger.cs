namespace BrighterTools.ModularLocalization.Abstractions;

public interface IMissingTranslationLogger
{
    void LogMissingOnce(Guid? tenantId, string culture, string key);
}
