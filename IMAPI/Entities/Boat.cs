using System.ComponentModel.DataAnnotations;


namespace IMAPI.Api.Entities;


public class Boat
{
    public Guid Id { get; set; } = Guid.NewGuid();


    [Required, MaxLength(120)]
    public string Name { get; set; } = default!;


    [MaxLength(120)]
    public string? HullNo { get; set; }


    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = default!;


    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;


    public List<Device> Devices { get; set; } = new();
}