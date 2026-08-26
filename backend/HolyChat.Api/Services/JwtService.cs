using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HolyChat.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace HolyChat.Api.Services;

public interface IJwtService
{
    (string token, DateTime expiresAt) GenerateToken(User user);
}

public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;

    // Duração do token: 3 meses a partir da emissão.
    // Nota: isto é a duração MÁXIMA de uma sessão sem uso. Cabe à app
    // renovar o token sempre que o utilizador a abre dentro deste
    // período (ver nota no AuthController), simulando um "sliding
    // expiration" sem complexidade de refresh tokens.
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(90);

    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string token, DateTime expiresAt) GenerateToken(User user)
    {
        var secret = _configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret not configured");

        var expiresAt = DateTime.UtcNow.Add(TokenLifetime);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("username", user.Username),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "holychat-api",
            audience: _configuration["Jwt:Audience"] ?? "holychat-app",
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
