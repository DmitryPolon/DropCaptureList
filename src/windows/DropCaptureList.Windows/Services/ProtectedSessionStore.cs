using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DropCaptureList.Windows.Models;

namespace DropCaptureList.Windows.Services;

public sealed class ProtectedSessionStore
{
    private readonly string _path;

    public ProtectedSessionStore(string path)
    {
        _path = path;
    }

    public UserSession? Load()
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        try
        {
            var protectedBytes = File.ReadAllBytes(_path);
            var jsonBytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(jsonBytes);
            return JsonSerializer.Deserialize<UserSession>(json);
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Save(UserSession session)
    {
        var directory = System.IO.Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var jsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(session));
        var protectedBytes = ProtectedData.Protect(jsonBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_path, protectedBytes);
    }

    public void Clear()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}
