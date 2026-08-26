using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HolyChat.Api.Models;

public class AuditLog
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Quem executou a ação. Null = ação automática do sistema (ex: expiração por cron job).
    /// </summary>
    public Guid? ActorId { get; set; }

    [ForeignKey(nameof(ActorId))]
    public User? Actor { get; set; }

    public AuditAction Action { get; set; }

    /// <summary>
    /// Sobre quem foi a ação (quando aplicável), ex: o utilizador suspenso.
    /// </summary>
    public Guid? TargetId { get; set; }

    [ForeignKey(nameof(TargetId))]
    public User? Target { get; set; }

    /// <summary>
    /// Preenchido só em ações relacionadas com convites.
    /// </summary>
    public Guid? RelatedInviteId { get; set; }

    [ForeignKey(nameof(RelatedInviteId))]
    public Invite? RelatedInvite { get; set; }

    /// <summary>
    /// Contexto extra em texto livre/JSON, ex: motivo de suspensão.
    /// </summary>
    [MaxLength(1000)]
    public string? Details { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
