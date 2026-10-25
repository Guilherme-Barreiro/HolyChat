using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace HolyChat.Api.Services;

public interface IMatrixAuthService
{
    Task<bool> ValidateCredentialsAsync(string username, string password);
}

public class MatrixAuthService : IMatrixAuthService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MatrixAuthService> _logger;

    public MatrixAuthService(HttpClient httpClient, IConfiguration configuration, ILogger<MatrixAuthService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> ValidateCredentialsAsync(string username, string password)
    {
        var baseUrl = _configuration["Synapse:BaseUrl"]?.TrimEnd('/')
            ?? throw new InvalidOperationException("Synapse:BaseUrl not configured");

        var payload = new
        {
            type = "m.login.password",
            identifier = new { type = "m.id.user", user = username },
            password
        };

        try
        {
            using var content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(
                $"{baseUrl}/_matrix/client/v3/login", content);

            if (!response.IsSuccessStatusCode)
                return false;

            // A Matrix password login creates a Matrix access token/device.
            // Revoke this temporary session: HolyChat.Api only verifies the
            // password and the Flutter client manages its own Matrix session.
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (!json.RootElement.TryGetProperty("access_token", out var tokenElement)
                || string.IsNullOrWhiteSpace(tokenElement.GetString()))
            {
                _logger.LogError("Matrix login returned no access token for {Username}", username);
                return false;
            }

            using var revokeRequest = new HttpRequestMessage(
                HttpMethod.Post, $"{baseUrl}/_matrix/client/v3/logout");
            revokeRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", tokenElement.GetString());
            using var revokeResponse = await _httpClient.SendAsync(revokeRequest);
            if (!revokeResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Could not revoke the temporary Matrix session for {Username}", username);
                return false;
            }

            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Matrix login verification failed");
            return false;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Matrix login verification timed out");
            return false;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Synapse returned an invalid login response");
            return false;
        }
    }
}
