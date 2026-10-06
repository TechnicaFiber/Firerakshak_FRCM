using System;

namespace FRCM
{
    public class ChannelConfigurationInfo
    {
        public int ChannelId { get; set; }

        public double ChannelLength { get; set; }
        public double Length { get; set; }

        public string Name { get; set; } = string.Empty;

        //public int CorrectionLength { get; set; }

        public double CorrectionLength { get; set; }


        public bool IsEnabled { get; set; }

        public int ScanPeriod { get; set; }
        public int NumberOfZones { get; set; }

        public int FrequencyOfScanningSeconds { get; set; }

      
    }
}
