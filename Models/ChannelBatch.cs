using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FRCM.Models
{
    public class ChannelBatch
    {
        public string? ChannelKey { get; set; }
        public int ChannelId { get; set; }
        public string? Name { get; set; }
        public double Length { get; set; }
        public bool Enabled { get; set; }
        public int ScanPeriod { get; set; }
        public int NumberOfZones { get; set; }
        //public int CorrectionLength { get; set; }

        public double CorrectionLength { get; set; }

        public List<ZoneConfigEntry> Zones { get; set; } = new();
    }
}