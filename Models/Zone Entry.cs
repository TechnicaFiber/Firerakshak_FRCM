using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FRCM.Models
{
    public class ZoneConfigEntry
    {
        public int ChannelId { get; set; }
        public string? ChannelKey { get; set; }

        //public string? ZoneId { get; set; }

        public int ZoneId { get; set; }
        public string? Name { get; set; }

        public double Start { get; set; }
        public double End { get; set; }
        public double MaxTemp { get; set; }
        public double MinTemp { get; set; }
        public double PreAlarm { get; set; }
        public double RateOfRise { get; set; }
        public bool Enabled { get; set; }

        // Updated names to match your ZoneInfo.cs
        public double DeviationThreshold { get; set; }

        public bool MaxTempEnabled { get; set; }
        public bool MinTempEnabled { get; set; }
        public bool PreAlarmEnabled { get; set; }
        public bool RoREnabled { get; set; }
        public bool DeviationEnabled { get; set; }
    }

}
