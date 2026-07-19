using ProphetOps.Forecasting;
using Xunit;

namespace ProphetOps.Forecasting.Tests;

public class TrajectoryInsightsTests
{
    private static TrajectoryInput Input(
        IReadOnlyList<TrajectoryStep>? steps = null,
        string direction = "up",
        double changePercent = 5,
        double lastRecordedValue = 1_000_000,
        double mape = 4.8,
        double mae = 40_000,
        double seasonalNaiveMae = 90_000) => new()
    {
        Steps = steps ?? Rising(),
        Direction = direction,
        ChangePercent = changePercent,
        LastRecordedLabel = "July 2026",
        LastRecordedValue = lastRecordedValue,
        Mape = mape,
        Accuracy = (int)Math.Round(100 - mape),
        Mae = mae,
        SeasonalNaiveMae = seasonalNaiveMae,
    };

    private static List<TrajectoryStep> Rising() =>
    [
        new("August 2026", 1_000_000, 950_000, 1_050_000),
        new("September 2026", 1_100_000, 1_030_000, 1_170_000),
        new("October 2026", 1_250_000, 1_160_000, 1_340_000),
        new("November 2026", 1_400_000, 1_280_000, 1_520_000),
        new("December 2026", 1_800_000, 1_640_000, 1_960_000),
        new("January 2027", 1_200_000, 1_000_000, 1_400_000),
    ];

    private static string Text(TrajectoryInput input, string kind) =>
        TrajectoryInsights.Build(input).SingleOrDefault(n => n.Kind == kind)?.Text ?? "";

    private static bool Has(TrajectoryInput input, string kind) =>
        TrajectoryInsights.Build(input).Any(n => n.Kind == kind);

    [Fact]
    public void Leads_with_the_direction_and_names_the_peak()
    {
        var notes = TrajectoryInsights.Build(Input());

        Assert.Equal("direction", notes[0].Kind);
        Assert.Contains("trending upward", notes[0].Text);
        Assert.Contains("+5%", notes[0].Text);
        Assert.Contains("December 2026", notes[0].Text);
        Assert.Contains("₱1,800,000", notes[0].Text);
    }

    [Fact]
    public void Says_holding_steady_without_a_signed_figure_when_flat()
    {
        var text = Text(Input(direction: "flat", changePercent: 0.2), "direction");

        Assert.Contains("holding steady", text);
        Assert.DoesNotContain("+0.2%", text);
    }

    [Fact]
    public void Reports_a_decline_without_a_doubled_minus_sign()
    {
        var text = Text(Input(direction: "down", changePercent: -7.4), "direction");

        Assert.Contains("trending downward", text);
        Assert.Contains("-7.4%", text);
        Assert.DoesNotContain("--", text);
    }

    [Fact]
    public void Names_the_quietest_month_and_the_gap_to_the_peak()
    {
        var text = Text(Input(), "trough");

        Assert.Contains("August 2026", text);
        Assert.Contains("₱800,000", text);
    }

    [Fact]
    public void Stays_quiet_about_a_trough_when_every_month_is_identical()
    {
        var flat = Enumerable.Range(0, 6)
            .Select(i => new TrajectoryStep($"M{i}", 1_000_000, 950_000, 1_050_000))
            .ToList();

        Assert.False(Has(Input(flat, direction: "flat", changePercent: 0), "trough"));
    }

    [Fact]
    public void Flags_the_peak_as_the_month_to_prepare_for()
    {
        var text = Text(Input(), "capacity");

        Assert.Contains("December 2026", text);
        Assert.Contains("above", text);
    }

    [Fact]
    public void Compares_the_first_forecast_month_against_the_last_recorded_one()
    {
        var text = Text(Input(lastRecordedValue: 800_000), "next");

        Assert.Contains("August 2026", text);
        Assert.Contains("above", text);
        Assert.Contains("July 2026", text);
    }

    [Fact]
    public void Calls_the_next_month_close_rather_than_quoting_a_meaningless_change()
    {
        var text = Text(Input(lastRecordedValue: 1_000_500), "next");

        Assert.Contains("close to", text);
        Assert.DoesNotContain("above", text);
    }

    [Fact]
    public void Counts_a_run_of_rises_and_names_where_it_breaks()
    {
        var text = Text(Input(), "momentum");

        Assert.Contains("4 months", text);
        Assert.Contains("December 2026", text);
        Assert.Contains("January 2027", text);
    }

    [Fact]
    public void Stays_quiet_about_momentum_when_the_series_alternates()
    {
        var choppy = new List<TrajectoryStep>
        {
            new("August 2026", 1_000_000, 900_000, 1_100_000),
            new("September 2026", 900_000, 800_000, 1_000_000),
            new("October 2026", 1_050_000, 950_000, 1_150_000),
            new("November 2026", 950_000, 850_000, 1_050_000),
        };

        Assert.False(Has(Input(choppy), "momentum"));
    }

    [Fact]
    public void Warns_when_two_neighbouring_months_cannot_be_told_apart()
    {
        var close = new List<TrajectoryStep>
        {
            new("August 2026", 1_000_000, 900_000, 1_100_000),
            new("September 2026", 1_010_000, 910_000, 1_110_000),
            new("October 2026", 1_500_000, 1_400_000, 1_600_000),
        };

        var text = Text(Input(close), "overlap");

        Assert.Contains("August 2026", text);
        Assert.Contains("September 2026", text);
    }

    [Fact]
    public void Stays_quiet_about_overlap_when_the_months_are_clearly_separated()
    {
        var separated = new List<TrajectoryStep>
        {
            new("August 2026", 1_000_000, 990_000, 1_010_000),
            new("September 2026", 1_500_000, 1_490_000, 1_510_000),
            new("October 2026", 2_000_000, 1_990_000, 2_010_000),
        };

        Assert.False(Has(Input(separated), "overlap"));
    }

    [Fact]
    public void Explains_that_the_range_widens_further_out()
    {
        var text = Text(Input(), "spread");

        Assert.Contains("January 2027", text);
        Assert.Contains("times", text);
    }

    [Fact]
    public void Stays_quiet_about_spread_when_the_range_barely_grows()
    {
        var even = new List<TrajectoryStep>
        {
            new("August 2026", 1_000_000, 950_000, 1_050_000),
            new("September 2026", 1_100_000, 1_048_000, 1_152_000),
            new("October 2026", 1_200_000, 1_147_000, 1_253_000),
        };

        Assert.False(Has(Input(even), "spread"));
    }

    [Fact]
    public void Reports_beating_the_seasonal_baseline()
    {
        var text = Text(Input(mae: 40_000, seasonalNaiveMae: 90_000), "benchmark");

        Assert.Contains("₱40,000", text);
        Assert.Contains("same month last year", text);
    }

    [Fact]
    public void Claims_no_benchmark_win_when_the_baseline_is_as_good()
    {
        Assert.False(Has(Input(mae: 90_000, seasonalNaiveMae: 90_000), "benchmark"));
        Assert.False(Has(Input(mae: 95_000, seasonalNaiveMae: 90_000), "benchmark"));
    }

    [Fact]
    public void Softens_the_reading_when_the_error_is_wide()
    {
        Assert.Contains("as a range rather than a target", Text(Input(mape: 18), "reliability"));
        Assert.Contains("stayed close", Text(Input(mape: 3.2), "reliability"));
    }

    [Fact]
    public void Returns_nothing_at_all_without_steps()
    {
        Assert.Empty(TrajectoryInsights.Build(Input(new List<TrajectoryStep>())));
    }

    [Fact]
    public void Every_note_reads_as_a_finished_sentence()
    {
        foreach (var note in TrajectoryInsights.Build(Input()))
        {
            Assert.False(string.IsNullOrWhiteSpace(note.Kind));
            Assert.EndsWith(".", note.Text);
            Assert.Matches("^[A-Z₱]", note.Text);
            Assert.DoesNotContain("  ", note.Text);
        }
    }
}
