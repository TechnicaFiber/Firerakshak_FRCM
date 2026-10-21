using FireRakshak.FRMC.Core.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace FRCM
{
    public class TemperatureDataService : ITemperatureDataService
    {
        private readonly IWebSocketClient _webSocketClient;
        private readonly IAuthService _authService;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<int, TemperatureDataPoint[]> _channelData = new ConcurrentDictionary<int, TemperatureDataPoint[]>();
        // Centralized URI generation scheme (updates dynamically based on configuration)
        private Uri _webSocketServerUri => Program.GetControlWebSocketUri();
        public event EventHandler<TemperatureDataUpdateEventArgs>? TemperatureDataUpdated;
        public int MaxChannels { get; } = 8;

        public TemperatureDataService(IWebSocketClient webSocketClient, IAuthService authService, ILogger logger)
        {
            _webSocketClient = webSocketClient;
            _authService = authService;
            _logger = logger;
            _webSocketClient.MessageReceived += OnWebSocketMessageReceived;
        }
        public async Task StartReceivingData(string username, string password)
        {
            try
            {
                // FIX: Skip connection if already connected (Program.cs already connects the WebSocket)
                // This service only needs to listen for messages, not create a new connection
                if (_webSocketClient.IsConnected)
                {
                    _logger.Log("WebSocket already connected. Temperature data service ready to receive data.");
                    return;
                }

                var token = await _authService.GetTokenAsync(username, password);
                if (string.IsNullOrEmpty(token))
                {
                    _logger.Log("Failed to get authentication token. Cannot start receiving data.");
                    return;
                }

                await _webSocketClient.ConnectAsync(_webSocketServerUri, token);
            }
            catch (Exception ex)
            {
                _logger.Log($"Error starting data reception: {ex.Message}");
                throw;
            }
        }


        public async Task StopReceivingData()
        {
            await _webSocketClient.DisconnectAsync();
        }

        public TemperatureDataPoint[]? GetChannelData(int channelId)
        {
            _channelData.TryGetValue(channelId, out var data);
            return data;
        }


        private void OnWebSocketMessageReceived(object? sender, string messageString)
        {
            // FIX: Removed excessive logging - only log errors, not every message
            try
            {
                var webSocketMessage = JsonSerializer.Deserialize<WebSocketMessage>(messageString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (webSocketMessage?.MessageType?.Equals("live_temperature_data", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var liveData = webSocketMessage.Payload.Deserialize<LiveTemperatureDataPayload>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (liveData != null)
                    {
                        _channelData[liveData.ZoneId] = liveData.Data;
                        // FIX: Removed per-point logging to file - was causing severe performance issues
                        // and disk space consumption with thousands of points per second
                        TemperatureDataUpdated?.Invoke(this, new TemperatureDataUpdateEventArgs(liveData.ZoneId, liveData.Data.ToList()));
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.Log($"JsonException during message processing in TemperatureDataService: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.Log($"General Exception during message processing in TemperatureDataService: {ex.Message}");
            }
        }
    }
    public class LiveTemperatureDataPayload
    {
        public int ZoneId { get; set; }
        public TemperatureDataPoint[] Data { get; set; } = Array.Empty<TemperatureDataPoint>();
    }
}
