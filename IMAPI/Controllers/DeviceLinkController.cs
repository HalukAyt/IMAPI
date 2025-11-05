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

        var items = await _db.PendingCommands
            .Where(x => x.DeviceSerial == req.Serial
                        && x.ExpiresAt > now
                        && x.Status != "delivered")
            .OrderBy(x => x.CreatedAt)
            .Take(max)
            .ToListAsync(ct);

        if (items.Count == 0)
            return Ok(new { commands = Array.Empty<object>() });

        // queued -> sent_http (HTTP ile servis edildiğini işaretle)
        foreach (var pc in items)
        {
            pc.SentAt ??= now;
            if (pc.Status == "queued") pc.Status = "sent_http";
        }
        await _db.SaveChangesAsync(ct);

        // DÖNÜŞ: her komutta id + payload birlikte
        var commands = new List<object>(items.Count);
        foreach (var pc in items)
        {
            try
            {
                using var doc = JsonDocument.Parse(pc.Payload); // payload JSON ise
                                                                // payload zaten bir obje ise düz gövdeyi döndür (id ekleyerek)
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    // payload objesini alıp "id" alanını inject edelim
                    var dict = new Dictionary<string, object?>();
                    foreach (var p in doc.RootElement.EnumerateObject())
                        dict[p.Name] = p.Value.ValueKind == JsonValueKind.Null ? null : JsonSerializer.Deserialize<object>(p.Value.GetRawText());

                    dict["id"] = pc.Id; // <-- KRİTİK
                    commands.Add(dict);
                }
                else
                {
                    // payload obje değilse, sarmala
                    commands.Add(new { id = pc.Id, payload = JsonSerializer.Deserialize<object>(pc.Payload) });
                }
            }
            catch
            {
                // JSON değilse string olarak sarmala
                commands.Add(new { id = pc.Id, payload = pc.Payload });
            }
        }

        return Ok(new { commands });
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
