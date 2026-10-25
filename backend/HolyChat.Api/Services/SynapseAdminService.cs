using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HolyChat.Api.Services;

public interface ISynapseAdminService
{
    Task CreateUserAsync(string username, string password, string displayName);
}

public class SynapseAdminException : Exception
{
    public SynapseAdminException(string message) : base(message) { }
    public SynapseAdminException(string message, Exception inner) : base(message, inner) { }
}

public sealed class SynapseUserAlreadyExistsException : SynapseAdminException
{
    public SynapseUserAlreadyExistsException()
        : base("A Matrix account with this username already exists.") { }
}

public sealed class SynapseAdminService : ISynapseAdminService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SynapseAdminService> _logger;

    public SynapseAdminService(HttpClient httpClient, IConfiguration configuration, ILogger<SynapseAdminService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task CreateUserAsync(string username, string password, string displayName)
    {
        var baseUrl = _configuration["Synapse:BaseUrl"]?.TrimEnd('/')
            ?? throw new InvalidOperationException("Synapse:BaseUrl not configured");
        var sharedSecret = _configuration["Synapse:RegistrationSharedSecret"];
        if (string.IsNullOrWhiteSpace(sharedSecret))
        {
            // Never fall back to PUT /admin/v2/users: PUT resets existing users' passwords.
            throw new SynapseAdminException("Synapse:RegistrationSharedSecret is not configured");
        }

        // Synapse's shared-secret registration endpoint is create-only. Unlike
        // PUT /_synapse/admin/v2/users/{userId}, it cannot change an existing
        // account's password, even if two registrations race.
        var registrationUrl = $"{baseUrl}/_synapse/admin/v1/register";
        try
        {
            using var nonceResponse = await _httpClient.GetAsync(registrationUrl);
            if (!nonceResponse.IsSuccessStatusCode)
            {
                throw new SynapseAdminException(
                    $"Could not obtain Synapse registration nonce ({(int)nonceResponse.StatusCode})");
            }

            using var nonceDocument = JsonDocument.Parse(await nonceResponse.Content.ReadAsStringAsync());
            var nonce = nonceDocument.RootElement.GetProperty("nonce").GetString();
            if (string.IsNullOrEmpty(nonce))
            {
                throw new SynapseAdminException("Synapse returned an invalid registration nonce");
            }

            // The HMAC-SHA1 format is mandated by Synapse's registration API.
            // It authenticates this administrative request; it is NOT used for
            // message encryption or password storage.
            var signedData = $"{nonce}\0{username}\0{password}\0notadmin";
            var mac = Convert.ToHexString(HMACSHA1.HashData(
                Encoding.UTF8.GetBytes(sharedSecret),
                Encoding.UTF8.GetBytes(signedData))).ToLowerInvariant();

            var payload = new { nonce, username, password, displayname = displayName, admin = false, mac };
            using var request = new HttpRequestMessage(HttpMethod.Post, registrationUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            using var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.BadRequest && IsUserAlreadyRegistered(responseBody))
                    throw new SynapseUserAlreadyExistsException();

                _logger.LogWarning("Synapse registration rejected for {Username} (HTTP {StatusCode})",
                    username, (int)response.StatusCode);
                throw new SynapseAdminException($"Synapse registration failed (HTTP {(int)response.StatusCode})");
            }

            // Shared-secret registration returns an access token for the freshly
            // registered device. Revoke it: our Flutter app establishes its own
            // Matrix session after signing in.
            using var registrationResult = JsonDocument.Parse(responseBody);
            if (registrationResult.RootElement.TryGetProperty("access_token", out var accessTokenElement))
            {
                var accessToken = accessTokenElement.GetString();
                if (!string.IsNullOrWhiteSpace(accessToken))
                {
                    try
                    {
                        using var logoutRequest = new HttpRequestMessage(
                            HttpMethod.Post, $"{baseUrl}/_matrix/client/v3/logout");
                        logoutRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                        using var logoutResponse = await _httpClient.SendAsync(logoutRequest);
                        if (!logoutResponse.IsSuccessStatusCode)
                            _logger.LogWarning("Could not revoke registration session for {Username}", username);
                    }
                    catch (HttpRequestException ex)
                    {
                        // The account has already been created; failing now
                        // would leave it orphaned from the HolyChat database.
                        _logger.LogWarning(ex, "Could not revoke registration session for {Username}", username);
                    }
                }
            }
        }
        catch (HttpRequestException ex)
        {
            throw new SynapseAdminException("Synapse is unreachable", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new SynapseAdminException("Synapse request timed out", ex);
        }
    }

    private static bool IsUserAlreadyRegistered(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            return json.RootElement.TryGetProperty("errcode", out var code)
                && code.GetString() == "M_USER_IN_USE";
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
