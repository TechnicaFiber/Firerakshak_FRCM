
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FRCM.Models;
using System.Linq;

namespace FRCM
{
    public class ChannelClient
    {
        private readonly CustomWebSocketClient _client;

        private static readonly JsonSerializerOptions _jsonOptions =
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

        public ChannelClient(CustomWebSocketClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public async Task<ChannelConfigurationInfo?> GetChannelConfigAsync(int uiChannelId)
        {
            if (uiChannelId < 1)
                throw new ArgumentException("UI channel must be >= 1");

            var root = await _client.SendCommandAsync(
                "GetChannelConfig",
                new { ChannelId = uiChannelId }
            );

            if (!root.HasValue) return null;
            if (!root.Value.TryGetProperty("Payload", out var payload)) return null;
            if (!payload.TryGetProperty("Success", out var ok) || !ok.GetBoolean()) return null;
            if (!payload.TryGetProperty("Config", out var cfg)) return null;

            // ⭐ UPDATED: case-insensitive JSON ⭐
            var channel = JsonSerializer.Deserialize<ChannelConfigurationInfo>(cfg.GetRawText(), _jsonOptions);

            if (channel != null)
            {
                channel.Name = $"Channel {uiChannelId}";

                // Derive base length from monitored range minus correction
                channel.Length = channel.ChannelLength - channel.CorrectionLength;
            }


            return channel;
        }
        public async Task<bool> SetChannelConfigAsync(ChannelConfigurationInfo localCfg)
        {
            int channelId = ExtractChannelIdFromName(localCfg.Name);
            if (channelId < 1) channelId = 1;

            //var response = await _client.SendCommandAsync(
            //    "SetChannelConfig",
            //    new
            //    {
            //        Config = new
            //        {
            //            ChannelId = channelId,
            //            ChannelLength = localCfg.Length,
            //            IsEnabled = localCfg.IsEnabled,
            //            ScanPeriod = localCfg.ScanPeriod,
            //            NumberOfZones = localCfg.NumberOfZones
            //        }
            //    });

            var response = await _client.SendCommandAsync(
            "SetChannelConfig",
            new
            {
                Config = new
                {
                    ChannelId = channelId,
                    ChannelLength = localCfg.Length + localCfg.CorrectionLength,
                    CorrectionLength = localCfg.CorrectionLength,
                    ScanPeriod = localCfg.ScanPeriod,
                    NumberOfZones = localCfg.NumberOfZones,
                    IsEnabled = localCfg.IsEnabled
                }
            });

            if (!response.HasValue) return false;
            if (!response.Value.TryGetProperty("Payload", out var payload)) return false;

            return payload.TryGetProperty("Success", out var ok) && ok.GetBoolean();
        }
        public async Task<ZoneInfo?> GetZoneConfigAsync(int uiChannelId, int index)
        {
            if (uiChannelId < 1)
                throw new ArgumentException("UI channel must be >= 1");

            var root = await _client.SendCommandAsync(
                "GetZoneConfig",
                new { ChannelId = uiChannelId, Index = index }
            );

            if (!root.HasValue) return null;
            if (!root.Value.TryGetProperty("Payload", out var payload)) return null;
            if (!payload.TryGetProperty("Success", out var ok) || !ok.GetBoolean()) return null;
            if (!payload.TryGetProperty("Zone", out var cfg)) return null;

            // ⭐ UPDATED: case-insensitive JSON ⭐
            var zone = JsonSerializer.Deserialize<ZoneInfo>(cfg.GetRawText(), _jsonOptions);

            if (zone != null)
            {
                zone.ChannelId = uiChannelId;
            }

            // ✅ Don't overwrite zone name - use the name from FRMC
            // This allows custom zone names to persist

            return zone;
        }

        public async Task<ZoneInfo?> GetZoneConfigInternalAsync(int frmcId, int index)
        {
            var root = await _client.SendCommandAsync(
                "GetZoneConfig",
                new { ChannelId = frmcId, Index = index }
            );

            if (!root.HasValue) return null;
            if (!root.Value.TryGetProperty("Payload", out var payload)) return null;
            if (!payload.TryGetProperty("Success", out var ok) || !ok.GetBoolean()) return null;
            if (!payload.TryGetProperty("Zone", out var cfg)) return null;

            // ⭐ UPDATED: case-insensitive JSON ⭐
            var zone = JsonSerializer.Deserialize<ZoneInfo>(cfg.GetRawText(), _jsonOptions);
            if (zone != null)
            {
                zone.ChannelId = frmcId;
            }
            return zone;
        }

        public async Task<List<ChannelConfigurationInfo>> GetAllChannelsAsync()
        {
            var root = await _client.SendCommandAsync("GetAllChannels", new { });

            var list = new List<ChannelConfigurationInfo>();

            if (!root.HasValue) return list;
            if (!root.Value.TryGetProperty("Payload", out var payload)) return list;
            if (!payload.TryGetProperty("Channels", out var arr)) return list;

            foreach (var el in arr.EnumerateArray())
            {
                // ⭐ UPDATED: case-insensitive JSON ⭐
                var cfg = JsonSerializer.Deserialize<ChannelConfigurationInfo>(el.GetRawText(), _jsonOptions);
                if (cfg != null)
                {
                    // Derive base length from monitored range minus correction
                    cfg.Length = cfg.ChannelLength - cfg.CorrectionLength;
                    list.Add(cfg);
                }
            }

            return list;
        }

        /// <summary>
        /// Fetches all zone configs for a single channel in one WS call.
        /// Uses FRMC's GetZoneConfiguration endpoint with optional ChannelId filter.
        /// </summary>
        public async Task<List<ZoneInfo>?> GetZoneConfigsForChannelAsync(int channelId)
        {
            var root = await _client.SendCommandAsync("GetZoneConfiguration", new { ChannelId = channelId });

            if (!root.HasValue) return null;
            if (!root.Value.TryGetProperty("Payload", out var payload)) return null;
            if (!payload.TryGetProperty("Success", out var ok) || !ok.GetBoolean()) return null;
            if (!payload.TryGetProperty("Zones", out var zones)) return null;

            string channelKey = $"Channel {channelId}";
            if (zones.TryGetProperty(channelKey, out var channelZones))
            {
                var list = JsonSerializer.Deserialize<List<ZoneInfo>>(
                    channelZones.GetRawText(), _jsonOptions);

                if (list != null)
                {
                    foreach (var z in list)
                    {
                        z.ChannelId = channelId;
                    }
                }
                return list;
            }

            return new List<ZoneInfo>();
        }

        public async Task<List<PointData>> GetChannelPointsAsync(int uiChannelId)
        {
            if (uiChannelId < 1)
                throw new ArgumentException("Channel ID must be >= 1");

            var root = await _client.SendCommandAsync(
                "GetChannelPoints",
                new { ChannelId = uiChannelId }
            );

            var list = new List<PointData>();

            if (!root.HasValue) return list;
            if (!root.Value.TryGetProperty("Payload", out var payload)) return list;
            if (!payload.TryGetProperty("Success", out var ok) || !ok.GetBoolean()) return list;
            if (!payload.TryGetProperty("Points", out var points)) return list;

            foreach (var pt in points.EnumerateArray())
            {
                if (pt.TryGetProperty("Position", out var pos) &&
                    pt.TryGetProperty("Temperature", out var temp))
                {
                    list.Add(new PointData
                    {
                        Position = pos.GetDouble(),
                        Temperature = temp.GetDouble()
                    });
                }
            }

            return list;
        }

        public async Task<List<PointData>> GetZonePointsAsync(int uiChannelId, int zoneId)
        {
            if (uiChannelId < 1)
                throw new ArgumentException("Channel ID must be >= 1");

            Console.WriteLine($"[GetZonePoints] Requesting zone points for channel {uiChannelId}, zone {zoneId}");

            var root = await _client.SendCommandAsync(
                "GetZonePoints",
                new { ChannelId = uiChannelId, ZoneId = zoneId }
            );

            var list = new List<PointData>();

            if (!root.HasValue)
            {
                Console.WriteLine($"[GetZonePoints] No response received (timeout or error)");
                return list;
            }

            Console.WriteLine($"[GetZonePoints] Response received: {root.Value.GetRawText().Substring(0, Math.Min(300, root.Value.GetRawText().Length))}...");

            if (!root.Value.TryGetProperty("Payload", out var payload))
            {
                Console.WriteLine($"[GetZonePoints] No Payload property in response");
                return list;
            }

            if (!payload.TryGetProperty("Points", out var arr))
            {
                Console.WriteLine($"[GetZonePoints] No Points property in payload. Payload: {payload.GetRawText().Substring(0, Math.Min(200, payload.GetRawText().Length))}...");
                return list;
            }

            Console.WriteLine($"[GetZonePoints] Found {arr.GetArrayLength()} points");

            foreach (var el in arr.EnumerateArray())
            {
                // ⭐ UPDATED: case-insensitive JSON ⭐
                var p = JsonSerializer.Deserialize<PointData>(el.GetRawText(), _jsonOptions);
                if (p != null)
                    list.Add(p);
            }

            Console.WriteLine($"[GetZonePoints] Deserialized {list.Count} points");
            return list;
        }

        public async Task<List<ZoneStateInfo>> GetZoneStateAsync(int uiChannelId)
        {
            if (uiChannelId < 1)
                throw new ArgumentException("UI channel must be >= 1");

            Console.WriteLine($"[GetZoneState] Requesting zone state for channel {uiChannelId}");

            var root = await _client.SendCommandAsync(
                "GetZoneState",
                new { ChannelId = uiChannelId }
            );

            var list = new List<ZoneStateInfo>();

            if (!root.HasValue)
            {
                Console.WriteLine($"[GetZoneState] No response received (timeout or error)");
                return list;
            }

            Console.WriteLine($"[GetZoneState] Response received: {root.Value.GetRawText().Substring(0, Math.Min(200, root.Value.GetRawText().Length))}...");

            if (!root.Value.TryGetProperty("Payload", out var payload))
            {
                Console.WriteLine($"[GetZoneState] No Payload property in response");
                return list;
            }

            if (!payload.TryGetProperty("Zones", out var arr))
            {
                Console.WriteLine($"[GetZoneState] No Zones property in payload. Payload: {payload.GetRawText().Substring(0, Math.Min(200, payload.GetRawText().Length))}...");
                return list;
            }

            Console.WriteLine($"[GetZoneState] Found {arr.GetArrayLength()} zones");

            foreach (var el in arr.EnumerateArray())
            {
                var st = JsonSerializer.Deserialize<ZoneStateInfo>(el.GetRawText(), _jsonOptions);
                if (st != null)
                    list.Add(st);
            }

            Console.WriteLine($"[GetZoneState] Deserialized {list.Count} zone states");
            return list;
        }

        /// <summary>
        /// Updates a single zone configuration (most optimized - sends only one zone)
        /// </summary>
        public async Task<bool> UpdateSingleZoneAsync(string channelKey, ZoneInfo zone)
        {
            string uiKey = channelKey.Trim();      // "Channel 1"
            int channelId = ExtractChannelIdFromName(uiKey);
            if (channelId < 1) channelId = 1;

            // Set ChannelId to match the channel
            zone.ChannelId = channelId;

            // Preserve custom zone name - only set default if empty
            if (string.IsNullOrWhiteSpace(zone.Name))
            {
                zone.Name = $"Zone {zone.ZoneId}";
            }

            var root = await _client.SendCommandAsync(
                "UpdateZone",
                new { ChannelId = channelId, Zone = zone }
            );

            if (!root.HasValue) return false;
            if (!root.Value.TryGetProperty("Payload", out var payload)) return false;

            return payload.TryGetProperty("Success", out var ok) && ok.GetBoolean();
        }

        /// <summary>
        /// Updates zone configuration for a single channel (optimized)
        /// NOTE: This method reassigns ZoneIds to sequential 1, 2, 3...
        /// Use SetChannelZonesPreserveIdsAsync to preserve existing ZoneIds.
        /// </summary>
        public async Task<bool> SetChannelZonesAsync(string channelKey, List<ZoneInfo> zones)
        {
            string uiKey = channelKey.Trim();      // "Channel 1"
            int channelId = ExtractChannelIdFromName(uiKey);
            if (channelId < 1) channelId = 1;

            var zoneList = new List<ZoneInfo>(zones);

            for (int i = 0; i < zoneList.Count; i++)
            {
                // Set ChannelId to match the channel
                zoneList[i].ChannelId = channelId;
                // ✅ ZoneId MUST be int
                zoneList[i].ZoneId = i + 1;
                // Preserve custom zone name - only set default if empty
                if (string.IsNullOrWhiteSpace(zoneList[i].Name))
                {
                    zoneList[i].Name = $"Zone {i + 1}";
                }
            }

            // Send only this channel's zones
            var singleChannelZones = new Dictionary<string, List<ZoneInfo>>
            {
                { uiKey, zoneList }
            };

            var root = await _client.SendCommandAsync(
                "SetZoneConfiguration",
                new { Zones = singleChannelZones }
            );

            if (!root.HasValue) return false;
            if (!root.Value.TryGetProperty("Payload", out var payload)) return false;

            return payload.TryGetProperty("Success", out var ok) && ok.GetBoolean();
        }

        /// <summary>
        /// Updates zone configuration for a single channel, preserving existing ZoneIds.
        /// Use this for batch zone creation where ZoneIds are already assigned.
        /// This triggers only ONE Modbus reinitialization regardless of zone count.
        /// </summary>
        public async Task<bool> SetChannelZonesPreserveIdsAsync(string channelKey, List<ZoneInfo> zones)
        {
            string uiKey = channelKey.Trim();      // "Channel 1"
            int channelId = ExtractChannelIdFromName(uiKey);
            if (channelId < 1) channelId = 1;

            // Create a copy to avoid modifying the original list
            var zoneList = zones.Select(z => new ZoneInfo
            {
                ChannelId = channelId,
                ZoneId = z.ZoneId,  // Preserve existing ZoneId
                Name = string.IsNullOrWhiteSpace(z.Name) ? $"Zone {z.ZoneId}" : z.Name,
                StartPoint = z.StartPoint,
                EndPoint = z.EndPoint,
                MaxTemp = z.MaxTemp,
                MinTemp = z.MinTemp,
                PreAlarm = z.PreAlarm,
                RoRThreshold = z.RoRThreshold,
                DeviationThreshold = z.DeviationThreshold,
                IsMaxTempEnabled = z.IsMaxTempEnabled,
                IsMinTempEnabled = z.IsMinTempEnabled,
                IsPreAlarmEnabled = z.IsPreAlarmEnabled,
                IsRateOfRiseEnabled = z.IsRateOfRiseEnabled,
                IsDeviationEnabled = z.IsDeviationEnabled,
                Enabled = z.Enabled,
                AssignedRelayId = z.AssignedRelayId
            }).ToList();

            // Send only this channel's zones
            var singleChannelZones = new Dictionary<string, List<ZoneInfo>>
            {
                { uiKey, zoneList }
            };

            Console.WriteLine($"📤 SetChannelZonesPreserveIdsAsync: Sending {zoneList.Count} zones for {uiKey} in single batch");

            var root = await _client.SendCommandAsync(
                "SetZoneConfiguration",
                new { Zones = singleChannelZones }
            );

            if (!root.HasValue) return false;
            if (!root.Value.TryGetProperty("Payload", out var payload)) return false;

            return payload.TryGetProperty("Success", out var ok) && ok.GetBoolean();
        }

        /// <summary>
        /// Updates zone configuration for all channels (legacy, sends everything)
        /// </summary>
        public async Task<bool> SetAllZoneConfigsAsync(Dictionary<string, List<ZoneInfo>> zones)
        {
            var normalized = new Dictionary<string, List<ZoneInfo>>();

            foreach (var kvp in zones)
            {
                string uiKey = kvp.Key.Trim();      // "Channel 1"
                int channelId = ExtractChannelIdFromName(uiKey);
                if (channelId < 1) channelId = 1;

                var zoneList = kvp.Value;

                for (int i = 0; i < zoneList.Count; i++)
                {
                    // Set ChannelId to match the channel
                    zoneList[i].ChannelId = channelId;
                    // ✅ ZoneId MUST be int
                    zoneList[i].ZoneId = i + 1;
                    // Preserve custom zone name - only set default if empty
                    if (string.IsNullOrWhiteSpace(zoneList[i].Name))
                    {
                        zoneList[i].Name = $"Zone {i + 1}";
                    }
                }

                normalized[uiKey] = zoneList;
            }

            var root = await _client.SendCommandAsync(
                "SetZoneConfiguration",
                new { Zones = normalized }
            );

            if (!root.HasValue) return false;
            if (!root.Value.TryGetProperty("Payload", out var payload)) return false;

            return payload.TryGetProperty("Success", out var ok) && ok.GetBoolean();
        }

        // ---------------------------------------------------------
        // SYSTEM CONFIGURATION
        // ---------------------------------------------------------

        /// <summary>
        /// Gets the system configuration from FRMC
        /// </summary>
        public async Task<SystemConfiguration?> GetSystemConfigAsync()
        {
            var root = await _client.SendCommandAsync("GetSystemConfig", new { });

            if (!root.HasValue) return null;
            if (!root.Value.TryGetProperty("Payload", out var payload)) return null;
            if (!payload.TryGetProperty("Success", out var ok) || !ok.GetBoolean()) return null;
            if (!payload.TryGetProperty("Config", out var cfg)) return null;

            return JsonSerializer.Deserialize<SystemConfiguration>(cfg.GetRawText(), _jsonOptions);
        }

        /// <summary>
        /// Updates the system configuration in FRMC
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> SetSystemConfigAsync(SystemConfiguration config)
        {
            var root = await _client.SendCommandAsync(
                "SetSystemConfig",
                new { Config = config }
            );

            if (!root.HasValue) return (false, "No response from FRMC");
            if (!root.Value.TryGetProperty("Payload", out var payload)) return (false, "Invalid response format");

            bool success = payload.TryGetProperty("Success", out var ok) && ok.GetBoolean();

            string? errorMessage = null;
            if (!success && payload.TryGetProperty("Error", out var error))
            {
                errorMessage = error.GetString();
            }

            return (success, errorMessage);
        }

        // ---------------------------------------------------------
        // RELAY CONTROL
        // ---------------------------------------------------------

        /// <summary>
        /// Gets all relay states from FRMC
        /// </summary>
        public async Task<List<RelayStateInfo>?> GetRelayStatesAsync()
        {
            var root = await _client.SendCommandAsync("GetRelayStates", new { });

            if (!root.HasValue) return null;
            if (!root.Value.TryGetProperty("Payload", out var payload)) return null;
            if (!payload.TryGetProperty("Success", out var ok) || !ok.GetBoolean()) return null;
            if (!payload.TryGetProperty("States", out var states)) return null;

            var list = new List<RelayStateInfo>();
            foreach (var el in states.EnumerateArray())
            {
                var state = JsonSerializer.Deserialize<RelayStateInfo>(el.GetRawText(), _jsonOptions);
                if (state != null)
                    list.Add(state);
            }

            return list;
        }

        /// <summary>
        /// Sets the state of a specific relay
        /// </summary>
        public async Task<bool> SetRelayStateAsync(int relayId, bool turnOn)
        {
            var root = await _client.SendCommandAsync(
                "SetRelayState",
                new { RelayId = relayId, TurnOn = turnOn }
            );

            if (!root.HasValue) return false;
            if (!root.Value.TryGetProperty("Payload", out var payload)) return false;

            return payload.TryGetProperty("Success", out var ok) && ok.GetBoolean();
        }

        // ---------------------------------------------------------
        // SYSTEM HEALTH
        // ---------------------------------------------------------

        /// <summary>
        /// Gets health parameters from FRMC
        /// </summary>
        public async Task<HealthParametersInfo?> GetHealthParametersAsync()
        {
            var root = await _client.SendCommandAsync("GetHealthParameters", new { });

            if (!root.HasValue) return null;
            if (!root.Value.TryGetProperty("Payload", out var payload)) return null;
            if (!payload.TryGetProperty("Success", out var ok) || !ok.GetBoolean()) return null;
            if (!payload.TryGetProperty("Health", out var health)) return null;

            return JsonSerializer.Deserialize<HealthParametersInfo>(health.GetRawText(), _jsonOptions);
        }

        /// <summary>
        /// Gets active system health faults from FRMC (same faults that control Relay 2)
        /// </summary>
        public async Task<List<SystemHealthFaultInfo>> GetActiveHealthFaultsAsync()
        {
            var root = await _client.SendCommandAsync("GetActiveHealthFaults", new { });

            if (!root.HasValue) return new List<SystemHealthFaultInfo>();
            if (!root.Value.TryGetProperty("Payload", out var payload)) return new List<SystemHealthFaultInfo>();
            if (!payload.TryGetProperty("Success", out var ok) || !ok.GetBoolean()) return new List<SystemHealthFaultInfo>();
            if (!payload.TryGetProperty("Faults", out var faults)) return new List<SystemHealthFaultInfo>();

            var list = new List<SystemHealthFaultInfo>();
            foreach (var el in faults.EnumerateArray())
            {
                var fault = JsonSerializer.Deserialize<SystemHealthFaultInfo>(el.GetRawText(), _jsonOptions);
                if (fault != null)
                    list.Add(fault);
            }

            return list;
        }

        public async Task<List<int>> GetAvailableRelaysAsync()
        {
            var root = await _client.SendCommandAsync(
                "GetAvailableRelays",
                new { }
            );

            if (!root.HasValue)
                return new List<int>();

            if (!root.Value.TryGetProperty("Payload", out var payload))
                return new List<int>();

            if (!payload.TryGetProperty("Success", out var ok) || !ok.GetBoolean())
                return new List<int>();

            if (!payload.TryGetProperty("AvailableRelays", out var relayArray))
                return new List<int>();

            return relayArray
                .EnumerateArray()
                .Select(r => r.GetInt32())
                .ToList();
        }



        // ---------------------------------------------------------
        // DECIMATION CONFIG
        // ---------------------------------------------------------

        /// <summary>
        /// Gets the current decimation configuration from FRMC.
        /// </summary>
        /// <returns>Tuple of (success, mediumThresholdKm, largeThresholdKm, mediumFactor, largeFactor)</returns>
        public async Task<(bool Success, double MediumThresholdKm, double LargeThresholdKm, int MediumFactor, int LargeFactor)> GetDecimationConfigAsync()
        {
            try
            {
                var response = await _client.SendCommandAsync("GetDecimationConfig", new { });
                if (!response.HasValue) return (false, 3, 6, 5, 10);
                if (!response.Value.TryGetProperty("Payload", out var payload)) return (false, 3, 6, 5, 10);
                if (!payload.TryGetProperty("Success", out var ok) || !ok.GetBoolean()) return (false, 3, 6, 5, 10);

                double mediumKm = payload.TryGetProperty("MediumThresholdKm", out var m) ? m.GetDouble() : 3;
                double largeKm = payload.TryGetProperty("LargeThresholdKm", out var l) ? l.GetDouble() : 6;
                int mediumFactor = payload.TryGetProperty("MediumFactor", out var mf) ? mf.GetInt32() : 5;
                int largeFactor = payload.TryGetProperty("LargeFactor", out var lf) ? lf.GetInt32() : 10;

                return (true, mediumKm, largeKm, mediumFactor, largeFactor);
            }
            catch
            {
                return (false, 3, 6, 5, 10);
            }
        }

        /// <summary>
        /// Sets the decimation configuration on FRMC.
        /// </summary>
        public async Task<bool> SetDecimationConfigAsync(double mediumThresholdKm, double largeThresholdKm, int mediumFactor, int largeFactor)
        {
            try
            {
                var response = await _client.SendCommandAsync("SetDecimationConfig", new
                {
                    MediumThresholdKm = mediumThresholdKm,
                    LargeThresholdKm = largeThresholdKm,
                    MediumFactor = mediumFactor,
                    LargeFactor = largeFactor
                });

                if (!response.HasValue) return false;
                if (!response.Value.TryGetProperty("Payload", out var payload)) return false;
                if (!payload.TryGetProperty("Success", out var ok)) return false;

                return ok.GetBoolean();
            }
            catch
            {
                return false;
            }
        }

        // ---------------------------------------------------------
        // RESET CONFIGURATION
        // ---------------------------------------------------------

        /// <summary>
        /// Resets all channel configurations to default (all disabled, no zones)
        /// </summary>
        public async Task<(bool Success, string? Message)> ResetAllConfigurationAsync()
        {
            var root = await _client.SendCommandAsync("ResetAllConfiguration", new { });

            if (!root.HasValue) return (false, "No response from FRMC");
            if (!root.Value.TryGetProperty("Payload", out var payload)) return (false, "Invalid response format");

            bool success = payload.TryGetProperty("Success", out var ok) && ok.GetBoolean();

            string? message = null;
            if (payload.TryGetProperty("Message", out var msg))
            {
                message = msg.GetString();
            }
            else if (payload.TryGetProperty("Error", out var error))
            {
                message = error.GetString();
            }

            return (success, message);
        }

        // ---------------------------------------------------------
        // UTILITY
        // ---------------------------------------------------------
        private int ExtractChannelIdFromName(string name)
        {
            var m = Regex.Match(name, @"\d+");
            return (m.Success && int.TryParse(m.Value, out int n)) ? n : 1;
        }
    }
}
