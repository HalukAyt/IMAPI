using System.ComponentModel.DataAnnotations;

namespace IMAPI.Api.Entities;

public class LightChannel
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = default!;

    [Range(1, 32)]
    public int ChNo { get; set; }  // 1..8

    [MaxLength(80)]
    public string? Name { get; set; }

    public bool IsOn { get; set; } = false;     // son bilinen durum
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
