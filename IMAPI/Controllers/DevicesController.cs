namespace IMAPI.Controllers;

using IMAPI.Data;
using IMAPI.Entities;
using IMAPI.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;

[ApiController]
[Route("devices")]
[Authorize]
public class DevicesController(AppDbContext db, IProtectionService ps, UserManager<AppUser> um) : ControllerBase
{
    // Claim code akışı basitleştirilmiş: client bize (DeviceId, ClaimCode) yollar.
    [HttpPost("claim")]
    public async Task<IActionResult> Claim([FromBody] ClaimDto dto)
    {
        var me = await um.GetUserAsync(User);
        if (me is null) return Unauthorized();

        // DEMO: ClaimCode doğrulaması burada varsayılıyor (örn. siparişle eşleşme)
        var boat = await db.Boats.FirstOrDefaultAsync(b => b.OwnerId == me.Id);
        if (boat is null)
        {
            boat = new Boat { Name = dto.BoatName ?? "My Boat", OwnerId = me.Id };
            db.Add(boat);
        }

        var dev = await db.Devices.FirstOrDefaultAsync(d => d.DeviceId == dto.DeviceId);
        if (dev is null)
        {
            dev = new Device { DeviceId = dto.DeviceId, Boat = boat };
            // Cihaza özel HMAC secret üret ve korumalı sakla
            var plain = Guid.NewGuid().ToString("N");
            dev.ProtectedSecret = ps.Protect(plain);
            // 8 kanal şablon
            for (var i = 0; i < 8; i++) dev.RelayChannels.Add(new RelayChannel { Index = i, Name = $"CH{i + 1}", ActiveLow = true });
            db.Add(dev);
        }
        else dev.Boat = boat;

        await db.SaveChangesAsync();
        return Ok(new { boatId = boat.Id, deviceDbId = dev.Id });
    }

    public record ClaimDto(string DeviceId, string? ClaimCode, string? BoatName);
}
