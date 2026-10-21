using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FRCM
{
    public class CustomWebSocketClient : IWebSocketClient, IDisposable
    {
        private ClientWebSocket? _client;
        private Uri? _uri;
        private CancellationTokenSource _cts = new();
        private readonly object _ctsLock = new();

        // True only when we intentionally disconnect (user logout/app close)
        // Prevents ConnectionLost from firing on voluntary disconnects
        private bool _isDisconnecting = false;

        public bool IsConnected => _client?.State == WebSocketState.Open;

        public event EventHandler<string>? MessageReceived;
        public event EventHandler<byte[]>? BinaryMessageReceived;

        // Session management
        private string? _storedToken;
        private string? _storedUsername;
        private string? _storedRole;
        private DateTime _tokenExpiration = DateTime.MinValue;
        private CancellationTokenSource? _tokenRefreshCts;

        // JWT Secret loaded from configuration (synced with FRMC)
        private readonly string _jwtSecret;

        /// <summary>
        /// Event fired when session expires and re-authentication is required
        /// </summary>
        public event EventHandler? SessionExpired;

        /// <summary>
        /// Event fired when the WebSocket connection is lost (e.g., FRMC gets killed)
        /// </summary>
        public event EventHandler? ConnectionLost;

        // Track pending requests with their waiters for cleanup on timeout
        private readonly ConcurrentDictionary<string, ConcurrentQueue<TaskCompletionSource<JsonElement>>> _pendingByResponse
            = new ConcurrentDictionary<string, ConcurrentQueue<TaskCompletionSource<JsonElement>>>();

        /// <summary>
        /// Initializes the WebSocket client, loading JWT secret from configuration.
        /// </summary>
        public CustomWebSocketClient()
        {
            // Load JWT secret from appsettings.json to stay in sync with FRMC
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (File.Exists(configPath))
                {
                    var config = new ConfigurationBuilder()
                        .AddJsonFile(configPath, optional: false)
                        .Build();
                    _jwtSecret = config["Jwt:Secret"] ?? GetDefaultJwtSecret();
                }
                else
                {
                    _jwtSecret = GetDefaultJwtSecret();
                }
            }
            catch
            {
                _jwtSecret = GetDefaultJwtSecret();
            }
        }

        private static string GetDefaultJwtSecret()
        {
            // Fallback - should match FRMC's appsettings.json Jwt:Secret
            return "your_super_secret_jwt_key_that_is_at_least_32_characters_long";
        }


        public async Task ConnectAsync(Uri uri, string token)
        {
            if (_client != null && _client.State == WebSocketState.Open)
                return;

            // Reset CTS for new connection (handles reconnection after disconnection)
            lock (_ctsLock)
            {
                if (_cts.IsCancellationRequested)
                {
                    _cts.Dispose();
                    _cts = new CancellationTokenSource();
                }
            }

            _isDisconnecting = false; // Reset on every new connection attempt
            _client = new ClientWebSocket();

            // Handle WSS connections and configure TLS/SSL validation
            if (uri.Scheme.Equals("wss", StringComparison.OrdinalIgnoreCase) || Program.FrmcUseSecure)
            {
                // Configure RemoteCertificateValidationCallback for self-signed certificates or secure communication
                _client.Options.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                {
                    // For development / testing environments using self-signed certs, we log policy errors and allow connection.
                    // In a strict production environment, check if errors == SslPolicyErrors.None.
                    if (sslPolicyErrors != System.Net.Security.SslPolicyErrors.None)
                    {
                        Console.WriteLine($"[WS WARNING] SSL certificate validation errors: {sslPolicyErrors} for subject {certificate?.Subject}");
                    }
                    return true; // Bypass validation for self-signed development certificates
                };
            }

            // Use stored token if available and valid, otherwise use provided token
            string tokenToUse = token;
            if (HasValidToken() && !string.IsNullOrEmpty(_storedToken))
            {
                tokenToUse = _storedToken;
                Console.WriteLine("[RECONNECT] Using stored token for authentication");
            }

            var uriWithToken = new Uri($"{uri}?token={Uri.EscapeDataString(tokenToUse)}");

            Console.WriteLine($"[WS] Attempting to connect to {uriWithToken}...");

            // Add timeout to connection
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            try
            {
                Console.WriteLine("[WS] Starting ConnectAsync...");
                await _client.ConnectAsync(uriWithToken, cts.Token);
                Console.WriteLine("[WS] ConnectAsync completed successfully");
                Console.WriteLine($"[WS] WebSocket state: {_client.State}");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("❌ WebSocket connection timed out after 10 seconds");
                throw new TimeoutException("Failed to connect to FRMC server. Connection timed out after 10 seconds.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ WebSocket connection failed: {ex.Message}");
                Console.WriteLine($"[WS] Exception type: {ex.GetType().Name}");
                
                // Detailed handling for TLS handshake/validation failures
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[WS DETAIL] Inner exception: {ex.InnerException.Message}");
                }
                
                Console.WriteLine($"[WS] Stack trace: {ex.StackTrace}");
                throw new Exception($"Failed to connect to FRMC server: {ex.Message}", ex);
            }

            Console.WriteLine("[WS] Starting listener task...");
            _ = Task.Run(() => StartListeningAsync());
            Console.WriteLine("✅ WebSocket connected via interface method.");
        }


        public async Task ConnectAsync(string host = "", bool useSecure = false, int timeoutSeconds = 10)
        {
            if (_client != null && _client.State == WebSocketState.Open)
                return;

            // Reset CTS for new connection (handles reconnection after disconnection)
            lock (_ctsLock)
            {
                if (_cts.IsCancellationRequested)
                {
                    _cts.Dispose();
                    _cts = new CancellationTokenSource();
                }
            }

            _isDisconnecting = false; // Reset on every new connection attempt
            _client = new ClientWebSocket();

            bool secure = useSecure || Program.FrmcUseSecure;

            if (secure)
            {
                // Configure SSL certificate validation (RemoteCertificateValidationCallback)
                // Bypasses certificate verification for development/testing environments.
                Console.WriteLine("[WS WARNING] SSL certificate validation callback configured");
                _client.Options.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                {
                    if (sslPolicyErrors != System.Net.Security.SslPolicyErrors.None)
                    {
                        Console.WriteLine($"[WS WARNING] SSL policy errors: {sslPolicyErrors} for subject {certificate?.Subject}");
                    }
                    return true; // Accept all certificates (Self-signed development bypass)
                };
            }

            // Use stored token if available and valid, otherwise generate new token
            var jwtToken = HasValidToken() && !string.IsNullOrEmpty(_storedToken)
                ? _storedToken
                : GenerateJwt();

            if (HasValidToken())
            {
                Console.WriteLine("[RECONNECT] Using stored token for authentication");
            }

            // Centralized secure path generation preserving legacy 5001/service_ws option
            _uri = secure
                ? (Program.FrmcUseSecure
                    ? new Uri($"wss://{host}:{Program.FrmcControlPort}/general_ws/?token={Uri.EscapeDataString(jwtToken)}")
                    : new Uri($"wss://{host}:5001/service_ws/?token={Uri.EscapeDataString(jwtToken)}"))
                : new Uri($"ws://{host}:{Program.FrmcControlPort}/general_ws/?token={Uri.EscapeDataString(jwtToken)}");

            Console.WriteLine($"[WS] Attempting to connect to {_uri} (timeout: {timeoutSeconds}s)...");

            // Add timeout to connection
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            try
            {
                await _client.ConnectAsync(_uri, cts.Token);
                Console.WriteLine("✅ WebSocket connected.");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"❌ WebSocket connection timed out after {timeoutSeconds} seconds");
                throw new TimeoutException($"Failed to connect to FRMC server. Connection timed out after {timeoutSeconds} seconds.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ WebSocket connection failed: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[WS DETAIL] Inner exception: {ex.InnerException.Message}");
                }
                throw new Exception($"Failed to connect to FRMC server: {ex.Message}", ex);
            }

            _ = Task.Run(() => StartListeningAsync());
        }

        private async Task EnsureConnectedAsync()
        {
            if (_client == null || _client.State != WebSocketState.Open)
            {
                await ConnectAsync();
                // ConnectAsync already starts the listener internally via Task.Run
                // No extra step needed here.
            }
        }

        private string GenerateJwt()
        {
            // Use JWT secret loaded from configuration (synced with FRMC)
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: "FRMC",
                audience: "FRCM",
                claims: new[]
                {
                    new Claim(ClaimTypes.Name, "FRCMClient"),
                    new Claim(ClaimTypes.Role, "Admin")
                },
                expires: DateTime.UtcNow.AddMinutes(30),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        private async Task StartListeningAsync()
        {
            Console.WriteLine("[WS LISTENER] Starting listener task...");
            var buffer = new byte[8192];
            var builder = new StringBuilder();
            bool normalClosure = false;

            // Get the cancellation token safely
            CancellationToken token;
            lock (_ctsLock)
            {
                token = _cts.Token;
            }

            while (_client != null && _client.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                try
                {
                    // Use cancellation token so Dispose() can cancel the listener
                    var result = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Console.WriteLine("[WS LISTENER] Close message received");
                        normalClosure = true;
                        break;
                    }

                    // Binary frames: fire BinaryMessageReceived and skip text processing.
                    // Must check before UTF-8 decoding to avoid corrupting the text StringBuilder.
                    if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        if (result.EndOfMessage)
                        {
                            var binaryData = new byte[result.Count];
                            Buffer.BlockCopy(buffer, 0, binaryData, 0, result.Count);
                            BinaryMessageReceived?.Invoke(this, binaryData);
                        }
                        continue; // Skip text processing for all binary frames
                    }

                    builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                    if (result.EndOfMessage)
                    {
                        var msg = builder.ToString();
                        builder.Clear();

                        // Try parse JSON and route to waiting request by MessageType
                        try
                        {
                            using var doc = JsonDocument.Parse(msg);
                            var root = doc.RootElement.Clone();

                            if (root.TryGetProperty("MessageType", out var mt) && mt.ValueKind == JsonValueKind.String)
                            {
                                string messageType = mt.GetString()!;

                                // Check for authentication failure / session expiration
                                // Only check if Payload is an object (not array)
                                if (root.TryGetProperty("Payload", out var payload) && payload.ValueKind == JsonValueKind.Object)
                                {
                                    if (payload.TryGetProperty("Message", out var msgElem))
                                    {
                                        string message = msgElem.GetString() ?? "";
                                        if (message.Contains("authentication", StringComparison.OrdinalIgnoreCase) ||
                                            message.Contains("session", StringComparison.OrdinalIgnoreCase) ||
                                            message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase))
                                        {
                                            Console.WriteLine($"[SESSION] Session expired or authentication required: {message}");
                                            ClearStoredToken();
                                            SessionExpired?.Invoke(this, EventArgs.Empty);
                                        }
                                    }
                                }


                                if (_pendingByResponse.TryGetValue(messageType, out var queue))
                                {
                                    if (queue.TryDequeue(out var waiter))
                                    {
                                        waiter.TrySetResult(root);
                                        continue;
                                    }
                                }
                            }
                            MessageReceived?.Invoke(this, msg);
                        }
                        catch (JsonException)
                        {

                            MessageReceived?.Invoke(this, msg);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("[WS LISTENER] Operation cancelled");
                    normalClosure = true;
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[WS LISTEN ERROR] " + ex.Message);
                    Console.WriteLine("[WS LISTEN ERROR] Stack trace: " + ex.StackTrace);
                    break;
                }
            }
            Console.WriteLine($"[WS LISTENER] Listener exited. Client state: {_client?.State}");

            // Fire ConnectionLost whenever the server drops us - whether via a clean close frame
            // (normalClosure=true) or an abrupt drop (exception). Only skip if WE chose to disconnect.
            if (!_isDisconnecting)
            {
                Console.WriteLine("[WS LISTENER] Connection lost (server-side) - firing ConnectionLost event");
                ConnectionLost?.Invoke(this, EventArgs.Empty);
            }
        }


        public async Task<JsonElement?> SendCommandAsync(string command, object payload)
        {
            try
            {
                await EnsureConnectedAsync();

                var jsonMsg = JsonSerializer.Serialize(new
                {
                    MessageType = command,
                    Payload = payload
                });

                string expectedResponseType = command + "_response";

                var waiter = new TaskCompletionSource<JsonElement>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                var queue = _pendingByResponse.GetOrAdd(
                    expectedResponseType,
                    _ => new ConcurrentQueue<TaskCompletionSource<JsonElement>>());

                queue.Enqueue(waiter);

                // Send AFTER registering waiter
                var buffer = Encoding.UTF8.GetBytes(jsonMsg);
                await _client!.SendAsync(
                    new ArraySegment<byte>(buffer),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);

                // Use longer timeout for audit trail and event history (can be large datasets)
                // ✅ FIX: Increased default timeout from 5s to 15s to handle large zone broadcasts
                // For long channels (>5km), zone state broadcasts may take longer to complete
                int timeoutSeconds = 15; // Default timeout for most operations
                if (expectedResponseType == "get_audit_trail_response" ||
                    expectedResponseType == "get_event_history_response" ||
                    expectedResponseType == "GetZoneConfiguration_response" ||
                    expectedResponseType == "GetDtsCalibration_response" ||
                    expectedResponseType == "SetDtsCalibration_response")
                {
                    timeoutSeconds = 30; // 30 seconds for large data queries and hardware calibration
                }
                else if (expectedResponseType == "ResetAllConfiguration_response")
                {
                    timeoutSeconds = 60; // Reset triggers Modbus reinitialization on Linux which can take time
                }

                var completed = await Task.WhenAny(
                    waiter.Task,
                    Task.Delay(TimeSpan.FromSeconds(timeoutSeconds))
                );

                if (completed == waiter.Task)
                {
                    Console.WriteLine($"[WS] Response received for {expectedResponseType}");
                    return await waiter.Task;
                }

                // BUGFIX: Clean up timed-out waiter to prevent memory leak
                // Try to cancel the waiter and remove it from the queue
                waiter.TrySetCanceled();

                // Clean up the queue - remove cancelled/completed waiters
                if (_pendingByResponse.TryGetValue(expectedResponseType, out var pendingQueue))
                {
                    // Drain and re-enqueue only non-completed items (excluding our timed-out waiter)
                    var itemsToKeep = new List<TaskCompletionSource<JsonElement>>();
                    while (pendingQueue.TryDequeue(out var item))
                    {
                        if (!item.Task.IsCompleted && item != waiter)
                        {
                            itemsToKeep.Add(item);
                        }
                    }
                    // Re-enqueue valid items
                    foreach (var item in itemsToKeep)
                    {
                        pendingQueue.Enqueue(item);
                    }
                }

                Console.WriteLine($"[WS TIMEOUT] Waiting for {expectedResponseType} - no response after {timeoutSeconds} seconds");
                return null;
            }
            catch (WebSocketException wsEx)
            {
                Console.WriteLine($"[WS SEND ERROR] WebSocket error sending '{command}': {wsEx.Message}");
                return null;
            }
            catch (InvalidOperationException ioEx)
            {
                Console.WriteLine($"[WS SEND ERROR] Invalid operation sending '{command}' (connection may be closed): {ioEx.Message}");
                return null;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"[WS SEND ERROR] '{command}' was cancelled (connection lost).");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WS SEND ERROR] Unexpected error sending '{command}': {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        public async Task SendAsync(string message)
        {
            if (_client?.State == WebSocketState.Open)
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                await _client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        public async Task DisconnectAsync()
        {
            _isDisconnecting = true; // Mark as intentional so ConnectionLost is NOT fired
            if (_client != null && _client.State == WebSocketState.Open)
                await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
        }


        public async Task<DateTime?> GetFrmcTimeAsync()
        {
            var response = await SendCommandAsync("GetFRMCTime", new { });

            if (!response.HasValue)
                return null;

            if (response.Value.TryGetProperty("Payload", out var payload) &&
                payload.TryGetProperty("CurrentTime", out var timeElem))
                return DateTime.Parse(timeElem.GetString()!);

            return null;
        }

        public async Task<string> SetFrmcTimeAsync(DateTime newTime)
        {
            var response = await SendCommandAsync("SetFRMCTime", new
            {
                DeviceTime = newTime.ToString("O")
            });

            if (response.HasValue &&
                response.Value.TryGetProperty("Payload", out var payload) &&
                payload.TryGetProperty("Message", out var msg))
                return msg.GetString() ?? "No response";

            return "No response from FRMC.";
        }

        public async Task<string?> GetFrmcIpAsync()
        {
            var response = await SendCommandAsync("GetFRMCIP", new { });
            if (!response.HasValue) return null;

            if (response.Value.TryGetProperty("Payload", out var payload) &&
                payload.TryGetProperty("CurrentIP", out var ip))
                return ip.GetString();

            return null;
        }

        public async Task<string> SetFrmcIpAsync(string ip)
        {
            var response = await SendCommandAsync("SetFRMCIP", new { NewIP = ip });
            if (response.HasValue &&
                response.Value.TryGetProperty("Payload", out var payload) &&
                payload.TryGetProperty("Message", out var msg))
                return msg.GetString() ?? "";

            return "No response";
        }

        public async Task<Dictionary<string, List<ZoneInfo>>?> ReadZoneConfigAsync()
        {
            var resp = await SendCommandAsync("GetZoneConfiguration", new { });
            if (!resp.HasValue) return null;

            if (resp.Value.TryGetProperty("Payload", out var payload) &&
                payload.TryGetProperty("Zones", out var zonesElement))
                return JsonSerializer.Deserialize<Dictionary<string, List<ZoneInfo>>>(zonesElement.GetRawText());

            return null;
        }

        public async Task<bool> SendZoneConfigAsync(Dictionary<string, List<ZoneInfo>> zones)
        {
            var resp = await SendCommandAsync("SetZoneConfiguration", new { Zones = zones });
            if (!resp.HasValue) return false;

            if (resp.Value.TryGetProperty("Payload", out var payload) &&
                payload.TryGetProperty("Success", out var ok))
                return ok.GetBoolean();

            return false;
        }

        public async Task<AlarmConfigurationPayload?> ReadAlarmConfigAsync()
        {
            var resp = await SendCommandAsync("GetAlarmConfiguration", new { });
            if (!resp.HasValue) return null;

            return JsonSerializer.Deserialize<AlarmConfigurationPayload>(resp.Value.GetRawText());
        }

        public async Task<bool> SendAlarmConfigAsync(List<AlarmIndex> idx, List<AlarmMapping> map)
        {
            var resp = await SendCommandAsync("SetAlarmConfiguration", new { Indices = idx, Mappings = map });
            if (!resp.HasValue) return false;

            if (resp.Value.TryGetProperty("Payload", out var payload) &&
                payload.TryGetProperty("Success", out var ok))
                return ok.GetBoolean();

            return false;
        }

        /// <summary>
        /// Authenticates with FRMC using username and password.
        /// Returns a LoginResponse object containing token, username, role, and session ID if successful.
        /// Stores the token for future use and auto-reconnect.
        /// </summary>
        public async Task<LoginResponse?> LoginAsync(string username, string password)
        {
            try
            {
                Console.WriteLine($"[LOGIN] Attempting to login as {username}...");

                var resp = await SendCommandAsync("login", new { username, password });

                if (!resp.HasValue)
                {
                    Console.WriteLine("[LOGIN] No response received from server");
                    return null;
                }

                // Parse the response
                if (resp.Value.TryGetProperty("Payload", out var payload))
                {
                    if (payload.TryGetProperty("Success", out var successElem) && successElem.GetBoolean())
                    {
                        var loginResponse = new LoginResponse
                        {
                            Success = true,
                            Token = payload.TryGetProperty("Token", out var tokenElem) ? tokenElem.GetString() : null,
                            Username = payload.TryGetProperty("Username", out var userElem) ? userElem.GetString() : null,
                            Role = payload.TryGetProperty("Role", out var roleElem) ? roleElem.GetString() : null,
                            SessionId = payload.TryGetProperty("SessionId", out var sessionElem) ? sessionElem.GetString() : null
                        };

                        // Store token and session information
                        _storedToken = loginResponse.Token;
                        _storedUsername = loginResponse.Username;
                        _storedRole = loginResponse.Role;
                        _tokenExpiration = ExtractExpirationFromJwt(loginResponse.Token);

                        Console.WriteLine($"[LOGIN] Successfully logged in as {loginResponse.Username} (Role: {loginResponse.Role})");
                        Console.WriteLine($"[TOKEN] Token stored, expires at {_tokenExpiration:yyyy-MM-dd HH:mm:ss} UTC");

                        // FM-02: Start background TokenRefreshWorker to preemptively renew token before expiration
                        StartTokenRefreshWorker();

                        return loginResponse;
                    }
                    else
                    {
                        // Failed login
                        string message = payload.TryGetProperty("Message", out var msgElem) ? msgElem.GetString() ?? "Unknown error" : "Login failed";
                        Console.WriteLine($"[LOGIN] Login failed: {message}");
                        return new LoginResponse { Success = false, Message = message };
                    }
                }

                Console.WriteLine("[LOGIN] Invalid response format");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LOGIN ERROR] {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// FM-02: Parses exp claim from JWT token string to determine exact UTC expiration.
        /// </summary>
        private DateTime ExtractExpirationFromJwt(string? token)
        {
            if (string.IsNullOrEmpty(token))
                return DateTime.UtcNow.AddHours(1);

            try
            {
                var handler = new JwtSecurityTokenHandler();
                if (handler.CanReadToken(token))
                {
                    var jwt = handler.ReadJwtToken(token);
                    if (jwt.ValidTo > DateTime.MinValue)
                    {
                        return jwt.ValidTo;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TOKEN] Error parsing JWT expiration claim: {ex.Message}");
            }

            return DateTime.UtcNow.AddHours(1);
        }

        /// <summary>
        /// FM-02: Starts background TokenRefreshWorker loop to preemptively renew JWT token before expiration.
        /// Runs an initial check after 10 seconds, then checks continuously every 5 minutes.
        /// Preemptively renews token when remaining lifetime < 20 minutes.
        /// </summary>
        private void StartTokenRefreshWorker()
        {
            StopTokenRefreshWorker();

            _tokenRefreshCts = new CancellationTokenSource();
            var token = _tokenRefreshCts.Token;

            _ = Task.Run(async () =>
            {
                Console.WriteLine("[TOKEN WORKER] FM-02 TokenRefreshWorker loop started (initial: 10s, period: 5m)");

                // Initial check after 10 seconds
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), token);
                    await CheckAndRefreshTokenAsync();
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TOKEN WORKER ERROR] {ex.Message}");
                }

                // Continuous periodic check every 5 minutes
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(5), token);
                        if (!IsConnected || string.IsNullOrEmpty(_storedToken))
                            continue;

                        await CheckAndRefreshTokenAsync();
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[TOKEN WORKER ERROR] {ex.Message}");
                    }
                }

                Console.WriteLine("[TOKEN WORKER] FM-02 TokenRefreshWorker stopped.");
            }, token);
        }

        /// <summary>
        /// FM-02: Stops the background TokenRefreshWorker.
        /// </summary>
        private void StopTokenRefreshWorker()
        {
            if (_tokenRefreshCts != null)
            {
                try
                {
                    _tokenRefreshCts.Cancel();
                    _tokenRefreshCts.Dispose();
                }
                catch { }
                _tokenRefreshCts = null;
            }
        }

        /// <summary>
        /// FM-02: Checks if the stored JWT token is nearing expiration and preemptively renews it.
        /// </summary>
        public async Task<bool> CheckAndRefreshTokenAsync()
        {
            if (string.IsNullOrEmpty(_storedToken) || !IsConnected)
                return false;

            var remaining = _tokenExpiration - DateTime.UtcNow;
            Console.WriteLine($"[TOKEN WORKER] Remaining token lifetime: {remaining.TotalMinutes:F1} minutes (Expires: {_tokenExpiration:HH:mm:ss} UTC)");

            // Preemptively refresh when less than 20 minutes remain (or if token has expired)
            if (remaining.TotalMinutes < 20)
            {
                Console.WriteLine("[TOKEN WORKER] Token lifetime under threshold (<20m). Triggering preemptive renewal...");
                return await RefreshTokenAsync();
            }

            return true;
        }

        /// <summary>
        /// FM-02: Asynchronously refreshes the active JWT authentication token with FRMC.
        /// </summary>
        public async Task<bool> RefreshTokenAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_storedToken))
                {
                    Console.WriteLine("[TOKEN REFRESH] Cannot refresh: no stored token available");
                    return false;
                }

                Console.WriteLine("[TOKEN REFRESH] Sending refresh_token command to FRMC...");
                var resp = await SendCommandAsync("refresh_token", new { token = _storedToken });

                if (!resp.HasValue)
                {
                    Console.WriteLine("[TOKEN REFRESH] No response received from server");
                    return false;
                }

                if (resp.Value.TryGetProperty("Payload", out var payload))
                {
                    if (payload.TryGetProperty("Success", out var successElem) && successElem.GetBoolean())
                    {
                        string? newToken = payload.TryGetProperty("Token", out var tokenElem) ? tokenElem.GetString() : null;
                        if (!string.IsNullOrEmpty(newToken))
                        {
                            _storedToken = newToken;
                            _tokenExpiration = ExtractExpirationFromJwt(newToken);
                            Console.WriteLine($"✅ [TOKEN REFRESH] Token successfully renewed. New expiration: {_tokenExpiration:yyyy-MM-dd HH:mm:ss} UTC");
                            return true;
                        }
                    }
                    else
                    {
                        string message = payload.TryGetProperty("Message", out var msgElem) ? msgElem.GetString() ?? "" : "Token renewal failed";
                        Console.WriteLine($"❌ [TOKEN REFRESH] Renewal failed: {message}");
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TOKEN REFRESH ERROR] {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if the stored token is still valid (not expired).
        /// </summary>
        public bool HasValidToken()
        {
            return !string.IsNullOrEmpty(_storedToken) && DateTime.UtcNow < _tokenExpiration;
        }

        /// <summary>
        /// Gets the stored username from the last successful login.
        /// </summary>
        public string? GetStoredUsername() => _storedUsername;

        /// <summary>
        /// Gets the stored role from the last successful login.
        /// </summary>
        public string? GetStoredRole() => _storedRole;

        /// <summary>
        /// Clears stored token and session information.
        /// </summary>
        public void ClearStoredToken()
        {
            StopTokenRefreshWorker();
            _storedToken = null;
            _storedUsername = null;
            _storedRole = null;
            _tokenExpiration = DateTime.MinValue;
            Console.WriteLine("[TOKEN] Stored token cleared");
        }

        /// <summary>
        /// Lists all users from FRMC (Admin only).
        /// </summary>
        public async Task<ListUsersResponse> ListUsersAsync()
        {
            try
            {
                Console.WriteLine("[USER_MGMT] Requesting user list...");

                var resp = await SendCommandAsync("list_users", new { });

                if (!resp.HasValue)
                {
                    Console.WriteLine("[USER_MGMT] No response received");
                    return new ListUsersResponse
                    {
                        Success = false,
                        Message = "No response received from FRMC server. Please check connection."
                    };
                }

                if (resp.Value.TryGetProperty("Payload", out var payload))
                {
                    if (payload.TryGetProperty("Success", out var successElem) && successElem.GetBoolean())
                    {
                        if (payload.TryGetProperty("Users", out var usersElem))
                        {
                            var users = JsonSerializer.Deserialize<List<UserInfo>>(usersElem.GetRawText()) ?? new List<UserInfo>();
                            Console.WriteLine($"[USER_MGMT] Retrieved {users.Count} users");
                            return new ListUsersResponse
                            {
                                Success = true,
                                Message = "Users retrieved successfully",
                                Users = users
                            };
                        }
                    }
                    else
                    {
                        string message = payload.TryGetProperty("Message", out var msgElem) ? msgElem.GetString() ?? "Unknown error" : "Failed to list users";
                        Console.WriteLine($"[USER_MGMT] List users failed: {message}");
                        return new ListUsersResponse
                        {
                            Success = false,
                            Message = message
                        };
                    }
                }

                return new ListUsersResponse
                {
                    Success = false,
                    Message = "Invalid response format from FRMC server"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[USER_MGMT ERROR] {ex.Message}");
                return new ListUsersResponse
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Adds a new user to FRMC (Admin only).
        /// </summary>
        public async Task<UserManagementResponse> AddUserAsync(string username, string password, string role)
        {
            try
            {
                Console.WriteLine($"[USER_MGMT] Adding user '{username}' with role '{role}'...");

                var resp = await SendCommandAsync("add_user", new { username, password, role });

                if (!resp.HasValue)
                {
                    return new UserManagementResponse { Success = false, Message = "No response from server" };
                }

                if (resp.Value.TryGetProperty("Payload", out var payload))
                {
                    bool success = payload.TryGetProperty("Success", out var successElem) && successElem.GetBoolean();
                    string message = payload.TryGetProperty("Message", out var msgElem) ? msgElem.GetString() ?? "" : "";

                    if (success)
                    {
                        Console.WriteLine($"[USER_MGMT] User '{username}' added successfully");
                    }
                    else
                    {
                        Console.WriteLine($"[USER_MGMT] Failed to add user: {message}");
                    }

                    return new UserManagementResponse { Success = success, Message = message };
                }

                return new UserManagementResponse { Success = false, Message = "Invalid response format" };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[USER_MGMT ERROR] {ex.Message}");
                return new UserManagementResponse { Success = false, Message = ex.Message };
            }
        }

        /// <summary>
        /// Deletes a user from FRMC (Admin only).
        /// </summary>
        public async Task<UserManagementResponse> DeleteUserAsync(string username)
        {
            try
            {
                Console.WriteLine($"[USER_MGMT] Deleting user '{username}'...");

                var resp = await SendCommandAsync("delete_user", new { username });

                if (!resp.HasValue)
                {
                    return new UserManagementResponse { Success = false, Message = "No response from server" };
                }

                if (resp.Value.TryGetProperty("Payload", out var payload))
                {
                    bool success = payload.TryGetProperty("Success", out var successElem) && successElem.GetBoolean();
                    string message = payload.TryGetProperty("Message", out var msgElem) ? msgElem.GetString() ?? "" : "";

                    if (success)
                    {
                        Console.WriteLine($"[USER_MGMT] User '{username}' deleted successfully");
                    }
                    else
                    {
                        Console.WriteLine($"[USER_MGMT] Failed to delete user: {message}");
                    }

                    return new UserManagementResponse { Success = success, Message = message };
                }

                return new UserManagementResponse { Success = false, Message = "Invalid response format" };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[USER_MGMT ERROR] {ex.Message}");
                return new UserManagementResponse { Success = false, Message = ex.Message };
            }
        }

        public void Dispose()
        {
            StopTokenRefreshWorker();

            lock (_ctsLock)
            {
                try
                {
                    _cts?.Cancel();
                }
                catch (ObjectDisposedException) { }
            }

            // Cancel all pending requests to prevent memory leaks
            foreach (var kvp in _pendingByResponse)
            {
                while (kvp.Value.TryDequeue(out var waiter))
                {
                    waiter.TrySetCanceled();
                }
            }
            _pendingByResponse.Clear();

            try
            {
                _client?.Dispose();
            }
            catch (ObjectDisposedException) { }

            lock (_ctsLock)
            {
                try
                {
                    _cts?.Dispose();
                }
                catch (ObjectDisposedException) { }
            }

            GC.SuppressFinalize(this);
        }
    }

    // Required DTO
    internal class WebSocketMessage
    {
        public string MessageType { get; set; } = "";
        public JsonElement Payload { get; set; }
    }

    /// <summary>
    /// Response from FRMC login command
    /// </summary>
    public class LoginResponse
    {
        public bool Success { get; set; }
        public string? Token { get; set; }
        public string? Username { get; set; }
        public string? Role { get; set; }
        public string? SessionId { get; set; }
        public string? Message { get; set; }
    }

    /// <summary>
    /// User information without sensitive data
    /// </summary>
    public class UserInfo
    {
        public string Username { get; set; } = string.Empty;
        public int Role { get; set; } // 0 = User, 1 = Admin

        public string RoleName => Role == 1 ? "Admin" : "User";
    }

    /// <summary>
    /// Response from user management commands
    /// </summary>
    public class UserManagementResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response from list users command
    /// </summary>
    public class ListUsersResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<UserInfo> Users { get; set; } = new List<UserInfo>();
    }
}
