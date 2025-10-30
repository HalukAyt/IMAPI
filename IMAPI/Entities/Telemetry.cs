namespace IMAPI.Entities;

public class Telemetry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public Device? Device { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public double? BatteryV { get; set; }
    public double? TemperatureC { get; set; }
    public double? Lat { get; set; }
    public double? Lon { get; set; }
    public string? RawJson { get; set; }
}