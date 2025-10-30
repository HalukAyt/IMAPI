using System.Security.Claims;
using IMAPI.Api.Data;
using IMAPI.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace IMAPI.Api.Controllers;


[ApiController]
[Authorize]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly ItechMarineDbContext _db;
    public DevicesController(ItechMarineDbContext db) { _db = db; }
    private Guid GetUserId() => Guid.Parse(User.FindFirstValue("uid")!);


    // /api/devices?boatId=...
    [HttpGet]
    public async Task<ActionResult<List<object>>> List([FromQuery] Guid boatId)
    {
        var uid = GetUserId();
        var ok = await _db.Boats.AnyAsync(b => b.Id == boatId && b.OwnerId == uid);
        if (!ok) return NotFound();
        var list = await _db.Devices.Where(d => d.BoatId == boatId)
        .Select(d => new { d.Id, d.Serial, d.LastSeen })
        .ToListAsync();
        return Ok(list);
    }


    // claim device to a boat (serial no ile eşleştirme)
    public record ClaimDeviceRequest(Guid BoatId, string Serial);


    [HttpPost("claim")]
    public async Task<IActionResult> Claim([FromBody] ClaimDeviceRequest req)
    {
        var uid = GetUserId();
        var boat = await _db.Boats.FirstOrDefaultAsync(b => b.Id == req.BoatId && b.OwnerId == uid);
        if (boat is null) return NotFound("Boat not found");


        var existing = await _db.Devices.FirstOrDefaultAsync(d => d.Serial == req.Serial);
        if (existing is null)
        {
            existing = new Device { Serial = req.Serial, BoatId = boat.Id };
            _db.Devices.Add(existing);
        }
        else
        {
            existing.BoatId = boat.Id;
        }
        await _db.SaveChangesAsync();
        return Ok(new { existing.Id, existing.Serial, existing.BoatId });
    }
}