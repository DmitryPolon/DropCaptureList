using DropCaptureList.Windows.Models;

namespace DropCaptureList.Windows.Services;

public interface ICaptureStore
{
    LocalDatabase Load();
    void Save(LocalDatabase database);
}
