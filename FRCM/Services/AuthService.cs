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

            HttpClient httpClient;
            if (Program.FrmcUseSecure)
            {
                // Configure HttpClientHandler to allow self-signed certificates for development/testing
                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                    {
                        // In production, you would perform stricter validation checks
                        // For development/self-signed certificates, we log warnings and bypass errors
                        if (errors != System.Net.Security.SslPolicyErrors.None)
                        {
                            _logger.Log($"[SSL Warning] SSL Policy Errors observed: {errors}. Cert Subject: {cert?.Subject}");
                        }
                        return true;
                    }
                };
                httpClient = new HttpClient(handler);
            }
            else
            {
                httpClient = new HttpClient();
            }

            try
            {
                using (httpClient)
                {
                    var credentials = new { username, password };
                    var content = new StringContent(JsonSerializer.Serialize(credentials), Encoding.UTF8, "application/json");
                    
                    // Centralized secure/unsecure URI generation
                    var tokenUri = Program.GetTokenUri();
                    _logger.Log($"Sending authentication request to: {tokenUri}");

                    var response = await httpClient.PostAsync(tokenUri, content);
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
            catch (HttpRequestException ex)
            {
                _logger.Log($"[AUTH ERROR] HTTP request error (possible TLS handshake failure, invalid/expired certificate, or hostname mismatch): {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logger.Log($"[AUTH ERROR DETAIL] Inner exception: {ex.InnerException.Message}");
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.Log($"[AUTH ERROR] Unexpected error while getting token: {ex.Message}");
                return null;
            }
        }
    }
    public class TokenResponse
    {
        public string Token { get; set; } = string.Empty;
    }
}
