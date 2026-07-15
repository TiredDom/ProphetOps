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
    }
}
