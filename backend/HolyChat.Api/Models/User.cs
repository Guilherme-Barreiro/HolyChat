using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HolyChat.Api.Models;

public class User
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Identificador completo Matrix, ex: "@barreiro:holychat.cc".
    /// Usado ao chamar a Synapse Admin API.
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string MatrixUserId { get; set; } = string.Empty;

    /// <summary>
    /// Localpart, ex: "barreiro" (extraído de MatrixUserId, guardado à parte por conveniência).
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string DisplayName { get; set; } = string.Empty;

    public Role Role { get; set; } = Role.Member;

    public UserStatus Status { get; set; } = UserStatus.PendingApproval;

    /// <summary>
    /// Secret TOTP (Base32), null se 2FA nunca foi configurado ou foi desativado.
    /// </summary>
    [MaxLength(64)]
    public string? TotpSecret { get; set; }

    /// <summary>
    /// Só true depois de o utilizador confirmar um código válido no setup.
    /// Um TotpSecret preenchido com TotpEnabled=false é um setup pendente.
    /// </summary>
    public bool TotpEnabled { get; set; } = false;

    /// <summary>
    /// FK recursiva - quem convidou este utilizador. Null para os admins fundadores.
    /// </summary>
    public Guid? InvitedById { get; set; }

    [ForeignKey(nameof(InvitedById))]
    public User? InvitedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<User> InvitedUsers { get; set; } = new List<User>();
    public ICollection<Invite> CreatedInvites { get; set; } = new List<Invite>();
}
