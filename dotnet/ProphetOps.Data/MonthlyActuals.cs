namespace ProphetOps.Data;

public record MonthlyActual(DateOnly Month, long RevenuePhp, long BookingCount, long PassengerCount);

public static class MonthlyActuals
{
    // The cutoff includes its entire calendar month. Missing months carry no recorded actuals.
    public static IReadOnlyList<MonthlyActual> Build(AppDbContext db, DateOnly throughMonth)
    {
        var cutoff = new DateOnly(throughMonth.Year, throughMonth.Month,
            DateTime.DaysInMonth(throughMonth.Year, throughMonth.Month));

        return db.Bookings
            .Where(b => b.VoidedAt == null && b.BookingDate <= cutoff)
            .Select(b => new { b.BookingDate, b.GrossRevenue, b.PassengerCount })
            .AsEnumerable()
            .GroupBy(b => new DateOnly(b.BookingDate.Year, b.BookingDate.Month, 1))
            .OrderBy(g => g.Key)
            .Select(g => new MonthlyActual(g.Key, g.Sum(b => (long)b.GrossRevenue),
                g.LongCount(), g.Sum(b => (long)b.PassengerCount)))
            .ToList();
    }
}
