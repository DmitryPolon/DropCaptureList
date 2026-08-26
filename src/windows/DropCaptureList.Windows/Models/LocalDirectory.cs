namespace DropCaptureList.Windows.Models;

public sealed class LocalUser
{
    public Guid Id { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsAppAdmin { get; set; }
}

public sealed class LocalTenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class LocalMembership
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
}

public sealed class LocalDatabase
{
    public List<LocalUser> Users { get; set; } = [];
    public List<LocalTenant> Tenants { get; set; } = [];
    public List<LocalMembership> Memberships { get; set; } = [];
    public List<CapturedItem> Items { get; set; } = [];
}
