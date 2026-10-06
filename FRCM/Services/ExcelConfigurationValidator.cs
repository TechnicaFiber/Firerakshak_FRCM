using System;
using System.Collections.Generic;
using System.Linq;
using FRCM.Models;

namespace FRCM.Services
{
    public static class ExcelConfigurationValidator
    {
        public static List<string> ValidateChannels(
            List<ChannelBatch> channels)
        {
            var errors = new List<string>();

            foreach (var ch in channels)
            {

                if (ch.ChannelId < 1 || ch.ChannelId > 8)
                {
                    errors.Add(
                        $"Channel {ch.ChannelId}: Invalid ChannelId (allowed 1-8)");
                }

                if (string.IsNullOrWhiteSpace(ch.Name))
                {
                    errors.Add(
                        $"Channel {ch.ChannelId}: Channel Name cannot be empty");
                }

                string expectedName = $"Channel {ch.ChannelId}";

                if (!string.Equals(ch.Name?.Trim(), expectedName, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(
                        $"Channel {ch.ChannelId}: Invalid channel name. Expected '{expectedName}'");
                }

                if (!ch.Enabled)
                    continue;

                if (ch.Length <= 0)
                    errors.Add($"Channel {ch.ChannelId}: Length must be greater than 0");

                if (ch.Length > 100000)
                    errors.Add($"Channel {ch.ChannelId}: Length exceeds 100000");

                if (ch.CorrectionLength < 0)
                    errors.Add($"Channel {ch.ChannelId}: Correction Length cannot be negative");

                if (ch.CorrectionLength > ch.Length)
                    errors.Add($"Channel {ch.ChannelId}: Correction Length exceeds Channel Length");

                if (ch.ScanPeriod <= 0 || ch.ScanPeriod > 3600)
                    errors.Add($"Channel {ch.ChannelId}: Invalid Scan Period");

                const int MAX_ZONES_PER_CHANNEL = 1000;

                if (ch.NumberOfZones < 1)
                {
                    errors.Add(
                        $"Channel {ch.ChannelId}: Number Of Zones must be at least 1");
                }

                if (ch.NumberOfZones > MAX_ZONES_PER_CHANNEL)
                {
                    errors.Add(
                        $"Channel {ch.ChannelId}: Number Of Zones exceeds limit ({MAX_ZONES_PER_CHANNEL})");
                }
            }

            return errors;
        }

        public static List<string> ValidateZones(
            List<ZoneInfo> zones,
            List<ChannelBatch> channels)
        {
            var errors = new List<string>();

            foreach (var zone in zones)
            {
                if (string.IsNullOrWhiteSpace(zone.Name?.Trim()))
                {
                    errors.Add(
                        $"Channel {zone.ChannelId} Zone {zone.ZoneId}: Zone Name cannot be empty");
                }

                if (!zone.Enabled)
                    continue;

                bool isUnconfigured =
                    zone.StartPoint == 0 &&
                    zone.EndPoint == 0 &&
                    zone.MaxTemp == 0 &&
                    zone.MinTemp == 0 &&
                    zone.PreAlarm == 0 &&
                    zone.RoRThreshold == 0 &&
                    zone.DeviationThreshold == 0;

                if (isUnconfigured)
                    continue;

                var channel = channels.FirstOrDefault(
                    c => c.ChannelId == zone.ChannelId);

                if (channel == null)
                {
                    errors.Add($"Zone {zone.ZoneId}: Channel does not exist");
                    continue;
                }
                if (!channel.Enabled && zone.Enabled)
                {
                    errors.Add(
                        $"Channel {zone.ChannelId} Zone {zone.ZoneId}: " +
                        $"Zone cannot be enabled when Channel is disabled");
                }
                double channelLength = channel.Length;
                const double MIN_TEMP_LIMIT = -273.15;
                const double MAX_TEMP_LIMIT = 1500.0;

                if (zone.StartPoint < 0)
                    errors.Add($"Channel {zone.ChannelId} Zone {zone.ZoneId}: StartPoint < 0");

                if (zone.EndPoint <= zone.StartPoint)
                    errors.Add($"Channel {zone.ChannelId} Zone {zone.ZoneId}: EndPoint must be greater than StartPoint");

                if ((zone.EndPoint - zone.StartPoint) < 1.0)
                    errors.Add($"Channel {zone.ChannelId} Zone {zone.ZoneId}: Zone length must be at least 1 meter");

                if (zone.EndPoint > channelLength)
                    errors.Add($"Channel {zone.ChannelId} Zone {zone.ZoneId}: EndPoint exceeds Channel Length");

                if (zone.MaxTemp < MIN_TEMP_LIMIT || zone.MaxTemp > MAX_TEMP_LIMIT)
                    errors.Add($"Channel {zone.ChannelId} Zone {zone.ZoneId}: MaxTemp out of valid range");

                //if (zone.MaxTemp <= zone.MinTemp)
                //    errors.Add($"Channel {zone.ChannelId} Zone {zone.ZoneId}: MaxTemp must be greater than MinTemp");

                if (zone.IsMaxTempEnabled && zone.IsMinTempEnabled)
                {
                    if (zone.MaxTemp <= zone.MinTemp)
                        errors.Add($"Channel {zone.ChannelId} Zone {zone.ZoneId}: MaxTemp must be greater than MinTemp");
                }

                if (zone.MinTemp < MIN_TEMP_LIMIT || zone.MinTemp > MAX_TEMP_LIMIT)
                    errors.Add($"Channel {zone.ChannelId} Zone {zone.ZoneId}: MinTemp out of valid range");

                //if (zone.PreAlarm >= zone.MaxTemp)
                //    errors.Add($"Channel {zone.ChannelId} Zone {zone.ZoneId}: PreAlarm must be less than MaxTemp");

                if (zone.IsPreAlarmEnabled && zone.IsMaxTempEnabled)
                {
                    if (zone.PreAlarm >= zone.MaxTemp)
                        errors.Add($"Channel {zone.ChannelId} Zone {zone.ZoneId}: PreAlarm must be less than MaxTemp");
                }

                if (zone.PreAlarm < MIN_TEMP_LIMIT || zone.PreAlarm > MAX_TEMP_LIMIT)
                    errors.Add($"Channel {zone.ChannelId} Zone {zone.ZoneId}: PreAlarm out of valid range");

                //if (zone.PreAlarm <= zone.MinTemp)
                //    errors.Add($"Channel {zone.ChannelId} Zone {zone.ZoneId}: PreAlarm must be greater than MinTemp");

                if (zone.IsPreAlarmEnabled && zone.IsMinTempEnabled)
                {
                    if (zone.PreAlarm <= zone.MinTemp)
                        errors.Add($"Channel {zone.ChannelId} Zone {zone.ZoneId}: PreAlarm must be greater than MinTemp");
                }

                //if (zone.RoRThreshold <= 0 || zone.RoRThreshold > 1000)
                //    errors.Add($"Channel {zone.ChannelId} Zone {zone.ZoneId}: Invalid RoR Threshold");

                if (zone.IsRateOfRiseEnabled)
                {
                    if (zone.RoRThreshold <= 0)
                        errors.Add($"Channel {zone.ChannelId} Zone {zone.ZoneId}: RoR Threshold must be greater than 0");
                }

                if (zone.RoRThreshold > 1000)
                    errors.Add($"Channel {zone.ChannelId} Zone {zone.ZoneId}: RoR Threshold exceeds limit");

                //if (zone.DeviationThreshold <= 0 || zone.DeviationThreshold > 500)
                //    errors.Add($"Channel {zone.ChannelId} Zone {zone.ZoneId}: Invalid Deviation Threshold");

                if (zone.IsDeviationEnabled)
                {
                    if (zone.DeviationThreshold <= 0)
                        errors.Add($"Channel {zone.ChannelId} Zone {zone.ZoneId}: Deviation must be greater than 0");
                }

                if (zone.DeviationThreshold > 500)
                    errors.Add($"Channel {zone.ChannelId} Zone {zone.ZoneId}: Deviation exceeds limit");

                if (zone.AssignedRelayId.HasValue)
                {
                    if (zone.AssignedRelayId.Value < 12 ||
                        zone.AssignedRelayId.Value > 48)
                    {
                        errors.Add(
                            $"Channel {zone.ChannelId} Zone {zone.ZoneId}: " +
                            $"Assigned Relay must be between 12 and 48 or None");
                    }
                }
            }

            return errors;
        }

        public static List<string> ValidateZoneOverlaps(
            List<ZoneInfo> zones)
        {
            var errors = new List<string>();

            foreach (var grp in zones.GroupBy(z => z.ChannelId))
            {
                //var ordered = grp
                //    .OrderBy(z => z.StartPoint)
                //    .ToList();
                var ordered = grp
                .Where(z =>
                    z.Enabled &&
                    !(z.StartPoint == 0 &&
                      z.EndPoint == 0))
                .OrderBy(z => z.StartPoint)
                .ToList();

                for (int i = 0; i < ordered.Count - 1; i++)
                {
                    var current = ordered[i];
                    var next = ordered[i + 1];

                    if (current.StartPoint == next.StartPoint &&
                    current.EndPoint == next.EndPoint)
                    {
                        errors.Add(
                            $"Channel {current.ChannelId}: " +
                            $"Zone {current.ZoneId} and Zone {next.ZoneId} " +
                            $"have the same range ({current.StartPoint} - {current.EndPoint})");
                    }
                }
            }

            return errors;
        }

        public static List<string> ValidateDuplicateChannelIds(
        List<ChannelBatch> channels)
        {
            var errors = new List<string>();

            var duplicates = channels
                .GroupBy(c => c.ChannelId)
                .Where(g => g.Count() > 1);

            foreach (var dup in duplicates)
            {
                errors.Add(
                    $"Duplicate ChannelId {dup.Key}");
            }

            return errors;
        }

        public static List<string> ValidateDuplicateZoneIds(
        List<ZoneInfo> zones)
        {
            var errors = new List<string>();

            var duplicates = zones
                .Where(z => z.Enabled)
                .GroupBy(z => new { z.ChannelId, z.ZoneId })
                .Where(g => g.Count() > 1);

            foreach (var dup in duplicates)
            {
                errors.Add(
                    $"Channel {dup.Key.ChannelId}: Duplicate ZoneId {dup.Key.ZoneId}");
            }

            return errors;
        }
        public static List<string> ValidateZoneCountConsistency(
    List<ChannelBatch> channels,
    List<ZoneInfo> zones)
        {
            var errors = new List<string>();

            foreach (var ch in channels)
            {
                if (!ch.Enabled)
                    continue;

                int actualZones = zones.Count(z =>
                    z.ChannelId == ch.ChannelId &&
                    z.Enabled);

                if (actualZones > ch.NumberOfZones)
                {
                    errors.Add(
                        $"Channel {ch.ChannelId}: " +
                        $"Configured zones ({actualZones}) exceed allowed NumberOfZones ({ch.NumberOfZones})");
                }
            }

            return errors;
        }

        public static List<string> ValidateDuplicateZoneNames(
        List<ZoneInfo> zones)
        {
            var errors = new List<string>();

            var duplicates = zones
                .Where(z => z.Enabled &&
            !string.IsNullOrWhiteSpace(z.Name?.Trim()))
                .GroupBy(z => new
                {
                    z.ChannelId,
                    Name = z.Name.Trim().ToLower()
                })
                .Where(g => g.Count() > 1);

            foreach (var dup in duplicates)
            {
                errors.Add(
                    $"Channel {dup.Key.ChannelId}: Duplicate Zone Name '{dup.First().Name}'");
            }

            return errors;
        }
    }
}