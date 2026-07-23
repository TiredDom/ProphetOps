namespace ProphetOps.Domain;

public static class Roles
{
    public const string OwnerManagement = "Owner / Management";
    public const string Admin = "Admin";
    public const string Staff = "Staff";

    public static readonly IReadOnlyList<string> All = new[] { OwnerManagement, Admin, Staff };

    private static readonly Dictionary<string, string[]> Permissions = new()
    {
        [OwnerManagement] = new[] { "Dashboard", "Bookings", "Package Catalog", "Expenses", "Analytics", "Forecast", "Reports", "Users" },
        [Admin] = new[] { "Dashboard", "Bookings", "Package Catalog", "Expenses", "Analytics", "Forecast", "Reports" },
        [Staff] = new[] { "Bookings", "Package Catalog" },
    };

    private static readonly Dictionary<string, string> DefaultPaths = new()
    {
        [OwnerManagement] = "/dashboard",
        [Admin] = "/dashboard",
        [Staff] = "/bookings",
    };

    private static readonly Dictionary<string, string> Summaries = new()
    {
        [OwnerManagement] = "Full access, including managing accounts.",
        [Admin] = "Everything except managing accounts.",
        [Staff] = "Bookings and package catalog only.",
    };

    public static bool CanAccess(string? role, string label) =>
        role is not null && Permissions.TryGetValue(role, out var labels) && labels.Contains(label);

    public static string DefaultPathForRole(string? role) =>
        role is not null && DefaultPaths.TryGetValue(role, out var path) ? path : "/login";

    public static string AccessSummary(string? role) =>
        role is not null && Summaries.TryGetValue(role, out var summary) ? summary : "";
}
