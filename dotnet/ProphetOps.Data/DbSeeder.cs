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

        if (!db.AuditEntries.Any()) SeedHistory(db);
        db.SaveChanges();
    }

    private static void SeedUsers(AppDbContext db)
    {
        var accounts = new (string Name, string Email, string Role, string Password, string Status)[]
        {
            ("Maria Santos", "owner@prophetops.local", Roles.OwnerManagement, "owner123", "Active"),
            ("Admin User", "admin@prophetops.local", Roles.Admin, "admin123", "Active"),
            ("Staff User", "staff@prophetops.local", Roles.Staff, "staff123", "Active"),
            ("Rita Delos Santos", "rita.delossantos@prophetops.local", Roles.Admin, "rita123", "Active"),
            ("Ana Reyes", "ana.reyes@prophetops.local", Roles.Staff, "ana123", "Active"),
            ("Mark Villanueva", "mark.villanueva@prophetops.local", Roles.Staff, "mark123", "Active"),
            ("Joy Tolentino", "joy.tolentino@prophetops.local", Roles.Staff, "joy123", "Active"),
            ("Carlo Mendoza", "carlo.mendoza@prophetops.local", Roles.Staff, "carlo123", "Suspended"),
        };

        foreach (var a in accounts)
        {
            db.Users.Add(new User
            {
                Name = a.Name,
                Email = a.Email,
                Role = a.Role,
                Status = a.Status,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(a.Password),
            });
        }
    }

    // Voided records and an activity trail, so the audit features are visible in the demonstration.
    private static void SeedHistory(AppDbContext db)
    {
        var owner = db.Users.First(u => u.Role == Roles.OwnerManagement);
        var bookings = db.Bookings.OrderBy(b => b.Id).ToList();
        if (bookings.Count == 0) return;

        var reasons = new[]
        {
            "Client cancelled after the airline moved the flight.",
            "Duplicate entry - the same booking was recorded twice.",
            "Passenger count typed wrong; re-entered as a new booking.",
        };

        var voided = bookings
            .Where(b => b.BookingDate.Year >= 2025)
            .OrderByDescending(b => b.BookingDate)
            .Skip(4)
            .Take(reasons.Length)
            .ToList();

        for (var i = 0; i < voided.Count; i++)
        {
            voided[i].VoidedAt = voided[i].BookingDate.ToDateTime(new TimeOnly(14, 5)).AddDays(3);
            voided[i].VoidedBy = owner.Email;
            voided[i].VoidReason = reasons[i];
        }

        var entries = new List<AuditEntry>();

        void Record(DateOnly on, int hour, string action, string type, string code, string? summary)
        {
            entries.Add(new AuditEntry
            {
                At = on.ToDateTime(new TimeOnly(hour, (code.Length * 7) % 60)),
                Actor = owner.Email,
                ActorName = owner.Name,
                Action = action,
                EntityType = type,
                EntityCode = code,
                Summary = summary,
            });
        }

        foreach (var b in bookings)
        {
            Record(b.BookingDate, 9, "Created", "Booking", b.Code,
                $"{b.Client} - {b.PackageName}, {b.PassengerCount} pax");
        }

        foreach (var b in bookings.Where((_, i) => i % 17 == 0))
        {
            Record(b.BookingDate.AddDays(1), 11, "Updated", "Booking", b.Code, "Payment status");
        }

        foreach (var b in voided)
        {
            Record(DateOnly.FromDateTime(b.VoidedAt!.Value), 14, "Voided", "Booking", b.Code, b.VoidReason);
        }

        foreach (var p in db.TravelPackages.OrderBy(p => p.Id).ToList())
        {
            Record(p.LastUpdatedAt ?? new DateOnly(2026, 1, 6), 8, "Created", "TravelPackage", p.Code,
                $"{p.PackageName} - {p.Destination}");
        }

        foreach (var x in db.Expenses.OrderBy(x => x.Id).ToList())
        {
            Record(x.ExpenseDate, 16, "Created", "Expense", x.Code, $"{x.Category} - PHP {x.Amount:N0}");
        }

        db.AuditEntries.AddRange(entries);
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
