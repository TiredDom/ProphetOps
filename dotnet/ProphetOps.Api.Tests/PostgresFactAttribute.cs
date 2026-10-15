using Xunit;

namespace ProphetOps.Api.Tests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        var configured = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PROPHETOPS_TEST_POSTGRES"))
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("POSTGRES_TEST_CONNECTION"));
        var required = string.Equals(
            Environment.GetEnvironmentVariable("PROPHETOPS_REQUIRE_POSTGRES"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (!configured && !required)
            Skip = "PostgreSQL fixture is not configured; set PROPHETOPS_TEST_POSTGRES for provider tests.";
    }
}
