using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IMAPI.Api.Data;
using IMAPI.Api.Entities;

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

    // ==============================================================
    // 1️⃣  Cihaz komut çekiyor → /api/device-link/poll
    // ==============================================================
    public record PollReq(string Serial, int? Max);

    [HttpPost("poll")]
    [AllowAnonymous]
    public async Task<IActionResult> Poll([FromBody] PollReq req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Serial))
            return BadRequest(new { error = "Serial required" });

        var now = DateTime.UtcNow;
        var max = Math.Clamp(req.Max ?? 8, 1, 32);

        // Henüz gönderilmemiş veya acknowledged olmayan, süresi geçmemiş komutları getir
        var cmds = await _db.PendingCommands
            .Where(x => x.DeviceSerial == req.Serial &&
                        (x.Status == "queued" || x.Status == "delivered") &&
                        x.ExpiresAt > now)
            .OrderBy(x => x.CreatedAt)
            .Take(max)
            .Select(x => new { x.Id, x.Payload })
            .ToListAsync(ct);

        if (cmds.Count == 0)
        {
            _logger.LogDebug("No pending commands for {serial}", req.Serial);
            return Ok(new { commands = Array.Empty<object>() });
        }

        // Her komutu “delivered” olarak işaretle
        foreach (var c in cmds)
        {
            var pc = await _db.PendingCommands.FirstAsync(x => x.Id == c.Id, ct);
            pc.Status = "delivered";
            pc.DeliveredAt = now;
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Delivered {count} command(s) to {serial}", cmds.Count, req.Serial);

        // Cihaza sade JSON döndür
        var commandList = cmds
            .Select(c => JsonSerializer.Deserialize<JsonElement>(c.Payload))
            .ToList();

        return Ok(commandList);
    }

    // ==============================================================
    // 2️⃣  Cihaz komutu işledikten sonra → /api/device-link/ack
    // ==============================================================
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

        pc.Status = req.Ok ? "acknowledged" : "failed";
        pc.DeliveredAt = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(req.Reason))
            _logger.LogWarning("ACK reason from {serial}: {reason}", req.Serial, req.Reason);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("ACK from {serial} for {id} (ok={ok})", req.Serial, req.Id, req.Ok);
        return Ok(new { ok = true });
    }
}
