using System.ComponentModel.DataAnnotations;


namespace IMAPI.Api.Entities;


public class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();


    [Required, MaxLength(80)]
    public string Serial { get; set; } = default!; // ESP32 cihaz seri/UID


    public Guid BoatId { get; set; }
    public Boat Boat { get; set; } = default!;


    [MaxLength(200)]
    public string? SecretHash { get; set; } // claim için HMAC doğrulamada kullanılacak


    public DateTime? LastSeen { get; set; }
}