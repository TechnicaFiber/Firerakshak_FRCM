using System;

namespace FRCM.Models
{
    /// <summary>
    /// Represents system health parameters from the DTS hardware (DTSCM model)
    /// </summary>
    public class HealthParametersInfo
    {
        public bool LampStatus { get; set; }
        public bool DtsResponsive { get; set; }
        public double ModuleTemperature { get; set; }
        public double LaserCurrent { get; set; }
        public double PumpCurrent { get; set; }
        public double ApdValue { get; set; }
        public DateTime LastCheck { get; set; }

        // System resource monitoring
        public double CpuUsage { get; set; }
        public double MemoryUsage { get; set; }
        public double MemoryUsedGB { get; set; }
        public double MemoryTotalGB { get; set; }
    }
}
