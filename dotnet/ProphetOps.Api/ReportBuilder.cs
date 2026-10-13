using System.Globalization;
using ProphetOps.Data;
using ProphetOps.Domain;

namespace ProphetOps.Api;

/// The figures behind Reports and its export: one period of recorded bookings and expenses, voided
/// records left out, with the matching earlier period to compare against. Read-only.
public static class ReportBuilder
{
    public sealed record Totals(DateOnly? From, DateOnly? To, long Revenue, long Costs, int Bookings, long Passengers, int Expenses)
    {
        /// Gross booking revenue less recorded expenses; not accounting profit.
        public long AfterCosts => Revenue - Costs;
    }

    public sealed record Month(DateOnly Start, long Revenue, long Costs, int Bookings, long Passengers)
    {
        public long AfterCosts => Revenue - Costs;
    }

    public sealed record Line(string Label, long Value, int Count);

    public sealed record Report(
        DateOnly? From,
        DateOnly? To,
        DateOnly? Through,
        Totals Current,
        Totals? Previous,
        IReadOnlyList<Month> Months,
        IReadOnlyList<Line> RevenueByPackage,
        IReadOnlyList<Line> RevenueByDestination,
        IReadOnlyList<Line> ExpensesByCategory,
        IReadOnlyList<Line> BookingsByStatus,
        IReadOnlyList<Line> BookingsByPayment);

    private const int MaxMonths = 240;

    /// Reads optional yyyy-MM-dd bounds. Either end may be left open.
    public static bool TryReadPeriod(string? from, string? to, out DateOnly? start, out DateOnly? end, out Dictionary<string, string> errors)
    {
        errors = new Dictionary<string, string>();
        start = ReadDate(from, "from", errors);
        end = ReadDate(to, "to", errors);
        if (start is not null && end is not null && start > end)
            errors["to"] = "The end date is before the start date.";
        return errors.Count == 0;
    }

    private static DateOnly? ReadDate(string? value, string field, Dictionary<string, string> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;
        errors[field] = "Use a date written as yyyy-mm-dd.";
        return null;
    }

    public static Report Build(AppDbContext db, DateOnly today, DateOnly? from, DateOnly? to)
    {
        var bookings = db.Bookings.Where(b => b.VoidedAt == null).ToList();
        var expenses = db.Expenses.Where(e => e.VoidedAt == null).ToList();

        // A period that runs past today is read up to today, and compared with the same stretch
        // of the period before it, so a year to date is never set against a whole year.
        DateOnly? through = to is DateOnly end && end > today && (from is null || from <= today) ? today : to;

        bool InPeriod(DateOnly date) => (from is null || date >= from) && (through is null || date <= through);
        var periodBookings = bookings.Where(b => InPeriod(b.BookingDate)).ToList();
        var periodExpenses = expenses.Where(e => InPeriod(e.ExpenseDate)).ToList();

        var current = TotalsFor(bookings, expenses, from, through);

        Totals? previous = null;
        if (from is DateOnly start && to is DateOnly stop && through is DateOnly last)
        {
            var (previousFrom, previousTo) = PreviousWindow(start, stop, last);
            previous = TotalsFor(bookings, expenses, previousFrom, previousTo);
        }

        return new Report(
            from,
            to,
            through,
            current,
            previous,
            MonthsFor(bookings, expenses, from, through),
            periodBookings.GroupBy(b => b.PackageName)
                .Select(g => new Line(g.Key, g.Sum(b => (long)b.GrossRevenue), g.Count()))
                .OrderByDescending(l => l.Value).ThenBy(l => l.Label).ToList(),
            periodBookings.GroupBy(b => b.Destination)
                .Select(g => new Line(g.Key, g.Sum(b => (long)b.GrossRevenue), g.Count()))
                .OrderByDescending(l => l.Value).ThenBy(l => l.Label).ToList(),
            periodExpenses.GroupBy(e => e.Category)
                .Select(g => new Line(g.Key, g.Sum(e => (long)e.Amount), g.Count()))
                .OrderByDescending(l => l.Value).ThenBy(l => l.Label).ToList(),
            periodBookings.GroupBy(b => b.BookingStatus)
                .Select(g => new Line(g.Key, g.Count(), g.Count()))
                .OrderByDescending(l => l.Value).ThenBy(l => l.Label).ToList(),
            periodBookings.GroupBy(b => b.PaymentStatus)
                .Select(g => new Line(g.Key, g.Sum(b => (long)b.GrossRevenue), g.Count()))
                .OrderByDescending(l => l.Count).ThenBy(l => l.Label).ToList());
    }

    private static Totals TotalsFor(
        IReadOnlyCollection<Booking> bookings,
        IReadOnlyCollection<Expense> expenses,
        DateOnly? from,
        DateOnly? to)
    {
        bool Inside(DateOnly date) => (from is null || date >= from) && (to is null || date <= to);
        var b = bookings.Where(x => Inside(x.BookingDate)).ToList();
        var e = expenses.Where(x => Inside(x.ExpenseDate)).ToList();
        return new Totals(
            from,
            to,
            b.Sum(x => (long)x.GrossRevenue),
            e.Sum(x => (long)x.Amount),
            b.Count,
            b.Sum(x => (long)x.PassengerCount),
            e.Count);
    }

    /// Whole calendar months compare with the same number of months before them, cut at the same
    /// point when the period is only read up to today; any other range compares with the same
    /// number of days just before it.
    private static (DateOnly From, DateOnly To) PreviousWindow(DateOnly from, DateOnly to, DateOnly through)
    {
        var wholeMonths = from.Day == 1 && to.Day == DateTime.DaysInMonth(to.Year, to.Month);
        if (wholeMonths)
        {
            var months = (to.Year - from.Year) * 12 + to.Month - from.Month + 1;
            var previousFrom = from.AddMonths(-months);
            var previousTo = through == to ? from.AddDays(-1) : through.AddMonths(-months);
            return (previousFrom, previousTo);
        }

        var days = through.DayNumber - from.DayNumber + 1;
        var previousEnd = from.AddDays(-1);
        return (previousEnd.AddDays(-(days - 1)), previousEnd);
    }

    private static IReadOnlyList<Month> MonthsFor(
        IReadOnlyCollection<Booking> bookings,
        IReadOnlyCollection<Expense> expenses,
        DateOnly? from,
        DateOnly? through)
    {
        var dates = bookings.Select(b => b.BookingDate).Concat(expenses.Select(e => e.ExpenseDate)).ToList();
        if (dates.Count == 0) return [];

        static DateOnly MonthOf(DateOnly date) => new(date.Year, date.Month, 1);
        var firstRecorded = MonthOf(dates.Min());
        var lastRecorded = MonthOf(dates.Max());

        // Months before the first record would only be empty rows, so the list starts at the
        // later of the period's start and the first record. Up to today it keeps empty months,
        // because nothing recorded is itself worth seeing.
        var start = from is DateOnly f && MonthOf(f) > firstRecorded ? MonthOf(f) : firstRecorded;
        var end = through is DateOnly t ? MonthOf(t) : lastRecorded;
        if (end < start) return [];

        var count = (end.Year - start.Year) * 12 + end.Month - start.Month + 1;
        if (count > MaxMonths) start = end.AddMonths(-(MaxMonths - 1));

        bool Inside(DateOnly date) => (from is null || date >= from) && (through is null || date <= through);
        var byMonthBookings = bookings.Where(b => Inside(b.BookingDate)).GroupBy(b => MonthOf(b.BookingDate))
            .ToDictionary(g => g.Key, g => g.ToList());
        var byMonthExpenses = expenses.Where(e => Inside(e.ExpenseDate)).GroupBy(e => MonthOf(e.ExpenseDate))
            .ToDictionary(g => g.Key, g => g.Sum(e => (long)e.Amount));

        var months = new List<Month>();
        for (var month = start; month <= end; month = month.AddMonths(1))
        {
            var monthBookings = byMonthBookings.GetValueOrDefault(month) ?? [];
            months.Add(new Month(
                month,
                monthBookings.Sum(b => (long)b.GrossRevenue),
                byMonthExpenses.GetValueOrDefault(month),
                monthBookings.Count,
                monthBookings.Sum(b => (long)b.PassengerCount)));
        }
        return months;
    }
}
