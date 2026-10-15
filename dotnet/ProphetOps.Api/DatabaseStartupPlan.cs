using ProphetOps.Data;

namespace ProphetOps.Api;

public sealed record DatabaseStartupPlan(
    bool Migrate,
    bool AllowAutomaticOwnerBootstrap,
    bool AllowDemoSeed,
    bool ValidateSchemaOnly)
{
    public static DatabaseStartupPlan ForWeb(DatabaseProviderKind provider) =>
        provider == DatabaseProviderKind.Postgres
            ? new DatabaseStartupPlan(
                Migrate: false,
                AllowAutomaticOwnerBootstrap: false,
                AllowDemoSeed: false,
                ValidateSchemaOnly: true)
            : new DatabaseStartupPlan(
                Migrate: true,
                AllowAutomaticOwnerBootstrap: true,
                AllowDemoSeed: true,
                ValidateSchemaOnly: false);
}
