namespace BrighterTools.ModularLocalization;

public sealed class OpenAiTranslationOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";
    public string Model { get; set; } = "gpt-4.1-mini";
    public double Temperature { get; set; } = 0.1;
    public int MaxOutputTokens { get; set; } = 4000;
    public string PromptContext { get; set; } = "Application UI localization";
    public int HttpTimeoutSeconds { get; set; } = 60;
    public int MaxRetryAttempts { get; set; } = 2;
    public int RetryBaseDelayMs { get; set; } = 500;
    public int MaxCandidatesPerRun { get; set; } = 2000;
}
