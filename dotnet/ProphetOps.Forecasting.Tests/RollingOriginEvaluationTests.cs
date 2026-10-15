using ProphetOps.Forecasting;
using Xunit;

namespace ProphetOps.Forecasting.Tests;

public class RollingOriginEvaluationTests
{
    /// A rising monthly series with a yearly season and a little irregular movement, like booking
    /// revenue that grows while peaking at the same time each year.
    private static double[] TrendingSeasonal(int months)
    {
        var values = new double[months];
        for (int t = 0; t < months; t++)
        {
            double season = 20_000 * Math.Sin(2 * Math.PI * t / 12);
            double irregular = 3_000 * (((t * 37) % 11) - 5) / 5.0;
            values[t] = 100_000 + 1_500 * t + season + irregular;
        }
        return values;
    }

    [Fact]
    public void The_test_needs_two_seasons_to_learn_from_and_one_later_month_to_check()
    {
        BacktestResult tooShort = RollingOriginEvaluation.Evaluate(TrendingSeasonal(24));
        Assert.False(tooShort.Ok);
        Assert.Equal(24, tooShort.MonthsAvailable);
        Assert.Equal(25, tooShort.MonthsRequired);
        Assert.Empty(tooShort.Points);

        BacktestResult justEnough = RollingOriginEvaluation.Evaluate(TrendingSeasonal(25));
        Assert.True(justEnough.Ok);
        Assert.Equal(1, justEnough.Origins);
        BacktestPoint only = Assert.Single(justEnough.Points);
        Assert.Equal(24, only.Origin);
        Assert.Equal(24, only.Target);
        Assert.Equal(1, only.Step);
    }

    [Fact]
    public void Every_forecast_with_a_recorded_month_to_compare_is_counted()
    {
        BacktestResult result = RollingOriginEvaluation.Evaluate(TrendingSeasonal(30));

        Assert.True(result.Ok);
        Assert.Equal(24, result.FirstOrigin);
        Assert.Equal(6, result.Origins);
        // Standing at months 24 to 29, one month ahead can be checked six times, six months ahead once.
        Assert.Equal(new[] { 6, 5, 4, 3, 2, 1 }, result.ByHorizon.Select(h => h.Forecasts).ToArray());
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, result.ByHorizon.Select(h => h.Step).ToArray());
        Assert.Equal(21, result.Points.Count);
        Assert.Equal(21, result.Overall!.Forecasts);
        Assert.All(result.Points, p => Assert.Equal(p.Origin + p.Step - 1, p.Target));
    }

    [Fact]
    public void Each_forecast_uses_only_the_months_before_it()
    {
        double[] original = TrendingSeasonal(36);
        double[] changedLater = (double[])original.Clone();
        for (int t = 29; t < changedLater.Length; t++) changedLater[t] *= 3;

        var before = RollingOriginEvaluation.Evaluate(original).Points.Where(p => p.Origin <= 29).ToList();
        var after = RollingOriginEvaluation.Evaluate(changedLater).Points.Where(p => p.Origin <= 29).ToList();

        Assert.NotEmpty(before);
        Assert.Equal(before.Count, after.Count);
        for (int i = 0; i < before.Count; i++)
        {
            Assert.Equal(before[i].Forecast, after[i].Forecast);
            Assert.Equal(before[i].Lower, after[i].Lower);
            Assert.Equal(before[i].Upper, after[i].Upper);
        }
    }

    [Fact]
    public void Each_forecast_is_the_application_forecast_from_the_earlier_months()
    {
        double[] values = TrendingSeasonal(32);
        BacktestResult result = RollingOriginEvaluation.Evaluate(values);

        ForecastResult fromMonth27 = HoltWintersForecaster.Forecast(values.Take(27).ToArray(), 12, 6);
        var points = result.Points.Where(p => p.Origin == 27).OrderBy(p => p.Step).ToList();

        Assert.Equal(5, points.Count);
        for (int i = 0; i < points.Count; i++)
        {
            Assert.Equal(fromMonth27.Forecast![i].Value, points[i].Forecast);
            Assert.Equal(fromMonth27.Forecast![i].Lower, points[i].Lower);
            Assert.Equal(fromMonth27.Forecast![i].Upper, points[i].Upper);
        }
    }

    [Fact]
    public void The_baseline_repeats_the_same_month_a_year_earlier()
    {
        double[] values = TrendingSeasonal(36);
        BacktestResult result = RollingOriginEvaluation.Evaluate(values);

        Assert.All(result.Points, p => Assert.Equal(values[p.Target - 12], p.SeasonalNaive));
    }

    [Fact]
    public void Holt_Winters_misses_less_than_the_baseline_on_a_rising_seasonal_series()
    {
        BacktestResult result = RollingOriginEvaluation.Evaluate(TrendingSeasonal(40));
        OverallAccuracy overall = result.Overall!;

        // Repeating last year's month misses the year's growth; Holt-Winters follows the trend.
        Assert.True(overall.Mae < overall.SeasonalNaiveMae, $"{overall.Mae} vs {overall.SeasonalNaiveMae}");
        Assert.True(overall.Mape < overall.SeasonalNaiveMape);
        Assert.True(overall.SkillVsSeasonalNaive > 0);
        // Skill is worked out before the misses are rounded to centavos, so allow for the rounding.
        Assert.InRange(overall.SkillVsSeasonalNaive!.Value - (1 - overall.Mae / overall.SeasonalNaiveMae), -0.001, 0.001);
    }

    [Fact]
    public void Range_hits_and_coverage_agree_with_the_forecasts()
    {
        BacktestResult result = RollingOriginEvaluation.Evaluate(TrendingSeasonal(36));

        int inside = result.Points.Count(p => p.Actual >= p.Lower && p.Actual <= p.Upper);
        Assert.Equal(inside, result.Overall!.InsideRange);
        Assert.Equal(Math.Round((double)inside / result.Points.Count, 4, MidpointRounding.AwayFromZero), result.Overall.Coverage);
        Assert.Equal(inside, result.ByHorizon.Sum(h => h.InsideRange));
    }

    [Fact]
    public void Months_with_nothing_recorded_stay_out_of_the_percentage_miss()
    {
        double[] values = TrendingSeasonal(24).Concat(new double[] { 0, 0, 0 }).ToArray();
        BacktestResult result = RollingOriginEvaluation.Evaluate(values);

        Assert.True(result.Ok);
        Assert.All(result.Points, p => Assert.Equal(0, p.Actual));
        Assert.Null(result.Overall!.Mape);
        Assert.Null(result.Overall.SeasonalNaiveMape);
        Assert.True(double.IsFinite(result.Overall.Mae));
    }
}
