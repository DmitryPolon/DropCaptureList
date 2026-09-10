using System.IO;
using Microsoft.Extensions.Configuration;

namespace DropCaptureList.Windows.Services;

public static class AppConfiguration
{
    public static string? LoadApiBase()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Local.json", optional: true);

        return builder.Build()["ApiBase"];
    }
}
