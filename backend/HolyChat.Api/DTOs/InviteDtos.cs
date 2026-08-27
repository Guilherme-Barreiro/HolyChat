namespace HolyChat.Api.DTOs;

public class CreateInviteResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int MaxUses { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateBulkInviteRequest
{
    public Guid TargetUserId { get; set; }
    public int MaxUses { get; set; }
    public int ExpiresInHours { get; set; }
    public string? RequestMessage { get; set; }
}
