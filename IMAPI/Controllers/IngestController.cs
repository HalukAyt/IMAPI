namespace IMAPI.Controllers;

using System;
using System.Text.Json;
using IMAPI.Data;
using IMAPI.Entities;
using IMAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

[ApiController]
[Route("ingest")]
public class IngestController(AppDbContext db, DeviceAuthService devAuth) : ControllerBase
{
    public sealed class TelemetryInDto
    {
        public double? batteryV { get; set; }
        public double? temperatureC { get; set; }
        public double? lat { get; set; }
        public double? lon { get; set; }
        public DateTime? timestampUtc { get; set; }
        public JsonElement? extras { get; set; }
    }

    [HttpPost("telemetry")]
    [AllowAnonymous]
    [EnableRateLimiting("device-rl")]
    public async Task<IActionResult> Telemetry([FromBody] TelemetryInDto dto)
    {
        var v = await devAuth.ValidateAsync(Request);
        if (!v.ok) return Unauthorized(new { error = v.err });
        var t = new Telemetry
        {
            DeviceId = v.deviceDbId,
            BatteryV = dto.batteryV,
            TemperatureC = dto.temperatureC,
            Lat = dto.lat,
            Lon = dto.lon,
            TimestampUtc = dto.timestampUtc ?? DateTime.UtcNow,
            RawJson = dto.extras?.ToString()
        };
        db.Add(t);
        await db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}