namespace ProphetOps.Domain;

public class Booking
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public DateOnly BookingDate { get; set; }
    public int PassengerCount { get; set; }
    public string Client { get; set; } = "";
    public int? TravelPackageId { get; set; }
    public TravelPackage? TravelPackage { get; set; }
    public string PackageName { get; set; } = "";
    public string? PackageCode { get; set; }
    public string EntryType { get; set; } = "Package preset";
    public string Destination { get; set; } = "";
    public int GrossRevenue { get; set; }
    public string PaymentStatus { get; set; } = "Pending";
    public string BookingStatus { get; set; } = "Pending";
    public string? StaffAssigned { get; set; }
    public string Source { get; set; } = "Manual quotation";
    public string? Notes { get; set; }

    public DateTime? VoidedAt { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }

    public bool IsVoided => VoidedAt is not null;
}
