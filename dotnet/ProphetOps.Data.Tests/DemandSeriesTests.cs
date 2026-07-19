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
    public void Entries_added_through_the_running_month_do_not_disturb_the_series()
    {
        using var db = NewDb();
        FillMonths(db, new DateOnly(2024, 1, 1), 24);

        var before = DemandSeriesBuilder.Build(db, new DateOnly(2026, 1, 4));

        // Staff record bookings across the first fortnight of the month they are standing in.
        foreach (var day in new[] { 4, 7, 9, 12, 15 })
        {
            AddBooking(db, 2026, 1, 30000);
            db.SaveChanges();

            var during = DemandSeriesBuilder.Build(db, new DateOnly(2026, 1, day));
            Assert.Equal(before.Values.Count, during.Values.Count);
            Assert.Equal(before.LastMonth, during.LastMonth);
            Assert.Equal(before.Values[^1], during.Values[^1]);
        }
    }

    [Fact]
    public void The_series_takes_the_month_up_once_that_month_has_finished()
    {
        using var db = NewDb();
        FillMonths(db, new DateOnly(2024, 1, 1), 24);
        AddBooking(db, 2026, 1, 150000);
        db.SaveChanges();

        var standingInJanuary = DemandSeriesBuilder.Build(db, new DateOnly(2026, 1, 20));
        var standingInFebruary = DemandSeriesBuilder.Build(db, new DateOnly(2026, 2, 1));

        Assert.Equal(24, standingInJanuary.Values.Count);
        Assert.Equal(25, standingInFebruary.Values.Count);
        Assert.Equal(150000, standingInFebruary.Values[^1]);
    }

    [Fact]
    public void Switches_from_the_sample_series_to_live_records_on_the_month_that_completes_two_seasons()
    {
        using var db = NewDb();
        FillMonths(db, new DateOnly(2024, 1, 1), 23);

        var justShort = DemandSeriesBuilder.Build(db, new DateOnly(2025, 12, 10));
        Assert.False(justShort.UsingLiveRecords);
        Assert.Equal(23, justShort.LiveMonthsAvailable);

        AddBooking(db, 2025, 12, 100000);
        db.SaveChanges();

        var crossed = DemandSeriesBuilder.Build(db, new DateOnly(2026, 1, 10));
        Assert.True(crossed.UsingLiveRecords);
        Assert.Equal(24, crossed.LiveMonthsAvailable);
    }

    [Fact]
    public void A_booking_entered_late_for_an_earlier_month_lands_in_that_month()
    {
        using var db = NewDb();
        FillMonths(db, new DateOnly(2024, 1, 1), 24, 100000);

        var before = DemandSeriesBuilder.Build(db, new DateOnly(2026, 1, 9));
        var marchIndex = 14;

        // Someone catches up on a March 2025 booking nine months later.
        AddBooking(db, 2025, 3, 45000);
        db.SaveChanges();

        var after = DemandSeriesBuilder.Build(db, new DateOnly(2026, 1, 9));

        Assert.Equal(before.Values.Count, after.Values.Count);
        Assert.Equal(before.Values[marchIndex] + 45000, after.Values[marchIndex]);
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
