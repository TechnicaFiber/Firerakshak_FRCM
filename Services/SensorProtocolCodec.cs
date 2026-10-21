using System;
using System.Collections.Generic;
using System.Linq;
using FRCM.Models;

namespace FRCM.Services
{
    /// <summary>
    /// Binary serializer, deserializer, and Checksum-16 engine for the Temperature Sensor Communication Protocol.
    /// Frame: [ Start Byte (2B) | Message Type (1B) | Length (2B) | Sensor Count (1B) | Sensor Data (Var) | Checksum-16 (2B) | End Byte (2B) ]
    /// </summary>
    public static class SensorProtocolCodec
    {
        public const byte START_BYTE_1 = 0xAA;
        public const byte START_BYTE_2 = 0x55;
        public const ushort START_BYTES = 0xAA55;

        public const byte END_BYTE_1 = 0x55;
        public const byte END_BYTE_2 = 0xAA;
        public const ushort END_BYTES = 0x55AA;

        // Minimum packet length: Start(2) + MsgType(1) + Len(2) + Count(1) + Checksum(2) + End(2) = 10 bytes
        public const int MIN_PACKET_LENGTH = 10;

        #region Packet Creation / Encoding

        /// <summary>
        /// Creates a Temperature Request packet (0x01) for the specified Sensor IDs.
        /// </summary>
        public static byte[] CreateTemperatureRequest(IEnumerable<byte> sensorIds)
        {
            return BuildRequestPacket(SensorMessageType.TemperatureRequest, sensorIds);
        }

        /// <summary>
        /// Creates a Start Periodic monitoring packet (0x03) for the specified Sensor IDs.
        /// </summary>
        public static byte[] CreateStartPeriodic(IEnumerable<byte> sensorIds)
        {
            return BuildRequestPacket(SensorMessageType.StartPeriodic, sensorIds);
        }

        /// <summary>
        /// Creates a Stop Periodic monitoring packet (0x05).
        /// </summary>
        public static byte[] CreateStopPeriodic()
        {
            return BuildRequestPacket(SensorMessageType.StopPeriodic, Enumerable.Empty<byte>());
        }

        /// <summary>
        /// Encodes a request/command packet containing a list of 1-byte Sensor IDs.
        /// Frame: [ Start (2B) | MsgType (1B) | Length (2B) | Sensor Count (1B) | Sensor IDs (Var) | Checksum-16 (2B) | End (2B) ]
        /// </summary>
        private static byte[] BuildRequestPacket(SensorMessageType msgType, IEnumerable<byte> sensorIds)
        {
            var idList = sensorIds?.Distinct().ToList() ?? new List<byte>();
            byte sensorCount = (byte)idList.Count;

            // Total length: 6 (Header) + N (Sensor IDs) + 2 (Checksum) + 2 (End) = 10 + N bytes
            ushort totalLength = (ushort)(6 + idList.Count + 4);
            byte[] packet = new byte[totalLength];

            int idx = 0;

            // 1. Start Byte (2 Bytes: 0xAA 0x55)
            packet[idx++] = START_BYTE_1;
            packet[idx++] = START_BYTE_2;

            // 2. Message Type (1 Byte: 0x01 - 0x05)
            packet[idx++] = (byte)msgType;

            // 3. Length (2 Bytes, Big Endian)
            packet[idx++] = (byte)((totalLength >> 8) & 0xFF);
            packet[idx++] = (byte)(totalLength & 0xFF);

            // 4. Sensor Count (1 Byte)
            packet[idx++] = sensorCount;

            // 5. Sensor Data (Variable: 1 Byte per Sensor ID)
            foreach (var id in idList)
            {
                packet[idx++] = id;
            }

            // 6. Checksum-16 (2 Bytes, calculated over Message Type .. Sensor Data: index 2 to idx-1)
            ushort checksum = CalculateChecksum16(packet, 2, idx - 2);
            packet[idx++] = (byte)((checksum >> 8) & 0xFF);
            packet[idx++] = (byte)(checksum & 0xFF);

            // 7. End Byte (2 Bytes: 0x55 0xAA)
            packet[idx++] = END_BYTE_1;
            packet[idx++] = END_BYTE_2;

            return packet;
        }

        /// <summary>
        /// Encodes a response/data packet (0x02 or 0x04) containing Sensor ID + Temperature records.
        /// Useful for testing, microcontroller simulation, or emulator mocks.
        /// </summary>
        public static byte[] EncodeDataPacket(SensorMessageType msgType, IEnumerable<SensorReading> readings)
        {
            var list = readings?.ToList() ?? new List<SensorReading>();
            byte sensorCount = (byte)list.Count;

            // Each record is 3 bytes (1 byte Sensor ID + 2 bytes Temperature)
            int sensorDataLength = list.Count * 3;
            ushort totalLength = (ushort)(6 + sensorDataLength + 4);
            byte[] packet = new byte[totalLength];

            int idx = 0;
            packet[idx++] = START_BYTE_1;
            packet[idx++] = START_BYTE_2;
            packet[idx++] = (byte)msgType;
            packet[idx++] = (byte)((totalLength >> 8) & 0xFF);
            packet[idx++] = (byte)(totalLength & 0xFF);
            packet[idx++] = sensorCount;

            foreach (var r in list)
            {
                packet[idx++] = r.SensorId;
                // Temperature scaled by 100 (e.g. 25.43 -> 2543)
                short rawTemp = (short)Math.Round(r.Temperature * 100.0);
                packet[idx++] = (byte)((rawTemp >> 8) & 0xFF);
                packet[idx++] = (byte)(rawTemp & 0xFF);
            }

            ushort checksum = CalculateChecksum16(packet, 2, idx - 2);
            packet[idx++] = (byte)((checksum >> 8) & 0xFF);
            packet[idx++] = (byte)(checksum & 0xFF);

            packet[idx++] = END_BYTE_1;
            packet[idx++] = END_BYTE_2;

            return packet;
        }

        #endregion

        #region Packet Decoding / Parsing

        /// <summary>
        /// Decodes a Temperature Response (0x02) packet.
        /// </summary>
        public static List<SensorReading> DecodeTemperatureResponse(byte[] packet)
        {
            if (TryDecodePacket(packet, out var msgType, out var readings))
            {
                if (msgType == SensorMessageType.TemperatureResponse)
                {
                    return readings;
                }
            }
            return new List<SensorReading>();
        }

        /// <summary>
        /// Decodes a Periodic Temperature Data (0x04) packet.
        /// </summary>
        public static List<SensorReading> DecodePeriodicData(byte[] packet)
        {
            if (TryDecodePacket(packet, out var msgType, out var readings))
            {
                if (msgType == SensorMessageType.PeriodicTemperatureData)
                {
                    return readings;
                }
            }
            return new List<SensorReading>();
        }

        /// <summary>
        /// Validates packet framing, length, and Checksum-16, then extracts the message type and sensor readings.
        /// </summary>
        public static bool TryDecodePacket(
            ReadOnlySpan<byte> packet,
            out SensorMessageType messageType,
            out List<SensorReading> readings)
        {
            readings = new List<SensorReading>();
            messageType = 0;

            if (!TryValidateFrame(packet, out messageType, out byte sensorCount, out int dataStart, out int dataLen))
            {
                return false;
            }

            // Decode Sensor Data for response message types
            if (messageType == SensorMessageType.TemperatureResponse ||
                messageType == SensorMessageType.PeriodicTemperatureData)
            {
                int cursor = dataStart;
                for (int i = 0; i < sensorCount; i++)
                {
                    if (cursor + 3 > packet.Length - 4) break;

                    byte sensorId = packet[cursor++];
                    short rawTemp = (short)((packet[cursor++] << 8) | packet[cursor++]);
                    double actualTemp = rawTemp / 100.0;

                    readings.Add(new SensorReading
                    {
                        SensorId = sensorId,
                        Temperature = actualTemp,
                        Timestamp = DateTime.UtcNow
                    });
                }
            }

            return true;
        }

        /// <summary>
        /// Validates basic framing (Start, End, Length, Checksum) and returns key offsets.
        /// </summary>
        public static bool TryValidateFrame(
            ReadOnlySpan<byte> packet,
            out SensorMessageType messageType,
            out byte sensorCount,
            out int dataStart,
            out int dataLen)
        {
            messageType = 0;
            sensorCount = 0;
            dataStart = 0;
            dataLen = 0;

            if (packet.Length < MIN_PACKET_LENGTH) return false;

            // 1. Verify Start Bytes (0xAA 0x55)
            if (packet[0] != START_BYTE_1 || packet[1] != START_BYTE_2) return false;

            // 2. Verify End Bytes (0x55 0xAA)
            if (packet[packet.Length - 2] != END_BYTE_1 || packet[packet.Length - 1] != END_BYTE_2) return false;

            // 3. Extract and verify Length (bytes 3 and 4)
            ushort declaredLength = (ushort)((packet[3] << 8) | packet[4]);
            if (declaredLength != packet.Length) return false;

            // 4. Verify Checksum-16 or CRC-16
            ushort packetChecksum = (ushort)((packet[packet.Length - 4] << 8) | packet[packet.Length - 3]);
            ushort packetChecksumLE = (ushort)((packet[packet.Length - 3] << 8) | packet[packet.Length - 4]);

            int payloadSpanLength = packet.Length - 4 - 2; // [MsgType .. Data]
            var payloadSpan = packet.Slice(2, payloadSpanLength);
            var fullHeaderSpan = packet.Slice(0, packet.Length - 4); // [Start .. Data]

            bool isValidChecksum =
                (CalculateChecksum16(payloadSpan) == packetChecksum) ||
                (CalculateChecksum16(fullHeaderSpan) == packetChecksum) ||
                (CalculateCrc16Ccitt(payloadSpan, 0xFFFF) == packetChecksum) ||
                (CalculateCrc16Ccitt(payloadSpan, 0x0000) == packetChecksum) ||
                (CalculateCrc16Ccitt(fullHeaderSpan, 0xFFFF) == packetChecksum) ||
                (CalculateCrc16Ccitt(fullHeaderSpan, 0x0000) == packetChecksum) ||
                (CalculateCrc16Modbus(payloadSpan) == packetChecksum) ||
                (CalculateCrc16Modbus(fullHeaderSpan) == packetChecksum) ||
                (CalculateCrc16Modbus(payloadSpan) == packetChecksumLE) ||
                (CalculateCrc16Modbus(fullHeaderSpan) == packetChecksumLE) ||
                (CalculateCrc16Ccitt(payloadSpan, 0xFFFF) == packetChecksumLE) ||
                (CalculateCrc16Ccitt(payloadSpan, 0x0000) == packetChecksumLE);

            if (!isValidChecksum) return false;

            messageType = (SensorMessageType)packet[2];
            sensorCount = packet[5];
            dataStart = 6;
            dataLen = packet.Length - 10;

            return true;
        }

        #endregion

        #region Checksum & CRC-16 Calculations

        /// <summary>
        /// Computes 16-bit additive checksum over a byte buffer slice.
        /// </summary>
        public static ushort CalculateChecksum16(byte[] buffer, int offset, int count)
        {
            return CalculateChecksum16(new ReadOnlySpan<byte>(buffer, offset, count));
        }

        /// <summary>
        /// Computes 16-bit additive checksum over a byte span.
        /// Sums all byte values into an unsigned 16-bit integer.
        /// </summary>
        public static ushort CalculateChecksum16(ReadOnlySpan<byte> data)
        {
            ushort sum = 0;
            for (int i = 0; i < data.Length; i++)
            {
                sum = (ushort)(sum + data[i]);
            }
            return sum;
        }

        /// <summary>
        /// Computes CRC-16-CCITT (Poly: 0x1021).
        /// </summary>
        public static ushort CalculateCrc16Ccitt(ReadOnlySpan<byte> data, ushort init = 0xFFFF)
        {
            ushort crc = init;
            for (int i = 0; i < data.Length; i++)
            {
                crc ^= (ushort)(data[i] << 8);
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x8000) != 0)
                        crc = (ushort)((crc << 1) ^ 0x1021);
                    else
                        crc = (ushort)(crc << 1);
                }
            }
            return crc;
        }

        /// <summary>
        /// Computes CRC-16-Modbus (Poly: 0x8005, reversed 0xA001, Init: 0xFFFF).
        /// </summary>
        public static ushort CalculateCrc16Modbus(ReadOnlySpan<byte> data)
        {
            ushort crc = 0xFFFF;
            for (int i = 0; i < data.Length; i++)
            {
                crc ^= data[i];
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x0001) != 0)
                        crc = (ushort)((crc >> 1) ^ 0xA001);
                    else
                        crc = (ushort)(crc >> 1);
                }
            }
            return crc;
        }

        #endregion
    }
}
