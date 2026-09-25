using ProphetOps.Data;

namespace ProphetOps.Api;

public sealed class BookingMutationService(AppDbContext db, IBusinessClock clock)
{
    public void Reserve(int? packageId, int passengers)
    {
        if (packageId is not int id) return;
        var package = db.TravelPackages.Find(id) ?? throw new MutationConflictException();
        if (passengers < 1 || package.AvailableSlots < passengers || package.SoldCount < 0
            || (long)package.SoldCount + passengers > int.MaxValue) throw new MutationConflictException();
        package.AvailableSlots -= passengers;
        package.SoldCount += passengers;
        package.LastUpdatedAt = clock.Today;
    }

    public void Release(int? packageId, int passengers)
    {
        if (packageId is not int id) return;
        var package = db.TravelPackages.Find(id) ?? throw new MutationConflictException();
        if (passengers < 1 || package.SoldCount < passengers || package.AvailableSlots < 0
            || (long)package.AvailableSlots + passengers > int.MaxValue) throw new MutationConflictException();
        package.AvailableSlots += passengers;
        package.SoldCount -= passengers;
        package.LastUpdatedAt = clock.Today;
    }
}
