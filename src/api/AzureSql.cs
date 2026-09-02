using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient;

namespace DropCaptureList.Api;

public sealed class AzureSql
{
    private static readonly TokenRequestContext TokenRequest = new(["https://database.windows.net/.default"]);
    private readonly SqlSettings _settings;
    private readonly string _authRecordPath;
    private readonly object _gate = new();
    private TokenCredential? _credential;
    private AccessToken _cached;

    public AzureSql(SqlSettings settings)
    {
        _settings = settings;
        _authRecordPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DropCaptureList",
            "entra.bin");
    }

    public SqlConnection Open()
    {
        if (!_settings.IsConfigured)
        {
            throw new InvalidOperationException(
                "SQL is not configured. Copy appsettings.Local.json.example to appsettings.Local.json next to the API project.");
        }

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

    private TokenCredential CreateCredential()
    {
        if (RunningInAzure())
        {
            return new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                TenantId = string.IsNullOrWhiteSpace(_settings.TenantId) ? null : _settings.TenantId,
                ExcludeInteractiveBrowserCredential = true
            });
        }

        var options = new InteractiveBrowserCredentialOptions
        {
            TokenCachePersistenceOptions = new TokenCachePersistenceOptions
            {
                Name = "DropCaptureList.Sql"
            },
            AdditionallyAllowedTenants = { "*" }
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

        return new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            TenantId = string.IsNullOrWhiteSpace(_settings.TenantId) ? null : _settings.TenantId,
            ExcludeInteractiveBrowserCredential = true
        });
    }

    private static bool RunningInAzure()
    {
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"))
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IDENTITY_ENDPOINT"));
    }
}
