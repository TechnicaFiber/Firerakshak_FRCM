using System;

namespace FireRakshak.FRMC.Core.Models
{

    public class TemperatureDataPoint
    {
        public double Temperature { get; set; }
        public double Position { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public TemperatureDataPoint(double temperature, double position)
        {
            Temperature = temperature;
            Position = position;
            Timestamp = DateTime.UtcNow; // Set timestamp on creation
        }
    }
    

}