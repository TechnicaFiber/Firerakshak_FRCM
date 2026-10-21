using System;
using System.Collections.Generic;

namespace FRCM.Models
{
    /// <summary>
    /// Message types supported by the Temperature Sensor Communication Protocol.
    /// </summary>
    public enum SensorMessageType : byte
    {
        TemperatureRequest       = 0x01,
        TemperatureResponse      = 0x02,
        StartPeriodic            = 0x03,
        PeriodicTemperatureData = 0x04,
        StopPeriodic             = 0x05
    }

    /// <summary>
    /// Represents a decoded sensor reading from the microcontroller.
    /// </summary>
    public class SensorReading
    {
        /// <summary>
        /// Sensor Port ID (1 byte: 1..255).
        /// </summary>
        public byte SensorId { get; set; }

        /// <summary>
        /// Decoded temperature in °C (converted from transmitted integer / 100.0).
        /// </summary>
        public double Temperature { get; set; }

        /// <summary>
        /// Timestamp when this reading was received.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public override string ToString()
        {
            return $"Sensor {SensorId}: {Temperature:F2}°C";
        }
    }

    /// <summary>
    /// Represents port/sensor information for a PT100 sensor port.
    /// </summary>
    public class PortInfo
    {
        public byte PortId { get; set; }
        public string PortName => $"Port {PortId}";
        public string SensorType { get; set; } = "PT100";
        public double DefaultTemp { get; set; } = 25.0;

        public override string ToString() => $"{PortName} ({SensorType})";
    }

    /// <summary>
    /// Represents the full frame structure for the protocol packet.
    /// </summary>
    public class SensorPacket
    {
        public ushort StartByte { get; set; } = 0xAA55;
        public SensorMessageType MessageType { get; set; }
        public ushort Length { get; set; }
        public byte SensorCount { get; set; }
        public byte[] SensorData { get; set; } = Array.Empty<byte>();
        public ushort Checksum16 { get; set; }
        public ushort EndByte { get; set; } = 0x55AA;
    }
}
