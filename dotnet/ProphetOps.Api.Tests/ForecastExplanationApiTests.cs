using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProphetOps.Data;
using ProphetOps.Domain;
using Xunit;

namespace ProphetOps.Api.Tests;

/// The forecast in plain words: ProphetOps' own wording, and Gemini's when the agency sets a key.
/// Gemini is never reached here; a stand-in answers in its place and records what it was sent.
public class ForecastExplanationApiTests
{
    private const string Key = "test-key-not-a-real-key";
    private const string ClientName = "Delos Reyes Family";
    private const string StaffEmail = "staff.explain@prophetops.local";
    private const string StaffPassword = "staff-explain-1";

    private static readonly double[] Season = [0.9, 0.8, 1.0, 1.1, 1.3, 1.2, 0.9, 0.85, 0.95, 1.05, 1.15, 1.4];
    private static readonly IReadOnlyList<string> Chain = GeminiOptions.DefaultModels;

    /// February 2024 onwards, two bookings a month for one client, revenue following a yearly shape.
    private static Action<AppDbContext> Months(int count) => db =>
    {
        var start = new DateOnly(2024, 2, 1);
        for (var i = 0; i < count; i++)
        {
            var month = start.AddMonths(i);
            var revenue = (int)(150_000 * Season[(month.Month - 1) % 12] * (1 + i * 0.004));
            foreach (var (suffix, day, share) in new[] { ("A", 6, 0.6), ("B", 21, 0.4) })
            {
                db.Bookings.Add(new Booking
                {
                    Code = $"{suffix}-{month:yyyyMM}",
                    BookingDate = new DateOnly(month.Year, month.Month, day),
                    PassengerCount = 2 + i % 4,
                    Client = ClientName,
                    PackageName = "Tailored trip",
                    EntryType = "Custom quotation",
                    Destination = "Explanation destination",
                    GrossRevenue = (int)(revenue * share),
                    PaymentStatus = "Paid",
                    BookingStatus = "Confirmed",
                });
            }
        }
    };

    [Fact]
    public async Task Without_a_key_the_forecast_is_worded_by_rules_and_nothing_is_sent()
    {
        var gemini = new StandInGemini();
        using var factory = new ExplanationFactory(Months(30), gemini);
        using var client = await AuthenticatedClient.Login(factory);

        var worded = await Body(await client.GetAsync("/api/forecast/explanation"));

        Assert.True(worded.GetProperty("ok").GetBoolean());
        Assert.Equal("rules", worded.GetProperty("source").GetString());
        Assert.False(worded.GetProperty("aiAvailable").GetBoolean());
        Assert.Empty(worded.GetProperty("aiModels").EnumerateArray());
        Assert.StartsWith("Booking revenue is forecast to", worded.GetProperty("summary").GetString());
        var points = worded.GetProperty("points").EnumerateArray().Select(p => p.GetString()!).ToList();
        Assert.Equal(4, points.Count);
        Assert.Contains(points, p => p.Contains("80% range", StringComparison.Ordinal));
        Assert.Contains(points, p => p.StartsWith("Tested on ", StringComparison.Ordinal));

        var asked = await Body(await client.PostAsync("/api/forecast/explanation", null));

        Assert.Equal("rules", asked.GetProperty("source").GetString());
        Assert.Equal("not-configured", asked.GetProperty("fallbackReason").GetString());
        Assert.Empty(gemini.Requests);
    }

    [Fact]
    public async Task With_a_key_the_strongest_model_words_the_same_figures_and_is_given_nothing_about_clients()
    {
        var gemini = new StandInGemini { Answer = StandInGemini.WordingFromFacts };
        using var factory = new ExplanationFactory(Months(30), gemini, Key);
        using var client = await AuthenticatedClient.Login(factory);

        var offered = await Body(await client.GetAsync("/api/forecast/explanation"));
        Assert.True(offered.GetProperty("aiAvailable").GetBoolean());
        Assert.Equal(Chain, offered.GetProperty("aiModels").EnumerateArray().Select(m => m.GetString()));

        var worded = await Body(await client.PostAsync("/api/forecast/explanation", null));

        Assert.Equal("gemini", worded.GetProperty("source").GetString());
        Assert.Equal(Chain[0], worded.GetProperty("model").GetString());
        Assert.Empty(worded.GetProperty("passedOver").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, worded.GetProperty("fallbackReason").ValueKind);
        var peak = worded.GetProperty("facts").GetProperty("highestForecastMonth");
        Assert.Equal(
            $"The forecast is highest in {peak.GetProperty("month").GetString()}, at about {peak.GetProperty("forecast").GetString()}.",
            worded.GetProperty("summary").GetString());

        var sent = Assert.Single(gemini.Requests);
        Assert.Equal($"https://generativelanguage.googleapis.com/v1beta/models/{Chain[0]}:generateContent", sent.Url);
        Assert.Equal(Key, sent.ApiKey);
        Assert.DoesNotContain(Key, sent.Url, StringComparison.Ordinal);
        Assert.DoesNotContain(Key, sent.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(ClientName, sent.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("prophetops.local", sent.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Agency owner", sent.Body, StringComparison.Ordinal);

        var request = JsonNode.Parse(sent.Body)!;
        var generation = request["generationConfig"]!;
        Assert.Equal("application/json", generation["responseMimeType"]!.GetValue<string>());
        Assert.Equal("low", generation["thinkingConfig"]!["thinkingLevel"]!.GetValue<string>());
        Assert.Null(generation["temperature"]);
        Assert.Contains("Use only the facts you are given", request["systemInstruction"]!["parts"]![0]!["text"]!.GetValue<string>());
    }

    [Fact]
    public async Task The_same_figures_are_worded_once_and_then_answered_from_memory()
    {
        var gemini = new StandInGemini { Answer = StandInGemini.WordingFromFacts };
        using var factory = new ExplanationFactory(Months(30), gemini, Key);
        using var client = await AuthenticatedClient.Login(factory);

        var first = await Body(await client.PostAsync("/api/forecast/explanation", null));
        var second = await Body(await client.PostAsync("/api/forecast/explanation", null));

        Assert.Equal("gemini", first.GetProperty("source").GetString());
        Assert.Equal(first.GetProperty("summary").GetString(), second.GetProperty("summary").GetString());
        Assert.Single(gemini.Requests);
    }

    [Theory]
    [InlineData(429)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task A_failing_strongest_model_hands_over_to_the_next_and_then_rests(int status)
    {
        var gemini = new StandInGemini
        {
            Answer = sent => sent.Model == Chain[0]
                ? StandInGemini.Error(status, status == 429 ? "RESOURCE_EXHAUSTED" : "UNAVAILABLE", "Not now.", StandInGemini.RetryInfo("30s"))
                : StandInGemini.WordingFromFacts(sent),
        };
        using var factory = new ExplanationFactory(Months(30), gemini, Key);
        using var client = await AuthenticatedClient.Login(factory);

        var revenue = await Body(await client.PostAsync("/api/forecast/explanation?measure=revenue", null));

        Assert.Equal("gemini", revenue.GetProperty("source").GetString());
        Assert.Equal(Chain[1], revenue.GetProperty("model").GetString());
        Assert.Equal(new[] { Chain[0] }, revenue.GetProperty("passedOver").EnumerateArray().Select(m => m.GetString()));
        Assert.Equal(new[] { Chain[0], Chain[1] }, gemini.Requests.Select(r => r.Model));

        // The next request goes straight to the model that answered.
        var passengers = await Body(await client.PostAsync("/api/forecast/explanation?measure=passengers", null));

        Assert.Equal(Chain[1], passengers.GetProperty("model").GetString());
        Assert.Equal(new[] { Chain[0] }, passengers.GetProperty("passedOver").EnumerateArray().Select(m => m.GetString()));
        Assert.Equal(new[] { Chain[0], Chain[1], Chain[1] }, gemini.Requests.Select(r => r.Model));
    }

    [Fact]
    public async Task Each_kind_of_failure_moves_down_the_chain_until_a_model_answers()
    {
        var gemini = new StandInGemini
        {
            Answer = sent => sent.Model switch
            {
                "gemini-4-argon" => StandInGemini.Error(503, "UNAVAILABLE", "The model is overloaded."),
                "gemini-3.8-flash" => StandInGemini.WordingFromFacts(sent),
                "gemini-3.6-flash" => StandInGemini.Raw("Here is your explanation: revenue looks fine."),
                "gemini-3.5-flash-lite" => StandInGemini.Written("Booking revenue should grow 37% next year.", []),
                _ => StandInGemini.WordingFromFacts(sent),
            },
            DelayFor = sent => sent.Model == "gemini-3.8-flash" ? TimeSpan.FromSeconds(10) : TimeSpan.Zero,
        };
        using var factory = new ExplanationFactory(Months(30), gemini, Key, ("Gemini:TimeoutSeconds", "2"));
        using var client = await AuthenticatedClient.Login(factory);

        var worded = await Body(await client.PostAsync("/api/forecast/explanation", null));

        Assert.Equal("gemini", worded.GetProperty("source").GetString());
        Assert.Equal("gemini-3.1-flash-lite", worded.GetProperty("model").GetString());
        Assert.Equal(Chain.Take(4), worded.GetProperty("passedOver").EnumerateArray().Select(m => m.GetString()));
        Assert.Equal(Chain, gemini.Requests.Select(r => r.Model));
    }

    [Fact]
    public async Task When_every_model_fails_the_rules_wording_is_used_with_the_reason()
    {
        var gemini = new StandInGemini { Answer = _ => StandInGemini.Error(500, "INTERNAL", "Something went wrong.") };
        using var factory = new ExplanationFactory(Months(30), gemini, Key);
        using var client = await AuthenticatedClient.Login(factory);

        var worded = await Body(await client.PostAsync("/api/forecast/explanation", null));

        Assert.Equal("rules", worded.GetProperty("source").GetString());
        Assert.Equal("unavailable", worded.GetProperty("fallbackReason").GetString());
        Assert.StartsWith("Booking revenue is forecast to", worded.GetProperty("summary").GetString());
        Assert.Equal(Chain, gemini.Requests.Select(r => r.Model));
    }

    [Fact]
    public async Task When_every_model_is_resting_nothing_more_is_sent()
    {
        var gemini = new StandInGemini { Answer = _ => StandInGemini.Error(429, "RESOURCE_EXHAUSTED", "Quota exceeded.") };
        using var factory = new ExplanationFactory(Months(30), gemini, Key, ("Gemini:Models", "gemini-4-argon, gemini-3.8-flash"));
        using var client = await AuthenticatedClient.Login(factory);

        var first = await Body(await client.PostAsync("/api/forecast/explanation?measure=revenue", null));
        var second = await Body(await client.PostAsync("/api/forecast/explanation?measure=passengers", null));

        Assert.Equal("busy", first.GetProperty("fallbackReason").GetString());
        Assert.Equal("busy", second.GetProperty("fallbackReason").GetString());
        Assert.Equal(new[] { "gemini-4-argon", "gemini-3.8-flash" }, gemini.Requests.Select(r => r.Model));
    }

    [Fact]
    public async Task A_rejected_key_stops_at_once_and_is_not_tried_again_for_a_while()
    {
        var gemini = new StandInGemini
        {
            Answer = _ => StandInGemini.Error(400, "INVALID_ARGUMENT", "API key not valid. Please pass a valid API key.", StandInGemini.Reason("API_KEY_INVALID")),
        };
        using var factory = new ExplanationFactory(Months(30), gemini, Key);
        using var client = await AuthenticatedClient.Login(factory);

        var first = await Body(await client.PostAsync("/api/forecast/explanation?measure=revenue", null));
        var second = await Body(await client.PostAsync("/api/forecast/explanation?measure=passengers", null));

        Assert.Equal("key-rejected", first.GetProperty("fallbackReason").GetString());
        Assert.Equal("key-rejected", second.GetProperty("fallbackReason").GetString());
        Assert.Single(gemini.Requests);
    }

    [Fact]
    public async Task Wording_with_a_figure_the_forecast_does_not_hold_is_set_aside_by_every_model()
    {
        var gemini = new StandInGemini
        {
            Answer = _ => StandInGemini.Written("Booking revenue should grow 37% next year.", ["Each forecast month has an 80% range."]),
        };
        using var factory = new ExplanationFactory(Months(30), gemini, Key);
        using var client = await AuthenticatedClient.Login(factory);

        var worded = await Body(await client.PostAsync("/api/forecast/explanation", null));

        Assert.Equal("rules", worded.GetProperty("source").GetString());
        Assert.Equal("unknown-figure", worded.GetProperty("fallbackReason").GetString());
        Assert.StartsWith("Booking revenue is forecast to", worded.GetProperty("summary").GetString());
        Assert.Equal(Chain, gemini.Requests.Select(r => r.Model));
    }

    public static TheoryData<string> BadAnswers => new()
    {
        "not-json",
        "no-summary",
        "too-long",
        "cut-short",
        "too-many-points",
    };

    [Theory]
    [MemberData(nameof(BadAnswers))]
    public async Task An_answer_out_of_shape_is_never_shown(string answer)
    {
        var gemini = new StandInGemini
        {
            Answer = _ => answer switch
            {
                "not-json" => StandInGemini.Raw("Here is your explanation: revenue looks fine."),
                "no-summary" => StandInGemini.Raw("{\"points\": [\"Each forecast month has an 80% range.\"]}"),
                "too-long" => StandInGemini.Written(new string('a', ForecastExplainer.SummaryLimit + 1), []),
                "cut-short" => StandInGemini.Raw("{\"summary\": \"Booking revenue", finishReason: "MAX_TOKENS"),
                _ => StandInGemini.Written("Booking revenue follows its season.", ["One.", "Two.", "Three.", "Four.", "Five."]),
            },
        };
        using var factory = new ExplanationFactory(Months(30), gemini, Key, ("Gemini:Models", "gemini-3.8-flash"));
        using var client = await AuthenticatedClient.Login(factory);

        var worded = await Body(await client.PostAsync("/api/forecast/explanation", null));

        Assert.Equal("rules", worded.GetProperty("source").GetString());
        Assert.Equal("unavailable", worded.GetProperty("fallbackReason").GetString());
    }

    [Fact]
    public async Task A_slow_model_is_given_up_on()
    {
        var gemini = new StandInGemini { Answer = StandInGemini.WordingFromFacts, DelayFor = _ => TimeSpan.FromSeconds(10) };
        using var factory = new ExplanationFactory(Months(30), gemini, Key, ("Gemini:Models", "gemini-3.8-flash"), ("Gemini:TimeoutSeconds", "2"));
        using var client = await AuthenticatedClient.Login(factory);

        var started = DateTime.UtcNow;
        var worded = await Body(await client.PostAsync("/api/forecast/explanation", null));

        Assert.Equal("unavailable", worded.GetProperty("fallbackReason").GetString());
        Assert.InRange(DateTime.UtcNow - started, TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(8));
    }

    [Fact]
    public async Task The_whole_chain_keeps_to_its_time_limit()
    {
        var gemini = new StandInGemini { Answer = StandInGemini.WordingFromFacts, DelayFor = _ => TimeSpan.FromSeconds(10) };
        using var factory = new ExplanationFactory(Months(30), gemini, Key,
            ("Gemini:Models", "gemini-4-argon,gemini-3.8-flash,gemini-3.6-flash"), ("Gemini:TimeoutSeconds", "2"), ("Gemini:TotalTimeoutSeconds", "4"));
        using var client = await AuthenticatedClient.Login(factory);

        var started = DateTime.UtcNow;
        var worded = await Body(await client.PostAsync("/api/forecast/explanation", null));

        Assert.Equal("unavailable", worded.GetProperty("fallbackReason").GetString());
        Assert.Equal(new[] { "gemini-4-argon", "gemini-3.8-flash" }, gemini.Requests.Select(r => r.Model));
        Assert.InRange(DateTime.UtcNow - started, TimeSpan.FromSeconds(3.5), TimeSpan.FromSeconds(9));
    }

    [Fact]
    public async Task The_hourly_limit_counts_every_call_the_fallbacks_included()
    {
        var gemini = new StandInGemini
        {
            Answer = sent => sent.Model == Chain[0] ? StandInGemini.Error(429, "RESOURCE_EXHAUSTED", "Quota exceeded.") : StandInGemini.WordingFromFacts(sent),
        };
        using var factory = new ExplanationFactory(Months(30), gemini, Key, ("Gemini:MaxRequestsPerHour", "1"));
        using var client = await AuthenticatedClient.Login(factory);

        var worded = await Body(await client.PostAsync("/api/forecast/explanation", null));

        Assert.Equal("rules", worded.GetProperty("source").GetString());
        Assert.Equal("busy", worded.GetProperty("fallbackReason").GetString());
        Assert.Equal(new[] { Chain[0] }, gemini.Requests.Select(r => r.Model));
    }

    [Fact]
    public async Task A_gemini_2_5_model_is_sent_no_thinking_level()
    {
        var gemini = new StandInGemini { Answer = StandInGemini.WordingFromFacts };
        using var factory = new ExplanationFactory(Months(30), gemini, Key, ("Gemini:Models", "gemini-2.5-flash"));
        using var client = await AuthenticatedClient.Login(factory);

        var worded = await Body(await client.PostAsync("/api/forecast/explanation", null));

        Assert.Equal("gemini-2.5-flash", worded.GetProperty("model").GetString());
        Assert.Null(JsonNode.Parse(Assert.Single(gemini.Requests).Body)!["generationConfig"]!["thinkingConfig"]);
    }

    [Fact]
    public async Task Too_little_history_is_said_plainly_and_nothing_is_sent()
    {
        var gemini = new StandInGemini { Answer = StandInGemini.WordingFromFacts };
        using var factory = new ExplanationFactory(Months(20), gemini, Key);
        using var client = await AuthenticatedClient.Login(factory);

        var worded = await Body(await client.PostAsync("/api/forecast/explanation", null));

        Assert.False(worded.GetProperty("ok").GetBoolean());
        Assert.Contains("two full years", worded.GetProperty("reason").GetString());
        Assert.Empty(worded.GetProperty("points").EnumerateArray());
        Assert.Empty(gemini.Requests);
    }

    [Fact]
    public async Task Only_owner_and_admin_may_ask_and_asking_gemini_needs_the_antiforgery_token()
    {
        var gemini = new StandInGemini { Answer = StandInGemini.WordingFromFacts };
        using var factory = new ExplanationFactory(Months(30), gemini, Key);
        using var anonymous = factory.CreateClient();
        using var staff = await AuthenticatedClient.Login(factory, StaffEmail, StaffPassword);
        using var owner = await AuthenticatedClient.Login(factory);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/forecast/explanation")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/forecast/explanation")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsync("/api/forecast/explanation", null)).StatusCode);

        owner.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync("/api/forecast/explanation", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync("/api/forecast/explanation?measure=profit")).StatusCode);
        Assert.Empty(gemini.Requests);
    }

    [Fact]
    public void The_settings_default_to_the_strongest_first_chain_and_refuse_what_is_not_a_model()
    {
        static GeminiOptions Read(params (string Key, string Value)[] values) => GeminiOptions.FromConfiguration(
            new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value)).Build());

        var off = Read();
        Assert.False(off.Enabled);
        Assert.Equal(GeminiOptions.DefaultModels, off.Models);
        Assert.Equal("gemini-4-argon", off.Models[0]);
        Assert.Equal("low", off.ThinkingLevel);
        Assert.Equal(TimeSpan.FromSeconds(20), off.Timeout);
        Assert.Equal(TimeSpan.FromSeconds(45), off.TotalTimeout);
        Assert.Equal(30, off.MaxRequestsPerHour);

        var on = Read(("Gemini:ApiKey", "  abc  "), ("Gemini:Models", "gemini-3.8-flash, gemini-3.5-flash-lite"),
            ("Gemini:TimeoutSeconds", "999"), ("Gemini:TotalTimeoutSeconds", "1"), ("Gemini:ThinkingLevel", "None"));
        Assert.True(on.Enabled);
        Assert.Equal("abc", on.ApiKey);
        Assert.Equal(new[] { "gemini-3.8-flash", "gemini-3.5-flash-lite" }, on.Models);
        Assert.Equal(TimeSpan.FromSeconds(60), on.Timeout);
        Assert.Equal(TimeSpan.FromSeconds(60), on.TotalTimeout);
        Assert.Null(on.ThinkingLevel);
        Assert.Equal(
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash-lite:generateContent",
            GeminiOptions.GenerateContentUrl("gemini-3.5-flash-lite").ToString());

        Assert.Equal(new[] { "gemini-3.6-flash" }, Read(("Gemini:Model", "gemini-3.6-flash")).Models);
        Assert.Equal(new[] { "gemini-4-argon", "gemini-3.8-flash" }, Read(("Gemini:Models:0", "gemini-4-argon"), ("Gemini:Models:1", "gemini-3.8-flash")).Models);
        Assert.Null(Read().ThinkingLevelFor("gemini-2.5-flash"));
        Assert.Equal("low", Read().ThinkingLevelFor("gemini-3.8-flash"));

        // Google's own variable is used when no Gemini:ApiKey is given, but not over an empty one.
        Assert.Equal("from-google-variable", Read(("GEMINI_API_KEY", "from-google-variable")).ApiKey);
        Assert.False(Read(("Gemini:ApiKey", ""), ("GEMINI_API_KEY", "from-google-variable")).Enabled);

        Assert.Throws<InvalidOperationException>(() => Read(("Gemini:Models", "../models/other")));
        Assert.Throws<InvalidOperationException>(() => Read(("Gemini:Models", "gemini-3.8-flash,gemini?key=x")));
        Assert.Throws<InvalidOperationException>(() => Read(("Gemini:Models", string.Join(",", Enumerable.Range(1, 9).Select(i => $"gemini-m{i}")))));
        Assert.Throws<InvalidOperationException>(() => Read(("Gemini:ThinkingLevel", "extreme")));
    }

    [Fact]
    public void Figures_pass_when_they_are_the_forecasts_own_rounded_or_not_and_fail_otherwise()
    {
        var facts = SampleFacts();

        Assert.True(Check("Revenue peaks in March 2027 at about ₱1,309,876."));
        Assert.True(Check("Revenue peaks in March 2027 at about ₱1.3 million, within ₱1.1M to ₱1.5M."));
        Assert.True(Check("That is about ₱1,310,000, up 8.4% (roughly 8%) on recent months."));
        Assert.True(Check("It missed by 7.4% against 11.6%, inside the 80% range in 52 of 63 forecasts."));
        Assert.True(Check("Each of the 6 months has a 12-month pattern behind it; Q4 is busiest."));

        Assert.False(Check("Revenue should grow 37% next year."));
        Assert.False(Check("Expect about ₱2.5 million in March 2027."));
        Assert.False(Check("By 2030 the agency could double."));
        Assert.False(Check("About 140 more passengers are likely."));

        bool Check(string text) => ExplanationFigures.AllKnown(text, [], facts);
    }

    [Fact]
    public void The_rules_wording_reads_the_facts()
    {
        var (summary, points) = ForecastWording.Write(SampleFacts());

        Assert.Equal(
            "Booking revenue is forecast to rise about 8.4% from October 2026 to March 2027, against the last six recorded months, " +
            "with the highest month March 2027 at about ₱1,309,876.",
            summary);
        Assert.Equal(
            "Each month comes with an 80% range: for March 2027, ₱1,102,000 to ₱1,517,000. In about 8 months out of 10 the recorded figure should land inside it.",
            points[0]);
        Assert.Equal("Across the six months that is about ₱6,900,000 in booking revenue, before any costs.", points[1]);
        Assert.Equal(
            "Tested on 63 forecasts of months it had not seen (September 2025 to September 2026), it missed by 7.4% on average, " +
            "less than the 11.6% of repeating the same month last year, and the recorded figure fell inside the range in 52 of them.",
            points[2]);
        Assert.Equal("It learns from the bookings recorded up to September 2026.", points[3]);
        Assert.True(ExplanationFigures.AllKnown(summary, points, SampleFacts()), "ProphetOps' own wording passes its own check");
    }

    private static ForecastFacts SampleFacts() => new(
        DemandMeasure.Revenue,
        false,
        "September 2026",
        1_150_000,
        [
            new FactMonth("October 2026", 1_080_000, 960_000, 1_200_000),
            new FactMonth("November 2026", 1_120_000, 990_000, 1_250_000),
            new FactMonth("December 2026", 1_230_000, 1_080_000, 1_380_000),
            new FactMonth("January 2027", 1_000_124, 850_000, 1_150_000),
            new FactMonth("February 2027", 1_160_000, 980_000, 1_340_000),
            new FactMonth("March 2027", 1_309_876, 1_102_000, 1_517_000),
        ],
        6_900_000,
        "up",
        8.4,
        "March 2027",
        1_309_876,
        [],
        new FactTest("September 2025", "September 2026", 13, 63, 7.4, 11.6, 52, 83, 36));

    private static async Task<JsonElement> Body(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    /// Answers in Gemini's place and keeps what each request carried.
    private sealed class StandInGemini : HttpMessageHandler
    {
        public sealed record Sent(string Url, string? ApiKey, string Body)
        {
            /// The model named in the address, such as gemini-3.8-flash.
            public string Model => Url[(Url.LastIndexOf('/') + 1)..Url.LastIndexOf(':')];
        }

        public List<Sent> Requests { get; } = [];
        public Func<Sent, HttpResponseMessage> Answer { get; init; } = _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        public Func<Sent, TimeSpan> DelayFor { get; init; } = _ => TimeSpan.Zero;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            var sent = new Sent(
                request.RequestUri!.ToString(),
                request.Headers.TryGetValues("x-goog-api-key", out var keys) ? keys.Single() : null,
                body);
            lock (Requests) Requests.Add(sent);
            var delay = DelayFor(sent);
            if (delay > TimeSpan.Zero) await Task.Delay(delay, cancellationToken);
            return Answer(sent);
        }

        /// A faithful writer: it repeats the highest month and its figure exactly as given.
        public static HttpResponseMessage WordingFromFacts(Sent sent)
        {
            var prompt = JsonNode.Parse(sent.Body)!["contents"]![0]!["parts"]![0]!["text"]!.GetValue<string>();
            var facts = JsonNode.Parse(prompt[(prompt.IndexOf('\n') + 1)..])!;
            var peak = facts["highestForecastMonth"]!;
            return Written(
                $"The forecast is highest in {peak["month"]!.GetValue<string>()}, at about {peak["forecast"]!.GetValue<string>()}.",
                ["Each forecast month has an 80% range: the recorded figure should land inside it about 8 times in 10."]);
        }

        public static HttpResponseMessage Written(string summary, string[] points) =>
            Raw(new JsonObject
            {
                ["summary"] = summary,
                ["points"] = new JsonArray(points.Select(p => (JsonNode)JsonValue.Create(p)).ToArray()),
            }.ToJsonString());

        public static HttpResponseMessage Raw(string text, string finishReason = "STOP")
        {
            var answer = new JsonObject
            {
                ["candidates"] = new JsonArray(new JsonObject
                {
                    ["content"] = new JsonObject
                    {
                        ["role"] = "model",
                        ["parts"] = new JsonArray(new JsonObject { ["text"] = text }),
                    },
                    ["finishReason"] = finishReason,
                }),
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(answer.ToJsonString(), Encoding.UTF8, "application/json"),
            };
        }

        /// An error in Google's shape: {"error": {"code", "message", "status", "details"}}.
        public static HttpResponseMessage Error(int status, string statusText, string message, params JsonObject[] details) =>
            new((HttpStatusCode)status)
            {
                Content = new StringContent(new JsonObject
                {
                    ["error"] = new JsonObject
                    {
                        ["code"] = status,
                        ["message"] = message,
                        ["status"] = statusText,
                        ["details"] = new JsonArray(details.Select(d => (JsonNode)d).ToArray()),
                    },
                }.ToJsonString(), Encoding.UTF8, "application/json"),
            };

        public static JsonObject RetryInfo(string delay) =>
            new() { ["@type"] = "type.googleapis.com/google.rpc.RetryInfo", ["retryDelay"] = delay };

        public static JsonObject Reason(string reason) =>
            new() { ["@type"] = "type.googleapis.com/google.rpc.ErrorInfo", ["reason"] = reason, ["domain"] = "googleapis.com" };
    }

    private sealed class ExplanationFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), "prophetops-forecast-explanation-" + Guid.NewGuid().ToString("N"));
        private readonly StandInGemini _gemini;
        private readonly Dictionary<string, string?> _settings;

        public ExplanationFactory(Action<AppDbContext> prepare, StandInGemini gemini, string? key = null, params (string Key, string Value)[] settings)
        {
            _gemini = gemini;
            _settings = new Dictionary<string, string?>
            {
                ["Demo:Enabled"] = "false",
                ["Storage:Root"] = _storageRoot,
                ["Business:TimeZone"] = "Asia/Manila",
                ["Gemini:ApiKey"] = key ?? "",
            };
            foreach (var (name, value) in settings) _settings[name] = value;

            Directory.CreateDirectory(_storageRoot);
            _connection.Open();
            using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
            db.Database.Migrate();
            db.Users.Add(new User
            {
                Name = "Agency owner",
                Email = "owner@prophetops.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("owner123"),
                Role = Roles.OwnerManagement,
                Status = "Active",
            });
            db.Users.Add(new User
            {
                Name = "Agency staff",
                Email = StaffEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(StaffPassword),
                Role = Roles.Staff,
                Status = "Active",
            });
            prepare(db);
            db.SaveChanges();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(_settings));
            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services.Where(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType == typeof(IBusinessClock)).ToList())
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
                services.AddSingleton<IBusinessClock>(new FixedClock(new DateOnly(2026, 8, 15)));
                services.AddHttpClient(HttpGeminiTransport.ClientName).ConfigurePrimaryHttpMessageHandler(() => _gemini);
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _connection.Dispose();
                if (Directory.Exists(_storageRoot)) Directory.Delete(_storageRoot, recursive: true);
            }
        }
    }

    private sealed class FixedClock(DateOnly today) : IBusinessClock
    {
        public DateTimeOffset UtcNow => new(today.Year, today.Month, today.Day, 4, 0, 0, TimeSpan.Zero);
        public DateOnly Today => today;
        public string TimeZoneId => "Asia/Manila";
    }
}
