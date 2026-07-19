using Microsoft.EntityFrameworkCore;
using ProphetOps.Data;
using ProphetOps.Domain;
using Xunit;

namespace ProphetOps.Data.Tests;

public class DemandSeriesTests
{
    private static AppDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        var db = new AppDbContext(options);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        return db;
    }

    private static void AddBooking(AppDbContext db, int year, int month, int revenue)
    {
        db.Bookings.Add(new Booking
        {
            Code = $"BKG-{year}{month:00}-{Guid.NewGuid().ToString()[..4]}",
            BookingDate = new DateOnly(year, month, 5),
            PassengerCount = 1,
            Client = "Test",
            PackageName = "Test",
            Destination = "Test",
            GrossRevenue = revenue,
        });
    }

    private static void FillMonths(AppDbContext db, DateOnly from, int months, int revenue = 100000)
    {
        for (int i = 0; i < months; i++)
        {
            var m = from.AddMonths(i);
            AddBooking(db, m.Year, m.Month, revenue);
        }
        db.SaveChanges();
    }

    [Fact]
    public void Falls_back_to_the_sample_series_when_history_is_short()
    {
        using var db = NewDb();
        FillMonths(db, new DateOnly(2026, 1, 1), 6);

        var series = DemandSeriesBuilder.Build(db, new DateOnly(2026, 8, 15));

        Assert.False(series.UsingLiveRecords);
        Assert.Equal(6, series.LiveMonthsAvailable);
        Assert.Equal(36, series.Values.Count);
    }

    [Fact]
    public void Uses_live_records_once_two_seasons_are_complete()
    {
        using var db = NewDb();
        FillMonths(db, new DateOnly(2024, 1, 1), 24);

        var series = DemandSeriesBuilder.Build(db, new DateOnly(2026, 2, 10));

        Assert.True(series.UsingLiveRecords);
        Assert.Equal(24, series.LiveMonthsAvailable);
        // Ends on the last recorded month (Dec 2025), not the last complete calendar month.
        Assert.Equal(new DateOnly(2025, 12, 1), series.LastMonth);
    }

    [Fact]
    public void Excludes_the_running_month_so_a_partial_total_is_not_read_as_a_collapse()
    {
        using var db = NewDb();
        FillMonths(db, new DateOnly(2024, 1, 1), 25);

        // The 25th month is the month we are standing in; only part of it has happened.
        var series = DemandSeriesBuilder.Build(db, new DateOnly(2026, 1, 20));

        Assert.Equal(new DateOnly(2025, 12, 1), series.LastMonth);
        Assert.Equal(24, series.Values.Count);
    }

    [Fact]
    public void Months_without_bookings_are_kept_as_zero_so_seasons_stay_aligned()
    {
        using var db = NewDb();
        FillMonths(db, new DateOnly(2024, 1, 1), 24);
        AddBooking(db, 2026, 3, 250000);
        db.SaveChanges();

        var series = DemandSeriesBuilder.Build(db, new DateOnly(2026, 4, 5));

        // Jan 2024 through Mar 2026 inclusive, with Jan and Feb 2026 empty but present.
        Assert.Equal(27, series.Values.Count);
        Assert.Equal(2, series.FilledMonths);
        Assert.Equal(0, series.Values[24]);
        Assert.Equal(0, series.Values[25]);
        Assert.Equal(250000, series.Values[26]);
    }

    [Fact]
    public void Trailing_unrecorded_months_do_not_drag_the_series_to_zero()
    {
        using var db = NewDb();
        FillMonths(db, new DateOnly(2024, 1, 1), 24);

        // Entry has fallen six months behind; those months are unrecorded, not genuinely zero.
        var series = DemandSeriesBuilder.Build(db, new DateOnly(2026, 7, 3));

        Assert.Equal(24, series.Values.Count);
        Assert.Equal(new DateOnly(2025, 12, 1), series.LastMonth);
        Assert.All(series.Values, v => Assert.True(v > 0));
    }

    [Fact]
    public void Sums_every_booking_inside_a_month()
    {
        using var db = NewDb();
        FillMonths(db, new DateOnly(2024, 1, 1), 24, 100000);
        AddBooking(db, 2025, 12, 50000);
        db.SaveChanges();

        var series = DemandSeriesBuilder.Build(db, new DateOnly(2026, 1, 9));

        Assert.Equal(150000, series.Values[^1]);
    }
}
