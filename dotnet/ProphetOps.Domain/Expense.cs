namespace ProphetOps.Domain;

public class Expense
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public DateOnly ExpenseDate { get; set; }
    public string Category { get; set; } = "";
    public int Amount { get; set; }
    public string RelatedPackage { get; set; } = "";
    public string PaymentStatus { get; set; } = "Pending";
    public string? Notes { get; set; }

    public DateTime? VoidedAt { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }

    public bool IsVoided => VoidedAt is not null;
}
