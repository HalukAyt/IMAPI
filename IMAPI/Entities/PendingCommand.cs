using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMAPI.Api.Entities;

public class PendingCommand
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? DeviceId { get; set; }
    [Required] public string DeviceSerial { get; set; } = default!;
    [Column(TypeName = "jsonb")] public string Payload { get; set; } = "{}";

    // queued | sent | delivered | expired
    [MaxLength(16)] public string Status { get; set; } = "queued";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(2);
}
