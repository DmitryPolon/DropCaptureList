using System.IO;
using System.Text.Json;
using DropCaptureList.Windows.Models;

namespace DropCaptureList.Windows.Services;

public sealed class JsonFileCaptureStore : ICaptureStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    private readonly string _path;
    private readonly object _gate = new();

    public JsonFileCaptureStore(string path)
    {
        _path = path;
    }

    public LocalDatabase Load()
    {
        lock (_gate)
        {
            if (!File.Exists(_path))
            {
                return new LocalDatabase();
            }

            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<LocalDatabase>(json, Options) ?? new LocalDatabase();
        }
    }

    public void Save(LocalDatabase database)
    {
        lock (_gate)
        {
            var directory = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(database, Options);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, _path, overwrite: true);
        }
    }
}
