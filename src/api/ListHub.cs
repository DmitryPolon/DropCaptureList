using Microsoft.AspNetCore.SignalR;

namespace DropCaptureList.Api;

public sealed class ListHub : Hub
{
    public Task Join(string household)
    {
        household = household.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(household)
            ? Task.CompletedTask
            : Groups.AddToGroupAsync(Context.ConnectionId, household);
    }
}

public sealed class ListNotifier
{
    private readonly IHubContext<ListHub> _hubs;
    private readonly StorageMode _mode;

    public ListNotifier(IHubContext<ListHub> hubs, StorageMode mode)
    {
        _hubs = hubs;
        _mode = mode;
    }

    public Task ListChanged(string household)
    {
        if (!_mode.IsFile)
        {
            return Task.CompletedTask;
        }

        household = household.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(household)
            ? Task.CompletedTask
            : _hubs.Clients.Group(household).SendAsync("listChanged");
    }
}
