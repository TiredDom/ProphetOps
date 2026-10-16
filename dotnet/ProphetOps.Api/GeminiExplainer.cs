using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ProphetOps.Api;

/// The optional Gemini wording of the forecast. It is off unless Gemini:ApiKey is set (on a host,
/// the environment variable Gemini__ApiKey; GEMINI_API_KEY, which Google's own tools read, is used
/// when that is absent). The key is never logged or returned.
public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    /// Strongest first; each is tried in turn until one answers. Google's stable text models as of
    /// October 2026: the flagship, then the Flash models down to the cheapest Flash-Lite. The 2.5
    /// models are left out because Google keeps them for accounts that already used them.
    public static readonly IReadOnlyList<string> DefaultModels =
        ["gemini-4-argon", "gemini-3.8-flash", "gemini-3.6-flash", "gemini-3.5-flash-lite", "gemini-3.1-flash-lite"];

    public const int MaxModels = 8;

    public static readonly Uri ApiBase = new("https://generativelanguage.googleapis.com/v1beta/");

    private static readonly Regex ModelName = new("^[a-z0-9][a-z0-9.-]{0,63}$", RegexOptions.CultureInvariant);

    private static readonly string[] ThinkingLevels = ["minimal", "low", "medium", "high"];

    private GeminiOptions(
        string apiKey,
        IReadOnlyList<string> models,
        string? thinkingLevel,
        TimeSpan timeout,
        TimeSpan totalTimeout,
        int maxRequestsPerHour,
        TimeSpan cacheLifetime)
    {
        ApiKey = apiKey;
        Models = models;
        ThinkingLevel = thinkingLevel;
        Timeout = timeout;
        TotalTimeout = totalTimeout;
        MaxRequestsPerHour = maxRequestsPerHour;
        CacheLifetime = cacheLifetime;
    }

    public string ApiKey { get; }

    /// The models to try, strongest first.
    public IReadOnlyList<string> Models { get; }

    /// How much a model may think before answering; null leaves it to the model.
    public string? ThinkingLevel { get; }

    /// The time each model gets, and the time the whole chain gets.
    public TimeSpan Timeout { get; }
    public TimeSpan TotalTimeout { get; }

    /// Calls the whole service may send in an hour, the fallbacks included.
    public int MaxRequestsPerHour { get; }

    public TimeSpan CacheLifetime { get; }

    public bool Enabled => ApiKey.Length > 0;

    public static Uri GenerateContentUrl(string model) => new(ApiBase, $"models/{model}:generateContent");

    /// Gemini 2.5 models take a token budget rather than a thinking level, so they are sent none.
    public string? ThinkingLevelFor(string model) =>
        model.StartsWith("gemini-2.", StringComparison.Ordinal) ? null : ThinkingLevel;

    public static GeminiOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);

        var thinking = section["ThinkingLevel"]?.Trim().ToLowerInvariant() ?? "low";
        if (thinking is "" or "none" or "default") thinking = null;
        else if (!ThinkingLevels.Contains(thinking))
            throw new InvalidOperationException("Gemini:ThinkingLevel must be minimal, low, medium, high or none.");

        var timeout = Math.Clamp(section.GetValue("TimeoutSeconds", 20), 2, 60);
        return new GeminiOptions(
            (section["ApiKey"] ?? configuration["GEMINI_API_KEY"] ?? "").Trim(),
            ReadModels(section),
            thinking,
            TimeSpan.FromSeconds(timeout),
            TimeSpan.FromSeconds(Math.Clamp(section.GetValue("TotalTimeoutSeconds", 45), timeout, 120)),
            Math.Clamp(section.GetValue("MaxRequestsPerHour", 30), 1, 1000),
            TimeSpan.FromHours(Math.Clamp(section.GetValue("CacheHours", 12), 0, 72)));
    }

    /// Gemini:Models as a comma-separated list (as an environment variable carries it) or as an
    /// array in a settings file; Gemini:Model alone names a single model.
    private static IReadOnlyList<string> ReadModels(IConfigurationSection section)
    {
        var listed = new List<string>();
        if (!string.IsNullOrWhiteSpace(section["Models"]))
            listed.AddRange(section["Models"]!.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        else
            listed.AddRange(section.GetSection("Models").GetChildren().Select(c => (c.Value ?? "").Trim()).Where(m => m.Length > 0));
        if (listed.Count == 0 && !string.IsNullOrWhiteSpace(section["Model"])) listed.Add(section["Model"]!.Trim());
        if (listed.Count == 0) return DefaultModels;

        // The value is not repeated in the message, in case a key was pasted into the wrong setting.
        if (listed.Any(m => !ModelName.IsMatch(m)))
            throw new InvalidOperationException("Gemini:Models must list model names such as gemini-3.8-flash, separated by commas.");
        var models = listed.Distinct(StringComparer.Ordinal).ToList();
        if (models.Count > MaxModels)
            throw new InvalidOperationException($"Gemini:Models may list at most {MaxModels} models.");
        return models;
    }
}

/// Gemini's reply to one request: its status, its body, and the wait it asked for, if any.
public sealed record GeminiReply(int Status, string Body, TimeSpan? RetryAfter);

/// Sends one generateContent request. It throws only when no reply came back at all: a network
/// failure, or the time limit passing.
public interface IGeminiTransport
{
    Task<GeminiReply> GenerateContentAsync(Uri url, string apiKey, string requestJson, CancellationToken cancellationToken);
}

public sealed class HttpGeminiTransport(IHttpClientFactory clients) : IGeminiTransport
{
    public const string ClientName = "gemini";

    public async Task<GeminiReply> GenerateContentAsync(Uri url, string apiKey, string requestJson, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json"),
        };
        // In a header rather than the address, so the key stays out of anything that logs URLs.
        request.Headers.Add("x-goog-api-key", apiKey);

        using var response = await clients.CreateClient(ClientName).SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var retryAfter = response.Headers.RetryAfter is { } header
            ? header.Delta ?? (header.Date is { } date ? date - DateTimeOffset.UtcNow : null)
            : null;
        return new GeminiReply((int)response.StatusCode, body, retryAfter);
    }
}

/// An explanation of the forecast in plain words. Source is "gemini" when a Gemini model wrote it
/// and "rules" when ProphetOps did. PassedOver names the stronger models that did not write it, and
/// FallbackReason says why no Gemini wording is shown.
public sealed record Explanation(
    string Source,
    string? Model,
    string Summary,
    IReadOnlyList<string> Points,
    string? FallbackReason,
    IReadOnlyList<string> PassedOver)
{
    public const string NotConfigured = "not-configured";
    public const string Busy = "busy";
    public const string Unavailable = "unavailable";
    public const string UnknownFigure = "unknown-figure";
    public const string KeyRejected = "key-rejected";
}

/// Words the forecast, by rules or, when the agency has set it up, by Gemini. Gemini is given only
/// the forecast's own facts and may only rephrase them: its answer must be the expected JSON within
/// set lengths, and every figure in it must be one of the facts.
///
/// The models are tried strongest first. A model that is rate-limited, failing, slow, not open to
/// this key, or that answers out of shape is passed over for the next one; the rules' wording comes
/// back only when every model has been tried, the time runs out, or Google rejects the key. A model
/// that was rate-limited or failed rests for a while (as long as Google asks, and longer when it
/// keeps happening), so the next requests go straight to one that is answering. Answers are kept
/// per set of figures, and calls, fallbacks included, are capped per hour.
public sealed class ForecastExplainer(GeminiOptions options, IGeminiTransport transport, TimeProvider clock, ILogger<ForecastExplainer> logger)
{
    public const int SummaryLimit = 420;
    public const int PointLimit = 260;
    public const int MaxPoints = 4;

    private const int KeepAtMost = 64;

    private static readonly TimeSpan ShortestTry = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan KeyRejectedRest = TimeSpan.FromMinutes(10);

    private const string Instructions =
        "You explain a monthly forecast to the owner of a small travel agency in the Philippines who is not a statistician. " +
        "Write plain, calm, professional English. Use only the facts you are given. Do not calculate, estimate or invent any " +
        "figure: every number you write must be one of the given figures, written with digits, either as given or rounded. " +
        "Say \"forecast\" for figures from the model and \"recorded\" for figures that happened. Revenue is booking revenue, " +
        "never profit or cash. Mention the 80% range and, when it is available, how the accuracy test went. Do not give " +
        "financial advice, name people or companies, or add facts about the travel market. Reply with JSON only, in the form " +
        "{\"summary\": \"one or two sentences\", \"points\": [\"one short sentence\", ...]} with two to four points and no markup.";

    private static readonly char[] Markup = ['*', '`', '#', '<', '>'];

    private enum Outcome { Answered, OutOfShape, UnknownFigure, RateLimited, Unavailable, TimedOut, NotOpen, Refused, KeyRejected }

    private sealed record Attempt(Outcome Outcome, int Status = 0, (string Summary, IReadOnlyList<string> Points)? Written = null, TimeSpan? Wait = null);

    private sealed class Health
    {
        public DateTimeOffset RestUntil;
        public int Failures;
    }

    private readonly ConcurrentDictionary<string, (Explanation Answer, DateTimeOffset Expires)> _kept = new();
    private readonly Queue<DateTimeOffset> _calls = new();
    private readonly Lock _callsLock = new();
    private readonly Dictionary<string, Health> _health = new(StringComparer.Ordinal);
    private readonly Lock _healthLock = new();
    private DateTimeOffset _keyRejectedUntil;

    public bool AiAvailable => options.Enabled;

    public IReadOnlyList<string> Models => options.Enabled ? options.Models : [];

    public static Explanation Rules(ForecastFacts facts, string? fallbackReason = null)
    {
        var (summary, points) = ForecastWording.Write(facts);
        return new Explanation("rules", null, summary, points, fallbackReason, []);
    }

    public async Task<Explanation> WithGeminiAsync(ForecastFacts facts, CancellationToken cancellationToken)
    {
        if (!options.Enabled) return Rules(facts, Explanation.NotConfigured);

        var factsJson = facts.ForWriterJson();
        var key = KeyFor(factsJson);
        var start = clock.GetUtcNow();
        if (_kept.TryGetValue(key, out var kept) && kept.Expires > start) return kept.Answer;
        lock (_healthLock)
        {
            if (_keyRejectedUntil > start) return Rules(facts, Explanation.KeyRejected);
        }

        var deadline = start + options.TotalTimeout;
        var passedOver = new List<string>();
        string? reason = null;

        foreach (var model in options.Models)
        {
            var now = clock.GetUtcNow();
            if (RestingUntil(model) > now)
            {
                passedOver.Add(model);
                reason ??= Explanation.Busy;
                continue;
            }

            var left = deadline - now;
            if (left < ShortestTry) break;
            if (!TakeCall(now))
            {
                logger.LogInformation("The hourly limit of {Limit} Gemini calls is reached; the forecast was worded by rules.", options.MaxRequestsPerHour);
                reason = Explanation.Busy;
                break;
            }

            var attempt = await TryAsync(model, factsJson, facts, left < options.Timeout ? left : options.Timeout, cancellationToken);
            if (attempt is { Outcome: Outcome.Answered, Written: { } written })
            {
                Recovered(model);
                var answer = new Explanation("gemini", model, written.Summary, written.Points, null, passedOver.ToArray());
                Keep(key, answer, now);
                return answer;
            }

            if (attempt.Outcome == Outcome.KeyRejected)
            {
                lock (_healthLock) _keyRejectedUntil = now + KeyRejectedRest;
                logger.LogWarning("Gemini rejected the API key (HTTP {Status}); the forecast was worded by rules.", attempt.Status);
                return Rules(facts, Explanation.KeyRejected);
            }

            Rest(model, attempt, now);
            logger.LogWarning("Gemini model {Model} could not word the forecast ({Outcome}, HTTP {Status}); trying the next model.",
                model, attempt.Outcome, attempt.Status);
            passedOver.Add(model);
            reason = attempt.Outcome switch
            {
                Outcome.RateLimited => Explanation.Busy,
                Outcome.UnknownFigure => Explanation.UnknownFigure,
                _ => Explanation.Unavailable,
            };
        }

        logger.LogWarning("No Gemini model could word the forecast; it was worded by rules.");
        return Rules(facts, reason ?? Explanation.Unavailable);
    }

    private async Task<Attempt> TryAsync(string model, string factsJson, ForecastFacts facts, TimeSpan limit, CancellationToken cancellationToken)
    {
        GeminiReply reply;
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            timeout.CancelAfter(limit);
            try
            {
                reply = await transport.GenerateContentAsync(
                    GeminiOptions.GenerateContentUrl(model), options.ApiKey, RequestJson(factsJson, options.ThinkingLevelFor(model)), timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return new Attempt(Outcome.TimedOut);
            }
            catch (HttpRequestException)
            {
                return new Attempt(Outcome.Unavailable);
            }
        }

        if (reply.Status is >= 200 and < 300)
        {
            if (Read(reply.Body) is not { } written) return new Attempt(Outcome.OutOfShape, reply.Status);
            if (!ExplanationFigures.AllKnown(written.Summary, written.Points, facts)) return new Attempt(Outcome.UnknownFigure, reply.Status);
            return new Attempt(Outcome.Answered, reply.Status, written);
        }

        var (keyRejected, retryDelay) = ReadError(reply.Body);
        if (keyRejected || reply.Status == 401) return new Attempt(Outcome.KeyRejected, reply.Status);
        return reply.Status switch
        {
            429 => new Attempt(Outcome.RateLimited, 429, Wait: retryDelay ?? reply.RetryAfter),
            403 or 404 => new Attempt(Outcome.NotOpen, reply.Status),
            >= 500 => new Attempt(Outcome.Unavailable, reply.Status),
            _ => new Attempt(Outcome.Refused, reply.Status),
        };
    }

    private DateTimeOffset RestingUntil(string model)
    {
        lock (_healthLock) return _health.TryGetValue(model, out var health) ? health.RestUntil : default;
    }

    /// How long a model is passed over after it fails. An unusable answer says nothing about whether
    /// the model is up, so it costs no rest.
    private void Rest(string model, Attempt attempt, DateTimeOffset now)
    {
        if (attempt.Outcome is Outcome.OutOfShape or Outcome.UnknownFigure) return;
        lock (_healthLock)
        {
            if (!_health.TryGetValue(model, out var health)) _health[model] = health = new Health();
            health.Failures++;
            var rest = attempt.Outcome switch
            {
                Outcome.RateLimited => Longer(attempt.Wait, Doubling(TimeSpan.FromMinutes(1), health.Failures, TimeSpan.FromHours(1))),
                Outcome.NotOpen => TimeSpan.FromHours(6),
                Outcome.Refused => TimeSpan.FromMinutes(15),
                _ => Doubling(TimeSpan.FromSeconds(30), health.Failures, TimeSpan.FromMinutes(10)),
            };
            health.RestUntil = now + rest;
        }
    }

    private void Recovered(string model)
    {
        lock (_healthLock) _health.Remove(model);
    }

    private static TimeSpan Doubling(TimeSpan first, int failures, TimeSpan longest) =>
        TimeSpan.FromTicks(Math.Min(longest.Ticks, first.Ticks * (1L << Math.Min(failures - 1, 16))));

    private static TimeSpan Longer(TimeSpan? asked, TimeSpan own) =>
        asked is { } wait && wait > own ? (wait < TimeSpan.FromHours(6) ? wait : TimeSpan.FromHours(6)) : own;

    private void Keep(string key, Explanation answer, DateTimeOffset now)
    {
        if (options.CacheLifetime <= TimeSpan.Zero) return;
        _kept[key] = (answer, now + options.CacheLifetime);
        if (_kept.Count <= KeepAtMost) return;
        foreach (var stale in _kept.Where(e => e.Value.Expires <= now).Select(e => e.Key).ToList()) _kept.TryRemove(stale, out _);
        if (_kept.Count > KeepAtMost) _kept.Clear();
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
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(",", options.Models) + "\n" + factsJson)));

    /// No temperature is set: Google advises leaving it at the default for its current models. A low
    /// thinking level keeps a rewording quick and cheap; the answer is checked either way.
    private static string RequestJson(string factsJson, string? thinkingLevel)
    {
        var generation = new JsonObject
        {
            ["maxOutputTokens"] = 4096,
            ["responseMimeType"] = "application/json",
        };
        if (thinkingLevel is not null) generation["thinkingConfig"] = new JsonObject { ["thinkingLevel"] = thinkingLevel };

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
            ["generationConfig"] = generation,
        };
        return request.ToJsonString();
    }

    /// Whether an error reply rejects the key, and how long it asks callers to wait.
    private static (bool KeyRejected, TimeSpan? RetryDelay) ReadError(string body)
    {
        try
        {
            using var reply = JsonDocument.Parse(body);
            if (!reply.RootElement.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object) return (false, null);

            var keyRejected = error.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String
                && message.GetString()!.Contains("API key not valid", StringComparison.OrdinalIgnoreCase);
            TimeSpan? delay = null;
            if (error.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
            {
                foreach (var detail in details.EnumerateArray())
                {
                    if (detail.ValueKind != JsonValueKind.Object) continue;
                    if (detail.TryGetProperty("reason", out var reason) && reason.ValueKind == JsonValueKind.String
                        && reason.GetString() is "API_KEY_INVALID" or "API_KEY_EXPIRED") keyRejected = true;
                    if (detail.TryGetProperty("retryDelay", out var retry) && retry.ValueKind == JsonValueKind.String
                        && retry.GetString() is { } text && text.EndsWith('s')
                        && double.TryParse(text[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0)
                        delay = TimeSpan.FromSeconds(Math.Min(seconds, 6 * 3600));
                }
            }
            return (keyRejected, delay);
        }
        catch (JsonException)
        {
            return (false, null);
        }
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
