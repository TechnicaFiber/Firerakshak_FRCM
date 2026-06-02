using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Text.Json;
using FRCM.Services;

namespace FRCM
{
    public partial class ActiveAlarm : Form
    {
        // FIX: Made volatile for thread-safe singleton pattern with double-checked locking
        public static volatile ActiveAlarm? Instance;

        // DataGridView control
        private DataGridView dgvActiveAlarms;
        // Notify which channels currently have active alarms (zone alarms only for enabled zones, not fiber break)
        public event Action<HashSet<int>>? ActiveChannelsChanged;
        // Notify which channels have fiber break alarms (separate from zone alarms)
        public event Action<HashSet<int>>? ActiveFiberBreakChannelsChanged;
        // Notify ALL channels with any alarm from FRMC (for system LED sync with hardware)
        public event Action<HashSet<int>>? AllActiveChannelsChanged;
        public event Action<HashSet<(int channelId, int zoneId)>>? ActiveZonesChanged;

        private Button btnResetAlarm;
        private System.Windows.Forms.Timer? _refreshTimer;

        // Alarm reason enum
        public enum AlarmReason
        {
            MaximumTemp,
            MinimumTemp,
            PreAlarm,
            RateOfRise,
            Deviation,
            FibreBreak
        }

        private readonly CustomWebSocketClient _client;
        private readonly Services.IConfigurationService _configurationService;

        public ActiveAlarm(CustomWebSocketClient client, Services.IConfigurationService configurationService)
        {
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.AutoScaleMode = AutoScaleMode.Dpi;

            float scalingFactor = this.DeviceDpi / 96f;
            this.Size = new Size((int)(950 * scalingFactor), (int)(600 * scalingFactor));
            this.MinimumSize = new Size((int)(750 * scalingFactor), (int)(450 * scalingFactor));
            this.StartPosition = FormStartPosition.CenterScreen;
            this.WindowState = FormWindowState.Normal; // Changed from Maximized to Normal

            InitializeComponent();

            InitializeGrid();
            _client = client;
            _configurationService = configurationService;
            Instance = this;   // allow Form1 to update grid

            // Integrate with global font size scaling
            this.Load += (s, e) =>
            {
                // Ensure form fits within screen bounds
                EnsureFormIsVisible();

                FRCM.Services.FontSizeHelper.UpdateControlRecursive(
                    this,
                    FRCM.Services.FontSizeHelper.CurrentMultiplier
                );
            };

            // Request alarms whenever form becomes visible
            this.VisibleChanged += ActiveAlarm_VisibleChanged;

            // Reset Instance when form is closed
            this.FormClosed += ActiveAlarm_FormClosed;

            // Initialize refresh timer (3-second interval) to keep alarms in sync
            _refreshTimer = new System.Windows.Forms.Timer();
            _refreshTimer.Interval = 3000; // 3 seconds
            _refreshTimer.Tick += RefreshTimer_Tick;
        }

        private void EnsureFormIsVisible()
        {
            // Get the screen where the mouse is (usually where the user is looking)
            Screen screen = Screen.FromPoint(Cursor.Position);
            Rectangle area = screen.WorkingArea;

            // If form is larger than working area, resize it
            if (this.Width > area.Width) this.Width = area.Width - 40;
            if (this.Height > area.Height) this.Height = area.Height - 40;

            // Re-center on the current screen
            this.Left = area.Left + (area.Width - this.Width) / 2;
            this.Top = area.Top + (area.Height - this.Height) / 2;
        }

        private void ActiveAlarm_FormClosed(object? sender, FormClosedEventArgs e)
        {
            // Stop and dispose the refresh timer
            if (_refreshTimer != null)
            {
                _refreshTimer.Stop();
                _refreshTimer.Dispose();
                _refreshTimer = null;
            }

            // Set the flag in Form1 to indicate that the user closed the form
            // This prevents it from automatically reopening until a NEW alarm occurs
            if (e.CloseReason == CloseReason.UserClosing)
            {
                Form1.ActiveAlarmClosedByUser = true;
            }

            // Clear instance so it can be recreated next time
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void ActiveAlarm_VisibleChanged(object? sender, EventArgs e)
        {
            if (this.Visible)
            {
                // Request alarms and start periodic refresh when form becomes visible
                RequestActiveAlarms();
                _refreshTimer?.Start();
            }
            else
            {
                // Stop refresh timer when form is hidden
                _refreshTimer?.Stop();
            }
        }

        private void RefreshTimer_Tick(object? sender, EventArgs e)
        {
            // Periodically refresh alarms to stay in sync with FRMC
            RequestActiveAlarms();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            RequestActiveAlarms();
            _refreshTimer?.Start();
        }

        /// <summary>
        /// Requests active alarms from FRMC
        /// </summary>
        private void RequestActiveAlarms()
        {
            var msg = new
            {
                MessageType = "get_active_alarm",
                Payload = new { }
            };

            string json = JsonSerializer.Serialize(msg);
            _ = _client.SendAsync(json);
        }

        private void InitializeGrid()
        {
            // Calculate DPI-scaled sizes
            float scalingFactor = this.DeviceDpi / 96f;
            float fontScaling = FontSizeHelper.GetAutoScaling(this);
            int scaledButtonHeight = (int)(36 * scalingFactor);
            int scaledRowHeight = (int)(60 * scalingFactor);

            // 1. Main Layout
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                Padding = new Padding(15)
            };

            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, scaledRowHeight));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // 2. Header Layout (Using TableLayoutPanel for robust button positioning)
            var headerLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 10)
            };
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Spacer
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));    // Button

            btnResetAlarm = new Button
            {
                Text = "Reset Alarm",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size((int)(180 * scalingFactor), scaledButtonHeight),
                BackColor = Color.LightGray,
                Font = new Font("Segoe UI", 10 * fontScaling, FontStyle.Bold),
                Padding = new Padding(15, 0, 15, 0),
                UseVisualStyleBackColor = true
            };

            btnResetAlarm.Click += BtnResetAlarm_Click;
            headerLayout.Controls.Add(btnResetAlarm, 1, 0);

            // 3. DataGridView Initialization
            dgvActiveAlarms = new DataGridView
            {
                Name = "dgvActiveAlarms",
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                EnableHeadersVisualStyles = false,

                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,

                // Enable manual height setting for the header row to ensure it scales correctly on large screens
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = (int)(40 * scalingFactor),

                RowTemplate = { MinimumHeight = (int)(30 * scalingFactor) },

                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.Fixed3D
            };

            // Set Data Rows to the original size you liked, but with scaling for larger screens
            dgvActiveAlarms.Font = new System.Drawing.Font("Segoe UI", 9F * fontScaling);
            dgvActiveAlarms.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dgvActiveAlarms.DefaultCellStyle.Font = dgvActiveAlarms.Font;
            dgvActiveAlarms.DefaultCellStyle.Padding = new Padding((int)(3 * scalingFactor));
            dgvActiveAlarms.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dgvActiveAlarms.RowsDefaultCellStyle.WrapMode = DataGridViewTriState.True;

            // FIX: Specifically scale ONLY the Header line (the first line) to be larger and bold
            dgvActiveAlarms.ColumnHeadersDefaultCellStyle.Font = new System.Drawing.Font("Segoe UI", 9F * fontScaling, FontStyle.Bold);
            dgvActiveAlarms.ColumnHeadersDefaultCellStyle.BackColor = Color.LightGray;
            dgvActiveAlarms.ColumnHeadersDefaultCellStyle.ForeColor = Color.Black;
            dgvActiveAlarms.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.LightGray;
            dgvActiveAlarms.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.Black;
            dgvActiveAlarms.ColumnHeadersDefaultCellStyle.Padding = new Padding(0, (int)(2 * scalingFactor), 0, (int)(2 * scalingFactor));
            dgvActiveAlarms.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;

            // Setup columns
            dgvActiveAlarms.Columns.Add("ChannelName", "Channel Name");
            dgvActiveAlarms.Columns.Add("ZoneName", "Zone Name");
            dgvActiveAlarms.Columns.Add("Reason", "Alarm Type");
            dgvActiveAlarms.Columns.Add("AlarmValue", "Alarm Value");
            dgvActiveAlarms.Columns.Add("Position", "Position (m)");
            dgvActiveAlarms.Columns.Add("TimeStamp", "Time Stamp");

            // 4. Final Assembly
            mainLayout.Controls.Add(headerLayout, 0, 0); // Corrected: adding headerLayout
            mainLayout.Controls.Add(dgvActiveAlarms, 0, 1);

            this.Controls.Add(mainLayout);
        }

        private void BtnResetAlarm_Click(object? sender, EventArgs e)
        {
            SendResetAlarmRequest();

            // Immediately clear local UI for responsive feedback
            // (next refresh will re-sync with FRMC if any alarms remain)
            dgvActiveAlarms.Rows.Clear();
            ActiveChannelsChanged?.Invoke(new HashSet<int>());
            ActiveFiberBreakChannelsChanged?.Invoke(new HashSet<int>());
            AllActiveChannelsChanged?.Invoke(new HashSet<int>());
            ActiveZonesChanged?.Invoke(new HashSet<(int channelId, int zoneId)>());
        }
        private void SendResetAlarmRequest()
        {
            var msg = new
            {
                MessageType = "reset_active_alarm",
                Payload = new { }
            };

            string json = JsonSerializer.Serialize(msg);
            _ = _client.SendAsync(json);
        }


        // Convert enum to readable text
        private static string ReasonToText(AlarmReason reason)
        {
            switch (reason)
            {
                case AlarmReason.MaximumTemp: return "Maximum Temp";
                case AlarmReason.MinimumTemp: return "Minimum Temp";
                case AlarmReason.PreAlarm: return "Pre Alarm";
                case AlarmReason.RateOfRise: return "Rate of Rise";
                case AlarmReason.Deviation: return "Deviation";
                case AlarmReason.FibreBreak: return "Fibre Break";
                default: return reason.ToString();
            }
        }

        // Add row to grid
        // FIX: Added InvokeRequired check for thread safety
        public void AddAlarm(string channelName, string zoneName, AlarmReason reason, string alarmValue, DateTime timeStamp, double? position = null)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => AddAlarm(channelName, zoneName, reason, alarmValue, timeStamp, position)));
                return;
            }

            string positionStr = position.HasValue ? $"{position.Value:F1}" : "-";

            dgvActiveAlarms.Rows.Add(
                channelName,
                zoneName,
                ReasonToText(reason),
                alarmValue,
                positionStr,
                timeStamp.ToString("yyyy-MM-dd HH:mm:ss")
            );

            // Auto-scroll to latest
            int lastRow = dgvActiveAlarms.Rows.Count - 1;
            if (lastRow >= 0)
                dgvActiveAlarms.FirstDisplayedScrollingRowIndex = lastRow;
        }

        // Clear all alarms
        // FIX: Added InvokeRequired check for thread safety
        public void ClearAlarms()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(ClearAlarms));
                return;
            }

            dgvActiveAlarms.Rows.Clear();
        }

        /// <summary>
        /// Gets the display name for a zone (e.g., "Fire Alarm Zone", "Zone A")
        /// </summary>
        /// <param name="uiChannelId">1-based UI channel ID</param>
        /// <param name="uiZoneId">1-based UI zone ID</param>
        /// <returns>Zone name or "Zone {id}" if not found</returns>
        private string GetZoneName(int uiChannelId, int uiZoneId)
        {
            var zones = _configurationService.GetZoneConfig($"Channel {uiChannelId}");
            var zone = zones?.FirstOrDefault(z => z.ZoneId == uiZoneId);
            return zone?.Name ?? $"Zone {uiZoneId}";
        }

        private bool IsZoneEnabled(int uiChannelId, int uiZoneId)
        {
            var zones = _configurationService.GetZoneConfig($"Channel {uiChannelId}");
            var zone = zones?.FirstOrDefault(z => z.ZoneId == uiZoneId);

            // If zone not found or disabled → treat as disabled
            return zone?.Enabled ?? false;
        }


        public void UpdateAlarmList(JsonElement alarms)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateAlarmList(alarms)));
                return;
            }

            dgvActiveAlarms.Rows.Clear();

            HashSet<int> channelsWithAlarm = new();       // Zone alarms for enabled zones only
            HashSet<int> channelsWithFiberBreak = new();  // Fiber break alarms only
            HashSet<int> allChannelsWithAlarm = new();    // ALL alarms from FRMC (for system LED sync)
            HashSet<(int channelId, int zoneId)> zonesWithAlarm = new();

            // Handle both array format (active_alarm_response) and object format (active_alarm_snapshot)
            JsonElement alarmsArray;
            if (alarms.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                alarmsArray = alarms;
            }
            else if (alarms.TryGetProperty("Alarms", out var innerAlarms) &&
                     innerAlarms.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                alarmsArray = innerAlarms;
            }
            else
            {
                // No valid alarm data found
                Console.WriteLine("[ActiveAlarm] No alarm array found in payload");
                ActiveChannelsChanged?.Invoke(new HashSet<int>(channelsWithAlarm));
                ActiveFiberBreakChannelsChanged?.Invoke(new HashSet<int>(channelsWithFiberBreak));
                AllActiveChannelsChanged?.Invoke(new HashSet<int>(allChannelsWithAlarm));
                ActiveZonesChanged?.Invoke(new HashSet<(int, int)>(zonesWithAlarm));
                return;
            }

            foreach (var alarm in alarmsArray.EnumerateArray())
            {
                // Safe property access with fallback values
                if (!alarm.TryGetProperty("ChannelId", out var chProp) ||
                    !alarm.TryGetProperty("ZoneId", out var zProp))
                    continue;

                int channelId = chProp.GetInt32(); // 1-based
                int zoneId = zProp.GetInt32();     // 1-based

                // Get reason to check for fiber break
                string reason = alarm.TryGetProperty("Reason", out var reasonProp) ? reasonProp.GetString() ?? "-" : "-";

                // Check if this is a fiber break alarm (ZoneId == 0 indicates channel-level alarm)
                bool isFiberBreak = zoneId == 0 || reason.Equals("FiberBreak", StringComparison.OrdinalIgnoreCase);

                int frmcChannelId = channelId - 1;

                // Track ALL alarms for system LED sync with hardware
                allChannelsWithAlarm.Add(frmcChannelId);

                // Skip disabled zones for display (but NEVER skip fiber break alarms)
                if (!isFiberBreak && !IsZoneEnabled(channelId, zoneId))
                    continue;

                // For fiber break, show "Channel Level" instead of zone name
                string zoneName = isFiberBreak ? "Channel Level" : GetZoneName(channelId, zoneId);

                // Get position if available (for fiber break, this is the break position)
                string position = "-";
                if (alarm.TryGetProperty("Position", out var posProp) &&
                    posProp.ValueKind != System.Text.Json.JsonValueKind.Null)
                {
                    position = $"{posProp.GetDouble():F1}";
                }

                // Safe property access for other fields (reason already retrieved above)
                string value = alarm.TryGetProperty("Value", out var valueProp) ? valueProp.ToString() : "-";
                string timestamp = alarm.TryGetProperty("Timestamp", out var tsProp) ? tsProp.GetString() ?? "-" : "-";

                dgvActiveAlarms.Rows.Add(
                    $"Channel {channelId}",
                    zoneName,
                    reason,
                    value,
                    position,
                    timestamp
                );

                if (isFiberBreak)
                {
                    // Fiber break goes to separate tracking set
                    channelsWithFiberBreak.Add(frmcChannelId);
                }
                else
                {
                    // Zone alarm tracking (enabled zones only)
                    int frmcZoneId = zoneId - 1;
                    channelsWithAlarm.Add(frmcChannelId);
                    zonesWithAlarm.Add((frmcChannelId, frmcZoneId));
                }
            }


            ActiveChannelsChanged?.Invoke(new HashSet<int>(channelsWithAlarm));
            ActiveFiberBreakChannelsChanged?.Invoke(new HashSet<int>(channelsWithFiberBreak));
            AllActiveChannelsChanged?.Invoke(new HashSet<int>(allChannelsWithAlarm));
            ActiveZonesChanged?.Invoke(new HashSet<(int, int)>(zonesWithAlarm));

            // Auto-close form when FRMC has no active alarms at all.
            // Do NOT use dgvActiveAlarms.Rows.Count here: disabled zones are filtered
            // from the grid, so the grid can show 0 rows while FRMC still has active
            // alarms for those zones. allChannelsWithAlarm tracks all FRMC alarms
            // (before any filtering) and is the authoritative signal.
            if (allChannelsWithAlarm.Count == 0)
            {
                this.Close();
            }
        }

        public void ClearAlarmForZone(int channel0, int zone0)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => ClearAlarmForZone(channel0, zone0)));
                return;
            }

            // Convert 0-based to 1-based for UI comparison
            string targetChannel = $"Channel {channel0 + 1}";
            int targetZone1Based = zone0 + 1;

            bool removed = false;

            for (int i = dgvActiveAlarms.Rows.Count - 1; i >= 0; i--)
            {
                var row = dgvActiveAlarms.Rows[i];
                string channelName = row.Cells["ChannelName"].Value?.ToString() ?? "";
                string zoneName = row.Cells["ZoneName"].Value?.ToString() ?? "";

                // Check channel match
                if (channelName != targetChannel)
                    continue;

                // Extract zone number from zone name for exact matching
                // Zone names are like "Zone 1", "Zone 2", or custom names ending with zone number
                bool zoneMatches = false;

                // Try to extract zone number - look for "Zone X" pattern or just the number at end
                if (zoneName.StartsWith("Zone ") && int.TryParse(zoneName.Substring(5).Trim(), out int parsedZoneId))
                {
                    zoneMatches = (parsedZoneId == targetZone1Based);
                }
                else
                {
                    // Fallback: check if zone name ends with " X" where X is the zone number
                    string suffix = $" {targetZone1Based}";
                    zoneMatches = zoneName.EndsWith(suffix) || zoneName == $"Zone {targetZone1Based}";
                }

                if (zoneMatches)
                {
                    dgvActiveAlarms.Rows.RemoveAt(i);
                    removed = true;
                }
            }

            // After removal, recalculate remaining active alarms from grid
            if (removed)
            {
                HashSet<int> remainingChannels = new();
                HashSet<(int, int)> remainingZones = new();

                foreach (DataGridViewRow row in dgvActiveAlarms.Rows)
                {
                    string channelName = row.Cells["ChannelName"].Value?.ToString() ?? "";
                    string zoneName = row.Cells["ZoneName"].Value?.ToString() ?? "";

                    // Parse channel number (0-based for FRMC)
                    if (channelName.StartsWith("Channel ") &&
                        int.TryParse(channelName.Substring(8).Trim(), out int ch1Based))
                    {
                        int ch0Based = ch1Based - 1;
                        remainingChannels.Add(ch0Based);

                        // Parse zone number
                        if (zoneName.StartsWith("Zone ") &&
                            int.TryParse(zoneName.Substring(5).Trim(), out int z1Based))
                        {
                            int z0Based = z1Based - 1;
                            remainingZones.Add((ch0Based, z0Based));
                        }
                    }
                }

                ActiveChannelsChanged?.Invoke(new HashSet<int>(remainingChannels));
                ActiveZonesChanged?.Invoke(new HashSet<(int, int)>(remainingZones));
            }
        }

    }
}
