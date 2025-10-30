using System.ComponentModel.DataAnnotations;


namespace IMAPI.Api.DTOs;


public record BoatCreateRequest([
Required, MaxLength(120)] string Name,
string? HullNo
);


public record BoatResponse(Guid Id, string Name, string? HullNo, DateTime CreatedAt);