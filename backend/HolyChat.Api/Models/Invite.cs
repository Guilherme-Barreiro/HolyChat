using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HolyChat.Api.Models;

public class Invite
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Token único usado no link/código de registo.
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string Code { get; set; } = string.Empty;

    [Required]
    public Guid CreatedById { get; set; }

    [ForeignKey(nameof(CreatedById))]
    public User CreatedBy { get; set; } = null!;

    /// <summary>
    /// Nº máximo de utilizações. Default 1 (convite normal).
    /// Convites concedidos por admin (bulk grant) podem ter mais.
    /// </summary>
    public int MaxUses { get; set; } = 1;

    public int UsesCount { get; set; } = 0;

    public InviteStatus Status { get; set; } = InviteStatus.Active;

    /// <summary>
    /// Null = sem expiração definida. Obrigatório para bulk grants (ex: 24h).
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Preenchido quando o utilizador pede um bulk grant a um admin,
    /// ex: "quero convidar 5 amigos".
    /// </summary>
    [MaxLength(500)]
    public string? RequestMessage { get; set; }

    /// <summary>
    /// FK para o admin que aprovou/atribuiu um bulk grant. Null em convites standard.
    /// </summary>
    public Guid? GrantedById { get; set; }

    [ForeignKey(nameof(GrantedById))]
    public User? GrantedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
