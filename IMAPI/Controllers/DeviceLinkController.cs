using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IMAPI.Api.Data;

namespace IMAPI.Api.Controllers;

[ApiController]
[Route("api/device-link")]
public class DeviceLinkController : ControllerBase
{
    private readonly ItechMarineDbContext _db;
    private readonly ILogger<DeviceLinkController> _logger;

    public DeviceLinkController(ItechMarineDbContext db, ILogger<DeviceLinkController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ------------------------------------------------------------
    // 1) Cihaz komut çekiyor → /api/device-link/poll
    // ------------------------------------------------------------
    public record PollReq(string Serial, int? Max);

    [HttpPost("poll")]
    [AllowAnonymous]
    public async Task<IActionResult> Poll([FromBody] PollReq req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Serial))
            return BadRequest(new { error = "Serial required" });

        var now = DateTime.UtcNow;
        var max = Math.Clamp(req.Max ?? 8, 1, 32);

        // delivered HARİÇ her şeyi ver (queued + sent + sent_http), süresi geçmemiş olanlar
        var items = await _db.PendingCommands
            .Where(x => x.DeviceSerial == req.Serial
                        && x.ExpiresAt > now
                        && x.Status != "delivered")
            .OrderBy(x => x.CreatedAt)
            .Take(max)
            .ToListAsync(ct);

        if (items.Count == 0)
        {
            _logger.LogDebug("No pending commands for {serial}", req.Serial);
            return Ok(new { commands = Array.Empty<object>() });
        }

        // HTTP ile servis ettiklerimizi işaretle: queued → sent_http, SentAt now
        foreach (var pc in items)
        {
            if (pc.SentAt == null) pc.SentAt = now;
            if (pc.Status == "queued") pc.Status = "sent_http"; // MQTT ile gitmişse zaten "sent" olarak kalır
        }
        await _db.SaveChangesAsync(ct);

        // Cihaza sade Payload listesi döndür (JSON olarak)
        var commandList = new List<JsonElement>(items.Count);
        foreach (var pc in items)
        {
            try
            {
                // Payload sütunu string/Jsonb ise aşağıdaki deserialize çalışır
                var el = JsonSerializer.Deserialize<JsonElement>(pc.Payload);
                commandList.Add(el);
            }
            catch
            {
                // sorunlu payload'ı atla
            }
        }

        _logger.LogInformation("Served {count} command(s) to {serial}", commandList.Count, req.Serial);
        return Ok(new { commands = commandList });
    }

    // ------------------------------------------------------------
    // 2) Cihaz komutu işledi → /api/device-link/ack
    // ------------------------------------------------------------
    public record AckReq(string Serial, string Id, bool Ok, string? Reason);

    [HttpPost("ack")]
    [AllowAnonymous]
    public async Task<IActionResult> Ack([FromBody] AckReq req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Serial) || string.IsNullOrWhiteSpace(req.Id))
            return BadRequest(new { error = "Serial and Id required" });

        if (!Guid.TryParse(req.Id, out var cmdId))
            return BadRequest(new { error = "Invalid Id format" });

        var pc = await _db.PendingCommands
            .FirstOrDefaultAsync(x => x.Id == cmdId && x.DeviceSerial == req.Serial, ct);

        if (pc is null)
            return NotFound(new { error = "Command not found" });

        pc.DeliveredAt = DateTime.UtcNow;
        pc.Status = req.Ok ? "delivered" : "failed";

        if (!string.IsNullOrWhiteSpace(req.Reason))
            _logger.LogWarning("ACK reason from {serial}: {reason}", req.Serial, req.Reason);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("ACK from {serial} for {id} (ok={ok})", req.Serial, req.Id, req.Ok);
        return Ok(new { ok = true });
    }
}
