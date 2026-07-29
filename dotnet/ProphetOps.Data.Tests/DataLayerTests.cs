using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProphetOps.Data;
using ProphetOps.Domain;
using ProphetOps.Forecasting;
using Xunit;

namespace ProphetOps.Data.Tests;

public class DataLayerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;

    public DataLayerTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.Migrate();
        DbSeeder.Seed(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    /// Deliberately not exact counts. The demonstration data is regenerated whenever the packages
    /// or the date range change, and a test that pins the totals fails on every such change without
    /// anything actually being broken.
    [Fact]
    public void Seeds_enough_data_for_the_system_to_be_demonstrated()
    {
        Assert.True(_db.Users.Count() >= 3);
        Assert.NotEmpty(_db.TravelPackages);
        Assert.NotEmpty(_db.Expenses);

        // Holt-Winters needs two full seasons before it will run on real records rather than
        // falling back to the sample series.
        var months = _db.Bookings
            .Where(b => b.VoidedAt == null)
            .Select(b => new { b.BookingDate.Year, b.BookingDate.Month })
            .Distinct()
            .Count();
        Assert.True(months >= 24, $"Seeded bookings span only {months} months.");
    }

    [Fact]
    public void Booking_links_to_its_package_through_the_only_foreign_key()
    {
        var booking = _db.Bookings
            .Include(b => b.TravelPackage)
            .First(b => b.TravelPackageId != null);

        Assert.NotNull(booking.TravelPackage);
        Assert.Equal(booking.TravelPackageId, booking.TravelPackage!.Id);
    }

    [Fact]
    public void Seeds_the_three_roles_with_a_verifiable_hashed_password()
    {
        Assert.Equal(
            new[] { Roles.Admin, Roles.OwnerManagement, Roles.Staff },
            _db.Users.Select(u => u.Role).Distinct().OrderBy(r => r).ToArray());

        var admin = _db.Users.Single(u => u.Email == "admin@prophetops.local");
        Assert.NotEqual("admin123", admin.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("admin123", admin.PasswordHash));
    }

    [Fact]
    public void A_created_booking_persists_and_reads_back()
    {
        var before = _db.Bookings.Count();

        _db.Bookings.Add(new Booking
        {
            Code = "BKG-TEST",
            BookingDate = new DateOnly(2026, 7, 1),
            PassengerCount = 2,
            Client = "Test Client",
            PackageName = "Ad hoc",
            Destination = "Cebu",
            GrossRevenue = 15000,
        });
        _db.SaveChanges();

        Assert.Equal(before + 1, _db.Bookings.Count());
        Assert.NotNull(_db.Bookings.SingleOrDefault(b => b.Code == "BKG-TEST"));
    }

    [Fact]
    public void Sample_sales_history_reproduces_the_paper_forecast_accuracy()
    {
        var series = SampleSalesHistory.MonthlyRevenue(2026, 7).Select(v => (double)v).ToList();

        var result = HoltWintersForecaster.Forecast(series, 12, 6);

        Assert.True(result.Ok);
        Assert.Equal(36, series.Count);
        Assert.True(result.Metrics!.Mape < 6.0, $"MAPE was {result.Metrics.Mape}%");
        Assert.True(result.Baselines!.SeasonalNaiveMae > result.Metrics.Mae);
    }
}
