using System.Text.RegularExpressions;
using HolyChat.Api.Data;
using HolyChat.Api.DTOs;
using HolyChat.Api.Models;
using HolyChat.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace HolyChat.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly HolyChatDbContext _db;
    private readonly IMatrixAuthService _matrixAuth;
    private readonly ISynapseAdminService _synapseAdmin;
    private readonly IJwtService _jwtService;
    private readonly ITotpService _totpService;
    private readonly ILogger<AuthController> _logger;

    // Regras de nome de utilizador Matrix (localpart): minúsculas,
    // dígitos, e um pequeno conjunto de símbolos. Mantemos mais
    // restrito que a spec Matrix permite, por simplicidade.
    private static readonly Regex UsernameRegex = new(@"^[a-z0-9._-]{3,32}$", RegexOptions.Compiled);

    public AuthController(
        HolyChatDbContext db,
        IMatrixAuthService matrixAuth,
        ISynapseAdminService synapseAdmin,
        IJwtService jwtService,
        ITotpService totpService,
        ILogger<AuthController> logger)
    {
        _db = db;
        _matrixAuth = matrixAuth;
        _synapseAdmin = synapseAdmin;
        _jwtService = jwtService;
        _totpService = totpService;
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

        // 3.5. Se o utilizador tiver 2FA ativo, exige e valida o código TOTP.
        if (user.TotpEnabled)
        {
            if (string.IsNullOrWhiteSpace(request.TotpCode))
            {
                return Unauthorized(new { error = "totp_required", message = "This account has 2FA enabled. Provide totpCode." });
            }

            if (user.TotpSecret is null || !_totpService.VerifyCode(user.TotpSecret, request.TotpCode))
            {
                return Unauthorized(new { error = "invalid_totp_code" });
            }
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

    [HttpPost("register")]
    public async Task<ActionResult<LoginResponse>> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code) ||
            string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return BadRequest(new { error = "Code, username, password and displayName are required." });
        }

        var username = request.Username.Trim().ToLowerInvariant();

        if (!UsernameRegex.IsMatch(username))
        {
            return BadRequest(new { error = "Username must be 3-32 characters: lowercase letters, digits, dots, underscores or hyphens." });
        }

        if (request.Password.Length < 8)
        {
            return BadRequest(new { error = "Password must be at least 8 characters." });
        }

        // 1. Valida o convite.
        var invite = await _db.Invites.FirstOrDefaultAsync(i => i.Code == request.Code);
        if (invite is null)
        {
            return BadRequest(new { error = "Invalid invite code." });
        }

        if (invite.Status != InviteStatus.Active)
        {
            return BadRequest(new { error = $"This invite is no longer valid (status: {invite.Status})." });
        }

        if (invite.ExpiresAt is not null && invite.ExpiresAt < DateTime.UtcNow)
        {
            invite.Status = InviteStatus.Expired;
            await _db.SaveChangesAsync();
            return BadRequest(new { error = "This invite has expired." });
        }

        if (invite.UsesCount >= invite.MaxUses)
        {
            return BadRequest(new { error = "This invite has already been fully used." });
        }

        // 2. Confirma que o username não está em uso na nossa BD.
        var usernameTaken = await _db.Users.AnyAsync(u => u.Username == username);
        if (usernameTaken)
        {
            return Conflict(new { error = "This username is already taken." });
        }

        // 3. Cria a conta no Synapse. Se falhar (ex: username já existe
        // no Synapse mas não na nossa BD - cenário raro), nada mais
        // acontece, sem efeitos colaterais na nossa BD.
        try
        {
            await _synapseAdmin.CreateUserAsync(username, request.Password, request.DisplayName);
        }
        catch (SynapseUserAlreadyExistsException)
        {
            return Conflict(new { error = "This username is already taken." });
        }
        catch (SynapseAdminException ex)
        {
            _logger.LogError(ex, "Failed to create Matrix account for {Username}", username);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "Failed to create Matrix account. The username may already exist, or the Matrix server is unavailable." });
        }

        // 4. Cria o User local.
        var serverName = HttpContext.RequestServices.GetRequiredService<IConfiguration>()["Synapse:ServerName"] ?? "holychat.cc";
        var user = new User
        {
            MatrixUserId = $"@{username}:{serverName}",
            Username = username,
            DisplayName = request.DisplayName,
            Role = Role.Member,
            Status = UserStatus.Active,
            InvitedById = invite.CreatedById
        };
        _db.Users.Add(user);

        // 5. Atualiza o convite.
        invite.UsesCount++;
        if (invite.UsesCount >= invite.MaxUses)
        {
            invite.Status = InviteStatus.Used;
        }

        // 6. Audit log.
        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = user.Id,
            Action = AuditAction.UserRegistered,
            TargetId = user.Id,
            RelatedInviteId = invite.Id
        });
        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = invite.CreatedById,
            Action = AuditAction.InviteUsed,
            TargetId = user.Id,
            RelatedInviteId = invite.Id
        });

        await _db.SaveChangesAsync();

        // 7. Login automático - devolve já um JWT, sem obrigar a um segundo pedido.
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

    [HttpPost("totp/setup")]
    [Authorize]
    public async Task<ActionResult<TotpSetupResponse>> TotpSetup()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var user = await _db.Users.FindAsync(userId.Value);
        if (user is null) return NotFound();

        if (user.TotpEnabled)
        {
            return BadRequest(new { error = "2FA is already enabled. Disable it first to set up a new secret." });
        }

        // Gera um novo secret e guarda como "pendente" (TotpEnabled continua false
        // até ser confirmado com um código válido em /totp/enable).
        var secret = _totpService.GenerateSecret();
        user.TotpSecret = secret;
        await _db.SaveChangesAsync();

        return Ok(new TotpSetupResponse
        {
            Secret = secret,
            OtpauthUri = _totpService.GetOtpauthUri(secret, user.Username)
        });
    }

    [HttpPost("totp/enable")]
    [Authorize]
    public async Task<IActionResult> TotpEnable([FromBody] TotpCodeRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var user = await _db.Users.FindAsync(userId.Value);
        if (user is null) return NotFound();

        if (user.TotpSecret is null)
        {
            return BadRequest(new { error = "No pending 2FA setup. Call /totp/setup first." });
        }

        if (!_totpService.VerifyCode(user.TotpSecret, request.Code))
        {
            return BadRequest(new { error = "Invalid code." });
        }

        user.TotpEnabled = true;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = user.Id,
            Action = AuditAction.TotpEnabled,
            TargetId = user.Id
        });

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("totp/disable")]
    [Authorize]
    public async Task<IActionResult> TotpDisable([FromBody] TotpCodeRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var user = await _db.Users.FindAsync(userId.Value);
        if (user is null) return NotFound();

        if (!user.TotpEnabled || user.TotpSecret is null)
        {
            return BadRequest(new { error = "2FA is not enabled." });
        }

        // Exige um código válido para desativar - evita que alguém com
        // acesso momentâneo à sessão (mas sem a app authenticator) desative
        // o 2FA de outra pessoa.
        if (!_totpService.VerifyCode(user.TotpSecret, request.Code))
        {
            return BadRequest(new { error = "Invalid code." });
        }

        user.TotpEnabled = false;
        user.TotpSecret = null;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = user.Id,
            Action = AuditAction.TotpDisabled,
            TargetId = user.Id
        });

        await _db.SaveChangesAsync();
        return NoContent();
    }

    private Guid? GetCurrentUserId()
    {
        var subClaim = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(subClaim, out var id) ? id : null;
    }
}
