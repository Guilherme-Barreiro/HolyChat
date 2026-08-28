using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace HolyChat.Api.Services;

public interface ISynapseAdminService
{
    /// <summary>
    /// Cria uma conta no Synapse via Admin API. Lança SynapseAdminException
    /// em caso de falha (ex: username já existe, token inválido).
    /// </summary>
    Task CreateUserAsync(string username, string password, string displayName);
}

public class SynapseAdminException : Exception
{
    public SynapseAdminException(string message) : base(message) { }
}

public class SynapseAdminService : ISynapseAdminService
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
        var baseUrl = _configuration["Synapse:BaseUrl"]
            ?? throw new InvalidOperationException("Synapse:BaseUrl not configured");
        var adminToken = _configuration["Synapse:AdminAccessToken"]
            ?? throw new InvalidOperationException("Synapse:AdminAccessToken not configured");
        var serverName = _configuration["Synapse:ServerName"] ?? "holychat.cc";

        var matrixUserId = $"@{username}:{serverName}";
        // A Admin API v2 usa PUT idempotente no user_id completo, URL-encoded.
        var url = $"{baseUrl}/_synapse/admin/v2/users/{Uri.EscapeDataString(matrixUserId)}";

        var payload = new
        {
            password,
            displayname = displayName,
            admin = false
        };

        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogError("Synapse admin user creation failed for {Username}: {StatusCode} {Body}",
                username, response.StatusCode, body);
            throw new SynapseAdminException($"Failed to create Matrix account: {response.StatusCode}");
        }
    }
}
