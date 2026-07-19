namespace ProphetOps.Domain;

public class AuditEntry
{
    public int Id { get; set; }
    public DateTime At { get; set; }

    /// Email of the signed-in user, kept even if the account is later renamed or suspended.
    public string Actor { get; set; } = "";
    public string ActorName { get; set; } = "";

    /// Created, Updated, Voided, Restored, Imported.
    public string Action { get; set; } = "";

    /// Booking, Expense, TravelPackage, User.
    public string EntityType { get; set; } = "";
    public string EntityCode { get; set; } = "";

    /// What changed, in the words the record itself uses.
    public string? Summary { get; set; }
}
