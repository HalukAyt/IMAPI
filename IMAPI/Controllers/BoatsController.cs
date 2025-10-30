using System.Security.Claims;
using IMAPI.Api.Data;
using IMAPI.Api.DTOs;
using IMAPI.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace IMAPI.Api.Controllers;


[ApiController]
[Authorize]
[Route("api/[controller]")]
public class BoatsController : ControllerBase
{
    private readonly ItechMarineDbContext _db;
    public BoatsController(ItechMarineDbContext db) { _db = db; }


    private Guid GetUserId() => Guid.Parse(User.FindFirstValue("uid")!);


    [HttpGet]
    public async Task<ActionResult<List<BoatResponse>>> List()
    {
        var uid = GetUserId();
        var boats = await _db.Boats.Where(b => b.OwnerId == uid)
        .OrderByDescending(b => b.CreatedAt)
        .Select(b => new BoatResponse(b.Id, b.Name, b.HullNo, b.CreatedAt))
        .ToListAsync();
        return Ok(boats);
    }


    [HttpPost]
    public async Task<ActionResult<BoatResponse>> Create(BoatCreateRequest req)
    {
        var uid = GetUserId();
        var boat = new Boat { Name = req.Name, HullNo = req.HullNo, OwnerId = uid };
        _db.Boats.Add(boat);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = boat.Id }, new BoatResponse(boat.Id, boat.Name, boat.HullNo, boat.CreatedAt));
    }


    [HttpGet("{id}")]
    public async Task<ActionResult<BoatResponse>> Get(Guid id)
    {
        var uid = GetUserId();
        var b = await _db.Boats.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == uid);
        if (b is null) return NotFound();
        return Ok(new BoatResponse(b.Id, b.Name, b.HullNo, b.CreatedAt));
    }


    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, BoatCreateRequest req)
    {
        var uid = GetUserId();
        var b = await _db.Boats.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == uid);
        if (b is null) return NotFound();
        b.Name = req.Name; b.HullNo = req.HullNo;
        await _db.SaveChangesAsync();
        return NoContent();
    }


    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var uid = GetUserId();
        var b = await _db.Boats.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == uid);
        if (b is null) return NotFound();
        _db.Boats.Remove(b);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}