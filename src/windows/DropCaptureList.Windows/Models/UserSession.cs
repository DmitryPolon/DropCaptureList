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
