using Microsoft.AspNetCore.SignalR;

namespace DropCaptureList.Api;

public sealed class ListHub : Hub
{
    private readonly FileDirectory _files;

    public ListHub(FileDirectory files)
    {
        _files = files;
    }

    public Task Join(string email, string household, string? pin)
    {
        try
        {
            _files.SignIn(email, household, pin);
        }
        catch (InvalidOperationException)
        {
            return Task.CompletedTask;
        }

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
