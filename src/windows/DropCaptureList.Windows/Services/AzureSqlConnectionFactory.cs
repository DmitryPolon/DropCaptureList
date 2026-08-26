using System.IO;
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
    private readonly SqlSettings _settings;
    private readonly string _authRecordPath;
    private readonly object _gate = new();
    private InteractiveBrowserCredential? _credential;
    private AccessToken _cached;

    public AzureSqlConnectionFactory(SqlSettings settings, string dataDirectory)
    {
        _settings = settings;
        _authRecordPath = System.IO.Path.Combine(dataDirectory, "entra.bin");
    }

    public SqlConnection Open()
    {
        var connection = new SqlConnection(
            $"Server={_settings.Server};Database={_settings.Database};Encrypt=True;TrustServerCertificate=False;");
        connection.AccessToken = CurrentToken();
        connection.Open();
        return connection;
    }

    public void ClearPersistedLogin()
    {
        lock (_gate)
        {
            _credential = null;
            _cached = default;
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
