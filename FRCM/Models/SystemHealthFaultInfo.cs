using System;

namespace FRCM.Models
{
    /// <summary>
    /// Represents a system health fault from FRMC (mirrors FRMC's SystemHealthFault)
    /// </summary>
    public class SystemHealthFaultInfo
    {
        /// <summary>
        /// Unique identifier for this fault
        /// </summary>
        public int FaultId { get; set; }

        /// <summary>
        /// Type of system health fault (matches SystemHealthFaultType enum)
        /// </summary>
        public int FaultType { get; set; }

        /// <summary>
        /// Whether the fault is currently active
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// When the fault was first detected (UTC)
        /// </summary>
        public DateTime FirstDetectedAt { get; set; }

        /// <summary>
        /// When the fault was last updated (UTC)
        /// </summary>
        public DateTime LastUpdatedAt { get; set; }

        /// <summary>
        /// Current value that triggered the fault
        /// </summary>
        public double CurrentValue { get; set; }

        /// <summary>
        /// Threshold value that was exceeded
        /// </summary>
        public double ThresholdValue { get; set; }

        /// <summary>
        /// Human-readable description of the fault
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Additional details or context
        /// </summary>
        public string? Details { get; set; }

        /// <summary>
        /// Gets a human-readable name for the fault type
        /// </summary>
        public string FaultTypeName => FaultType switch
        {
            0 => "None",
            1 => "DTS Communication Lost",
            2 => "DTS Temperature High",
            3 => "DTS Temperature Low",
            4 => "CPU Load High",
            5 => "Memory Load High",
            6 => "Disk Space Low",
            7 => "Laser Off",
            99 => "System Fault",
            _ => $"Unknown ({FaultType})"
        };
    }
}
