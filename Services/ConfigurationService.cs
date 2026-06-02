using FireRakshak.FRMC.Core.Models;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Tasks;

namespace FRCM.Services
{
    public interface IConfigurationService
    {
        Task LoadConfigurationAsync();
        ConcurrentDictionary<string, ChannelConfigurationInfo> GetChannels();
        ConcurrentDictionary<string, List<ZoneInfo>> GetZones();
        ConcurrentDictionary<string, ChannelConfigurationInfo> GetStagedChannels();
        ConcurrentDictionary<string, List<ZoneInfo>> GetStagedZones();
        ChannelConfigurationInfo? GetChannelConfig(string channelKey);
        List<ZoneInfo>? GetZoneConfig(string channelKey);
        bool IsLoading { get; }

        /// <summary>
        /// Clears all zone data for all channels. Called after reset to ensure zone IDs start from 1.
        /// </summary>
        void ClearAllZones();
    }

    public class ConfigurationService : IConfigurationService
    {
        private readonly ChannelClient _channelClient;
        private readonly ConcurrentDictionary<string, ChannelConfigurationInfo> _globalChannels = new();
        private readonly ConcurrentDictionary<string, List<ZoneInfo>> _globalZones = new();
        private readonly ConcurrentDictionary<string, ChannelConfigurationInfo> _stagedChannels = new();
        private readonly ConcurrentDictionary<string, List<ZoneInfo>> _stagedZones = new();
        // FIX: Track loading state to prevent race conditions
        private volatile bool _isLoading = false;
        public bool IsLoading => _isLoading;

        public ConfigurationService(ChannelClient channelClient)
        {
            _channelClient = channelClient;
        }

        public async Task LoadConfigurationAsync()
        {
            _isLoading = true;
            Console.WriteLine("🔄 ConfigurationService Initialization Started...");

            try
            {
                _globalChannels.Clear();
                _globalZones.Clear();
                _stagedChannels.Clear();
                _stagedZones.Clear();

                // Step 1: Fetch channel metadata (1 WS call)
                var allChannels = await _channelClient.GetAllChannelsAsync();
                Console.WriteLine($"📡 FRMC returned {allChannels.Count} channels for configuration.");

                // Step 2: Fetch zones per-channel (1 WS call each, ~8 total instead of N per-zone calls)
                foreach (var chCfg in allChannels)
                {
                    string uiName = $"Channel {chCfg.ChannelId}";
                    _globalChannels[uiName] = chCfg;

                    var zones = await _channelClient.GetZoneConfigsForChannelAsync(chCfg.ChannelId);

                    if (zones != null && zones.Count > 0)
                    {
                        //_globalZones[uiName] = zones;
                        _globalZones[uiName] = zones;

                        // 🔥 FIX: Always use COUNT instead of max ZoneId
                        chCfg.NumberOfZones = zones.Count;
                        Console.WriteLine($"   ✔ {uiName}: {zones.Count} zones loaded");
                    }
                    else
                    {
                        Console.WriteLine($"   ⚠ {uiName}: No zones found. Initializing minimum 1 zone...");

                        var defaultZone = new ZoneInfo
                        {
                            ChannelId = chCfg.ChannelId,
                            ZoneId = 1,
                            Name = "Zone 1",
                            Enabled = false,
                            StartPoint = 0,
                            EndPoint = 0
                        };

                        var zoneList = new List<ZoneInfo> { defaultZone };

                        _globalZones[uiName] = zoneList;

                        // Update channel config
                        chCfg.NumberOfZones = 1;

                        // 🔥 SEND TO FRMC ONLY HERE (one-time initialization)
                        await _channelClient.SetChannelZonesPreserveIdsAsync(uiName, zoneList);
                        await _channelClient.SetChannelConfigAsync(chCfg);

                        Console.WriteLine($"   ✔ {uiName}: Minimum 1 zone initialized and pushed to FRMC");
                    }
                }
            }
            finally
            {
                _isLoading = false;
            }
        }

        public ConcurrentDictionary<string, ChannelConfigurationInfo> GetChannels() => _globalChannels;
        public ConcurrentDictionary<string, List<ZoneInfo>> GetZones() => _globalZones;
        public ConcurrentDictionary<string, ChannelConfigurationInfo> GetStagedChannels() => _stagedChannels;
        public ConcurrentDictionary<string, List<ZoneInfo>> GetStagedZones() => _stagedZones;
        public ChannelConfigurationInfo? GetChannelConfig(string channelKey)
        {
            // Check staged first, then global
            if (_stagedChannels.TryGetValue(channelKey, out var staged))
                return staged;

            _globalChannels.TryGetValue(channelKey, out var config);
            return config;
        }
        public List<ZoneInfo>? GetZoneConfig(string channelKey)
        {
            // Check staged first, then global
            if (_stagedZones.TryGetValue(channelKey, out var staged))
                return staged;

            _globalZones.TryGetValue(channelKey, out var zones);
            return zones;
        }


        /// <summary>
        /// Clears all zone data for all channels. Called after reset to ensure zone IDs start from 1.
        /// This creates fresh empty lists for each channel key to ensure no stale zone data remains.
        /// </summary>
        public void ClearAllZones()
        {
            Console.WriteLine("🗑️ ClearAllZones: Clearing all zone caches to reset zone IDs");

            // Get all existing channel keys before clearing
            var channelKeys = _globalZones.Keys.ToList();

            // Clear the dictionary
            _globalZones.Clear();

            // Re-create empty lists for all known channels to ensure zone IDs start from 1
            foreach (var key in channelKeys)
            {
                _globalZones[key] = new List<ZoneInfo>();
                Console.WriteLine($"   ✔ {key}: Zone cache cleared, next zone ID will be 1");
            }

            // Also ensure all 8 channels have empty zone lists
            for (int i = 1; i <= 8; i++)
            {
                string channelKey = $"Channel {i}";
                if (!_globalZones.ContainsKey(channelKey))
                {
                    _globalZones[channelKey] = new List<ZoneInfo>();
                    Console.WriteLine($"   ✔ {channelKey}: Zone cache initialized, next zone ID will be 1");
                }
            }

            Console.WriteLine("✅ ClearAllZones: All zone caches cleared successfully");
        }
    }
}
