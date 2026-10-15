using ProphetOps.Data;

namespace ProphetOps.Api;

/// What a forecast can add up each month, as the forecast endpoints and their exports read it.
internal static class ForecastMeasures
{
    public const string Choices = "Use revenue, bookings or passengers.";

    public static bool TryRead(string? value, out DemandMeasure measure)
    {
        switch ((value ?? "").Trim().ToLowerInvariant())
        {
            case "":
            case "revenue":
                measure = DemandMeasure.Revenue;
                return true;
            case "bookings":
                measure = DemandMeasure.Bookings;
                return true;
            case "passengers":
                measure = DemandMeasure.Passengers;
                return true;
            default:
                measure = DemandMeasure.Revenue;
                return false;
        }
    }

    public static string Name(DemandMeasure measure) => measure switch
    {
        DemandMeasure.Bookings => "bookings",
        DemandMeasure.Passengers => "passengers",
        _ => "revenue",
    };

    public static string Unit(DemandMeasure measure) => measure switch
    {
        DemandMeasure.Bookings => "bookings",
        DemandMeasure.Passengers => "passengers",
        _ => "PHP",
    };

    /// The built-in sample history stands in only for local demonstrations, never in production.
    public static bool AllowSampleFallback(IConfiguration configuration, IHostEnvironment environment) =>
        configuration.GetValue<bool>("Demo:Enabled") && !environment.IsProduction();
}
