namespace ProphetOps.Data;

public static class SampleSalesHistory
{
    private static readonly double[] Seasonal =
        { 0, 1.04, 0.97, 1.12, 1.22, 1.18, 0.93, 0.88, 0.92, 0.96, 1.03, 1.09, 1.26 };

    private static readonly double[] Variation =
        { 1.0, 0.98, 1.03, 0.99, 1.02, 0.97, 1.04, 1.0, 0.96, 1.05, 0.985, 1.015 };

    private const int BaseRevenue = 700000;
    private const int MonthlyGrowth = 12000;

    public static IReadOnlyList<int> MonthlyRevenue(int anchorYear, int anchorMonth, int months = 36)
    {
        var result = new int[months];
        var start = new DateOnly(anchorYear, anchorMonth, 1).AddMonths(-(months - 1));

        for (int i = 0; i < months; i++)
        {
            var date = start.AddMonths(i);
            double revenue = (BaseRevenue + MonthlyGrowth * i) * Seasonal[date.Month] * Variation[i % 12];
            result[i] = (int)(Math.Round(revenue / 1000.0, 0, MidpointRounding.AwayFromZero) * 1000);
        }

        return result;
    }
}
