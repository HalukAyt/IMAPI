namespace IMAPI.Controllers;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IMAPI.Entities;
using IMAPI.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

[ApiController]
[Route("auth")]
public class AuthController(UserManager<AppUser> um, SignInManager<AppUser> sm, IOptions<JwtConfig> jwtOpt) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        var user = new AppUser { UserName = dto.Email, Email = dto.Email, DisplayName = dto.DisplayName };
        var res = await um.CreateAsync(user, dto.Password);
        if (!res.Succeeded) return BadRequest(res.Errors);
        return Ok();
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var user = await um.FindByEmailAsync(dto.Email);
        if (user is null) return Unauthorized();
        var passOk = await sm.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: false);
        if (!passOk.Succeeded) return Unauthorized();
        var jwt = IssueJwt(user, jwtOpt.Value);
        return Ok(new { token = jwt });
    }

    private static string IssueJwt(AppUser user, JwtConfig cfg)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim("name", user.DisplayName ?? user.Email ?? string.Empty)
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: cfg.Issuer,
            audience: cfg.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(cfg.AccessTokenMinutes),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public record RegisterDto(string Email, string Password, string? DisplayName);
    public record LoginDto(string Email, string Password);
}
