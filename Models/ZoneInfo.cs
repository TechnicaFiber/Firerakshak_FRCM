


using System;
using System.Text.Json.Serialization;

namespace FRCM
{
    /// <summary>
    /// Represents a single Zone configuration.
    /// </summary>
    public class ZoneInfo
    {
        public int ChannelId { get; set; }

        public int ZoneId { get; set; }

        /// <summary>
        /// Display-only composite Zone ID in format "ChannelId.ZoneId" (e.g., "1.1" for Channel 1, Zone 1)
        /// </summary>
        [JsonIgnore]
        public string CompositeZoneId => $"{ChannelId}.{ZoneId}";

        public string Name { get; set; } = "";

        public double StartPoint { get; set; }

        public double EndPoint { get; set; }

        public double MaxTemp { get; set; }

        public double MinTemp { get; set; }

        /// <summary>
        /// Rate of Rise threshold in °C/minute.
        /// Typical fire detection thresholds: 5-15 °C/min.
        /// </summary>
        public double RoRThreshold { get; set; }

        public double PreAlarm { get; set; }

        /// <summary>
        /// Deviation threshold in °C.
        /// Alarm triggers when (MaxTemp - MinTemp) exceeds this value.
        /// </summary>
        public double DeviationThreshold { get; set; }

        // ------------------------------
        // Alarm enable flags from FRMC
        // ------------------------------

        [JsonPropertyName("isMaxTempEnabled")]
        public bool IsMaxTempEnabled { get; set; }

        [JsonPropertyName("isMinTempEnabled")]
        public bool IsMinTempEnabled { get; set; }

        [JsonPropertyName("isPreAlarmEnabled")]
        public bool IsPreAlarmEnabled { get; set; }

        [JsonPropertyName("isRateOfRiseEnabled")]
        public bool IsRateOfRiseEnabled { get; set; }

        [JsonPropertyName("isDeviationEnabled")]
        public bool IsDeviationEnabled { get; set; }

        public bool Enabled { get; set; }

        // ------------------------------
        // Relay control properties
        // ------------------------------

        /// <summary>
        /// Relay assigned to this zone. When ANY enabled alarm triggers for this zone,
        /// the assigned relay will be activated automatically.
        /// </summary>
        [JsonPropertyName("assignedRelayId")]
        public int? AssignedRelayId { get; set; }
    }
}
