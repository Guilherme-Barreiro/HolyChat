using System.Text;
using System.Text.Json;

namespace HolyChat.Api.Services;

public interface IMatrixAuthService
{
    /// <summary>
    /// Tenta autenticar contra o Synapse. Devolve true se as credenciais
    /// forem válidas (o Synapse aceitou o login), false caso contrário.
    /// </summary>
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
        var synapseBaseUrl = _configuration["Synapse:BaseUrl"]
            ?? throw new InvalidOperationException("Synapse:BaseUrl not configured");

        var payload = new
        {
            type = "m.login.password",
            identifier = new
            {
                type = "m.id.user",
                user = username
            },
            password = password
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(
                $"{synapseBaseUrl}/_matrix/client/v3/login",
                content);

            // 200 OK = Synapse aceitou as credenciais.
            // 403 = username/password errados. Qualquer outro erro
            // (rede, Synapse em baixo) também deve resultar em falha de login,
            // nunca em bypass.
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to reach Synapse for login validation");
            return false;
        }
    }
}
