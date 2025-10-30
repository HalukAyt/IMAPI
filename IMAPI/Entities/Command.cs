namespace IMAPI.Entities;

public class Command
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public Device? Device { get; set; }
    public string PayloadJson { get; set; } = default!; // e.g., { "ch":3, "state":"on" }
    public string Status { get; set; } = "PENDING"; // PENDING/OK/ERR
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
