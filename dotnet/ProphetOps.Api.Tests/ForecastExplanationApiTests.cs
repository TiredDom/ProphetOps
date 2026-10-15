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
        Assert.Equal(JsonValueKind.Null, worded.GetProperty("aiModel").ValueKind);
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
    public async Task With_a_key_Gemini_words_the_same_figures_and_is_given_nothing_about_clients()
    {
        var gemini = new StandInGemini { Answer = StandInGemini.WordingFromFacts };
        using var factory = new ExplanationFactory(Months(30), gemini, Key);
        using var client = await AuthenticatedClient.Login(factory);

        var offered = await Body(await client.GetAsync("/api/forecast/explanation"));
        Assert.True(offered.GetProperty("aiAvailable").GetBoolean());
        Assert.Equal(GeminiOptions.DefaultModel, offered.GetProperty("aiModel").GetString());

        var worded = await Body(await client.PostAsync("/api/forecast/explanation", null));

        Assert.Equal("gemini", worded.GetProperty("source").GetString());
        Assert.Equal(GeminiOptions.DefaultModel, worded.GetProperty("model").GetString());
        Assert.Equal(JsonValueKind.Null, worded.GetProperty("fallbackReason").ValueKind);
        var peak = worded.GetProperty("facts").GetProperty("highestForecastMonth");
        Assert.Equal(
            $"Booking revenue is forecast to be highest in {peak.GetProperty("month").GetString()}, at about {peak.GetProperty("forecast").GetString()}.",
            worded.GetProperty("summary").GetString());

        var sent = Assert.Single(gemini.Requests);
        Assert.Equal($"https://generativelanguage.googleapis.com/v1beta/models/{GeminiOptions.DefaultModel}:generateContent", sent.Url);
        Assert.Equal(Key, sent.ApiKey);
        Assert.DoesNotContain(Key, sent.Url, StringComparison.Ordinal);
        Assert.DoesNotContain(Key, sent.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(ClientName, sent.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("prophetops.local", sent.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Agency owner", sent.Body, StringComparison.Ordinal);

        var request = JsonNode.Parse(sent.Body)!;
        Assert.Equal("application/json", request["generationConfig"]!["responseMimeType"]!.GetValue<string>());
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

    [Fact]
    public async Task Wording_with_a_figure_the_forecast_does_not_hold_is_set_aside()
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
    }

    public static TheoryData<string> BadAnswers => new()
    {
        "server-error",
        "not-json",
        "no-summary",
        "too-long",
        "cut-short",
        "too-many-points",
    };

    [Theory]
    [MemberData(nameof(BadAnswers))]
    public async Task When_Gemini_fails_or_answers_out_of_shape_the_rules_wording_is_used(string answer)
    {
        var gemini = new StandInGemini
        {
            Answer = _ => answer switch
            {
                "server-error" => new HttpResponseMessage(HttpStatusCode.InternalServerError),
                "not-json" => StandInGemini.Raw("Here is your explanation: revenue looks fine."),
                "no-summary" => StandInGemini.Raw("{\"points\": [\"Each forecast month has an 80% range.\"]}"),
                "too-long" => StandInGemini.Written(new string('a', ForecastExplainer.SummaryLimit + 1), []),
                "cut-short" => StandInGemini.Raw("{\"summary\": \"Booking revenue", finishReason: "MAX_TOKENS"),
                _ => StandInGemini.Written("Booking revenue follows its season.", ["One.", "Two.", "Three.", "Four.", "Five."]),
            },
        };
        using var factory = new ExplanationFactory(Months(30), gemini, Key);
        using var client = await AuthenticatedClient.Login(factory);

        var worded = await Body(await client.PostAsync("/api/forecast/explanation", null));

        Assert.Equal("rules", worded.GetProperty("source").GetString());
        Assert.Equal("unavailable", worded.GetProperty("fallbackReason").GetString());
    }

    [Fact]
    public async Task A_slow_answer_is_given_up_on()
    {
        var gemini = new StandInGemini { Answer = StandInGemini.WordingFromFacts, Delay = TimeSpan.FromSeconds(10) };
        using var factory = new ExplanationFactory(Months(30), gemini, Key, ("Gemini:TimeoutSeconds", "2"));
        using var client = await AuthenticatedClient.Login(factory);

        var started = DateTime.UtcNow;
        var worded = await Body(await client.PostAsync("/api/forecast/explanation", null));

        Assert.Equal("unavailable", worded.GetProperty("fallbackReason").GetString());
        Assert.InRange(DateTime.UtcNow - started, TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(8));
    }

    [Fact]
    public async Task Calls_beyond_the_hourly_limit_wait_and_the_rules_wording_is_used()
    {
        var gemini = new StandInGemini { Answer = StandInGemini.WordingFromFacts };
        using var factory = new ExplanationFactory(Months(30), gemini, Key, ("Gemini:MaxRequestsPerHour", "1"));
        using var client = await AuthenticatedClient.Login(factory);

        var revenue = await Body(await client.PostAsync("/api/forecast/explanation?measure=revenue", null));
        var passengers = await Body(await client.PostAsync("/api/forecast/explanation?measure=passengers", null));

        Assert.Equal("gemini", revenue.GetProperty("source").GetString());
        Assert.Equal("rules", passengers.GetProperty("source").GetString());
        Assert.Equal("busy", passengers.GetProperty("fallbackReason").GetString());
        Assert.StartsWith("Passenger numbers are forecast to", passengers.GetProperty("summary").GetString());
        Assert.Single(gemini.Requests);
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
    public void The_settings_default_sensibly_and_refuse_a_model_name_that_is_not_one()
    {
        static GeminiOptions Read(params (string Key, string Value)[] values) => GeminiOptions.FromConfiguration(
            new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value)).Build());

        var off = Read();
        Assert.False(off.Enabled);
        Assert.Equal(GeminiOptions.DefaultModel, off.Model);
        Assert.Equal(TimeSpan.FromSeconds(12), off.Timeout);
        Assert.Equal(30, off.MaxRequestsPerHour);

        var on = Read(("Gemini:ApiKey", "  abc  "), ("Gemini:Model", "gemini-3.5-flash-lite"), ("Gemini:TimeoutSeconds", "999"));
        Assert.True(on.Enabled);
        Assert.Equal("abc", on.ApiKey);
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash-lite:generateContent", on.GenerateContentUrl.ToString());
        Assert.Equal(TimeSpan.FromSeconds(60), on.Timeout);

        Assert.Throws<InvalidOperationException>(() => Read(("Gemini:Model", "../models/other")));
        Assert.Throws<InvalidOperationException>(() => Read(("Gemini:Model", "gemini?key=x")));
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
        public sealed record Sent(string Url, string? ApiKey, string Body);

        public List<Sent> Requests { get; } = [];
        public Func<string, HttpResponseMessage> Answer { get; init; } = _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        public TimeSpan Delay { get; init; } = TimeSpan.Zero;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            lock (Requests)
            {
                Requests.Add(new Sent(
                    request.RequestUri!.ToString(),
                    request.Headers.TryGetValues("x-goog-api-key", out var keys) ? keys.Single() : null,
                    body));
            }
            if (Delay > TimeSpan.Zero) await Task.Delay(Delay, cancellationToken);
            return Answer(body);
        }

        /// A faithful writer: it repeats the highest month and its figure exactly as given.
        public static HttpResponseMessage WordingFromFacts(string requestBody)
        {
            var prompt = JsonNode.Parse(requestBody)!["contents"]![0]!["parts"]![0]!["text"]!.GetValue<string>();
            var facts = JsonNode.Parse(prompt[(prompt.IndexOf('\n') + 1)..])!;
            var peak = facts["highestForecastMonth"]!;
            return Written(
                $"Booking revenue is forecast to be highest in {peak["month"]!.GetValue<string>()}, at about {peak["forecast"]!.GetValue<string>()}.",
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
