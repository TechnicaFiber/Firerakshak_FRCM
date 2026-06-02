using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace FRCM.Services
{
    /// <summary>
    /// Binary deserialization protocol for high-frequency WebSocket messages from FRMC.
    /// Matches the serialization format in FireRakshak.FRMC.Core.Models.BinaryProtocol.
    /// </summary>
    public static class BinaryProtocol
    {
        // Message type IDs (first byte of every binary message)
        public const byte MSG_TEMP_FULL = 0x01;
        public const byte MSG_TEMP_DELTA = 0x02;
        public const byte MSG_ZONE_BATCH = 0x03;

        // Zone record alarm flag bits
        public const byte FLAG_ENABLED = 0x01;
        public const byte FLAG_ACTIVE_ALARM = 0x02;
        public const byte FLAG_MAX_ALARM = 0x04;
        public const byte FLAG_MIN_ALARM = 0x08;
        public const byte FLAG_PRE_ALARM = 0x10;
        public const byte FLAG_ROR_ALARM = 0x20;
        public const byte FLAG_DEVIATION_ALARM = 0x40;

        /// <summary>
        /// Parses a binary temperature data message (full or delta).
        /// Returns parsed data ready for trace cache merge.
        /// </summary>
        public static TempDataMessage ParseTemperatureData(ReadOnlySpan<byte> data)
        {
            byte msgType = data[0];
            int channelId = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(1));
            long ticks = BinaryPrimitives.ReadInt64LittleEndian(data.Slice(5));
            int count = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(13));

            var result = new TempDataMessage
            {
                ChannelId = channelId,
                Timestamp = new DateTime(ticks, DateTimeKind.Utc),
                IsDelta = msgType == MSG_TEMP_DELTA,
                Points = new List<TemperaturePoint>(count)
            };

            if (msgType == MSG_TEMP_FULL)
            {
                // Full: positions are implicit from startPos + index * spacing
                double startPos = BitConverter.ToDouble(data.Slice(17));
                double spacing = BitConverter.ToDouble(data.Slice(25));

                int pos = 33;
                for (int i = 0; i < count; i++)
                {
                    float temp = BitConverter.ToSingle(data.Slice(pos));
                    result.Points.Add(new TemperaturePoint
                    {
                        Position = startPos + i * spacing,
                        Temperature = temp
                    });
                    pos += 4;
                }
            }
            else // MSG_TEMP_DELTA
            {
                // Delta: each point has index (for position reconstruction) + temperature
                // Caller must merge with cached startPos/spacing to get full positions
                int pos = 17;
                for (int i = 0; i < count; i++)
                {
                    ushort index = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(pos));
                    float temp = BitConverter.ToSingle(data.Slice(pos + 2));
                    result.Points.Add(new TemperaturePoint
                    {
                        Position = index, // Store index temporarily; caller converts to position
                        Temperature = temp
                    });
                    result.DeltaIndices ??= new List<ushort>(count);
                    result.DeltaIndices.Add(index);
                    pos += 6;
                }
            }

            return result;
        }

        /// <summary>
        /// Parses a binary zone batch update message.
        /// </summary>
        public static ZoneBatchMessage ParseZoneBatch(ReadOnlySpan<byte> data)
        {
            int channelId = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(1));
            long ticks = BinaryPrimitives.ReadInt64LittleEndian(data.Slice(5));
            int batchIndex = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(13));
            int totalBatches = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(17));
            int zoneCount = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(21));

            var result = new ZoneBatchMessage
            {
                ChannelId = channelId,
                Timestamp = new DateTime(ticks, DateTimeKind.Utc),
                BatchIndex = batchIndex,
                TotalBatches = totalBatches,
                Zones = new List<ZoneBinaryRecord>(zoneCount)
            };

            int pos = 25;
            for (int i = 0; i < zoneCount; i++)
            {
                var zone = new ZoneBinaryRecord();
                zone.ZoneId = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(pos));
                pos += 4;

                int nameLen = data[pos];
                pos += 1;
                zone.Name = nameLen > 0
                    ? Encoding.UTF8.GetString(data.Slice(pos, nameLen))
                    : $"Zone {zone.ZoneId}";
                pos += nameLen;

                zone.StartPoint = BitConverter.ToSingle(data.Slice(pos)); pos += 4;
                zone.EndPoint = BitConverter.ToSingle(data.Slice(pos)); pos += 4;
                zone.Avg = BitConverter.ToSingle(data.Slice(pos)); pos += 4;
                zone.Max = BitConverter.ToSingle(data.Slice(pos)); pos += 4;
                zone.Min = BitConverter.ToSingle(data.Slice(pos)); pos += 4;
                zone.RateOfRise = BitConverter.ToSingle(data.Slice(pos)); pos += 4;
                zone.Deviation = BitConverter.ToSingle(data.Slice(pos)); pos += 4;
                zone.MaxTempPosition = BitConverter.ToSingle(data.Slice(pos)); pos += 4;

                byte flags = data[pos]; pos += 1;
                zone.Enabled = (flags & FLAG_ENABLED) != 0;
                zone.ActiveAlarm = (flags & FLAG_ACTIVE_ALARM) != 0;
                zone.MaxAlarm = (flags & FLAG_MAX_ALARM) != 0;
                zone.MinAlarm = (flags & FLAG_MIN_ALARM) != 0;
                zone.PreAlarm = (flags & FLAG_PRE_ALARM) != 0;
                zone.RorAlarm = (flags & FLAG_ROR_ALARM) != 0;
                zone.DeviationAlarm = (flags & FLAG_DEVIATION_ALARM) != 0;

                zone.LastUpdate = new DateTime(
                    BinaryPrimitives.ReadInt64LittleEndian(data.Slice(pos)), DateTimeKind.Utc);
                pos += 8;

                result.Zones.Add(zone);
            }

            return result;
        }
    }

    /// <summary>
    /// Parsed temperature data message from binary protocol.
    /// </summary>
    public class TempDataMessage
    {
        public int ChannelId { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsDelta { get; set; }
        public List<TemperaturePoint> Points { get; set; } = new();
        /// <summary>
        /// For delta messages: the original indices of changed points (used for position reconstruction).
        /// </summary>
        public List<ushort>? DeltaIndices { get; set; }
    }

    /// <summary>
    /// Parsed zone batch message from binary protocol.
    /// </summary>
    public class ZoneBatchMessage
    {
        public int ChannelId { get; set; }
        public DateTime Timestamp { get; set; }
        public int BatchIndex { get; set; }
        public int TotalBatches { get; set; }
        public List<ZoneBinaryRecord> Zones { get; set; } = new();
    }

    /// <summary>
    /// A single zone record parsed from binary protocol.
    /// Maps directly to DTSCM's ZoneStateInfo.
    /// </summary>
    public class ZoneBinaryRecord
    {
        public int ZoneId { get; set; }
        public string Name { get; set; } = string.Empty;
        public float StartPoint { get; set; }
        public float EndPoint { get; set; }
        public float Avg { get; set; }
        public float Max { get; set; }
        public float Min { get; set; }
        public float RateOfRise { get; set; }
        public float Deviation { get; set; }
        public float MaxTempPosition { get; set; }
        public bool Enabled { get; set; }
        public bool ActiveAlarm { get; set; }
        public bool MaxAlarm { get; set; }
        public bool MinAlarm { get; set; }
        public bool PreAlarm { get; set; }
        public bool RorAlarm { get; set; }
        public bool DeviationAlarm { get; set; }
        public DateTime LastUpdate { get; set; }
    }
}
