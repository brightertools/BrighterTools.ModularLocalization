using Xunit;

namespace BrighterTools.ModularLocalization.Tests;

public sealed class PackagingSurfaceTests
{
    [Fact]
    public void BaseCultureValueSyncMode_DefaultIsIfMissing()
    {
        var options = new LocalizationOptions();
        Assert.Equal(BaseCultureValueSyncMode.IfMissing, options.BaseCultureValueSyncMode);
    }

    [Fact]
    public void OpenAiTranslationOptions_HasSafeDefaults()
    {
        var options = new OpenAiTranslationOptions();

        Assert.Equal("gpt-4.1-mini", options.Model);
        Assert.True(options.MaxCandidatesPerRun > 0);
        Assert.True(options.MaxRetryAttempts >= 0);
    }
}
