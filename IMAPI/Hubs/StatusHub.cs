using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace IMAPI.Api.Hubs;

[Authorize]
public class StatusHub : Hub
{
    // İstersen mobil/web istemci bağlantıda bu gruba girsin:
    public async Task JoinBoat(Guid boatId) =>
        await Groups.AddToGroupAsync(Context.ConnectionId, $"boat-{boatId}");

    public async Task LeaveBoat(Guid boatId) =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"boat-{boatId}");
}
