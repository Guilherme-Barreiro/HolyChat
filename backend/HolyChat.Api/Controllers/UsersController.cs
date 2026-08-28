using HolyChat.Api.Data;
using HolyChat.Api.DTOs;
using HolyChat.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HolyChat.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = nameof(Role.Admin) + "," + nameof(Role.SuperAdmin))]
public class UsersController : ControllerBase
{
    private readonly HolyChatDbContext _db;

    public UsersController(HolyChatDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<UserSummaryResponse>>> List()
    {
        var users = await _db.Users
            .OrderBy(u => u.CreatedAt)
            .Select(u => new UserSummaryResponse
            {
                Id = u.Id,
                Username = u.Username,
                DisplayName = u.DisplayName,
                Role = u.Role.ToString(),
                Status = u.Status.ToString(),
                InvitedById = u.InvitedById,
                InvitedByUsername = u.InvitedBy != null ? u.InvitedBy.Username : null,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync();

        return Ok(users);
    }

    /// <summary>
    /// Lista quem esta pessoa convidou — usado por um admin para rever
    /// manualmente contas ligadas a alguém que está a ser removido
    /// (soft-delete, sem cascade automático, como decidido).
    /// </summary>
    [HttpGet("{id}/invited")]
    public async Task<ActionResult<List<UserSummaryResponse>>> ListInvited(Guid id)
    {
        var invited = await _db.Users
            .Where(u => u.InvitedById == id)
            .Select(u => new UserSummaryResponse
            {
                Id = u.Id,
                Username = u.Username,
                DisplayName = u.DisplayName,
                Role = u.Role.ToString(),
                Status = u.Status.ToString(),
                InvitedById = u.InvitedById,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync();

        return Ok(invited);
    }

    [HttpPost("{id}/suspend")]
    public Task<IActionResult> Suspend(Guid id, [FromBody] ActionRequest request) =>
        ChangeStatus(id, UserStatus.Suspended, AuditAction.UserSuspended, request.Reason, protectSuperAdmin: true);

    [HttpPost("{id}/reactivate")]
    public Task<IActionResult> Reactivate(Guid id, [FromBody] ActionRequest request) =>
        ChangeStatus(id, UserStatus.Active, AuditAction.UserReactivated, request.Reason, protectSuperAdmin: false);

    [HttpPost("{id}/remove")]
    public Task<IActionResult> Remove(Guid id, [FromBody] ActionRequest request) =>
        ChangeStatus(id, UserStatus.Removed, AuditAction.UserRemoved, request.Reason, protectSuperAdmin: true);

    /// <summary>
    /// Promove Member -> Admin. Restrito a SuperAdmin para evitar que
    /// admins criem outros admins em cadeia sem controlo central.
    /// </summary>
    [HttpPost("{id}/promote")]
    [Authorize(Roles = nameof(Role.SuperAdmin))]
    public async Task<IActionResult> Promote(Guid id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return NotFound();

        if (user.Role != Role.Member)
        {
            return BadRequest(new { error = $"User is already {user.Role}, not a Member." });
        }

        user.Role = Role.Admin;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = GetCurrentUserId(),
            Action = AuditAction.UserPromoted,
            TargetId = user.Id
        });

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id}/demote")]
    [Authorize(Roles = nameof(Role.SuperAdmin))]
    public async Task<IActionResult> Demote(Guid id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return NotFound();

        if (user.Role != Role.Admin)
        {
            return BadRequest(new { error = $"User is {user.Role}, not an Admin." });
        }

        user.Role = Role.Member;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = GetCurrentUserId(),
            Action = AuditAction.UserDemoted,
            TargetId = user.Id
        });

        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<IActionResult> ChangeStatus(Guid id, UserStatus newStatus, AuditAction action, string? reason, bool protectSuperAdmin)
    {
        var actorId = GetCurrentUserId();

        if (id == actorId)
        {
            return BadRequest(new { error = "You cannot perform this action on your own account." });
        }

        var user = await _db.Users.FindAsync(id);
        if (user is null) return NotFound();

        if (protectSuperAdmin && user.Role == Role.SuperAdmin)
        {
            return BadRequest(new { error = "The SuperAdmin account cannot be suspended or removed." });
        }

        user.Status = newStatus;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = actorId,
            Action = action,
            TargetId = user.Id,
            Details = reason
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
