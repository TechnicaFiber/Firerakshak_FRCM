using System;

namespace FRCM
{
    public class ZoneStateInfo
    {
        public int ZoneId { get; set; }
        public string Name { get; set; } = string.Empty;

        public double StartPoint { get; set; }
        public double EndPoint { get; set; }

        public bool ActiveAlarm { get; set; }
        public bool Enabled { get; set; }

        public double AverageTemperature { get; set; }
        public double MaxTemperature { get; set; }
        public double MinTemperature { get; set; }

        /// <summary>
        /// Rate of Rise in °C/minute. Calculated using a 60-second sliding window.
        /// Typical fire alarm thresholds: 5-15 °C/min.
        /// </summary>
        public double RateOfRise { get; set; }

        /// <summary>
        /// Deviation in °C. Calculated as (MaxTemperature - MinTemperature) within the zone.
        /// </summary>
        public double Deviation { get; set; }

        // Specific alarm flags for cell-level highlighting
        public bool MaxAlarm { get; set; }
        public bool MinAlarm { get; set; }
        public bool PreAlarm { get; set; }
        public bool RorAlarm { get; set; }
        public bool DeviationAlarm { get; set; }

        public DateTime LastUpdate { get; set; }
    }
}