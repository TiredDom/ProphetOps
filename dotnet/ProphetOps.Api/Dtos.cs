namespace ProphetOps.Api;

public record LoginRequest(string? Email, string? Password);

public record AuthUser(string Name, string Email, string Role, string DefaultPath);
