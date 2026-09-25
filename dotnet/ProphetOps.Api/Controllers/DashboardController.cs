using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProphetOps.Data;
using ProphetOps.Forecasting;

namespace ProphetOps.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Policy = "Dashboard")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IBusinessClock _clock;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public DashboardController(AppDbContext db, IBusinessClock clock, IConfiguration configuration, IHostEnvironment environment)
    {
        _db = db;
        _clock = clock;
        _configuration = configuration;
        _environment = environment;
    }

    [HttpGet]
    public IActionResult Get()
    {
        var bookings = _db.Bookings.Where(b => b.VoidedAt == null).ToList();
        var packages = _db.TravelPackages.ToList();

        var revenue = bookings.Sum(b => (long)b.GrossRevenue);
        var costs = _db.Expenses.Where(e => e.VoidedAt == null).Sum(e => (long)e.Amount);

        var demand = DemandSeriesBuilder.Build(_db, _clock.Today, AllowSampleFallback());
        var anchor = demand.LastMonth;
        var series = demand.Values.ToList();
        var forecastAvailable = demand.UsingLiveRecords || demand.UsingSample;
        var forecast = forecastAvailable
            ? HoltWintersForecaster.Forecast(series, DemandSeriesBuilder.SeasonLength, 6)
            : null;
        var mape = forecast?.Ok == true ? forecast.Metrics!.Mape : 0;
        var accuracy = forecast?.Ok == true ? Math.Max(0, (int)Math.Round(100 - mape)) : 0;

        var forecastSteps = forecast?.Forecast ?? new List<ForecastStep>();
        var recentMean = series.Count > 0 ? series.Skip(Math.Max(0, series.Count - 6)).Average() : 0;
        var forecastMean = forecastSteps.Count > 0 ? forecastSteps.Average(s => s.Value) : recentMean;
        var changePercent = recentMean != 0 ? Math.Round((forecastMean - recentMean) / recentMean * 100, 1) : 0;
        var direction = changePercent > 1 ? "up" : changePercent < -1 ? "down" : "flat";

        var peak = forecastSteps.OrderByDescending(s => s.Value).FirstOrDefault();
        var peakMonth = peak is not null
            ? anchor.AddMonths(peak.Step).ToString("MMMM yyyy", CultureInfo.InvariantCulture)
            : "";
        var peakValue = peak?.Value ?? 0;

        var lowStock = packages.Where(p => p.Status == "Low" || p.Status == "Critical").ToList();
        if (lowStock.Count == 0)
            lowStock = packages.Where(p => p.AvailableSlots <= 5).ToList();
        var lowStockPackages = lowStock
            .OrderBy(p => p.AvailableSlots)
            .Select(p => new
            {
                code = p.Code,
                packageName = p.PackageName,
                destination = p.Destination,
                availableSlots = p.AvailableSlots,
                status = p.Status,
            })
            .ToList();

        var unpaid = bookings.Where(b => b.PaymentStatus != "Paid").ToList();
        var pendingPayments = new
        {
            count = unpaid.Count,
            amount = unpaid.Sum(b => (long)b.GrossRevenue),
        };

        var recentBookings = bookings
            .OrderByDescending(b => b.BookingDate)
            .ThenByDescending(b => b.Id)
            .Take(5)
            .Select(b => new
            {
                code = b.Code,
                client = b.Client,
                package = b.PackageName,
                destination = b.Destination,
                grossRevenue = b.GrossRevenue,
                paymentStatus = b.PaymentStatus,
                bookingStatus = b.BookingStatus,
                ds = b.BookingDate.ToString("yyyy-MM-dd"),
            })
            .ToList();

        return Ok(new
        {
            totalsScope = "lifetime",
            excludesVoided = true,
            revenue,
            costs,
            estimatedProfit = revenue - costs,
            bookings = bookings.Count,
            packages = packages.Count,
            expenses = _db.Expenses.Count(e => e.VoidedAt == null),
            forecast = new
            {
                method = "Holt-Winters",
                horizon = 6,
                ok = forecast?.Ok == true,
                status = DemandStatus(demand),
                accuracy,
                mape,
                nextValue = forecast?.Ok == true ? forecast.Forecast![0].Value : 0,
                direction,
                changePercent,
                peakMonth,
                peakValue,
                dataSource = DataSource(demand),
            },
            lowStockPackages,
            pendingPayments,
            recentBookings,
            lastUpdated = _clock.UtcNow.UtcDateTime.ToString("MMM d, yyyy"),
        });
    }

    private bool AllowSampleFallback() =>
        _configuration.GetValue<bool>("Demo:Enabled") && !_environment.IsProduction();

    private static object DataSource(DemandSeries demand) => new
    {
        status = DemandStatus(demand),
        usingLiveRecords = demand.UsingLiveRecords,
        usingSample = demand.UsingSample,
        label = demand.UsingSample
            ? "Sample demonstration data"
            : demand.UsingLiveRecords
                ? "Live booking history"
                : "Insufficient booking history",
        liveMonthsAvailable = demand.LiveMonthsAvailable,
        recordedMonths = demand.RecordedMonths,
        minimumMonths = demand.MinimumMonths,
        filledMonths = demand.FilledMonths,
        lastRecordedMonth = demand.LiveMonthsAvailable > 0
            ? demand.LastMonth.ToString("MMMM yyyy", CultureInfo.InvariantCulture)
            : null,
    };

    private static string DemandStatus(DemandSeries demand) => demand.Source switch
    {
        DemandSeriesSource.LiveRecords => "live",
        DemandSeriesSource.SampleSeries => "sample",
        _ => "insufficient-history",
    };
}
