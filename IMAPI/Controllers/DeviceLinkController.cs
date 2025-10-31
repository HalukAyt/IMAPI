using IMAPI.Api.Data;
using IMAPI.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMAPI.Api.Controllers;

[ApiController]
[Route("api/device-link")]
public class DeviceLinkController : ControllerBase
{
    private readonly ItechMarineDbContext _db;
    public DeviceLinkController(ItechMarineDbContext db) { _db = db; }

    public record PollReq(string Serial, int? Max);
    public record AckReq(string Serial, string Id, bool Ok, string? Reason);

    // NOT: Üretimde HMAC imza ekleyin (X-Signature), şimdilik basit.
    [HttpPost("poll")]
    public async Task<IActionResult> Poll([FromBody] PollReq req, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var max = Math.Clamp(req.Max ?? 4, 1, 16);

        var items = await _db.PendingCommands
            .Where(x => x.DeviceSerial == req.Serial && x.Status != "delivered" && x.ExpiresAt > now)
            .OrderBy(x => x.CreatedAt)
            .Take(max)
            .Select(x => new { x.Id, x.Payload })
            .ToListAsync(ct);

        return Ok(new { commands = items });
    }

    [HttpPost("ack")]
    public async Task<IActionResult> Ack([FromBody] AckReq req, CancellationToken ct)
    {
        var id = Guid.Parse(req.Id);
        var pc = await _db.PendingCommands.FirstOrDefaultAsync(x => x.Id == id && x.DeviceSerial == req.Serial, ct);
        if (pc is null) return NotFound();

        pc.Status = "delivered";
        pc.DeliveredAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(new { stored = true });
    }
}
