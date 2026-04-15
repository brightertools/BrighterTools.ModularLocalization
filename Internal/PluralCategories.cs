namespace BrighterTools.ModularLocalization.Internal;

internal static class PluralCategories
{
    public const string Zero = "zero";
    public const string One = "one";
    public const string Two = "two";
    public const string Few = "few";
    public const string Many = "many";
    public const string Other = "other";

    public static bool IsValid(string? category)
        => category is null
           || category is Zero or One or Two or Few or Many or Other;
}
