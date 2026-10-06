using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FRCM.Models;

namespace FRCM.Services
{
    /// <summary>
    /// Implements the hardware transport and communication session with the Temperature Sensor Microcontroller.
    /// Operates with 3 PT100 sensor ports (Port 1, Port 2, Port 3).
    /// </summary>
    public class SensorCommunicationService : ISensorCommunicationService
    {
        private SerialPort? _serialPort;
        private readonly object _lock = new object();
        private readonly List<byte> _receiveBuffer = new List<byte>();

        private TaskCompletionSource<List<SensorReading>>? _pendingRequestTcs;
        private CancellationTokenSource? _periodicCts;
        private bool _isPeriodicActive = false;

        public bool IsConnected => (_serialPort != null && _serialPort.IsOpen) || (SimulationMode && _isSimConnected);
        public bool SimulationMode { get; set; } = false; // Default to false for real serial communication
        private bool _isSimConnected = false;
        public string? ConnectedPort => _serialPort?.PortName ?? (SimulationMode ? "SIM" : null);

        /// <summary>
        /// Configurable simulated PT100 temperature readings for the 3 ports.
        /// Only used if user explicitly selects SIM mode.
        /// </summary>
        public Dictionary<byte, double> SimulatedPortTemperatures { get; } = new Dictionary<byte, double>
        {
            { 1, 25.43 },
            { 2, 26.10 },
            { 3, 24.85 }
        };

        public event EventHandler<List<SensorReading>>? PeriodicDataReceived;
        public event EventHandler<string>? LogMessageReceived;

        public SensorCommunicationService(bool enableSimulation = false)
        {
            SimulationMode = enableSimulation;
        }

        /// <summary>
        /// Returns the 3 fixed PT100 sensor ports (Port 1, Port 2, Port 3).
        /// </summary>
        public List<PortInfo> GetConfiguredPorts()
        {
            return new List<PortInfo>
            {
                new PortInfo { PortId = 1, SensorType = "PT100", DefaultTemp = 25.0 },
                new PortInfo { PortId = 2, SensorType = "PT100", DefaultTemp = 25.0 },
                new PortInfo { PortId = 3, SensorType = "PT100", DefaultTemp = 25.0 }
            };
        }

        #region Connection Management

        public Task<bool> ConnectAsync(string portName, int baudRate = 115200)
        {
            lock (_lock)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(portName))
                    {
                        Log("[SensorComm] Invalid port name.");
                        return Task.FromResult(false);
                    }

                    if (portName.Equals("SIM", StringComparison.OrdinalIgnoreCase))
                    {
                        CloseSerialPort();
                        SimulationMode = true;
                        _isSimConnected = true;
                        Log($"[SensorComm] Connected in Simulation Mode.");
                        return Task.FromResult(true);
                    }

                    var availablePorts = SerialPort.GetPortNames();
                    if (!availablePorts.Contains(portName, StringComparer.OrdinalIgnoreCase))
                    {
                        Log($"[SensorComm] Port '{portName}' not found in system ports.");
                        return Task.FromResult(false);
                    }

                    CloseSerialPort();

                    _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
                    {
                        ReadTimeout = 2000,
                        WriteTimeout = 2000,
                        DtrEnable = true,
                        RtsEnable = true
                    };

                    _serialPort.DataReceived += SerialPort_DataReceived;
                    _serialPort.Open();
                    SimulationMode = false;
                    _isSimConnected = false;

                    Log($"[SensorComm] Successfully connected to {portName} @ {baudRate} baud.");
                    return Task.FromResult(true);
                }
                catch (Exception ex)
                {
                    Log($"[SensorComm] Failed to open {portName}: {ex.Message}");
                    CloseSerialPort();
                    return Task.FromResult(false);
                }
            }
        }

        public Task DisconnectAsync()
        {
            lock (_lock)
            {
                StopPeriodicMonitoringInternal();
                CloseSerialPort();
                _isSimConnected = false;
                Log("[SensorComm] Disconnected.");
            }
            return Task.CompletedTask;
        }

        private void CloseSerialPort()
        {
            if (_serialPort != null)
            {
                try
                {
                    if (_serialPort.IsOpen)
                    {
                        _serialPort.DataReceived -= SerialPort_DataReceived;
                        _serialPort.Close();
                    }
                    _serialPort.Dispose();
                }
                catch { }
                _serialPort = null;
            }
        }

        #endregion

        #region Mode 1: Request-Response (0x01 -> 0x02)

        public async Task<List<SensorReading>> RequestTemperaturesAsync(IEnumerable<byte> sensorIds, int timeoutMs = 3000)
        {
            var idList = sensorIds?.Distinct().ToList() ?? new List<byte>();
            if (idList.Count == 0) return new List<SensorReading>();

            // Encode Temperature Request packet (0x01)
            byte[] requestPacket = SensorProtocolCodec.CreateTemperatureRequest(idList);
            Log($"[SensorComm] TX Req (0x01) -> Sensor Count: {idList.Count}, IDs: [{string.Join(", ", idList)}], Packet: {FormatHex(requestPacket)}");

            if (SimulationMode)
            {
                return await SimulateRequestResponseAsync(idList);
            }

            if (_serialPort == null || !_serialPort.IsOpen)
            {
                throw new InvalidOperationException("Not connected to Microcontroller COM port. Please select a COM port and click 'Connect'.");
            }

            // Real Hardware Request-Response over Serial Port
            var tcs = new TaskCompletionSource<List<SensorReading>>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_lock)
            {
                _pendingRequestTcs = tcs;
                _receiveBuffer.Clear();

                try
                {
                    _serialPort.Write(requestPacket, 0, requestPacket.Length);
                }
                catch (Exception ex)
                {
                    _pendingRequestTcs = null;
                    Log($"[SensorComm] Serial write error: {ex.Message}");
                    throw;
                }
            }

            using var cts = new CancellationTokenSource(timeoutMs);
            using (cts.Token.Register(() => tcs.TrySetCanceled()))
            {
                try
                {
                    var result = await tcs.Task;
                    return result;
                }
                catch (TaskCanceledException)
                {
                    Log($"[SensorComm] Request timed out after {timeoutMs}ms on {ConnectedPort}.");
                    throw new TimeoutException($"No response from Microcontroller on {ConnectedPort} within {timeoutMs}ms.");
                }
                finally
                {
                    lock (_lock)
                    {
                        _pendingRequestTcs = null;
                    }
                }
            }
        }

        private async Task<List<SensorReading>> SimulateRequestResponseAsync(List<byte> sensorIds)
        {
            await Task.Delay(100);

            var readings = new List<SensorReading>();
            foreach (var id in sensorIds)
            {
                double temp = SimulatedPortTemperatures.TryGetValue(id, out double t) ? t : 25.0;

                readings.Add(new SensorReading
                {
                    SensorId = id,
                    Temperature = temp,
                    Timestamp = DateTime.UtcNow
                });
            }

            byte[] simResponsePacket = SensorProtocolCodec.EncodeDataPacket(SensorMessageType.TemperatureResponse, readings);
            Log($"[SensorComm-SIM] RX Resp (0x02) Packet: {FormatHex(simResponsePacket)}");

            var decoded = SensorProtocolCodec.DecodeTemperatureResponse(simResponsePacket);
            return decoded;
        }

        #endregion

        #region Mode 2: Periodic Mode (0x03 -> 0x04 -> 0x05)

        public async Task<bool> StartPeriodicAsync(IEnumerable<byte> sensorIds)
        {
            var idList = sensorIds?.Distinct().ToList() ?? new List<byte>();
            if (idList.Count == 0) return false;

            byte[] startPacket = SensorProtocolCodec.CreateStartPeriodic(idList);
            Log($"[SensorComm] TX Start Periodic (0x03) -> Sensors: [{string.Join(", ", idList)}], Packet: {FormatHex(startPacket)}");

            lock (_lock)
            {
                StopPeriodicMonitoringInternal();
                _isPeriodicActive = true;
                _periodicCts = new CancellationTokenSource();
            }

            if (!SimulationMode && _serialPort != null && _serialPort.IsOpen)
            {
                try
                {
                    _serialPort.Write(startPacket, 0, startPacket.Length);
                }
                catch (Exception ex)
                {
                    Log($"[SensorComm] Failed to send Start Periodic: {ex.Message}");
                    return false;
                }
            }
            else if (SimulationMode)
            {
                _ = RunSimulatedPeriodicLoopAsync(idList, _periodicCts.Token);
            }
            else
            {
                throw new InvalidOperationException("Not connected to Microcontroller COM port.");
            }

            return true;
        }

        public async Task<bool> StopPeriodicAsync()
        {
            byte[] stopPacket = SensorProtocolCodec.CreateStopPeriodic();
            Log($"[SensorComm] TX Stop Periodic (0x05) -> Packet: {FormatHex(stopPacket)}");

            lock (_lock)
            {
                StopPeriodicMonitoringInternal();
            }

            if (!SimulationMode && _serialPort != null && _serialPort.IsOpen)
            {
                try
                {
                    _serialPort.Write(stopPacket, 0, stopPacket.Length);
                }
                catch (Exception ex)
                {
                    Log($"[SensorComm] Failed to send Stop Periodic: {ex.Message}");
                    return false;
                }
            }

            return true;
        }

        private void StopPeriodicMonitoringInternal()
        {
            _isPeriodicActive = false;
            if (_periodicCts != null)
            {
                try { _periodicCts.Cancel(); } catch { }
                _periodicCts.Dispose();
                _periodicCts = null;
            }
        }

        private async Task RunSimulatedPeriodicLoopAsync(List<byte> sensorIds, CancellationToken token)
        {
            Log("[SensorComm-SIM] Periodic transmission loop started (1000ms interval).");
            try
            {
                while (!token.IsCancellationRequested && _isPeriodicActive)
                {
                    await Task.Delay(1000, token);

                    var readings = new List<SensorReading>();
                    foreach (var id in sensorIds)
                    {
                        double temp = SimulatedPortTemperatures.TryGetValue(id, out double t) ? t : 25.0;

                        readings.Add(new SensorReading
                        {
                            SensorId = id,
                            Temperature = temp,
                            Timestamp = DateTime.UtcNow
                        });
                    }

                    byte[] simPeriodicPacket = SensorProtocolCodec.EncodeDataPacket(SensorMessageType.PeriodicTemperatureData, readings);
                    var decoded = SensorProtocolCodec.DecodePeriodicData(simPeriodicPacket);

                    PeriodicDataReceived?.Invoke(this, decoded);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Log($"[SensorComm-SIM] Periodic loop error: {ex.Message}");
            }
            Log("[SensorComm-SIM] Periodic transmission loop stopped.");
        }

        #endregion

        #region Serial Data Ingestion & Framing

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (_serialPort == null || !_serialPort.IsOpen) return;

            lock (_lock)
            {
                try
                {
                    int bytesToRead = _serialPort.BytesToRead;
                    byte[] tempBuf = new byte[bytesToRead];
                    _serialPort.Read(tempBuf, 0, bytesToRead);
                    _receiveBuffer.AddRange(tempBuf);

                    ProcessReceiveBuffer();
                }
                catch (Exception ex)
                {
                    Log($"[SensorComm] Serial receive error: {ex.Message}");
                }
            }
        }

        private void ProcessReceiveBuffer()
        {
            // Scan for Start Bytes: 0xAA, 0x55
            while (_receiveBuffer.Count >= SensorProtocolCodec.MIN_PACKET_LENGTH)
            {
                int startIdx = -1;
                for (int i = 0; i <= _receiveBuffer.Count - 2; i++)
                {
                    if (_receiveBuffer[i] == SensorProtocolCodec.START_BYTE_1 &&
                        _receiveBuffer[i + 1] == SensorProtocolCodec.START_BYTE_2)
                    {
                        startIdx = i;
                        break;
                    }
                }

                if (startIdx == -1)
                {
                    if (_receiveBuffer.Count > 0 && _receiveBuffer[^1] == SensorProtocolCodec.START_BYTE_1)
                    {
                        byte last = _receiveBuffer[^1];
                        _receiveBuffer.Clear();
                        _receiveBuffer.Add(last);
                    }
                    else
                    {
                        _receiveBuffer.Clear();
                    }
                    return;
                }

                if (startIdx > 0)
                {
                    _receiveBuffer.RemoveRange(0, startIdx);
                }

                if (_receiveBuffer.Count < 5) return;

                ushort totalLength = (ushort)((_receiveBuffer[3] << 8) | _receiveBuffer[4]);
                if (totalLength < SensorProtocolCodec.MIN_PACKET_LENGTH || totalLength > 1024)
                {
                    _receiveBuffer.RemoveAt(0);
                    continue;
                }

                if (_receiveBuffer.Count < totalLength) return;

                byte[] packetBytes = _receiveBuffer.Take(totalLength).ToArray();
                _receiveBuffer.RemoveRange(0, totalLength);

                if (SensorProtocolCodec.TryValidateFrame(packetBytes, out var msgType, out byte count, out int dataStart, out int dataLen))
                {
                    Log($"[SensorComm] RX Valid Packet (0x{(byte)msgType:X2}) with count={count}.");

                    if (msgType == SensorMessageType.TemperatureResponse)
                    {
                        var readings = SensorProtocolCodec.DecodeTemperatureResponse(packetBytes);
                        _pendingRequestTcs?.TrySetResult(readings);
                    }
                    else if (msgType == SensorMessageType.PeriodicTemperatureData)
                    {
                        var readings = SensorProtocolCodec.DecodePeriodicData(packetBytes);
                        PeriodicDataReceived?.Invoke(this, readings);
                    }
                }
                else
                {
                    Log($"[SensorComm] Warning: Received packet failed Checksum or framing validation.");
                }
            }
        }

        #endregion

        #region Helpers

        private void Log(string message)
        {
            LogMessageReceived?.Invoke(this, message);
            System.Diagnostics.Debug.WriteLine(message);
        }

        private static string FormatHex(byte[] data)
        {
            return BitConverter.ToString(data).Replace("-", " ");
        }

        public void Dispose()
        {
            DisconnectAsync().GetAwaiter().GetResult();
        }

        #endregion
    }
}
