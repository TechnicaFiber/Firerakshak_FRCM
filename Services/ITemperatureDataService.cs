using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using FireRakshak.FRMC.Core.Models;

namespace FRCM
{
    public interface ITemperatureDataService
    {
        event EventHandler<TemperatureDataUpdateEventArgs> TemperatureDataUpdated;
        Task StartReceivingData(string username, string password);
        Task StopReceivingData();
        TemperatureDataPoint[]? GetChannelData(int channelId);
        int MaxChannels { get; }
    }
}
