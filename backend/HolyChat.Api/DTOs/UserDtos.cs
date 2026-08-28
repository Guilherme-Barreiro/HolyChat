namespace HolyChat.Api.DTOs;

public class UserSummaryResponse
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? InvitedById { get; set; }
    public string? InvitedByUsername { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ActionRequest
{
    /// <summary>Motivo opcional, guardado no AuditLog.</summary>
    public string? Reason { get; set; }
}
