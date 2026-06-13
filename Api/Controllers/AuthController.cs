using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Api.Controllers;

[Route("api/auth")]
[ApiController]
public class AuthController : ControllerBase
{
    // Test credentials for the evaluation — replace with a real user store in production.
    private static readonly Dictionary<string, string> TestUsers = new(StringComparer.Ordinal)
    {
        ["admin"]   = "Admin123!",
        ["usuario"] = "User123!"
    };

    private readonly IConfiguration _config;

    public AuthController(IConfiguration config) => _config = config;

    /// <summary>
    /// Obtiene un token JWT. Credenciales de prueba: admin/Admin123! o usuario/User123!
    /// </summary>
    [HttpPost("token")]
    public IActionResult Token([FromBody] LoginRequest request)
    {
        if (!TestUsers.TryGetValue(request.Username, out var expected) || expected != request.Password)
            return Unauthorized(new { error = "Credenciales inválidas." });

        var key         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expMinutes  = int.Parse(_config["Jwt:ExpirationMinutes"]!);

        var token = new JwtSecurityToken(
            issuer:             _config["Jwt:Issuer"],
            audience:           _config["Jwt:Audience"],
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, request.Username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ],
            expires:            DateTime.UtcNow.AddMinutes(expMinutes),
            signingCredentials: credentials);

        return Ok(new
        {
            access_token = new JwtSecurityTokenHandler().WriteToken(token),
            token_type   = "Bearer",
            expires_in   = expMinutes * 60
        });
    }
}

public class LoginRequest
{
    [Required] public string Username { get; set; } = null!;
    [Required] public string Password { get; set; } = null!;
}
