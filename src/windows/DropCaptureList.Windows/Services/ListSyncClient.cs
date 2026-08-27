using System.Net.Http;
using System.Text;
using System.Text.Json;
using DropCaptureList.Windows.Models;

namespace DropCaptureList.Windows.Services;

public sealed class ListSyncClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private readonly string? _apiBase;

    public ListSyncClient(string? apiBase)
    {
        _apiBase = string.IsNullOrWhiteSpace(apiBase) ? null : apiBase.Trim().TrimEnd('/');
    }

    public async Task NotifyAsync(UserSession session)
    {
        if (_apiBase is null || string.IsNullOrWhiteSpace(session.Email) || string.IsNullOrWhiteSpace(session.TenantName))
        {
            return;
        }

        var url = $"{_apiBase}/api/households/{Uri.EscapeDataString(session.TenantName)}/notify";
        var json = JsonSerializer.Serialize(new { email = session.Email, household = session.TenantName });
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await Http.PostAsync(url, content);
        response.EnsureSuccessStatusCode();
    }
}
