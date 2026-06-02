using System;
using System.Collections.Generic;

namespace FRCM
{
    public class ChannelStateInfo
    {
        public int ChannelId { get; set; }
        public int Length { get; set; }
        public List<double> CurrentData { get; set; } = new();
        public bool IsActive { get; set; }
        public DateTime LastUpdate { get; set; }
    }
}
