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

        var connection = new SqlConnection(
            $"Server={_settings.Server};Database={_settings.Database};Encrypt=True;TrustServerCertificate=False;Pooling=False;");
        connection.AccessToken = CurrentToken();
        connection.Open();
        return connection;
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
