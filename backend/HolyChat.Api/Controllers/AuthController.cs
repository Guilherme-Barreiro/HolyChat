using HolyChat.Api.Data;
using HolyChat.Api.DTOs;
using HolyChat.Api.Models;
using HolyChat.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HolyChat.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly HolyChatDbContext _db;
    private readonly IMatrixAuthService _matrixAuth;
    private readonly IJwtService _jwtService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        HolyChatDbContext db,
        IMatrixAuthService matrixAuth,
        IJwtService jwtService,
        ILogger<AuthController> logger)
    {
        _db = db;
        _matrixAuth = matrixAuth;
        _jwtService = jwtService;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "Username and password are required." });
        }

        // 1. Valida a password contra o Synapse — é a fonte de verdade,
        // o backend nunca guarda nem compara passwords diretamente.
        var isValid = await _matrixAuth.ValidateCredentialsAsync(request.Username, request.Password);
        if (!isValid)
        {
            return Unauthorized(new { error = "Invalid username or password." });
        }

        // 2. Procura o utilizador na nossa BD pelo username.
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
        if (user is null)
        {
            // A conta existe no Synapse mas ainda não foi registada/aprovada
            // no nosso sistema (ex: criada manualmente via register_new_matrix_user
            // sem passar pelo fluxo de convite). Cenário raro, mas tratado.
            _logger.LogWarning("Login attempt for {Username}: valid Matrix credentials but no local User record", request.Username);
            return Unauthorized(new { error = "Account not registered in HolyChat. Contact an admin." });
        }

        // 3. Só contas ativas podem entrar.
        if (user.Status != UserStatus.Active)
        {
            return Unauthorized(new { error = $"Account status is '{user.Status}'. Access denied." });
        }

        // 4. Emite o JWT próprio do backend.
        var (token, expiresAt) = _jwtService.GenerateToken(user);

        return Ok(new LoginResponse
        {
            Token = token,
            UserId = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Role = user.Role.ToString(),
            ExpiresAt = expiresAt
        });
    }
}
