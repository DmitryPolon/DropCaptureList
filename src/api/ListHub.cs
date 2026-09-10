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

    public ListNotifier(IHubContext<ListHub> hubs)
    {
        _hubs = hubs;
    }

    public Task ListChanged(string household)
    {
        household = household.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(household)
            ? Task.CompletedTask
            : _hubs.Clients.Group(household).SendAsync("listChanged");
    }
}
