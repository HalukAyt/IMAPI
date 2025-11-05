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

        // Cihazın LastSeen'ini güncelle (HTTP-only cihazlar için online takibi)
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Serial == req.Serial, ct);
        if (device != null)
        {
            device.LastSeen = now;
            await _db.SaveChangesAsync(ct);
        }

        // HTTP hattından servis edilecek komutlar: queued + sent_http (delivered/sent hariç)
        var items = await _db.PendingCommands
            .Where(x => x.DeviceSerial == req.Serial
                        && x.ExpiresAt > now
                        && (x.Status == "queued" || x.Status == "sent_http"))
            .OrderBy(x => x.CreatedAt)
            .Take(max)
            .ToListAsync(ct);

        if (items.Count == 0)
        {
            // Cihaz her ihtimale karşı tek tip cevap bekliyor
            return Ok(new { commands = Array.Empty<object>() });
        }

        // queued → sent_http; SentAt damgası
        foreach (var pc in items)
        {
            pc.SentAt ??= now;
            if (pc.Status == "queued")
                pc.Status = "sent_http";
        }
        await _db.SaveChangesAsync(ct);

        // Cihaza sade bir "commands" listesi döndür.
        // - payload JSON objesi ise direkt gönder
        // - id yoksa { id, payload: <json> } sarmala
        // - parse edilemezse { id, payload: "<raw>" } olarak gönder
        var commandList = new List<JsonElement>(items.Count);

        foreach (var pc in items)
        {
            try
            {
                var el = JsonSerializer.Deserialize<JsonElement>(pc.Payload);

                if (el.ValueKind == JsonValueKind.Object)
                {
                    if (el.TryGetProperty("id", out _))
                    {
                        // id zaten payload içinde
                        commandList.Add(el);
                    }
                    else
                    {
                        // id yoksa sarmala
                        var wrappedObj = new
                        {
                            id = pc.Id,
                            payload = el
                        };
                        commandList.Add(JsonSerializer.Deserialize<JsonElement>(
                            JsonSerializer.Serialize(wrappedObj)));
                    }
                }
                else
                {
                    // object değilse (array/primitive) sarmala
                    var wrapped = new
                    {
                        id = pc.Id,
                        payload = el
                    };
                    commandList.Add(JsonSerializer.Deserialize<JsonElement>(
                        JsonSerializer.Serialize(wrapped)));
                }
            }
            catch
            {
                // JSON parse hatası → raw string olarak dön
                var wrappedRaw = new
                {
                    id = pc.Id,
                    payload = pc.Payload
                };
                commandList.Add(JsonSerializer.Deserialize<JsonElement>(
                    JsonSerializer.Serialize(wrappedRaw)));
            }
        }

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
