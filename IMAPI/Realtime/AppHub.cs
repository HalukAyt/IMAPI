namespace IMAPI.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

[Authorize]
public class AppHub : Hub
{
    // App tarafına anlık bildirimler (komut sonucu, telemetry vs.)
}