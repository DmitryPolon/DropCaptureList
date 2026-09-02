using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Interop;
using Azure.Core;
using Azure.Identity;
using Azure.Identity.Broker;
using DropCaptureList.Windows.Models;
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
        const string extras = "Encrypt=True;TrustServerCertificate=False;Pooling=False;Connect Timeout=90;ConnectRetryCount=6;ConnectRetryInterval=10;";
        SqlException? last = null;
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var connection = new SqlConnection($"Server={_settings.Server};Database={_settings.Database};{extras}");
            try
            {
                connection.AccessToken = CurrentToken();
                connection.Open();
                return connection;
            }
            catch (SqlException ex) when (IsResumeWait(ex) && attempt < 7)
            {
                last = ex;
                connection.Dispose();
                Thread.Sleep(TimeSpan.FromSeconds(5 + attempt * 3));
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        throw last!;
    }

    public (double? Remaining, DateTimeOffset? SampledAt, string? Error) TryReadFreeVCoreSeconds()
    {
        var resourceId = _settings.DatabaseResourceId;
        if (resourceId is null)
        {
            return (null, null, "Add SubscriptionId and ResourceGroup to appsettings.Local.json to show vCore %.");
        }

        try
        {
            var token = CurrentArmToken();
            var end = DateTimeOffset.UtcNow.UtcDateTime;
            var start = end.AddHours(-24);
            var timespan = $"{start:yyyy-MM-ddTHH:mm:ssZ}/{end:yyyy-MM-ddTHH:mm:ssZ}";
            var url =
                $"https://management.azure.com{resourceId}/providers/microsoft.insights/metrics"
                + "?api-version=2018-01-01&metricnames=free_amount_consumed,free_amount_remaining"
                + $"&timespan={timespan}&interval=PT1H&aggregation=Average,Minimum,Maximum";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            using var response = Http.Send(request);
            var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode is System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.Unauthorized)
                {
                    return (null, null, "vCore % needs Monitoring Reader on the SQL database (same Entra account).");
                }

                return (null, null, $"vCore % unavailable ({(int)response.StatusCode}).");
            }

            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("value", out var metrics) || metrics.GetArrayLength() == 0)
            {
                return (null, null, "vCore metric not returned yet.");
            }

            (double Value, DateTimeOffset At)? consumed = null;
            (double Value, DateTimeOffset At)? remaining = null;
            foreach (var metric in metrics.EnumerateArray())
            {
                var name = metric.GetProperty("name").GetProperty("value").GetString();
                var sample = LatestGauge(metric, consumed: name == "free_amount_consumed");
                if (sample is null)
                {
                    continue;
                }

                if (name == "free_amount_consumed")
                {
                    consumed = sample;
                }
                else if (name == "free_amount_remaining")
                {
                    remaining = sample;
                }
            }

            if (consumed is { } used)
            {
                var left = Math.Max(0, AdminSnapshot.FreeVCoreSeconds - used.Value);
                return (left, used.At, null);
            }

            if (remaining is { } leftSample)
            {
                return (leftSample.Value, leftSample.At, null);
            }

            return (null, null, "No vCore samples in the last day (database may have been paused).");
        }
        catch (Exception ex)
        {
            return (null, null, ex.Message);
        }
    }

    private static (double Value, DateTimeOffset At)? LatestGauge(System.Text.Json.JsonElement metric, bool consumed)
    {
        if (!metric.TryGetProperty("timeseries", out var series) || series.GetArrayLength() == 0)
        {
            return null;
        }

        DateTimeOffset? bestAt = null;
        double? best = null;
        foreach (var point in series[0].GetProperty("data").EnumerateArray())
        {
            if (!point.TryGetProperty("timeStamp", out var stampEl))
            {
                continue;
            }

            var stampText = stampEl.GetString();
            if (string.IsNullOrWhiteSpace(stampText) || !DateTimeOffset.TryParse(stampText, out var at))
            {
                continue;
            }

            var value = consumed ? GaugeConsumed(point) : GaugeRemaining(point);
            if (value is null)
            {
                continue;
            }

            if (bestAt is null || at >= bestAt)
            {
                bestAt = at;
                best = value;
            }
        }

        return best is { } v && bestAt is { } t ? (v, t) : null;
    }

    private static double? GaugeConsumed(System.Text.Json.JsonElement point)
    {
        return TryNumber(point, "maximum") ?? TryNumber(point, "average") ?? TryNumber(point, "minimum");
    }

    private static double? GaugeRemaining(System.Text.Json.JsonElement point)
    {
        return TryNumber(point, "minimum") ?? TryNumber(point, "average") ?? TryNumber(point, "maximum");
    }

    private static double? TryNumber(System.Text.Json.JsonElement point, string name)
    {
        return point.TryGetProperty(name, out var el) && el.ValueKind is System.Text.Json.JsonValueKind.Number
            ? el.GetDouble()
            : null;
    }

    private static bool IsResumeWait(SqlException ex)
    {
        foreach (SqlError error in ex.Errors)
        {
            if (error.Number is 40613 or 40197 or 40501 or 49918 or 49919 or 49920 or 4221
                or 10928 or 10929 or 4060 or 10060 or 10054 or 64 or 233 or -2)
            {
                return true;
            }
        }

        return ex.Number is 40613 or -2;
    }

    private string CurrentArmToken()
    {
        lock (_gate)
        {
            if (!string.IsNullOrEmpty(_armCached.Token) && _armCached.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(3))
            {
                return _armCached.Token;
            }
        }

        return OnUi(() =>
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
        });
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
        }

        return OnUi(() =>
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
        });
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
        return OnUi(() =>
        {
            var window = Application.Current?.MainWindow;
            return window is null ? IntPtr.Zero : new WindowInteropHelper(window).EnsureHandle();
        });
    }

    private static T OnUi<T>(Func<T> action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            return action();
        }

        return dispatcher.Invoke(action);
    }
}
