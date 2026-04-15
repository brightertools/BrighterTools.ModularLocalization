using System.Globalization;

namespace BrighterTools.ModularLocalization.Abstractions;

public interface ICultureResolver
{
    CultureInfo GetCurrentCulture();
}

internal sealed class DefaultCultureResolver : ICultureResolver
{
    public CultureInfo GetCurrentCulture()
        => CultureInfo.CurrentUICulture; // safe for background services too
}
