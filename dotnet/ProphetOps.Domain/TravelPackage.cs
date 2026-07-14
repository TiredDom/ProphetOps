namespace ProphetOps.Domain;

public class TravelPackage
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string PackageName { get; set; } = "";
    public string Destination { get; set; } = "";
    public string? Duration { get; set; }
    public int BasePrice { get; set; }
    public string? Inclusions { get; set; }
    public int AvailableSlots { get; set; }
    public int SoldCount { get; set; }
    public int ReservedCount { get; set; }
    public string Status { get; set; } = "Normal";
    public DateOnly? LastUpdatedAt { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
