using System.Text.Json;

namespace DropCaptureList.Api;

public enum StorageKind
{
    Azure,
    File
}

public sealed class StorageMode
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    private readonly string _path;
    private readonly object _gate = new();
    private StorageKind _kind = StorageKind.Azure;

    public StorageMode(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _path = Path.Combine(dataDirectory, "mode.json");
        if (File.Exists(_path))
        {
            var doc = JsonSerializer.Deserialize<ModeFile>(File.ReadAllText(_path), Json);
            if (doc is { Mode: "File" })
            {
                _kind = StorageKind.File;
            }
        }
    }

    public StorageKind Kind
    {
        get
        {
            lock (_gate)
            {
                return _kind;
            }
        }
    }

    public bool IsFile => Kind == StorageKind.File;

    public void Set(StorageKind kind)
    {
        lock (_gate)
        {
            _kind = kind;
            File.WriteAllText(_path, JsonSerializer.Serialize(new ModeFile { Mode = kind.ToString() }, Json));
        }
    }

    private sealed class ModeFile
    {
        public string Mode { get; set; } = "Azure";
    }
}

public static class DataPaths
{
    public static string Resolve(IConfiguration config)
    {
        var configured = config["DataDirectory"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")))
        {
            return "/home/droplist";
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DropCaptureList",
            "file-store");
    }
}
