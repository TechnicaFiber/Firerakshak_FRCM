using System;

namespace FRCM.Models
{
    /// <summary>
    /// Represents the overall system configuration settings (DTSCM model)
    /// </summary>
    public class SystemConfiguration
    {
        public string IpAddress { get; set; } = "";
        public string SubnetMask { get; set; } = "";
        public string Gateway { get; set; } = "";
        public DateTime SystemTime { get; set; }
        public string SoftwareVersion { get; set; } = "";
        public int BackgroundServiceInterval { get; set; }

        // Relay/Zone Limits
        public int NumberOfRelays { get; set; } = 8;
        public int MaxZonesPerChannel { get; set; } = 10;

        // Timing Parameters (milliseconds)
        public int DtsPollingInterval { get; set; } = 6000;
        public int AlarmEvaluationInterval { get; set; } = 1000;
        public int AlarmStatusBroadcastInterval { get; set; } = 2000;
        public int HealthStatusBroadcastInterval { get; set; } = 5000;

        // Rate of Rise Calculation Window (seconds)
        // Sliding window duration for ROR calculation - industry standard is 60 seconds
        // Shorter windows (30s) = more sensitive to rapid changes but more noise
        // Longer windows (90s) = more stable but slower fire detection
        public int RateOfRiseWindowSeconds { get; set; } = 60;

        // Auto-Clear (seconds)
        public int AutoAlarmClearTimeout { get; set; } = 30;

        // System Health Monitoring Interval (seconds)
        // How often the system checks DTS communication, CPU load, memory, and module temperature
        // Default: 120 seconds (2 minutes)
        public int HealthMonitoringIntervalSeconds { get; set; } = 120;

        // DTS Point Resolution (meters)
        // Distance between consecutive temperature measurement points on the fiber.
        // This value depends on the DTS device configuration.
        // Default: 0.4 meters
        // Common values: 0.25m, 0.4m, 0.5m, 1.0m
        public double DtsPointResolutionMeters { get; set; } = 0.4;

        // ============================================================
        // SYSTEM HEALTH THRESHOLDS (Configurable)
        // These thresholds control when Relay 2 (System Health Fault) is triggered
        // ============================================================

        /// <summary>
        /// CPU usage threshold percentage. Fault raised when CPU > this value.
        /// Default: 90%
        /// </summary>
        public double CpuLoadThreshold { get; set; } = 90.0;

        /// <summary>
        /// Memory usage threshold percentage. Fault raised when memory > this value.
        /// Default: 90%
        /// </summary>
        public double MemoryLoadThreshold { get; set; } = 90.0;

        /// <summary>
        /// DTS module high temperature threshold (°C). Fault raised when temp > this value.
        /// Default: 70°C
        /// </summary>
        public double DtsModuleTempHighThreshold { get; set; } = 70.0;

        /// <summary>
        /// DTS module low temperature threshold (°C). Fault raised when temp < this value.
        /// Default: -10°C
        /// </summary>
        public double DtsModuleTempLowThreshold { get; set; } = -10.0;

        /// <summary>
        /// DTS communication timeout (seconds). Fault raised when no response for this duration.
        /// Default: 10 seconds
        /// </summary>
        public int DtsCommunicationTimeoutSeconds { get; set; } = 10;
    }
}
