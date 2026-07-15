namespace ProphetOps.Api;

public record LoginRequest(string? Email, string? Password);

public record AuthUser(string Name, string Email, string Role, string DefaultPath);

public record BookingRequest(
    string? Id,
    string? Ds,
    int Y,
    string? Client,
    string? PackageId,
    string? EntryType,
    string? Package,
    string? Destination,
    int GrossRevenue,
    string? PaymentStatus,
    string? BookingStatus,
    string? StaffAssigned,
    string? Source,
    string? Notes);

public record BulkRequest(string[]? Ids, string? Action);
