using IMAPI.Api.Data;
using IMAPI.Api.DTOs;
using IMAPI.Api.Entities;
using IMAPI.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace ItechMarine.Api.Controllers;


[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ItechMarineDbContext _db;
    private readonly PasswordHasher _hasher;
    private readonly TokenService _tokens;


    public AuthController(ItechMarineDbContext db, PasswordHasher hasher, TokenService tokens)
    { _db = db; _hasher = hasher; _tokens = tokens; }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest req)
    {
        if (await _db.Users.AnyAsync(u => u.Email == req.Email))
            return Conflict("Email already registered");


        var (hash, salt) = _hasher.HashPassword(req.Password);
        var user = new User { Email = req.Email, PasswordHash = hash, PasswordSalt = salt, FullName = req.FullName };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();


        var token = _tokens.CreateToken(user.Id, user.Email);
        return Ok(new AuthResponse(token, user.Id, user.Email, user.FullName));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
        if (user is null) return Unauthorized("Invalid credentials");
        if (!_hasher.Verify(req.Password, user.PasswordHash, user.PasswordSalt))
            return Unauthorized("Invalid credentials");


        var token = _tokens.CreateToken(user.Id, user.Email);
        return Ok(new AuthResponse(token, user.Id, user.Email, user.FullName));
    }
}