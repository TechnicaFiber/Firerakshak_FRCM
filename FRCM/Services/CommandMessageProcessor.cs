using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace FRCM.Services
{
    /// <summary>
    /// High-performance command/control message processor using a dedicated background thread.
    /// Handles messages from the control WebSocket (health status, alarms, zone states, etc.)
    ///
    /// Architecture:
    /// - CustomWebSocketClient (network thread) -> Channel -> ProcessingThread -> UI Thread
    /// - Network thread never blocks waiting for processing
    /// - Processing thread handles JSON parsing and data extraction
    /// - UI thread only handles final updates (via callbacks)
    /// </summary>
    public class CommandMessageProcessor : IDisposable
    {
        private readonly Channel<string> _channel;
        private readonly CancellationTokenSource _cts;
        private readonly Task _processingTask;
        private bool _disposed;

        // Callbacks for different message types (called on processing thread)
        public event Action<JsonElement>? OnHealthStatus;
        public event Action<JsonElement>? OnZoneStateUpdate;
        public event Action<JsonElement>? OnAlarmTriggered;
        public event Action<JsonElement>? OnAlarmCleared;
        public event Action<JsonElement>? OnAlarmAutoCleared;
        public event Action<JsonElement>? OnRelayStateUpdate;
        public event Action<JsonElement>? OnChannelConfigUpdated;
        public event Action<JsonElement>? OnActiveAlarmSnapshot;
        public event Action<JsonElement>? OnAllAlarmsCleared;
        public event Action<JsonElement>? OnHealthFaultDetected;
        public event Action<JsonElement>? OnHealthFaultCleared;
        public event Action<JsonElement>? OnMeasurementStarted;
        public event Action<JsonElement>? OnMeasurementStopped;
        public event Action<string, JsonElement>? OnOtherMessage; // For unhandled message types

        // Statistics
        private long _messagesProcessed;
        private long _messagesDropped;
        private long _parseErrors;

        public long MessagesProcessed => Interlocked.Read(ref _messagesProcessed);
        public long MessagesDropped => Interlocked.Read(ref _messagesDropped);
        public long ParseErrors => Interlocked.Read(ref _parseErrors);

        /// <summary>
        /// Creates a new command message processor.
        /// </summary>
        /// <param name="maxQueueSize">Maximum messages in queue before dropping</param>
        public CommandMessageProcessor(int maxQueueSize = 100)
        {
            // Bounded channel - drops oldest when full
            _channel = Channel.CreateBounded<string>(new BoundedChannelOptions(maxQueueSize)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });

            _cts = new CancellationTokenSource();
            _processingTask = Task.Run(() => ProcessingLoopAsync(_cts.Token));

            Console.WriteLine($"[CMD PROCESSOR] Started with queue size {maxQueueSize}");
        }

        /// <summary>
        /// Enqueues a raw JSON message for processing. Non-blocking.
        /// Called from network thread (CustomWebSocketClient).
        /// </summary>
        public bool Enqueue(string jsonMessage)
        {
            if (_disposed || string.IsNullOrEmpty(jsonMessage)) return false;

            if (_channel.Writer.TryWrite(jsonMessage))
            {
                return true;
            }
            else
            {
                Interlocked.Increment(ref _messagesDropped);
                return false;
            }
        }

        private async Task ProcessingLoopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("[CMD PROCESSOR] Processing loop started");

            try
            {
                await foreach (var message in _channel.Reader.ReadAllAsync(cancellationToken))
                {
                    try
                    {
                        ProcessMessage(message);
                        Interlocked.Increment(ref _messagesProcessed);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[CMD PROCESSOR] Error processing message: {ex.Message}");
                        Interlocked.Increment(ref _parseErrors);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[CMD PROCESSOR] Processing loop cancelled");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CMD PROCESSOR] Processing loop error: {ex.Message}");
            }

            Console.WriteLine($"[CMD PROCESSOR] Loop ended. Processed: {_messagesProcessed}, Dropped: {_messagesDropped}, Errors: {_parseErrors}");
        }

        private void ProcessMessage(string jsonMessage)
        {
            using var doc = JsonDocument.Parse(jsonMessage);
            var root = doc.RootElement;

            if (!root.TryGetProperty("MessageType", out var msgTypeProp))
            {
                return;
            }

            string? messageType = msgTypeProp.GetString();
            if (string.IsNullOrEmpty(messageType))
            {
                return;
            }

            // Clone the element since the document will be disposed
            var payload = root.TryGetProperty("Payload", out var p) ? p.Clone() : default;

            // Route to appropriate handler based on message type
            switch (messageType)
            {
                case "health_status":
                    OnHealthStatus?.Invoke(payload);
                    break;

                case "zone_state_update":
                case "zone_state_broadcast":
                    OnZoneStateUpdate?.Invoke(payload);
                    break;

                case "alarm_triggered":
                    OnAlarmTriggered?.Invoke(payload);
                    break;

                case "alarm_cleared":
                    OnAlarmCleared?.Invoke(payload);
                    break;

                case "alarm_auto_cleared":
                    OnAlarmAutoCleared?.Invoke(payload);
                    break;

                case "relay_state_update":
                case "relay_state_changed":
                    OnRelayStateUpdate?.Invoke(payload);
                    break;

                case "channel_config_updated":
                    OnChannelConfigUpdated?.Invoke(payload);
                    break;

                case "active_alarm_snapshot":
                    OnActiveAlarmSnapshot?.Invoke(payload);
                    break;

                case "all_alarms_cleared":
                    OnAllAlarmsCleared?.Invoke(payload);
                    break;

                case "health_fault_detected":
                    OnHealthFaultDetected?.Invoke(payload);
                    break;

                case "health_fault_cleared":
                    OnHealthFaultCleared?.Invoke(payload);
                    break;

                case "measurement_started":
                    OnMeasurementStarted?.Invoke(payload);
                    break;

                case "measurement_stopped":
                    OnMeasurementStopped?.Invoke(payload);
                    break;

                // Skip response messages (handled by SendCommandAsync)
                case string s when s.EndsWith("_response"):
                    break;

                // Skip live temperature data (handled by DataStreamClient now)
                case "live_temperature_data":
                    break;

                default:
                    OnOtherMessage?.Invoke(messageType, payload);
                    break;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Console.WriteLine("[CMD PROCESSOR] Disposing...");

            _channel.Writer.Complete();
            _cts.Cancel();

            try
            {
                _processingTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch { }

            _cts.Dispose();

            Console.WriteLine($"[CMD PROCESSOR] Disposed. Total processed: {_messagesProcessed}");
        }
    }
}
