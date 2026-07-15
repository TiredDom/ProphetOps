using System.Text.Json;
using ProphetOps.Forecasting;
using Xunit;

namespace ProphetOps.Forecasting.Tests;

public class HoltWintersForecasterTests
{
    private const double Eps = 1e-6;

    private static readonly JsonElement Oracle = LoadOracle();

    private static JsonElement LoadOracle()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "oracle.json");
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();
    }

    private static double[] InputOf(string scenario)
    {
        var arr = Oracle.GetProperty(scenario).GetProperty("input");
        var values = new double[arr.GetArrayLength()];
        int i = 0;
        foreach (var e in arr.EnumerateArray()) values[i++] = e.GetDouble();
        return values;
    }

    private static JsonElement ResultOf(string scenario) => Oracle.GetProperty(scenario).GetProperty("result");

    [Theory]
    [InlineData("seasonal")]
    [InlineData("declining")]
    public void Matches_the_php_oracle_field_for_field(string scenario)
    {
        double[] input = InputOf(scenario);
        JsonElement want = ResultOf(scenario);

        ForecastResult got = HoltWintersForecaster.Forecast(input, 12, 6);

        Assert.True(got.Ok);
        Assert.Equal(want.GetProperty("method").GetString(), got.Method);
        Assert.Equal(want.GetProperty("seasonLength").GetInt32(), got.SeasonLength);
        Assert.Equal(want.GetProperty("horizon").GetInt32(), got.Horizon);

        var wp = want.GetProperty("params");
        Assert.Equal(wp.GetProperty("alpha").GetDouble(), got.Params!.Alpha, Eps);
        Assert.Equal(wp.GetProperty("beta").GetDouble(), got.Params!.Beta, Eps);
        Assert.Equal(wp.GetProperty("gamma").GetDouble(), got.Params!.Gamma, Eps);

        var wt = want.GetProperty("tuning");
        Assert.Equal(wt.GetProperty("strategy").GetString(), got.Tuning!.Strategy);
        Assert.Equal(wt.GetProperty("validationSize").GetInt32(), got.Tuning!.ValidationSize);
        AssertNullableClose(wt.GetProperty("validationMae"), got.Tuning!.ValidationMae);

        var wm = want.GetProperty("metrics");
        Assert.Equal(wm.GetProperty("mae").GetDouble(), got.Metrics!.Mae, Eps);
        Assert.Equal(wm.GetProperty("rmse").GetDouble(), got.Metrics!.Rmse, Eps);
        Assert.Equal(wm.GetProperty("mape").GetDouble(), got.Metrics!.Mape, Eps);
        Assert.Equal(wm.GetProperty("sampleSize").GetInt32(), got.Metrics!.SampleSize);

        var wb = want.GetProperty("baselines");
        Assert.Equal(wb.GetProperty("seasonalNaiveMae").GetDouble(), got.Baselines!.SeasonalNaiveMae, Eps);
        Assert.Equal(wb.GetProperty("naiveMae").GetDouble(), got.Baselines!.NaiveMae, Eps);

        var wf = want.GetProperty("final");
        Assert.Equal(wf.GetProperty("level").GetDouble(), got.Final!.Level, Eps);
        Assert.Equal(wf.GetProperty("trend").GetDouble(), got.Final!.Trend, Eps);

        var wfit = want.GetProperty("fitted");
        Assert.Equal(wfit.GetArrayLength(), got.Fitted!.Length);
        int i = 0;
        foreach (var e in wfit.EnumerateArray())
        {
            if (e.ValueKind == JsonValueKind.Null) Assert.Null(got.Fitted[i]);
            else { Assert.NotNull(got.Fitted[i]); Assert.Equal(e.GetDouble(), got.Fitted[i]!.Value, Eps); }
            i++;
        }

        var wfc = want.GetProperty("forecast");
        Assert.Equal(wfc.GetArrayLength(), got.Forecast!.Count);
        i = 0;
        foreach (var e in wfc.EnumerateArray())
        {
            ForecastStep step = got.Forecast[i++];
            Assert.Equal(e.GetProperty("step").GetInt32(), step.Step);
            Assert.Equal(e.GetProperty("value").GetDouble(), step.Value, Eps);
            Assert.Equal(e.GetProperty("lower").GetDouble(), step.Lower, Eps);
            Assert.Equal(e.GetProperty("upper").GetDouble(), step.Upper, Eps);
        }
    }

    [Fact]
    public void Matches_the_php_oracle_when_history_is_too_short()
    {
        double[] input = InputOf("tooShort");
        JsonElement want = ResultOf("tooShort");

        ForecastResult got = HoltWintersForecaster.Forecast(input, 12, 6);

        Assert.False(got.Ok);
        Assert.Equal(want.GetProperty("monthsRequired").GetInt32(), got.MonthsRequired);
        Assert.Equal(want.GetProperty("monthsAvailable").GetInt32(), got.MonthsAvailable);
    }

    [Fact]
    public void Forecasts_a_seasonal_series_with_low_error()
    {
        ForecastResult r = HoltWintersForecaster.Forecast(SeasonalSeries(), 12, 6);
        Assert.True(r.Ok);
        Assert.Equal(6, r.Forecast!.Count);
        Assert.True(r.Metrics!.Mape < 5.0);
        Assert.Equal(1288, r.Forecast[0].Value, 1288 * 0.1);
    }

    [Fact]
    public void Tuned_parameters_stay_within_bounds()
    {
        HoltWintersParams p = HoltWintersForecaster.Forecast(SeasonalSeries(), 12, 6).Params!;
        foreach (double v in new[] { p.Alpha, p.Beta, p.Gamma })
        {
            Assert.True(v >= 0);
            Assert.True(v <= 1);
        }
    }

    [Fact]
    public void Prediction_interval_brackets_each_forecast()
    {
        foreach (ForecastStep s in HoltWintersForecaster.Forecast(SeasonalSeries(), 12, 6).Forecast!)
        {
            Assert.True(s.Lower <= s.Value);
            Assert.True(s.Upper >= s.Value);
        }
    }

    [Fact]
    public void Prediction_interval_stays_ordered_on_a_declining_series()
    {
        foreach (ForecastStep s in HoltWintersForecaster.Forecast(DecliningSeries(), 12, 6).Forecast!)
        {
            Assert.True(s.Lower <= s.Value);
            Assert.True(s.Upper >= s.Value);
        }
    }

    [Fact]
    public void Refuses_to_forecast_without_two_seasonal_cycles()
    {
        ForecastResult r = HoltWintersForecaster.Forecast(SeasonalSeries(12), 12, 6);
        Assert.False(r.Ok);
        Assert.Equal(24, r.MonthsRequired);
    }

    private static readonly int[] Season = { 0, 40, 90, 140, 170, 120, -30, -60, -90, -40, 30, 70 };

    private static double[] SeasonalSeries(int months = 36)
    {
        var v = new double[months];
        for (int t = 0; t < months; t++) v[t] = 1000 + 8 * t + Season[t % 12];
        return v;
    }

    private static double[] DecliningSeries(int months = 36)
    {
        var v = new double[months];
        for (int t = 0; t < months; t++) v[t] = 5000 - 130 * t + Season[t % 12];
        return v;
    }

    private static void AssertNullableClose(JsonElement expected, double? actual)
    {
        if (expected.ValueKind == JsonValueKind.Null) Assert.Null(actual);
        else { Assert.NotNull(actual); Assert.Equal(expected.GetDouble(), actual!.Value, Eps); }
    }
}
