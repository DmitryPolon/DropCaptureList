namespace DropCaptureList.Api;

public sealed class SqlSettings
{
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Server) && !string.IsNullOrWhiteSpace(Database);
}
