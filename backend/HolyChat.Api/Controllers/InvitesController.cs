using System.Security.Cryptography;
using HolyChat.Api.Data;
using HolyChat.Api.DTOs;
using HolyChat.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HolyChat.Api.Controllers;

[ApiController]
[Route("api/invites")]
[Authorize]
public class InvitesController : ControllerBase
{
    private readonly HolyChatDbContext _db;
    private readonly ILogger<InvitesController> _logger;

    // Regras de negócio (não são constraints de BD, ficam aqui):
    private const int StandardInviteRateLimitHours = 24;
    private const int WeeklyInviteAlertThreshold = 6;

    public InvitesController(HolyChatDbContext db, ILogger<InvitesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Cria um convite standard (1 uso, sem expiração) para o utilizador autenticado.
    /// Sujeito a rate limit de 1 convite por 24h.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CreateInviteResponse>> Create()
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        // Rate limit: 1 convite standard por 24h, baseado no AuditLog
        // (fonte de verdade do histórico, não guardamos contadores na tabela User).
        var since = DateTime.UtcNow.AddHours(-StandardInviteRateLimitHours);
        var recentInviteExists = await _db.AuditLogs.AnyAsync(a =>
            a.ActorId == userId &&
            a.Action == AuditAction.InviteCreated &&
            a.Timestamp > since);

        if (recentInviteExists)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                error = $"You can only create one invite every {StandardInviteRateLimitHours}h. Try again later, or ask an admin for a bulk invite."
            });
        }

        var invite = new Invite
        {
            Code = GenerateInviteCode(),
            CreatedById = userId.Value,
            MaxUses = 1,
            Status = InviteStatus.Active,
            ExpiresAt = null
        };

        _db.Invites.Add(invite);

        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = userId.Value,
            Action = AuditAction.InviteCreated,
            RelatedInviteId = invite.Id
        });

        await _db.SaveChangesAsync();

        // Alerta soft para admins: nº de convites criados por este user nos
        // últimos 7 dias. Por agora só regista em log — não há ainda
        // sistema de notificações; fica para uma issue futura (admin panel).
        var weekAgo = DateTime.UtcNow.AddDays(-7);
        var weeklyCount = await _db.AuditLogs.CountAsync(a =>
            a.ActorId == userId &&
            a.Action == AuditAction.InviteCreated &&
            a.Timestamp > weekAgo);

        if (weeklyCount >= WeeklyInviteAlertThreshold)
        {
            _logger.LogWarning(
                "User {UserId} created {Count} invites in the last 7 days (threshold: {Threshold}). Consider reviewing.",
                userId, weeklyCount, WeeklyInviteAlertThreshold);
        }

        return CreatedAtAction(nameof(Create), new CreateInviteResponse
        {
            Id = invite.Id,
            Code = invite.Code,
            MaxUses = invite.MaxUses,
            ExpiresAt = invite.ExpiresAt,
            CreatedAt = invite.CreatedAt
        });
    }

    /// <summary>
    /// Admin atribui um convite multi-uso a um utilizador (bulk grant),
    /// ex: em resposta a um pedido "quero convidar 5 amigos".
    /// </summary>
    [HttpPost("grant")]
    [Authorize(Roles = nameof(Role.Admin) + "," + nameof(Role.SuperAdmin))]
    public async Task<ActionResult<CreateInviteResponse>> GrantBulkInvite([FromBody] CreateBulkInviteRequest request)
    {
        var adminId = GetCurrentUserId();
        if (adminId is null)
        {
            return Unauthorized();
        }

        if (request.MaxUses < 1)
        {
            return BadRequest(new { error = "MaxUses must be at least 1." });
        }

        if (request.ExpiresInHours < 1)
        {
            return BadRequest(new { error = "ExpiresInHours must be at least 1. Bulk invites always expire." });
        }

        var targetUser = await _db.Users.FindAsync(request.TargetUserId);
        if (targetUser is null || targetUser.Status != UserStatus.Active)
        {
            return BadRequest(new { error = "Target user not found or not active." });
        }

        var invite = new Invite
        {
            Code = GenerateInviteCode(),
            CreatedById = targetUser.Id,
            MaxUses = request.MaxUses,
            Status = InviteStatus.Active,
            ExpiresAt = DateTime.UtcNow.AddHours(request.ExpiresInHours),
            RequestMessage = request.RequestMessage,
            GrantedById = adminId.Value
        };

        _db.Invites.Add(invite);

        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = adminId.Value,
            Action = AuditAction.InviteBulkGranted,
            TargetId = targetUser.Id,
            RelatedInviteId = invite.Id,
            Details = $"MaxUses={request.MaxUses}, ExpiresInHours={request.ExpiresInHours}"
        });

        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Create), new CreateInviteResponse
        {
            Id = invite.Id,
            Code = invite.Code,
            MaxUses = invite.MaxUses,
            ExpiresAt = invite.ExpiresAt,
            CreatedAt = invite.CreatedAt
        });
    }

    private Guid? GetCurrentUserId()
    {
        var subClaim = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(subClaim, out var id) ? id : null;
    }

    private static string GenerateInviteCode()
    {
        // 32 bytes -> string URL-safe, suficientemente longa para ser
        // impraticável de adivinhar por força bruta.
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }
}
