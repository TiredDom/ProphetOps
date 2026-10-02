using ProphetOps.Api;
using ProphetOps.Data;
using Xunit;

namespace ProphetOps.Api.Tests;

public sealed class DatabaseStartupPlanTests
{
    [Fact]
    public void Web_startup_keeps_sqlite_development_automation()
    {
        var plan = DatabaseStartupPlan.ForWeb(DatabaseProviderKind.Sqlite);

        Assert.True(plan.Migrate);
        Assert.True(plan.AllowAutomaticOwnerBootstrap);
        Assert.True(plan.AllowDemoSeed);
        Assert.False(plan.ValidateSchemaOnly);
    }

    [Fact]
    public void Web_startup_does_not_mutate_postgres_schema_or_seed_data()
    {
        var plan = DatabaseStartupPlan.ForWeb(DatabaseProviderKind.Postgres);

        Assert.False(plan.Migrate);
        Assert.False(plan.AllowAutomaticOwnerBootstrap);
        Assert.False(plan.AllowDemoSeed);
        Assert.True(plan.ValidateSchemaOnly);
    }
}
