using Microsoft.EntityFrameworkCore;
using ProphetOps.Domain;

namespace ProphetOps.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<TravelPackage> TravelPackages => Set<TravelPackage>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().HasIndex(u => u.Email).IsUnique();

        b.Entity<TravelPackage>().HasIndex(p => p.Code).IsUnique();

        b.Entity<Booking>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.HasOne(x => x.TravelPackage)
                .WithMany(p => p.Bookings)
                .HasForeignKey(x => x.TravelPackageId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Expense>().HasIndex(x => x.Code).IsUnique();

        b.Entity<Booking>().Ignore(x => x.IsVoided);
        b.Entity<Expense>().Ignore(x => x.IsVoided);

        b.Entity<AuditEntry>(e =>
        {
            e.HasIndex(x => x.At);
            e.HasIndex(x => new { x.EntityType, x.EntityCode });
        });
    }
}
