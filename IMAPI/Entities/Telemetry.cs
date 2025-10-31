using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMAPI.Api.Entities;

public class Telemetry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string DeviceSerial { get; set; } = default!; // hızlı eşleme

    public Guid? DeviceId { get; set; }                  // eşleşirse set edilir
    public DateTime Ts { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "jsonb")]
    public string Payload { get; set; } = "{}";          // ham JSON
}
