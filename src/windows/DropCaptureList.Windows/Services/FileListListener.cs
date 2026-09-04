using Microsoft.AspNetCore.SignalR.Client;

namespace DropCaptureList.Windows.Services;

public sealed class FileListListener : IAsyncDisposable
{
    private HubConnection? _connection;

    public async Task Start(string apiBase, string household, Action onChanged)
    {
        await Stop();
        household = household.Trim();
        if (string.IsNullOrWhiteSpace(apiBase) || string.IsNullOrWhiteSpace(household))
        {
            return;
        }

        var hub = new HubConnectionBuilder()
            .WithUrl($"{apiBase.TrimEnd('/')}/hubs/list")
            .WithAutomaticReconnect()
            .Build();
        hub.On("listChanged", onChanged);
        hub.Reconnected += async _ =>
        {
            await hub.InvokeAsync("Join", household);
            onChanged();
        };
        await hub.StartAsync();
        await hub.InvokeAsync("Join", household);
        _connection = hub;
    }

    public async Task Stop()
    {
        if (_connection is null)
        {
            return;
        }

        var hub = _connection;
        _connection = null;
        await hub.DisposeAsync();
    }

    public ValueTask DisposeAsync() => new(Stop());
}
