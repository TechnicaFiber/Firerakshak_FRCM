using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FRCM.Models;

namespace FRCM.Services
{
    /// <summary>
    /// Service interface for communication between DTSCM and the Temperature Sensor Microcontroller.
    /// Operates with 3 PT100 sensor ports (Port 1, Port 2, Port 3).
    /// </summary>
    public interface ISensorCommunicationService : IDisposable
    {
        /// <summary>
        /// Gets whether the service is currently connected to the microcontroller or active in simulation.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// When enabled, simulates microcontroller responses with realistic temperature readings if hardware is unavailable.
        /// </summary>
        bool SimulationMode { get; set; }

        /// <summary>
        /// Configurable simulated PT100 temperatures for the 3 ports (Port ID -> Base Temperature °C).
        /// Default: Port 1 = 25.43°C, Port 2 = 26.10°C, Port 3 = 24.85°C.
        /// </summary>
        Dictionary<byte, double> SimulatedPortTemperatures { get; }

        /// <summary>
        /// Gets the list of the 3 configured PT100 sensor ports (Port 1, Port 2, Port 3).
        /// </summary>
        List<PortInfo> GetConfiguredPorts();

        /// <summary>
        /// Gets the current connected port name (e.g. "COM3", "COM1", or "SIM").
        /// </summary>
        string? ConnectedPort { get; }

        /// <summary>
        /// Connects to the specified Serial / COM port.
        /// </summary>
        Task<bool> ConnectAsync(string portName, int baudRate = 115200);

        /// <summary>
        /// Disconnects from the serial port.
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// Mode 1 (Request-Response): Sends a Temperature Request (0x01) for the given Sensor IDs
        /// and waits for the Temperature Response (0x02) from the Microcontroller.
        /// </summary>
        Task<List<SensorReading>> RequestTemperaturesAsync(IEnumerable<byte> sensorIds, int timeoutMs = 3000);

        /// <summary>
        /// Mode 2 (Periodic): Sends a Start Periodic command (0x03) for the given Sensor IDs.
        /// Incoming Periodic Temperature Data (0x04) will be raised via <see cref="PeriodicDataReceived"/>.
        /// </summary>
        Task<bool> StartPeriodicAsync(IEnumerable<byte> sensorIds);

        /// <summary>
        /// Mode 2 (Periodic): Sends a Stop Periodic command (0x05) to halt periodic transmission.
        /// </summary>
        Task<bool> StopPeriodicAsync();

        /// <summary>
        /// Event raised when periodic temperature packets (0x04) are received from the Microcontroller.
        /// </summary>
        event EventHandler<List<SensorReading>>? PeriodicDataReceived;

        /// <summary>
        /// Event raised for communication and diagnostic log messages.
        /// </summary>
        event EventHandler<string>? LogMessageReceived;
    }
}
