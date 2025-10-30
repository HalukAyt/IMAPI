namespace IMAPI.Controllers;

using System;
using System.Text.Json;
using IMAPI.Data;
using IMAPI.DTO;
using IMAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("devices/{deviceId}/commands")]
[Authorize]
public class CommandsController(AppDbContext db, IMqttPublisher mqtt) : ControllerBase
{
    [HttpPost("toggle")] // body: { ch, state: "on"|"off" }
    public async Task<IActionResult> Toggle(string deviceId, [FromBody] ToggleCommandDto dto, CancellationToken ct)
    {
        var exists = await db.Devices.AnyAsync(d => d.DeviceId == deviceId, ct);
        if (!exists) return NotFound("device");
        var json = JsonSerializer.Serialize(dto);
        await mqtt.PublishCommandAsync(deviceId, json, ct);
        return Accepted(new { deviceId, dto.ch, dto.state });
    }
}