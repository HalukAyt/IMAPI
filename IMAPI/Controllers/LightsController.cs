using System.Security.Claims;
using IMAPI.Api.Data;
using IMAPI.Api.DTOs;
using IMAPI.Api.Entities;
using IMAPI.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMAPI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/devices/{deviceId:guid}/lights")]
public class LightsController : ControllerBase
{
    private readonly ItechMarineDbContext _db;
    private readonly IMqttBridge _mqtt;

    public LightsController(ItechMarineDbContext db, IMqttBridge mqtt)
    { _db = db; _mqtt = mqtt; }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue("uid")!);

    [HttpGet]
    public async Task<ActionResult<List<LightResponse>>> List(Guid deviceId, CancellationToken ct)
    {
        var uid = GetUserId();
        var dev = await _db.Devices.Include(d => d.Boat).FirstOrDefaultAsync(d => d.Id == deviceId, ct);
        if (dev is null || dev.Boat.OwnerId != uid) return NotFound();

        var lights = await _db.LightChannels
            .Where(l => l.DeviceId == deviceId)
            .OrderBy(l => l.ChNo)
            .Select(l => new LightResponse(l.Id, l.ChNo, l.Name, l.IsOn, l.UpdatedAt))
            .ToListAsync(ct);

        return Ok(lights);
    }

    [HttpPatch("{ch:int}")]
    public async Task<IActionResult> Set(Guid deviceId, int ch, [FromBody] LightSetRequest req, CancellationToken ct)
    {
        if (ch < 1 || ch > 32) return BadRequest("invalid channel");

        var uid = GetUserId();
        var dev = await _db.Devices.Include(d => d.Boat).FirstOrDefaultAsync(d => d.Id == deviceId, ct);
        if (dev is null || dev.Boat.OwnerId != uid) return NotFound();

        var light = await _db.LightChannels.FirstOrDefaultAsync(l => l.DeviceId == deviceId && l.ChNo == ch, ct);
        if (light is null) return NotFound();

        int newState;
        if (req.Toggle == true)
            newState = light.IsOn ? 0 : 1;
        else if (req.State is int s && (s == 0 || s == 1))
            newState = s;
        else
            return BadRequest("Provide { state:0|1 } or { toggle:true }");

        // --- MQTT + HTTP fallback kuyruğu ---
        var cmdId = Guid.NewGuid().ToString("N");
        var payloadObj = new
        {
            id = cmdId,
            type = "light",
            ch = ch,
            state = newState,
            source = "app",
            expiry = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds()
        };
        var payloadJson = System.Text.Json.JsonSerializer.Serialize(payloadObj);

        // 1) Önce kuyrukla
        _db.PendingCommands.Add(new PendingCommand
        {
            DeviceId = dev.Id,
            DeviceSerial = dev.Serial,
            Payload = payloadJson,
            Status = "queued",
            ExpiresAt = DateTime.UtcNow.AddMinutes(2)
        });
        await _db.SaveChangesAsync(ct);

        // 2) MQTT dene (başarırsa 'sent' olarak işaretle)
        try
        {
            await _mqtt.PublishCommandAsync(dev.Serial, payloadObj, ct);

            var pc = await _db.PendingCommands
                .OrderByDescending(x => x.CreatedAt)
                .FirstAsync(x => x.DeviceSerial == dev.Serial && x.Payload == payloadJson, ct);

            pc.Status = "sent";
            pc.SentAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            // MQTT yoksa sessiz: cihaz HTTP ile poll edip bu komutu alacak
        }

        // 3) Optimistic update (ACK gelince yine güncellenecek)
        light.IsOn = newState == 1;
        light.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new { id = cmdId, queued = true, ch, state = newState });
    }

}
