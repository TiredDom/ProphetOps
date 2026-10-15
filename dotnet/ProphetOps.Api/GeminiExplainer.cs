using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ProphetOps.Api;

/// The optional Gemini wording of the forecast. It is off unless Gemini:ApiKey is set, which on a
/// host means the environment variable Gemini__ApiKey; the key is never logged or returned.
public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    /// Google's stable Flash model for new projects as of October 2026; Gemini:Model changes it.
    public const string DefaultModel = "gemini-3.8-flash";

    public static readonly Uri ApiBase = new("https://generativelanguage.googleapis.com/v1beta/");

    private static readonly Regex ModelName = new("^[a-z0-9][a-z0-9.-]{0,63}$", RegexOptions.CultureInvariant);

    private GeminiOptions(string apiKey, string model, TimeSpan timeout, int maxRequestsPerHour, TimeSpan cacheLifetime)
    {
        ApiKey = apiKey;
        Model = model;
        Timeout = timeout;
        MaxRequestsPerHour = maxRequestsPerHour;
        CacheLifetime = cacheLifetime;
    }

    public string ApiKey { get; }
    public string Model { get; }
    public TimeSpan Timeout { get; }
    public int MaxRequestsPerHour { get; }
    public TimeSpan CacheLifetime { get; }
    public bool Enabled => ApiKey.Length > 0;

    public Uri GenerateContentUrl => new(ApiBase, $"models/{Model}:generateContent");

    public static GeminiOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        var model = (section["Model"] ?? "").Trim();
        if (model.Length == 0) model = DefaultModel;
        if (!ModelName.IsMatch(model))
            throw new InvalidOperationException("Gemini:Model must be a model name such as gemini-3.8-flash.");

        return new GeminiOptions(
            (section["ApiKey"] ?? "").Trim(),
            model,
            TimeSpan.FromSeconds(Math.Clamp(section.GetValue("TimeoutSeconds", 12), 2, 60)),
            Math.Clamp(section.GetValue("MaxRequestsPerHour", 30), 1, 1000),
            TimeSpan.FromHours(Math.Clamp(section.GetValue("CacheHours", 12), 0, 72)));
    }
}

/// Sends one generateContent request and returns the answer's body.
public interface IGeminiTransport
{
    Task<string> GenerateContentAsync(Uri url, string apiKey, string requestJson, CancellationToken cancellationToken);
}

public sealed class HttpGeminiTransport(IHttpClientFactory clients) : IGeminiTransport
{
    public const string ClientName = "gemini";

    public async Task<string> GenerateContentAsync(Uri url, string apiKey, string requestJson, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json"),
        };
        // In a header rather than the address, so the key stays out of anything that logs URLs.
        request.Headers.Add("x-goog-api-key", apiKey);

        using var response = await clients.CreateClient(ClientName).SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Gemini answered {(int)response.StatusCode}.", null, response.StatusCode);
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}

/// An explanation of the forecast in plain words. Source is "gemini" when Gemini wrote it and
/// "rules" when ProphetOps did; FallbackReason says why Gemini's wording is not the one shown.
public sealed record Explanation(string Source, string? Model, string Summary, IReadOnlyList<string> Points, string? FallbackReason)
{
    public const string NotConfigured = "not-configured";
    public const string Busy = "busy";
    public const string Unavailable = "unavailable";
    public const string UnknownFigure = "unknown-figure";
}

/// Words the forecast, by rules or, when the agency has set it up, by Gemini. Gemini is given only
/// the forecast's own facts and may only rephrase them: its answer must be the expected JSON within
/// set lengths, and every figure in it must be one of the facts, or ProphetOps' own wording is
/// used instead. Answers are kept for a while, and calls are capped per hour.
public sealed class ForecastExplainer(GeminiOptions options, IGeminiTransport transport, TimeProvider clock, ILogger<ForecastExplainer> logger)
{
    public const int SummaryLimit = 420;
    public const int PointLimit = 260;
    public const int MaxPoints = 4;

    private const int KeepAtMost = 64;

    private const string Instructions =
        "You explain a monthly forecast to the owner of a small travel agency in the Philippines who is not a statistician. " +
        "Write plain, calm, professional English. Use only the facts you are given. Do not calculate, estimate or invent any " +
        "figure: every number you write must be one of the given figures, written with digits, either as given or rounded. " +
        "Say \"forecast\" for figures from the model and \"recorded\" for figures that happened. Revenue is booking revenue, " +
        "never profit or cash. Mention the 80% range and, when it is available, how the accuracy test went. Do not give " +
        "financial advice, name people or companies, or add facts about the travel market. Reply with JSON only, in the form " +
        "{\"summary\": \"one or two sentences\", \"points\": [\"one short sentence\", ...]} with two to four points and no markup.";

    private static readonly char[] Markup = ['*', '`', '#', '<', '>'];

    private readonly ConcurrentDictionary<string, (Explanation Answer, DateTimeOffset Expires)> _kept = new();
    private readonly Queue<DateTimeOffset> _calls = new();
    private readonly Lock _callsLock = new();

    public bool AiAvailable => options.Enabled;

    public string? Model => options.Enabled ? options.Model : null;

    public static Explanation Rules(ForecastFacts facts, string? fallbackReason = null)
    {
        var (summary, points) = ForecastWording.Write(facts);
        return new Explanation("rules", null, summary, points, fallbackReason);
    }

    public async Task<Explanation> WithGeminiAsync(ForecastFacts facts, CancellationToken cancellationToken)
    {
        if (!options.Enabled) return Rules(facts, Explanation.NotConfigured);

        var factsJson = facts.ForWriterJson();
        var key = KeyFor(factsJson);
        var now = clock.GetUtcNow();
        if (_kept.TryGetValue(key, out var kept) && kept.Expires > now) return kept.Answer;
        if (!TakeCall(now))
        {
            logger.LogInformation("The hourly limit of {Limit} Gemini requests is reached; the forecast was worded by rules.", options.MaxRequestsPerHour);
            return Rules(facts, Explanation.Busy);
        }

        string body;
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            timeout.CancelAfter(options.Timeout);
            try
            {
                body = await transport.GenerateContentAsync(options.GenerateContentUrl, options.ApiKey, RequestJson(factsJson), timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Gemini did not answer within {Seconds} seconds; the forecast was worded by rules.", options.Timeout.TotalSeconds);
                return Rules(facts, Explanation.Unavailable);
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning("Gemini could not be used ({Status}); the forecast was worded by rules.", ex.StatusCode is { } status ? (int)status : 0);
                return Rules(facts, Explanation.Unavailable);
            }
        }

        if (Read(body) is not { } written)
        {
            logger.LogWarning("Gemini's answer was not the expected JSON; the forecast was worded by rules.");
            return Rules(facts, Explanation.Unavailable);
        }

        if (!ExplanationFigures.AllKnown(written.Summary, written.Points, facts))
        {
            logger.LogWarning("Gemini's wording held a figure that is not in the forecast; the forecast was worded by rules.");
            return Rules(facts, Explanation.UnknownFigure);
        }

        var answer = new Explanation("gemini", options.Model, written.Summary, written.Points, null);
        if (options.CacheLifetime > TimeSpan.Zero)
        {
            _kept[key] = (answer, now + options.CacheLifetime);
            if (_kept.Count > KeepAtMost)
            {
                foreach (var stale in _kept.Where(e => e.Value.Expires <= now).Select(e => e.Key).ToList()) _kept.TryRemove(stale, out _);
                if (_kept.Count > KeepAtMost) _kept.Clear();
            }
        }
        return answer;
    }

    private bool TakeCall(DateTimeOffset now)
    {
        lock (_callsLock)
        {
            while (_calls.Count > 0 && _calls.Peek() <= now - TimeSpan.FromHours(1)) _calls.Dequeue();
            if (_calls.Count >= options.MaxRequestsPerHour) return false;
            _calls.Enqueue(now);
            return true;
        }
    }

    private string KeyFor(string factsJson) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(options.Model + "\n" + factsJson)));

    private static string RequestJson(string factsJson)
    {
        var request = new JsonObject
        {
            ["systemInstruction"] = new JsonObject
            {
                ["parts"] = new JsonArray(new JsonObject { ["text"] = Instructions }),
            },
            ["contents"] = new JsonArray(new JsonObject
            {
                ["role"] = "user",
                ["parts"] = new JsonArray(new JsonObject { ["text"] = "Explain this forecast. The facts, as JSON:\n" + factsJson }),
            }),
            ["generationConfig"] = new JsonObject
            {
                ["temperature"] = 0.2,
                ["maxOutputTokens"] = 2048,
                ["responseMimeType"] = "application/json",
            },
        };
        return request.ToJsonString();
    }

    /// The summary and points from a generateContent answer, cleaned to plain text, or null when the
    /// answer is not the JSON asked for or runs past the set lengths.
    public static (string Summary, IReadOnlyList<string> Points)? Read(string body)
    {
        try
        {
            using var answer = JsonDocument.Parse(body);
            if (!answer.RootElement.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array
                || candidates.GetArrayLength() == 0) return null;
            var first = candidates[0];
            if (first.TryGetProperty("finishReason", out var finish) && finish.GetString() is { } reason && reason != "STOP") return null;
            if (!first.TryGetProperty("content", out var content) || !content.TryGetProperty("parts", out var parts)) return null;

            var text = new StringBuilder();
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("thought", out var thought) && thought.ValueKind == JsonValueKind.True) continue;
                if (part.TryGetProperty("text", out var piece)) text.Append(piece.GetString());
            }

            // Asked for JSON only, but a fenced block is still JSON underneath.
            var json = text.ToString().Trim();
            if (json.StartsWith("```", StringComparison.Ordinal))
            {
                json = json.Trim('`').Trim();
                if (json.StartsWith("json", StringComparison.OrdinalIgnoreCase)) json = json[4..].Trim();
            }

            using var written = JsonDocument.Parse(json);
            var root = written.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (!root.TryGetProperty("summary", out var summaryElement) || summaryElement.ValueKind != JsonValueKind.String) return null;
            var summary = Clean(summaryElement.GetString());
            if (summary.Length is 0 or > SummaryLimit) return null;

            var points = new List<string>();
            if (root.TryGetProperty("points", out var pointsElement))
            {
                if (pointsElement.ValueKind != JsonValueKind.Array || pointsElement.GetArrayLength() > MaxPoints) return null;
                foreach (var point in pointsElement.EnumerateArray())
                {
                    if (point.ValueKind != JsonValueKind.String) return null;
                    var line = Clean(point.GetString());
                    if (line.Length > PointLimit) return null;
                    if (line.Length > 0) points.Add(line);
                }
            }
            return (summary, points);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// Plain text on one line: no control characters, no markup characters, single spaces.
    private static string Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var kept = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (char.IsControl(c) || char.IsWhiteSpace(c)) kept.Append(' ');
            else if (Array.IndexOf(Markup, c) < 0) kept.Append(c);
        }
        return Regex.Replace(kept.ToString(), " {2,}", " ").Trim();
    }
}

/// Checks that every figure in a piece of writing is one of the forecast's own facts, as given or
/// rounded. A figure that is not (an invented percentage, a total the writer worked out) fails it.
public static class ExplanationFigures
{
    private static readonly Regex Figure = new(
        @"(?<![\w.,])(?<currency>₱|PHP\s?)?(?<number>\d{1,3}(?:,\d{3})+(?:\.\d+)?|\d+(?:\.\d+)?)(?:\s?(?<scale>million|thousand|[kKmM](?![a-zA-Z])))?(?<percent>\s?(?:%|percent|per cent))?",
        RegexOptions.CultureInvariant);

    public static bool AllKnown(string summary, IEnumerable<string> points, ForecastFacts facts)
    {
        var amounts = new List<double> { facts.LastRecordedValue, facts.SixMonthTotal, facts.PeakValue };
        foreach (var month in facts.Months) amounts.AddRange([month.Value, month.Lower, month.Upper]);

        var percents = new List<double> { Math.Abs(facts.ChangePercent), 80 };
        // Small whole numbers that describe the method: months ahead, the season, two years, 8 in 10.
        var counts = new List<double> { 1, 2, 3, 4, 5, 6, 8, 10, 12, 24, 25, facts.PassedMonths.Count };
        foreach (var label in facts.Months.Select(m => m.Month).Append(facts.LastRecordedMonth).Concat(facts.PassedMonths))
            if (double.TryParse(label[^4..], NumberStyles.None, CultureInfo.InvariantCulture, out var year)) counts.Add(year);

        if (facts.Test is { } test)
        {
            counts.AddRange([test.PastMonths, test.Forecasts, test.InsideRange]);
            percents.Add(test.CoveragePercent);
            if (test.Mape is { } mape) percents.Add(mape);
            if (test.SameMonthLastYearMape is { } naive) percents.Add(naive);
            if (test.SmallerMissPercent is { } skill) percents.Add(Math.Abs(skill));
            foreach (var label in new[] { test.From, test.To })
                if (double.TryParse(label[^4..], NumberStyles.None, CultureInfo.InvariantCulture, out var year)) counts.Add(year);
        }

        foreach (var text in points.Prepend(summary))
        {
            foreach (Match match in Figure.Matches(text))
            {
                if (!Known(match, amounts, percents, counts)) return false;
            }
        }
        return true;
    }

    private static bool Known(Match match, List<double> amounts, List<double> percents, List<double> counts)
    {
        var written = match.Groups["number"].Value;
        var digits = written.Replace(",", "", StringComparison.Ordinal);
        if (!double.TryParse(digits, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number)) return false;
        var amount = written.Contains(',') || match.Groups["currency"].Success;

        var scale = match.Groups["scale"].Value.ToLowerInvariant() switch
        {
            "million" or "m" => 1_000_000d,
            "thousand" or "k" => 1_000d,
            _ => 1d,
        };
        var decimals = digits.Contains('.') ? digits.Length - digits.IndexOf('.') - 1 : 0;
        var precision = scale * Math.Pow(10, -decimals);
        // "₱160,000" is an amount rounded to the thousand, as people round; a year such as 2030 is
        // not, and neither is any small number.
        if (amount && decimals == 0 && scale == 1 && number >= 1000)
        {
            var zeros = 0;
            for (var rest = (long)number; rest % 10 == 0 && zeros < 6; rest /= 10) zeros++;
            precision = Math.Pow(10, zeros);
        }
        var value = number * scale;

        bool Near(IEnumerable<double> pool) => pool.Any(fact => Math.Abs(Math.Abs(fact) - value) <= precision / 2 * (1 + 1e-9));

        if (match.Groups["percent"].Success) return Near(percents);
        if (amount || scale > 1) return Near(amounts);
        return Near(amounts) || Near(counts) || Near(percents);
    }
}
