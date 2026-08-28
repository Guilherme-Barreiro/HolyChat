using OtpNet;

namespace HolyChat.Api.Services;

public interface ITotpService
{
    string GenerateSecret();
    string GetOtpauthUri(string secret, string username, string issuer = "HolyChat");
    bool VerifyCode(string secret, string code);
}

public class TotpService : ITotpService
{
    public string GenerateSecret()
    {
        var key = KeyGeneration.GenerateRandomKey(20); // 160 bits, standard TOTP length
        return Base32Encoding.ToString(key);
    }

    public string GetOtpauthUri(string secret, string username, string issuer = "HolyChat")
    {
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedLabel = Uri.EscapeDataString($"{issuer}:{username}");
        return $"otpauth://totp/{encodedLabel}?secret={secret}&issuer={encodedIssuer}&algorithm=SHA1&digits=6&period=30";
    }

    public bool VerifyCode(string secret, string code)
    {
        var key = Base32Encoding.ToBytes(secret);
        var totp = new Totp(key);
        // VerificationWindow permite uma pequena tolerância de relógio
        // (1 passo antes/depois, 30s cada) sem enfraquecer demasiado a segurança.
        return totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));
    }
}
