using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace FRCM.Services
{
    /// <summary>
    /// Dedicated WebSocket client for live temperature data streaming.
    /// Connects to FRMC's Data Stream server on port 6165.
    ///
    /// DUAL WEBSOCKET ARCHITECTURE:
    /// - Control WebSocket (port 6164): Commands, config, health, alarms (CustomWebSocketClient)
    /// - Data WebSocket (port 6165): Temperature streaming only (this class)
    ///
    /// This separation prevents temperature streaming from blocking command processing
    /// and eliminates health status delays.
    /// </summary>
    public class DataStreamClient : IDisposable
    {
        private ClientWebSocket? _client;
        private CancellationTokenSource _cts = new();
        private readonly object _ctsLock = new();
        private string _host = "192.168.0.128";
        private int _port = 6165;
        private bool _disposed = false;

        // FM-04: Dedicated background ingestion thread with AboveNormal priority
        private Thread? _ingestionThread;

        // FM-06: Bounded ingestion channel (capacity 100, DropOldest policy) to prevent UI lag / memory overflow
        private Channel<IngestFrame>? _ingestChannel;

        private readonly struct IngestFrame
        {
            public readonly bool IsBinary;
            public readonly byte[]? BinaryData;
            public readonly int Length;
            public readonly string? TextData;

            public IngestFrame(byte[] binaryData, int length)
            {
                IsBinary = true;
                BinaryData = binaryData;
                Length = length;
                TextData = null;
            }

            public IngestFrame(string textData)
            {
                IsBinary = false;
                BinaryData = null;
                Length = 0;
                TextData = textData;
            }
        }

        // Delta broadcasting: per-channel trace cache for merging delta updates.
        // Key: channelId, Value: sorted dictionary of position → temperature
        // On full message: replace entire cache. On delta: merge changed points only.
        private readonly System.Collections.Generic.Dictionary<int, System.Collections.Generic.SortedList<double, double>> _traceCache = new();

        // Binary protocol: cached start position and spacing per channel for delta index→position conversion.
        private readonly System.Collections.Generic.Dictionary<int, (double StartPos, double Spacing)> _channelGeometry = new();

        /// <summary>
        /// Event fired when live temperature data is received.
        /// Payload contains: type, channel, timestamp, data[]
        /// </summary>
        public event EventHandler<TemperatureDataEventArgs>? TemperatureDataReceived;

        /// <summary>
        /// Event fired when the data stream connection is lost.
        /// </summary>
        public event EventHandler? ConnectionLost;

        /// <summary>
        /// Event fired when connected to data stream server.
        /// </summary>
        public event EventHandler? Connected;

        /// <summary>
        /// Gets whether the client is connected to the data stream server.
        /// </summary>
        public bool IsConnected => _client?.State == WebSocketState.Open;

        /// <summary>
        /// Timestamp of the most recent data frame received on the data stream (FM-05).
        /// </summary>
        public DateTime LastDataReceivedUtc { get; private set; } = DateTime.MinValue;

        /// <summary>
        /// Gets the host address used for the current/last connection.
        /// Useful for reconnection and diagnostics.
        /// </summary>
        public string Host => _host;

        /// <summary>
        /// Gets the port number used for the current/last connection.
        /// Useful for reconnection and diagnostics.
        /// </summary>
        public int Port => _port;

        /// <summary>
        /// Connects to the FRMC Data Stream server.
        /// </summary>
        /// <param name="host">The hostname or IP address of the FRMC server</param>
        /// <param name="port">The port number for the data stream WebSocket (default: 6165)</param>
        public async Task ConnectAsync(string host = "", int port = 6165)
        {
            if (_client != null && _client.State == WebSocketState.Open)
                return;

            _host = host;
            _port = port;

            // Reset CTS for new connection
            lock (_ctsLock)
            {
                if (_cts.IsCancellationRequested)
                {
                    _cts.Dispose();
                    _cts = new CancellationTokenSource();
                }
            }

            _client = new ClientWebSocket();

            // Configure TLS/SSL certificate validation for secure connections
            if (Program.FrmcUseSecure)
            {
                // Bypasses SSL certificate errors for self-signed certificates in dev/testing environments.
                _client.Options.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                {
                    if (sslPolicyErrors != System.Net.Security.SslPolicyErrors.None)
                    {
                        Console.WriteLine($"[DATA STREAM WARNING] SSL validation errors: {sslPolicyErrors} for subject {certificate?.Subject}");
                    }
                    return true; // Bypass validation errors (Self-signed development bypass)
                };
            }

            // Centralized URI generation scheme (updates dynamically based on configuration)
            var uri = Program.GetDataStreamUri(host, port);
            Console.WriteLine($"[DATA STREAM] Connecting to {uri}...");

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                await _client.ConnectAsync(uri, timeoutCts.Token);
                Console.WriteLine("[DATA STREAM] Connected successfully");

                Connected?.Invoke(this, EventArgs.Empty);

                // FM-06: Create BoundedChannel with DropOldest mode (capacity 100)
                _ingestChannel = Channel.CreateBounded<IngestFrame>(new BoundedChannelOptions(100)
                {
                    FullMode = BoundedChannelFullMode.DropOldest,
                    SingleWriter = true,
                    SingleReader = true
                });

                // Start channel consumer loop
                _ = Task.Run(() => ProcessIngestChannelAsync());

                // FM-04: Dedicated background ingestion thread with AboveNormal priority
                _ingestionThread = new Thread(IngestionThreadWorker)
                {
                    Name = "FRCM-BinaryStreamConsumer",
                    IsBackground = true,
                    Priority = ThreadPriority.AboveNormal
                };
                _ingestionThread.Start();
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[DATA STREAM] Connection timed out");
                throw new TimeoutException("Failed to connect to FRMC data stream server. Connection timed out after 10 seconds.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DATA STREAM] Connection failed: {ex.Message}");
                
                // Detailed handling for TLS handshake/validation failures
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[DATA STREAM DETAIL] Inner exception: {ex.InnerException.Message}");
                }
                
                throw new Exception($"Failed to connect to FRMC data stream server: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Reconnects to the data stream server using the previously stored host and port.
        /// Useful for automatic reconnection after connection loss.
        /// </summary>
        public async Task ReconnectAsync()
        {
            Console.WriteLine($"[DATA STREAM] Attempting to reconnect to {_host}:{_port}...");
            await ConnectAsync(_host, _port);
        }

        /// <summary>
        /// Subscribes to temperature data for a specific channel.
        /// </summary>
        public async Task SubscribeToChannelAsync(int channelId)
        {
            if (_client?.State != WebSocketState.Open)
            {
                Console.WriteLine($"[DATA STREAM] Cannot subscribe - not connected");
                return;
            }

            var message = JsonSerializer.Serialize(new
            {
                type = "subscribe_channel",
                channelId = channelId
            });

            var bytes = Encoding.UTF8.GetBytes(message);
            await _client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            Console.WriteLine($"[DATA STREAM] Subscribed to channel {channelId}");
        }

        /// <summary>
        /// Unsubscribes from temperature data for a specific channel.
        /// </summary>
        public async Task UnsubscribeFromChannelAsync(int channelId)
        {
            if (_client?.State != WebSocketState.Open)
                return;

            var message = JsonSerializer.Serialize(new
            {
                type = "unsubscribe_channel",
                channelId = channelId
            });

            var bytes = Encoding.UTF8.GetBytes(message);
            await _client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            Console.WriteLine($"[DATA STREAM] Unsubscribed from channel {channelId}");
        }

        /// <summary>
        /// Enables streaming on the data stream server.
        /// Note: Streaming should typically be enabled via the control WebSocket (start_live_measurement).
        /// This method is for direct data stream control if needed.
        /// </summary>
        public async Task EnableStreamingAsync()
        {
            if (_client?.State != WebSocketState.Open)
                return;

            var message = JsonSerializer.Serialize(new { type = "start_streaming" });
            var bytes = Encoding.UTF8.GetBytes(message);
            await _client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            Console.WriteLine("[DATA STREAM] Streaming enabled");
        }

        /// <summary>
        /// Disables streaming on the data stream server.
        /// </summary>
        public async Task DisableStreamingAsync()
        {
            if (_client?.State != WebSocketState.Open)
                return;

            var message = JsonSerializer.Serialize(new { type = "stop_streaming" });
            var bytes = Encoding.UTF8.GetBytes(message);
            await _client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            Console.WriteLine("[DATA STREAM] Streaming disabled");
        }

        /// <summary>
        /// Disconnects from the data stream server.
        /// </summary>
        public async Task DisconnectAsync()
        {
            if (_client != null && _client.State == WebSocketState.Open)
            {
                try
                {
                    await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", CancellationToken.None);
                    Console.WriteLine("[DATA STREAM] Disconnected");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DATA STREAM] Error during disconnect: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Consumer loop processing frames from the bounded channel (FM-06).
        /// Decouples raw socket frame ingestion from JSON/binary deserialization and event dispatching.
        /// </summary>
        private async Task ProcessIngestChannelAsync()
        {
            Console.WriteLine("[DATA STREAM PROCESSOR] Ingestion queue processor started (FM-06 BoundedChannel max: 100)");
            if (_ingestChannel == null) return;

            try
            {
                while (await _ingestChannel.Reader.WaitToReadAsync())
                {
                    while (_ingestChannel.Reader.TryRead(out var frame))
                    {
                        if (frame.IsBinary && frame.BinaryData != null)
                        {
                            ProcessBinaryMessage(frame.BinaryData, frame.Length);
                        }
                        else if (!string.IsNullOrEmpty(frame.TextData))
                        {
                            ProcessMessage(frame.TextData);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DATA STREAM PROCESSOR] Channel processor error: {ex.Message}");
            }
        }

        /// <summary>
        /// Dedicated ingestion worker thread with AboveNormal priority (FM-04).
        /// Reads raw WebSocket packets as fast as possible from the network buffer and writes to the bounded queue.
        /// </summary>
        private void IngestionThreadWorker()
        {
            Console.WriteLine("[DATA STREAM INGESTION THREAD] Started with priority: AboveNormal (FM-04)");
            var buffer = new byte[262144]; // 256KB — covers up to ~65K points (~26km at 0.4m)
            var builder = new StringBuilder();
            bool normalClosure = false;

            // Accumulator for multi-frame binary messages (>128KB would need this)
            using var binaryStream = new System.IO.MemoryStream();
            bool inBinaryMessage = false;

            CancellationToken token;
            lock (_ctsLock)
            {
                token = _cts.Token;
            }

            while (_client != null && _client.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                try
                {
                    var result = _client.ReceiveAsync(new ArraySegment<byte>(buffer), token).GetAwaiter().GetResult();

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Console.WriteLine("[DATA STREAM LISTENER] Close message received");
                        normalClosure = true;
                        break;
                    }

                    // Binary protocol: accumulate frames, process when complete
                    if (result.MessageType == WebSocketMessageType.Binary || inBinaryMessage)
                    {
                        if (!inBinaryMessage)
                        {
                            binaryStream.SetLength(0);
                            inBinaryMessage = true;
                        }
                        binaryStream.Write(buffer, 0, result.Count);

                        if (result.EndOfMessage)
                        {
                            inBinaryMessage = false;
                            byte[] payload = binaryStream.ToArray();
                            _ingestChannel?.Writer.TryWrite(new IngestFrame(payload, payload.Length));
                        }
                        continue;
                    }

                    builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                    if (result.EndOfMessage)
                    {
                        var msg = builder.ToString();
                        builder.Clear();

                        _ingestChannel?.Writer.TryWrite(new IngestFrame(msg));
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("[DATA STREAM LISTENER] Operation cancelled");
                    normalClosure = true;
                    break;
                }
                catch (WebSocketException ex)
                {
                    Console.WriteLine($"[DATA STREAM LISTENER] WebSocket error: {ex.Message}");
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DATA STREAM LISTENER] Error: {ex.Message}");
                    break;
                }
            }

            _ingestChannel?.Writer.TryComplete();
            Console.WriteLine($"[DATA STREAM LISTENER] Exited. Client state: {_client?.State}");

            if (!normalClosure && _client?.State != WebSocketState.Open)
            {
                Console.WriteLine("[DATA STREAM LISTENER] Connection lost - firing event");
                ConnectionLost?.Invoke(this, EventArgs.Empty);
            }
        }

        private void ProcessMessage(string message)
        {
            try
            {
                using var doc = JsonDocument.Parse(message);
                var root = doc.RootElement;

                if (!root.TryGetProperty("MessageType", out var msgType))
                    return;

                string messageType = msgType.GetString() ?? "";

                switch (messageType)
                {
                    case "live_temperature_data":
                        ProcessTemperatureData(root);
                        break;

                    case "data_stream_connected":
                        Console.WriteLine("[DATA STREAM] Server confirmed connection");
                        break;

                    case "streaming_started":
                        Console.WriteLine("[DATA STREAM] Server confirmed streaming started");
                        break;

                    case "streaming_stopped":
                        Console.WriteLine("[DATA STREAM] Server confirmed streaming stopped");
                        break;

                    case "pong":
                        // Heartbeat response
                        break;

                    default:
                        Console.WriteLine($"[DATA STREAM] Unknown message type: {messageType}");
                        break;
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[DATA STREAM] JSON parse error: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes binary temperature data frames from FRMC.
        /// Binary protocol: ~91% smaller than JSON, ~98% less CPU.
        /// </summary>
        private void ProcessBinaryMessage(byte[] buffer, int length)
        {
            try
            {
                var span = new ReadOnlySpan<byte>(buffer, 0, length);
                byte msgType = span[0];

                if (msgType == BinaryProtocol.MSG_TEMP_FULL || msgType == BinaryProtocol.MSG_TEMP_DELTA)
                {
                    var msg = BinaryProtocol.ParseTemperatureData(span);
                    ProcessBinaryTemperatureData(msg);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DATA STREAM] Binary parse error: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes parsed binary temperature data, merging deltas with trace cache.
        /// </summary>
        private void ProcessBinaryTemperatureData(TempDataMessage msg)
        {
            try
            {
                System.Collections.Generic.List<TemperaturePoint> dataPoints;

                if (msg.IsDelta && _traceCache.ContainsKey(msg.ChannelId)
                    && _channelGeometry.ContainsKey(msg.ChannelId))
                {
                    // Delta mode: convert indices to positions and merge into cache
                    var cache = _traceCache[msg.ChannelId];
                    var geo = _channelGeometry[msg.ChannelId];

                    if (msg.DeltaIndices != null)
                    {
                        for (int i = 0; i < msg.DeltaIndices.Count; i++)
                        {
                            double position = geo.StartPos + msg.DeltaIndices[i] * geo.Spacing;
                            cache[position] = msg.Points[i].Temperature;
                        }
                    }

                    // Build complete point list from cache
                    dataPoints = new System.Collections.Generic.List<TemperaturePoint>(cache.Count);
                    foreach (var kvp in cache)
                    {
                        dataPoints.Add(new TemperaturePoint { Position = kvp.Key, Temperature = kvp.Value });
                    }
                }
                else
                {
                    // Full mode: replace cache entirely and save geometry for delta reconstruction
                    var cache = new System.Collections.Generic.SortedList<double, double>(msg.Points.Count);
                    foreach (var pt in msg.Points)
                    {
                        cache[pt.Position] = pt.Temperature;
                    }
                    _traceCache[msg.ChannelId] = cache;

                    // Cache geometry for converting delta indices to positions
                    if (msg.Points.Count >= 2)
                    {
                        _channelGeometry[msg.ChannelId] = (
                            msg.Points[0].Position,
                            msg.Points[1].Position - msg.Points[0].Position);
                    }

                    dataPoints = msg.Points;
                }

                LastDataReceivedUtc = DateTime.UtcNow;

                TemperatureDataReceived?.Invoke(this, new TemperatureDataEventArgs
                {
                    ChannelId = msg.ChannelId,
                    Timestamp = msg.Timestamp,
                    DataPoints = dataPoints

                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DATA STREAM] Error processing binary temp data: {ex.Message}");
            }
        }

        private void ProcessTemperatureData(JsonElement root)
        {
            try
            {
                if (!root.TryGetProperty("Payload", out var payload))
                    return;

                if (!payload.TryGetProperty("type", out var typeProp) ||
                    typeProp.GetString() != "temperatureData")
                    return;

                int channel = payload.TryGetProperty("channel", out var chProp) ? chProp.GetInt32() : -1;
                DateTime timestamp = payload.TryGetProperty("timestamp", out var tsProp)
                    ? DateTime.Parse(tsProp.GetString() ?? DateTime.UtcNow.ToString("O"))
                    : DateTime.UtcNow;

                if (!payload.TryGetProperty("data", out var dataArray))
                    return;

                // Check if this is a delta update (only changed points) or full refresh
                bool isDelta = payload.TryGetProperty("isDelta", out var deltaProp) && deltaProp.GetBoolean();

                // Extract received data points
                var receivedPoints = new System.Collections.Generic.List<TemperaturePoint>();
                foreach (var point in dataArray.EnumerateArray())
                {
                    double position = point.TryGetProperty("Position", out var pos) ? pos.GetDouble() : 0;
                    double temperature = point.TryGetProperty("Temperature", out var temp) ? temp.GetDouble() : 0;
                    receivedPoints.Add(new TemperaturePoint { Position = position, Temperature = temperature });
                }

                // Build complete trace using delta merge or full replace
                System.Collections.Generic.List<TemperaturePoint> dataPoints;

                if (isDelta && _traceCache.ContainsKey(channel))
                {
                    // Delta mode: merge changed points into cached trace
                    var cache = _traceCache[channel];
                    foreach (var pt in receivedPoints)
                    {
                        cache[pt.Position] = pt.Temperature;
                    }

                    // Build complete point list from cache
                    dataPoints = new System.Collections.Generic.List<TemperaturePoint>(cache.Count);
                    foreach (var kvp in cache)
                    {
                        dataPoints.Add(new TemperaturePoint { Position = kvp.Key, Temperature = kvp.Value });
                    }
                }
                else
                {
                    // Full mode: replace cache entirely
                    var cache = new System.Collections.Generic.SortedList<double, double>(receivedPoints.Count);
                    foreach (var pt in receivedPoints)
                    {
                        cache[pt.Position] = pt.Temperature;
                    }
                    _traceCache[channel] = cache;

                    dataPoints = receivedPoints;
                }

                LastDataReceivedUtc = DateTime.UtcNow;

                // Fire event with complete trace (processor/chart always gets full data)
                TemperatureDataReceived?.Invoke(this, new TemperatureDataEventArgs
                {
                    ChannelId = channel,
                    Timestamp = timestamp,
                    DataPoints = dataPoints
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DATA STREAM] Error processing temperature data: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            lock (_ctsLock)
            {
                try
                {
                    _cts?.Cancel();
                }
                catch (ObjectDisposedException) { }
            }

            try
            {
                _ingestChannel?.Writer.TryComplete();
            }
            catch (Exception) { }

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

    /// <summary>
    /// Event arguments for temperature data received events.
    /// </summary>
    public class TemperatureDataEventArgs : EventArgs
    {
        public int ChannelId { get; set; }
        public DateTime Timestamp { get; set; }
        public System.Collections.Generic.List<TemperaturePoint> DataPoints { get; set; } = new();
    }

    /// <summary>
    /// A single temperature data point.
    /// </summary>
    public class TemperaturePoint
    {
        public double Position { get; set; }
        public double Temperature { get; set; }
    }
}
