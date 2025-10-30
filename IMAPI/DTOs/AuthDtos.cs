using System.ComponentModel.DataAnnotations;


namespace IMAPI.Api.DTOs;


public record RegisterRequest([
Required, EmailAddress] string Email,
[Required, MinLength(6)] string Password,
string? FullName
);


public record LoginRequest([
Required, EmailAddress] string Email,
[Required] string Password
);


public record AuthResponse(string Token, Guid UserId, string Email, string? FullName);