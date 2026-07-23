using ProphetOps.Forecasting;
using Xunit;

namespace ProphetOps.Forecasting.Tests;

public class SeriesAnomaliesTests
{
    private static List<double> Ordinary() =>
        [100_000, 108_000, 96_000, 112_000, 104_000, 99_000, 107_000, 101_000, 110_000, 98_000, 103_000, 106_000];

    [Fact]
    public void Finds_nothing_in_a_series_that_only_wobbles()
    {
        Assert.Empty(SeriesAnomalies.Detect(Ordinary()));
    }

    [Fact]
    public void Catches_the_month_that_gained_a_digit()
    {
        var withTypo = Ordinary();
        withTypo[4] = 1_040_000;

        var found = SeriesAnomalies.Detect(withTypo);

        var one = Assert.Single(found);
        Assert.Equal(4, one.Index);
        Assert.Equal(1_040_000, one.Value);
    }

    [Fact]
    public void Catches_the_month_that_lost_one()
    {
        var withTypo = Ordinary();
        withTypo[7] = 10_100;

        var found = SeriesAnomalies.Detect(withTypo);

        Assert.Equal(7, Assert.Single(found).Index);
    }

    [Fact]
    public void A_single_extreme_month_does_not_hide_behind_its_own_effect_on_the_average()
    {
        // A mean-and-standard-deviation test misses this: the outlier pulls the mean toward
        // itself and inflates the deviation until it no longer looks unusual.
        var withTypo = Ordinary();
        withTypo[2] = 50_000_000;

        Assert.Single(SeriesAnomalies.Detect(withTypo));
    }

    [Fact]
    public void Reports_several_when_several_are_off()
    {
        var withTypos = Ordinary();
        withTypos[1] = 1_080_000;
        withTypos[9] = 980_000;

        Assert.Equal(2, SeriesAnomalies.Detect(withTypos).Count);
    }

    [Fact]
    public void Says_nothing_on_too_few_months_to_judge_against()
    {
        Assert.Empty(SeriesAnomalies.Detect([100_000, 5_000_000, 102_000]));
    }

    [Fact]
    public void Says_nothing_when_the_months_are_identical_and_there_is_no_spread()
    {
        var flat = Enumerable.Repeat(100_000d, 12).ToList();
        flat[5] = 900_000;

        // Half or more identical leaves a zero deviation; there is no scale to measure against,
        // so the honest answer is no finding rather than every other month at once.
        Assert.Empty(SeriesAnomalies.Detect(flat));
    }

    [Fact]
    public void Tolerates_a_month_with_no_bookings()
    {
        var withGap = Ordinary();
        withGap[6] = 0;

        var found = SeriesAnomalies.Detect(withGap);

        Assert.All(found, a => Assert.True(a.Index == 6));
    }
}
