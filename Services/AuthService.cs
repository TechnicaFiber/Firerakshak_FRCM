using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FRCM
{
    public class AuthService : IAuthService
    {
        private readonly ILogger _logger;

        public AuthService(ILogger logger)
        {
            _logger = logger;
        }

        public async Task<string?> GetTokenAsync(string username, string password)
        {
            _logger.Log($"Attempting to get token for user: {username}");
            using (var httpClient = new HttpClient())
            {
                var credentials = new { username, password };
                var content = new StringContent(JsonSerializer.Serialize(credentials), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync($"http://{Program.FrmcHost}:{Program.FrmcControlPort}/get_token", content);
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (tokenResponse?.Token != null)
                    {
                        _logger.Log("Token successfully received.");
                        return tokenResponse.Token;
                    }
                    else
                    {
                        _logger.Log("Token response was successful but token was null or empty.");
                        return null;
                    }
                }
                else
                {
                    _logger.Log($"Failed to get token. Status Code: {response.StatusCode}, Reason: {response.ReasonPhrase}");
                    return null;
                }
            }
        }
    }
    public class TokenResponse
    {
        public string Token { get; set; } = string.Empty;
    }
}
