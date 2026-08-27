using Microsoft.AspNetCore.SignalR;

namespace DropCaptureList.Api;

public static class HouseholdGroups
{
    public static string Name(string household) =>
        "household:" + household.Trim().ToLowerInvariant();
}

public sealed class ListHub(AppDirectory directory) : Hub
{
    public async Task JoinHousehold(string email, string household)
    {
        directory.SignIn(email, household);
        await Groups.AddToGroupAsync(Context.ConnectionId, HouseholdGroups.Name(household));
    }
}

public sealed class ListRealtime(IHubContext<ListHub> hubs)
{
    public Task NotifyAsync(string household) =>
        hubs.Clients.Group(HouseholdGroups.Name(household)).SendAsync("listChanged");
}
