namespace ProphetOps.Domain;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "";
    public string Status { get; set; } = "Active";
    public int SessionVersion { get; set; } = 1;
    public DateTime? LastLoginAt { get; set; }
}
