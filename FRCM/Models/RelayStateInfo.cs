using System;

namespace FRCM.Models
{
    /// <summary>
    /// Indicates how a relay's state is controlled
    /// </summary>
    public enum RelayControlSource
    {
        Auto,
        Manual
    }

    /// <summary>
    /// Represents the current state of a relay (DTSCM model)
    /// </summary>
    public class RelayStateInfo
    {
        public int RelayId { get; set; }
        public bool IsOn { get; set; }
        public RelayControlSource ControlSource { get; set; }
        public int? AssignedChannelId { get; set; }
        public int? AssignedZoneId { get; set; }
        public DateTime LastStateChange { get; set; }
        public string LastChangedBy { get; set; } = "System";
    }
}
