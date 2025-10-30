using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;


namespace IMAPI.Api.Services;


public class TokenService
{
    private readonly string _key; private readonly string _issuer; private readonly string _audience;
    public TokenService(string key, string issuer, string audience)
    { _key = key; _issuer = issuer; _audience = audience; }


    public string CreateToken(Guid userId, string email)
    {
        var claims = new List<Claim>
{
new(JwtRegisteredClaimNames.Sub, email),
new("uid", userId.ToString()),
new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
};


        var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
        issuer: _issuer,
        audience: _audience,
        claims: claims,
        notBefore: DateTime.UtcNow,
        expires: DateTime.UtcNow.AddDays(7),
        signingCredentials: creds
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}