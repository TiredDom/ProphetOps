using System.Text.Json;
using ProphetOps.Domain;

namespace ProphetOps.Data;

public static class DbSeeder
{
    public static void Seed(AppDbContext db)
    {
        if (!db.Users.Any()) SeedUsers(db);
        if (!db.TravelPackages.Any()) SeedOperations(db);
        db.SaveChanges();
    }

    private static void SeedUsers(AppDbContext db)
    {
        var accounts = new (string Name, string Email, string Role, string Password)[]
        {
            ("Maria Santos", "owner@prophetops.local", Roles.OwnerManagement, "owner123"),
            ("Admin User", "admin@prophetops.local", Roles.Admin, "admin123"),
            ("Staff User", "staff@prophetops.local", Roles.Staff, "staff123"),
        };

        foreach (var a in accounts)
        {
            db.Users.Add(new User
            {
                Name = a.Name,
                Email = a.Email,
                Role = a.Role,
                Status = "Active",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(a.Password),
            });
        }
    }

    private static void SeedOperations(AppDbContext db)
    {
        var assembly = typeof(DbSeeder).Assembly;
        var resource = assembly.GetManifestResourceNames().First(n => n.EndsWith("seed-data.json"));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        foreach (var p in root.GetProperty("travel_packages").EnumerateArray())
        {
            db.TravelPackages.Add(new TravelPackage
            {
                Code = Str(p, "code"),
                PackageName = Str(p, "package_name"),
                Destination = Str(p, "destination"),
                Duration = StrN(p, "duration"),
                BasePrice = Int(p, "base_price"),
                Inclusions = StrN(p, "inclusions"),
                AvailableSlots = Int(p, "available_slots"),
                SoldCount = Int(p, "sold_count"),
                ReservedCount = Int(p, "reserved_count"),
                Status = Str(p, "status"),
                LastUpdatedAt = DateN(p, "last_updated_at"),
            });
        }

        db.SaveChanges();

        foreach (var b in root.GetProperty("bookings").EnumerateArray())
        {
            db.Bookings.Add(new Booking
            {
                Code = Str(b, "code"),
                BookingDate = Date(b, "booking_date"),
                PassengerCount = Int(b, "passenger_count"),
                Client = Str(b, "client"),
                TravelPackageId = IntN(b, "travel_package_id"),
                PackageName = Str(b, "package_name"),
                PackageCode = StrN(b, "package_code"),
                EntryType = Str(b, "entry_type"),
                Destination = Str(b, "destination"),
                GrossRevenue = Int(b, "gross_revenue"),
                PaymentStatus = Str(b, "payment_status"),
                BookingStatus = Str(b, "booking_status"),
                StaffAssigned = StrN(b, "staff_assigned"),
                Source = Str(b, "source"),
                Notes = StrN(b, "notes"),
            });
        }

        foreach (var x in root.GetProperty("expenses").EnumerateArray())
        {
            db.Expenses.Add(new Expense
            {
                Code = Str(x, "code"),
                ExpenseDate = Date(x, "expense_date"),
                Category = Str(x, "category"),
                Amount = Int(x, "amount"),
                RelatedPackage = Str(x, "related_package"),
                PaymentStatus = Str(x, "payment_status"),
                Notes = StrN(x, "notes"),
            });
        }
    }

    private static string Str(JsonElement e, string k) => e.GetProperty(k).GetString() ?? "";

    private static string? StrN(JsonElement e, string k) =>
        e.TryGetProperty(k, out var v) && v.ValueKind != JsonValueKind.Null ? v.GetString() : null;

    private static int Int(JsonElement e, string k) => e.GetProperty(k).GetInt32();

    private static int? IntN(JsonElement e, string k) =>
        e.TryGetProperty(k, out var v) && v.ValueKind != JsonValueKind.Null ? v.GetInt32() : null;

    private static DateOnly Date(JsonElement e, string k) => DateOnly.Parse(e.GetProperty(k).GetString()!);

    private static DateOnly? DateN(JsonElement e, string k) =>
        e.TryGetProperty(k, out var v) && v.ValueKind != JsonValueKind.Null && !string.IsNullOrEmpty(v.GetString())
            ? DateOnly.Parse(v.GetString()!)
            : null;
}
