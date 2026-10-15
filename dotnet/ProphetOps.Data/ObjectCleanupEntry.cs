namespace ProphetOps.Data;

public sealed class ObjectCleanupEntry
{
    public int Id { get; set; }
    public string ObjectKey { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime EligibleAtUtc { get; set; }
    public string Reason { get; set; } = "";
    public int Attempts { get; set; }
    public string? LastError { get; set; }

    public static ObjectCleanupEntry Create(string objectKey, DateTimeOffset eligibleAtUtc, string reason) => new()
    {
        ObjectKey = objectKey,
        CreatedAtUtc = DateTime.UtcNow,
        EligibleAtUtc = eligibleAtUtc.UtcDateTime,
        Reason = reason,
    };
}

public static class ObjectCleanupReasons
{
    public const string PackageImageReplaced = "package_image_replaced";
    public const string PackageImageDeleted = "package_image_deleted";
    public const string PackageImageUploadRolledBack = "package_image_upload_rolled_back";
}
