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

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AdvanceRevisions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AdvanceRevisions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void AdvanceRevisions()
    {
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Modified
            && (e.Entity is Booking || e.Entity is TravelPackage)))
        {
            var revision = entry.Property("Revision");
            revision.CurrentValue = checked((int)revision.OriginalValue! + 1);
        }
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        if (Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
            b.HasDefaultSchema("prophetops");

        b.Entity<User>().HasIndex(u => u.Email).IsUnique();
        b.Entity<User>().Property(u => u.SessionVersion).HasDefaultValue(1);

        b.Entity<TravelPackage>().HasIndex(p => p.Code).IsUnique();
        b.Entity<TravelPackage>().Property(p => p.Revision).IsConcurrencyToken().HasDefaultValue(1);
        b.Entity<Booking>().Property(p => p.Revision).IsConcurrencyToken().HasDefaultValue(1);

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
