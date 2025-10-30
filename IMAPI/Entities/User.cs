using System.ComponentModel.DataAnnotations;


namespace IMAPI.Api.Entities;


public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();


    [Required, MaxLength(160)]
    public string Email { get; set; } = default!;


    [Required]
    public string PasswordHash { get; set; } = default!;


    [Required]
    public string PasswordSalt { get; set; } = default!;


    [MaxLength(120)]
    public string? FullName { get; set; }


    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;


    public List<Boat> Boats { get; set; } = new();
}