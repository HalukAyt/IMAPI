namespace IMAPI.Entities;

public class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DeviceId { get; set; } = default!; // e.g., ESP32 chip id / printed label
    public Guid BoatId { get; set; }
    public Boat? Boat { get; set; }

    // Encrypted (data protection) secret for HTTP HMAC fallback
    public string? ProtectedSecret { get; set; }

    public string Firmware { get; set; } = "1.0.0";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<RelayChannel> RelayChannels { get; set; } = new List<RelayChannel>();
}