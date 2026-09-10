using System.Text.Json;

namespace DropCaptureList.Windows.Services;

public sealed class StorageModeClient
{
    public StorageModeClient(string? apiBase)
    {
        HasApi = !string.IsNullOrWhiteSpace(apiBase);
    }

    public string Current => "File";

    public bool IsFile => true;

    public bool HasApi { get; }

    internal static string Problem(string body, string fallback) =>
        Problem(body, statusCode: 0, path: null, fallback);

    internal static string Problem(string body, System.Net.HttpStatusCode statusCode, string path)
    {
        return Problem(body, (int)statusCode, path, "API request failed.");
    }

    private static string Problem(string body, int statusCode, string? path, string fallback)
    {
        var detail = ReadDetail(body);
        if (statusCode == 404 && path is not null && path.StartsWith("/api/members", StringComparison.OrdinalIgnoreCase))
        {
            return "This API build does not have household members yet (404). Deploy the current API, or set ApiBase in appsettings.Local.json to a local dotnet run of src/api.";
        }

        if (detail.Length > 0)
        {
            return detail;
        }

        return statusCode > 0 ? $"{fallback} ({statusCode})." : fallback;
    }

    private static string ReadDetail(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("detail", out var detail) && detail.GetString() is { Length: > 0 } text)
            {
                return text;
            }

            if (doc.RootElement.TryGetProperty("title", out var title) && title.GetString() is { Length: > 0 } heading)
            {
                return heading;
            }
        }
        catch (JsonException)
        {
        }

        return "";
    }
}
