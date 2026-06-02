using System;
using System.Collections.Generic;
using FireRakshak.FRMC.Core.Models;

namespace FRCM
{
    public class TemperatureDataUpdateEventArgs : EventArgs
    {
        public int ZoneId { get; }
        public List<TemperatureDataPoint> TemperatureData { get; }

        public TemperatureDataUpdateEventArgs(int zoneId, List<TemperatureDataPoint> temperatureData)
        {
            ZoneId = zoneId;
            TemperatureData = temperatureData;
        }
    }
}
