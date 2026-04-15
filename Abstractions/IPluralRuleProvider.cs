namespace BrighterTools.ModularLocalization.Abstractions;

public interface IPluralRuleProvider
{
    string GetPluralCategory(string culture, int count);
}
