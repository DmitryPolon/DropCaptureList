namespace DropCaptureList.Windows.Services;

public sealed class SqlSettings
{
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Server) && !string.IsNullOrWhiteSpace(Database);

    public string? DatabaseResourceId
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SubscriptionId)
                || string.IsNullOrWhiteSpace(ResourceGroup)
                || string.IsNullOrWhiteSpace(Server)
                || string.IsNullOrWhiteSpace(Database))
            {
                return null;
            }

            var server = Server
                .Replace(".database.windows.net", "", StringComparison.OrdinalIgnoreCase)
                .Trim();
            return $"/subscriptions/{SubscriptionId.Trim()}/resourceGroups/{ResourceGroup.Trim()}/providers/Microsoft.Sql/servers/{server}/databases/{Database.Trim()}";
        }
    }
}
