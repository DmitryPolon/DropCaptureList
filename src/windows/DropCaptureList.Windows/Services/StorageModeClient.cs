using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace DropCaptureList.Windows.Services;

public sealed class StorageModeClient
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(2) };
    private readonly string? _base;

    public StorageModeClient(string? apiBase)
    {
        _base = string.IsNullOrWhiteSpace(apiBase) ? null : apiBase.TrimEnd('/');
    }

    public string Current { get; private set; } = "Azure";

    public bool IsFile => string.Equals(Current, "File", StringComparison.OrdinalIgnoreCase);

    public bool HasApi => !string.IsNullOrWhiteSpace(_base);

    public event Action? Changed;

    public string Refresh()
    {
        if (_base is null)
        {
            Current = "Azure";
            return Current;
        }

        using var response = _http.GetAsync($"{_base}/api/storage-mode").GetAwaiter().GetResult();
        var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("Could not read Azure / File mode from the API.");
        }

        using var doc = JsonDocument.Parse(body);
        Current = doc.RootElement.TryGetProperty("mode", out var mode) ? mode.GetString() ?? "Azure" : "Azure";
        Changed?.Invoke();
        return Current;
    }

    public void Set(string email, string mode)
    {
        if (_base is null)
        {
            throw new InvalidOperationException("Set ApiBase in appsettings.Local.json to switch Azure / File.");
        }

        var payload = JsonSerializer.Serialize(new { email, mode });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = _http.PostAsync($"{_base}/api/storage-mode", content).GetAwaiter().GetResult();
        var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(Problem(body, "Could not switch Azure / File."));
        }

        using var doc = JsonDocument.Parse(body);
        Current = doc.RootElement.TryGetProperty("mode", out var value) ? value.GetString() ?? mode : mode;
        Changed?.Invoke();
    }

    internal static string Problem(string body, string fallback)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("detail", out var detail) && detail.GetString() is { Length: > 0 } text)
            {
                return text;
            }
        }
        catch (JsonException)
        {
        }

        return fallback;
    }
}
