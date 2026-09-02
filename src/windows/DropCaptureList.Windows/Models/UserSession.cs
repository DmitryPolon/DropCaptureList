namespace DropCaptureList.Windows.Models;

public sealed class UserSession
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public bool IsAppAdmin { get; set; }
}

public sealed class AdminUserRow
{
    public Guid UserId { get; set; }
    public string LoginName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsAppAdmin { get; set; }
    public string Households { get; set; } = string.Empty;
}

public sealed class AdminSnapshot
{
    public const string WebAppUrl = "https://droplist.azpcloud.com";
    public const long FreeStorageBytes = 32L * 1024 * 1024 * 1024;
    public const double FreeVCoreSeconds = 100_000;

    public long DataUsedBytes { get; init; }
    public double DataUsedPercent => FreeStorageBytes <= 0 ? 0 : Math.Min(100, DataUsedBytes * 100.0 / FreeStorageBytes);
    public double? VCoreRemaining { get; init; }
    public string? VCoreError { get; init; }
    public DateTimeOffset? LastClearedAt { get; init; }
    public string? LastClearedHousehold { get; init; }
    public bool LastClearedIsApproximate { get; init; }
}
