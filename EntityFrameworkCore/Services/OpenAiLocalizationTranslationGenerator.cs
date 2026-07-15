using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BrighterTools.ModularLocalization.Abstractions;
using BrighterTools.ModularLocalization.EF.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BrighterTools.ModularLocalization.Services;

internal sealed class OpenAiLocalizationTranslationGenerator : ILocalizationTranslationGenerator
{
    private readonly ILocalizationDbContext _db;
    private readonly LocalizationOptions _localizationOptions;
    private readonly OpenAiTranslationOptions _openAiOptions;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAiLocalizationTranslationGenerator> _logger;

    public OpenAiLocalizationTranslationGenerator(
        ILocalizationDbContext db,
        IOptions<LocalizationOptions> localizationOptions,
        IOptions<OpenAiTranslationOptions> openAiOptions,
        HttpClient httpClient,
        ILogger<OpenAiLocalizationTranslationGenerator> logger)
    {
        _db = db;
        _localizationOptions = localizationOptions.Value;
        _openAiOptions = openAiOptions.Value;
        _httpClient = httpClient;
        _logger = logger;

        if (_openAiOptions.HttpTimeoutSeconds > 0)
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(_openAiOptions.HttpTimeoutSeconds);
        }
    }

    public async Task<LocalizationTranslationGenerationResult> GenerateAsync(
        LocalizationTranslationGenerationRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = new LocalizationTranslationGenerationResult();
        var sourceCulture = string.IsNullOrWhiteSpace(request.SourceCulture)
            ? _localizationOptions.DefaultCulture
            : request.SourceCulture.Trim();

        var targetCultures = request.TargetCultures
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Where(c => !string.Equals(c, sourceCulture, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (targetCultures.Count == 0)
        {
            result.Errors.Add("No target cultures were provided.");
            return result;
        }

        if (string.IsNullOrWhiteSpace(_openAiOptions.ApiKey))
        {
            result.Errors.Add("OpenAI ApiKey is not configured.");
            return result;
        }

        var effectiveTenant = NormalizeTenant(request.TenantId);
        var keyPrefix = request.KeyStartsWith?.Trim();
        var translationKeyIds = request.TranslationKeyIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
        var batchSize = request.BatchSize <= 0 ? 50 : request.BatchSize;
        var maxCandidates = _openAiOptions.MaxCandidatesPerRun <= 0 ? 2000 : _openAiOptions.MaxCandidatesPerRun;

        var keysQuery = _db.TranslationKeys
            .Where(k => k.TenantId == effectiveTenant);

        if (translationKeyIds.Length > 0)
        {
            keysQuery = keysQuery.Where(k => translationKeyIds.Contains(k.Id));
        }

        if (!string.IsNullOrWhiteSpace(keyPrefix))
        {
            keysQuery = keysQuery.Where(k => k.Key.StartsWith(keyPrefix));
        }

        var keys = await keysQuery
            .AsNoTracking()
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (keys.Count == 0)
            return result;

        var keyIds = keys.Select(k => k.Id).ToList();
        var sourceValues = await _db.TranslationValues
            .AsNoTracking()
            .Where(v =>
                v.TenantId == effectiveTenant &&
                v.Culture == sourceCulture &&
                keyIds.Contains(v.TranslationKeyId))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var sourceEntries = BuildSourceEntries(keys, sourceValues);
        if (sourceEntries.Count == 0)
            return result;

        var totalCandidateCount = 0;

        foreach (var targetCulture in targetCultures)
        {
            ct.ThrowIfCancellationRequested();

            var remainingCapacity = maxCandidates - totalCandidateCount;
            if (remainingCapacity <= 0)
            {
                result.Errors.Add($"MaxCandidatesPerRun limit reached ({maxCandidates}).");
                break;
            }

            var targetValues = await _db.TranslationValues
                .Where(v =>
                    v.TenantId == effectiveTenant &&
                    v.Culture == targetCulture &&
                    keyIds.Contains(v.TranslationKeyId))
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var targetByToken = targetValues.ToDictionary(
                static v => BuildToken(v.TranslationKeyId, v.PluralCategory),
                static v => v);

            var candidates = new List<CandidateEntry>();

            foreach (var entry in sourceEntries)
            {
                if (candidates.Count >= remainingCapacity)
                {
                    result.Errors.Add($"Candidate list truncated by MaxCandidatesPerRun ({maxCandidates}).");
                    break;
                }

                if (string.IsNullOrWhiteSpace(entry.SourceText))
                {
                    result.SkippedEntries++;
                    continue;
                }

                targetByToken.TryGetValue(entry.Token, out var existing);

                if (request.OnlyMissing && existing != null)
                {
                    result.SkippedEntries++;
                    continue;
                }

                if (existing != null)
                {
                    if (!existing.IsMachineTranslated)
                    {
                        // Never overwrite human translations.
                        result.SkippedEntries++;
                        continue;
                    }

                    if (!request.OverwriteMachineTranslatedValues)
                    {
                        result.SkippedEntries++;
                        continue;
                    }
                }

                candidates.Add(new CandidateEntry(entry, existing));
            }

            result.CandidateEntries += candidates.Count;
            totalCandidateCount += candidates.Count;

            if (candidates.Count == 0)
                continue;

            foreach (var batch in Batch(candidates, batchSize))
            {
                var translated = await TranslateBatchAsync(
                    sourceCulture,
                    targetCulture,
                    batch,
                    ct).ConfigureAwait(false);

                var now = DateTime.UtcNow;

                foreach (var candidate in batch)
                {
                    if (!translated.TryGetValue(candidate.Source.PromptKey, out var translatedText) ||
                        string.IsNullOrWhiteSpace(translatedText))
                    {
                        result.FailedEntries++;
                        continue;
                    }

                    if (request.DryRun)
                    {
                        if (candidate.Existing == null)
                            result.GeneratedEntries++;
                        else
                            result.UpdatedEntries++;

                        continue;
                    }

                    if (candidate.Existing == null)
                    {
                        _db.TranslationValues.Add(new TranslationValue
                        {
                            Id = Guid.NewGuid(),
                            TranslationKeyId = candidate.Source.TranslationKeyId,
                            Culture = targetCulture,
                            PluralCategory = candidate.Source.PluralCategory,
                            Value = translatedText,
                            IsMachineTranslated = true,
                            TenantId = effectiveTenant,
                            CreatedAtUtc = now,
                            UpdatedAtUtc = now
                        });

                        result.GeneratedEntries++;
                    }
                    else
                    {
                        candidate.Existing.Value = translatedText;
                        candidate.Existing.IsMachineTranslated = true;
                        candidate.Existing.UpdatedAtUtc = now;
                        result.UpdatedEntries++;
                    }
                }
            }

            if (!request.DryRun)
            {
                await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            }
        }

        return result;
    }

    private static List<SourceEntry> BuildSourceEntries(
        IReadOnlyList<TranslationKey> keys,
        IReadOnlyList<TranslationValue> sourceValues)
    {
        var valuesByToken = sourceValues.ToDictionary(
            static v => BuildToken(v.TranslationKeyId, v.PluralCategory),
            static v => v);
        var keyById = keys.ToDictionary(static k => k.Id, static k => k);

        var entries = new List<SourceEntry>(keys.Count * 2);
        var promptKeySet = new HashSet<string>(StringComparer.Ordinal);

        foreach (var key in keys)
        {
            var baseToken = BuildToken(key.Id, null);
            var basePromptKey = BuildPromptKey(key.Key, null, promptKeySet);

            valuesByToken.TryGetValue(baseToken, out var existingBaseValue);
            var baseSource = existingBaseValue?.Value ?? key.DefaultValue;

            entries.Add(new SourceEntry(
                key.Id,
                null,
                baseToken,
                basePromptKey,
                baseSource));
        }

        foreach (var value in sourceValues.Where(v => v.PluralCategory != null))
        {
            if (!keyById.TryGetValue(value.TranslationKeyId, out var key))
                continue;

            var promptKey = BuildPromptKey(key.Key, value.PluralCategory, promptKeySet);
            entries.Add(new SourceEntry(
                value.TranslationKeyId,
                value.PluralCategory,
                BuildToken(value.TranslationKeyId, value.PluralCategory),
                promptKey,
                value.Value));
        }

        return entries;
    }

    private async Task<Dictionary<string, string>> TranslateBatchAsync(
        string sourceCulture,
        string targetCulture,
        IReadOnlyList<CandidateEntry> batch,
        CancellationToken ct)
    {
        var payloadItems = batch.ToDictionary(
            static b => b.Source.PromptKey,
            static b => b.Source.SourceText);

        var userJson = JsonSerializer.Serialize(payloadItems);
        var systemPrompt = BuildSystemPrompt(sourceCulture, targetCulture);
        var requestBody = new
        {
            model = _openAiOptions.Model,
            temperature = _openAiOptions.Temperature,
            max_tokens = _openAiOptions.MaxOutputTokens,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new
                {
                    role = "user",
                    content = "Translate the following JSON values and return JSON only with the same keys: " + userJson
                }
            }
        };

        var endpoint = BuildChatCompletionsEndpoint();
        var body = JsonSerializer.Serialize(requestBody);

        for (var attempt = 0; attempt <= _openAiOptions.MaxRetryAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _openAiOptions.ApiKey);

            try
            {
                using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
                var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    if (IsTransientStatus(response.StatusCode) && attempt < _openAiOptions.MaxRetryAttempts)
                    {
                        await DelayForRetryAsync(attempt, ct).ConfigureAwait(false);
                        continue;
                    }

                    _logger.LogWarning(
                        "OpenAI translation request failed. Status={StatusCode}, Body={Body}",
                        (int)response.StatusCode,
                        TruncateForLog(responseBody));
                    return new Dictionary<string, string>(StringComparer.Ordinal);
                }

                var content = ExtractAssistantContent(responseBody);
                if (string.IsNullOrWhiteSpace(content))
                {
                    _logger.LogWarning("OpenAI response contained no content.");
                    return new Dictionary<string, string>(StringComparer.Ordinal);
                }

                try
                {
                    using var translatedJson = JsonDocument.Parse(content);
                    if (translatedJson.RootElement.ValueKind != JsonValueKind.Object)
                        return new Dictionary<string, string>(StringComparer.Ordinal);

                    var output = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var prop in translatedJson.RootElement.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.String)
                            output[prop.Name] = prop.Value.GetString() ?? string.Empty;
                    }

                    return output;
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "OpenAI content was not valid JSON object.");
                    return new Dictionary<string, string>(StringComparer.Ordinal);
                }
            }
            catch (HttpRequestException ex) when (attempt < _openAiOptions.MaxRetryAttempts)
            {
                _logger.LogDebug(ex, "Transient HTTP error while calling OpenAI. Attempt {Attempt}.", attempt + 1);
                await DelayForRetryAsync(attempt, ct).ConfigureAwait(false);
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested && attempt < _openAiOptions.MaxRetryAttempts)
            {
                _logger.LogDebug(ex, "OpenAI request timeout. Attempt {Attempt}.", attempt + 1);
                await DelayForRetryAsync(attempt, ct).ConfigureAwait(false);
            }
        }

        return new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private static bool IsTransientStatus(HttpStatusCode code)
        => code == HttpStatusCode.RequestTimeout ||
           code == (HttpStatusCode)429 ||
           (int)code >= 500;

    private async Task DelayForRetryAsync(int attempt, CancellationToken ct)
    {
        var baseDelay = _openAiOptions.RetryBaseDelayMs <= 0 ? 500 : _openAiOptions.RetryBaseDelayMs;
        var delayMs = baseDelay * (int)Math.Pow(2, attempt);
        await Task.Delay(delayMs, ct).ConfigureAwait(false);
    }

    private static string TruncateForLog(string? value, int maxLength = 1000)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value ?? string.Empty;

        return value[..maxLength] + "...";
    }

    private static string BuildToken(Guid translationKeyId, string? pluralCategory)
        => pluralCategory is null
            ? translationKeyId.ToString("N")
            : translationKeyId.ToString("N") + "|" + pluralCategory;

    private static string BuildPromptKey(
        string key,
        string? pluralCategory,
        ISet<string> existingPromptKeys)
    {
        var raw = pluralCategory is null ? key : key + "_" + pluralCategory;
        var sanitized = raw.Replace(" ", "_");
        var candidate = sanitized;
        var suffix = 2;

        while (!existingPromptKeys.Add(candidate))
        {
            candidate = sanitized + "__" + suffix;
            suffix++;
        }

        return candidate;
    }

    private string BuildSystemPrompt(string sourceCulture, string targetCulture)
    {
        var context = string.IsNullOrWhiteSpace(_openAiOptions.PromptContext)
            ? "Application UI localization"
            : _openAiOptions.PromptContext.Trim();

        return
            "You are a professional software localization translator. " +
            "Context: " + context + ". " +
            "Translate from " + sourceCulture + " to " + targetCulture + ". " +
            "Return ONLY a valid JSON object with the same keys. " +
            "Do not add keys, remove keys, or change placeholders like {count}, {name}, {0}. " +
            "Keep brand names and technical identifiers unchanged.";
    }

    private Uri BuildChatCompletionsEndpoint()
    {
        var baseUrl = string.IsNullOrWhiteSpace(_openAiOptions.BaseUrl)
            ? "https://api.openai.com/v1/"
            : _openAiOptions.BaseUrl.Trim();

        if (!baseUrl.EndsWith("/", StringComparison.Ordinal))
            baseUrl += "/";

        return new Uri(new Uri(baseUrl, UriKind.Absolute), "chat/completions");
    }

    private static string? ExtractAssistantContent(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0)
            return null;

        var first = choices[0];
        if (!first.TryGetProperty("message", out var message) ||
            !message.TryGetProperty("content", out var content) ||
            content.ValueKind != JsonValueKind.String)
            return null;

        return content.GetString();
    }

    private Guid NormalizeTenant(Guid? tenantId)
    {
        if (!_localizationOptions.EnableTenantSupport)
            return Guid.Empty;

        return tenantId ?? Guid.Empty;
    }

    private sealed record SourceEntry(
        Guid TranslationKeyId,
        string? PluralCategory,
        string Token,
        string PromptKey,
        string SourceText);

    private sealed record CandidateEntry(SourceEntry Source, TranslationValue? Existing);

    private static IEnumerable<IReadOnlyList<CandidateEntry>> Batch(
        IReadOnlyList<CandidateEntry> items,
        int batchSize)
    {
        for (var i = 0; i < items.Count; i += batchSize)
        {
            var count = Math.Min(batchSize, items.Count - i);
            var slice = new List<CandidateEntry>(count);
            for (var j = 0; j < count; j++)
            {
                slice.Add(items[i + j]);
            }

            yield return slice;
        }
    }
}

