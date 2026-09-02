using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Interop;
using Azure.Core;
using Azure.Identity;
using Azure.Identity.Broker;
using Microsoft.Data.SqlClient;

namespace DropCaptureList.Windows.Services;

public sealed class AzureSqlConnectionFactory
{
    private static readonly TokenRequestContext TokenRequest = new(["https://database.windows.net/.default"]);
    private static readonly TokenRequestContext ArmTokenRequest = new(["https://management.azure.com/.default"]);
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly SqlSettings _settings;
    private readonly string _authRecordPath;
    private readonly object _gate = new();
    private InteractiveBrowserCredential? _credential;
    private AccessToken _cached;
    private AccessToken _armCached;

    public AzureSqlConnectionFactory(SqlSettings settings, string dataDirectory)
    {
        _settings = settings;
        _authRecordPath = System.IO.Path.Combine(dataDirectory, "entra.bin");
    }

    public SqlConnection Open()
    {
        var connection = new SqlConnection(
            $"Server={_settings.Server};Database={_settings.Database};Encrypt=True;TrustServerCertificate=False;Pooling=False;");
        connection.AccessToken = CurrentToken();
        connection.Open();
        return connection;
    }

    public (double? Remaining, string? Error) TryReadFreeVCoreSeconds()
    {
        var resourceId = _settings.DatabaseResourceId;
        if (resourceId is null)
        {
            return (null, "Add SubscriptionId and ResourceGroup to appsettings.Local.json to show vCore %.");
        }

        try
        {
            var token = CurrentArmToken();
            var end = DateTimeOffset.UtcNow.UtcDateTime;
            var start = end.AddHours(-24);
            var timespan = $"{start:yyyy-MM-ddTHH:mm:ssZ}/{end:yyyy-MM-ddTHH:mm:ssZ}";
            var url =
                $"https://management.azure.com{resourceId}/providers/microsoft.insights/metrics"
                + "?api-version=2018-01-01&metricnames=free_amount_remaining"
                + $"&timespan={timespan}&interval=PT1H&aggregation=Maximum";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            using var response = Http.Send(request);
            var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode is System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.Unauthorized)
                {
                    return (null, "vCore % needs Monitoring Reader on the SQL database (same Entra account).");
                }

                return (null, $"vCore % unavailable ({(int)response.StatusCode}).");
            }

            using var doc = System.Text.Json.JsonDocument.Parse(json);
            double? last = null;
            if (!doc.RootElement.TryGetProperty("value", out var metrics) || metrics.GetArrayLength() == 0)
            {
                return (null, "vCore metric not returned yet.");
            }

            if (!metrics[0].TryGetProperty("timeseries", out var series) || series.GetArrayLength() == 0)
            {
                return (null, "No vCore samples in the last day (database may have been paused).");
            }

            foreach (var point in series[0].GetProperty("data").EnumerateArray())
            {
                if (point.TryGetProperty("maximum", out var max) && max.ValueKind is System.Text.Json.JsonValueKind.Number)
                {
                    last = max.GetDouble();
                }
            }

            return last is null ? (null, "No vCore samples in the last few hours (database may have been paused).") : (last, null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    private string CurrentArmToken()
    {
        lock (_gate)
        {
            if (!string.IsNullOrEmpty(_armCached.Token) && _armCached.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(3))
            {
                return _armCached.Token;
            }

            var credential = _credential ??= CreateCredential();
            _armCached = credential.GetToken(ArmTokenRequest, CancellationToken.None);
            return _armCached.Token;
        }
    }

    public void ClearPersistedLogin()
    {
        lock (_gate)
        {
            _credential = null;
            _cached = default;
            _armCached = default;
            if (File.Exists(_authRecordPath))
            {
                File.Delete(_authRecordPath);
            }
        }
    }

    private string CurrentToken()
    {
        lock (_gate)
        {
            if (!string.IsNullOrEmpty(_cached.Token) && _cached.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(3))
            {
                return _cached.Token;
            }

            var credential = _credential ??= CreateCredential();
            _cached = credential.GetToken(TokenRequest, CancellationToken.None);
            return _cached.Token;
        }
    }

    private InteractiveBrowserCredential CreateCredential()
    {
        var options = new InteractiveBrowserCredentialBrokerOptions(ParentWindowHandle())
        {
            TokenCachePersistenceOptions = new TokenCachePersistenceOptions
            {
                Name = "DropCaptureList.Sql"
            },
            AdditionallyAllowedTenants = { "*" },
            // Hotmail / personal Microsoft accounts need this to appear in the Windows picker.
            IsLegacyMsaPassthroughEnabled = true
        };

        if (!string.IsNullOrWhiteSpace(_settings.TenantId))
        {
            options.TenantId = _settings.TenantId;
        }

        if (!string.IsNullOrWhiteSpace(_settings.UserId))
        {
            options.LoginHint = _settings.UserId;
        }

        if (File.Exists(_authRecordPath))
        {
            using var stream = File.OpenRead(_authRecordPath);
            options.AuthenticationRecord = AuthenticationRecord.Deserialize(stream);
            return new InteractiveBrowserCredential(options);
        }

        var credential = new InteractiveBrowserCredential(options);
        var record = credential.Authenticate(TokenRequest);
        var directory = System.IO.Path.GetDirectoryName(_authRecordPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using (var stream = File.Create(_authRecordPath))
        {
            record.Serialize(stream);
        }

        return credential;
    }

    private static IntPtr ParentWindowHandle()
    {
        var window = Application.Current?.MainWindow;
        if (window is null)
        {
            return IntPtr.Zero;
        }

        return new WindowInteropHelper(window).EnsureHandle();
    }
}
