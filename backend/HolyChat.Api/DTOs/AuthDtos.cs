namespace HolyChat.Api.DTOs;

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Obrigatório apenas se o utilizador tiver TOTP ativo.
    /// </summary>
    public string? TotpCode { get; set; }
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public class RegisterRequest
{
    public string Code { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public class TotpSetupResponse
{
    public string Secret { get; set; } = string.Empty;
    public string OtpauthUri { get; set; } = string.Empty;
}

public class TotpCodeRequest
{
    public string Code { get; set; } = string.Empty;
}
