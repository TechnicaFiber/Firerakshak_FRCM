using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FRCM.Models;
using FRCM.Services;

namespace FRCM
{
    public partial class FormRelayMapping : Form
    {
        // Static instance for real-time updates from Form1
        public static volatile FormRelayMapping? Instance;

        private readonly ChannelClient _channelClient;
        private readonly IConfigurationService _configurationService;
        private DataGridView? dgvRelayMapping;
        private Button? btnRefresh, btnClose;

        public FormRelayMapping(ChannelClient channelClient, IConfigurationService configurationService)
        {
            _channelClient = channelClient;
            _configurationService = configurationService;

            this.WindowState = FormWindowState.Maximized;
            InitializeComponent();
            BuildUI();
            LoadRelayMapping(); // Load once when form opens

            Instance = this;
            this.FormClosed += (s, e) => { if (Instance == this) Instance = null; };
        }

        /// <summary>
        /// Updates a single relay's state in the grid without full reload.
        /// Called from Form1 when relay_state_changed message is received.
        /// Thread-safe: marshals to UI thread if needed.
        /// </summary>
        public void UpdateRelayState(int relayId, bool isOn, string controlSource)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => UpdateRelayState(relayId, isOn, controlSource));
                return;
            }

            if (dgvRelayMapping == null) return;

            // Find the row for this relay (check if cell value starts with "Relay {id}")
            foreach (DataGridViewRow row in dgvRelayMapping.Rows)
            {
                var cellValue = row.Cells["RelayId"].Value?.ToString() ?? "";
                if (cellValue.StartsWith($"Relay {relayId} ") || cellValue == $"Relay {relayId}")
                {
                    string stateDisplay = isOn ? "ON" : "OFF";
                    if (controlSource == "Manual")
                    {
                        stateDisplay += " (Manual)";
                    }

                    row.Cells["RelayState"].Value = stateDisplay;

                    // Update color coding
                    if (isOn)
                    {
                        row.Cells["RelayState"].Style.ForeColor = Color.Green;
                        row.Cells["RelayState"].Style.Font = new Font(dgvRelayMapping.Font, FontStyle.Bold);
                    }
                    else
                    {
                        row.Cells["RelayState"].Style.ForeColor = Color.Gray;
                        row.Cells["RelayState"].Style.Font = new Font(dgvRelayMapping.Font, FontStyle.Regular);
                    }

                    break;
                }
            }
        }

        private void InitializeComponent()
        {
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.AutoScaleMode = AutoScaleMode.Dpi;

            float scalingFactor = this.DeviceDpi / 96f;

            this.Text = "Relay Status";
            this.Size = new Size((int)(850 * scalingFactor), (int)(550 * scalingFactor));
            this.StartPosition = FormStartPosition.CenterParent;
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimumSize = new Size((int)(600 * scalingFactor), (int)(400 * scalingFactor));
        }

        private void BuildUI()
        {
            this.Controls.Clear();

            // Calculate DPI-scaled sizes
            float scalingFactor = this.DeviceDpi / 96f;
            float fontScaling = FontSizeHelper.GetAutoScaling(this);
            int scaledBottomHeight = (int)(36 * scalingFactor);

            // 1. Create Bottom Panel FIRST (reserves space at the bottom)
            var bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = scaledBottomHeight,
                BackColor = SystemColors.Control
            };

            btnClose = new Button
            {
                Text = "Close",
                Width = (int)(100 * scalingFactor),
                Height = (int)(24 * scalingFactor),
                Top = (int)(6 * scalingFactor),
                Font = new Font("Segoe UI", 8.5f * fontScaling)
            };
            btnClose.Click += (s, e) => Close();

            btnRefresh = new Button
            {
                Text = "Refresh Now",
                Width = (int)(120 * scalingFactor),
                Height = (int)(24 * scalingFactor),
                Top = (int)(6 * scalingFactor),
                Font = new Font("Segoe UI", 8.5f * fontScaling)
            };
            btnRefresh.Click += async (s, e) => await LoadRelayMapping();

            bottomPanel.Resize += (s, e) =>
            {
                // Dynamically calculate width based on panel width, but within reasonable bounds
                int minRefreshWidth = (int)(120 * scalingFactor);
                int minCloseWidth = (int)(100 * scalingFactor);
                
                // Allow buttons to grow up to 200/180 pixels or 20% of panel width
                int preferredRefreshWidth = (int)(bottomPanel.Width * 0.20);
                int preferredCloseWidth = (int)(bottomPanel.Width * 0.18);
                
                btnRefresh.Width = Math.Clamp(preferredRefreshWidth, minRefreshWidth, (int)(250 * scalingFactor));
                btnClose.Width = Math.Clamp(preferredCloseWidth, minCloseWidth, (int)(200 * scalingFactor));

                int spacing = (int)(20 * scalingFactor);
                int totalWidth = btnRefresh.Width + btnClose.Width + spacing;
                int startLeft = (bottomPanel.Width - totalWidth) / 2;
                
                btnRefresh.Left = startLeft;
                btnClose.Left = startLeft + btnRefresh.Width + spacing;
            };

            bottomPanel.Controls.Add(btnRefresh);
            bottomPanel.Controls.Add(btnClose);

            // 2. Create DataGridView SECOND (fills the remaining space ABOVE the panel)
            dgvRelayMapping = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,

                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,

                // IMPORTANT: Enable automatic row height scaling for 55-inch screens
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,

                // Fix: Force header height (first row) to scale for 55-inch screens
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = (int)(32 * scalingFactor),

                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,

                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.None,

                // Scale the baseline minimum row height
                RowTemplate = { MinimumHeight = (int)(26 * scalingFactor) }
            };

            dgvRelayMapping.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dgvRelayMapping.RowsDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dgvRelayMapping.EnableHeadersVisualStyles = false;
            dgvRelayMapping.ColumnHeadersDefaultCellStyle.BackColor = Color.LightGray;
            dgvRelayMapping.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.LightGray;
            dgvRelayMapping.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.Black;
            dgvRelayMapping.ColumnHeadersDefaultCellStyle.ForeColor = Color.Black;
            
            // Fix: Specifically scale the Header font to be larger and bold
            dgvRelayMapping.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F * fontScaling, FontStyle.Bold);
            dgvRelayMapping.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;

            // Fix: Scale the master font, data font and padding for better readability on 55-inch screens
            dgvRelayMapping.Font = new Font("Segoe UI", 8F * fontScaling);
            dgvRelayMapping.DefaultCellStyle.Font = dgvRelayMapping.Font;
            dgvRelayMapping.DefaultCellStyle.Padding = new Padding((int)(2 * scalingFactor));

            // Setup columns
            dgvRelayMapping.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Relay ID",
                Name = "RelayId",
                FillWeight = 25
            });

            dgvRelayMapping.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Relay State",
                Name = "RelayState",
                FillWeight = 20
            });

            dgvRelayMapping.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Assigned Zones / Function",
                Name = "AssignedZones",
                FillWeight = 55
            });

            // 3. Add to Form (Order is critical for docking)
            // Bottom control must be added before the Fill control
            this.Controls.Add(dgvRelayMapping);
            this.Controls.Add(bottomPanel);
        }

        private async System.Threading.Tasks.Task LoadRelayMapping()
        {
            try
            {
                if (dgvRelayMapping == null) return;

                dgvRelayMapping.Rows.Clear();

                // Get system config to determine number of relays
                var systemConfig = await _channelClient.GetSystemConfigAsync();
                int maxRelays = systemConfig?.NumberOfRelays ?? 64;

                // Get all zones from configuration service
                var allZones = _configurationService.GetZones();

                // Build relay to zones mapping
                var relayMapping = new Dictionary<int, List<string>>();
                for (int i = 1; i <= maxRelays; i++)
                {
                    relayMapping[i] = new List<string>();
                }

                // Iterate through all channels and zones
                foreach (var channelEntry in allZones)
                {
                    string channelName = channelEntry.Key;
                    var zones = channelEntry.Value;

                    for (int zoneIdx = 0; zoneIdx < zones.Count; zoneIdx++)
                    {
                        var zone = zones[zoneIdx];
                        if (zone.AssignedRelayId.HasValue && zone.AssignedRelayId.Value >= 1 && zone.AssignedRelayId.Value <= maxRelays)
                        {
                            int relayId = zone.AssignedRelayId.Value;
                            string zoneName = zone.Name ?? $"Zone {zoneIdx + 1}";
                            relayMapping[relayId].Add($"{channelName} - {zoneName}");
                        }
                    }
                }

                // Get relay states from FRMC
                var relayStates = await _channelClient.GetRelayStatesAsync();

                // Populate the grid
                for (int relayId = 1; relayId <= maxRelays; relayId++)
                {
                    // Get relay display name and purpose for reserved relays (1-11)
                    string relayName = GetRelayDisplayName(relayId);
                    string zonesDisplay = GetRelayPurpose(relayId, relayMapping);

                    // Find relay state
                    string stateDisplay = "Unknown";
                    if (relayStates != null && relayStates.Any())
                    {
                        var relayState = relayStates.FirstOrDefault(r => r.RelayId == relayId);
                        if (relayState != null)
                        {
                            stateDisplay = relayState.IsOn ? "ON" : "OFF";
                            if (relayState.ControlSource == RelayControlSource.Manual)
                            {
                                stateDisplay += " (Manual)";
                            }
                        }
                    }

                    int rowIndex = dgvRelayMapping.Rows.Add(
                        relayName,
                        stateDisplay,
                        zonesDisplay
                    );

                    // Color code the relay state
                    if (stateDisplay.StartsWith("ON"))
                    {
                        dgvRelayMapping.Rows[rowIndex].Cells["RelayState"].Style.ForeColor = Color.Green;
                        dgvRelayMapping.Rows[rowIndex].Cells["RelayState"].Style.Font = new Font(dgvRelayMapping.Font, FontStyle.Bold);
                    }
                    else if (stateDisplay.StartsWith("OFF"))
                    {
                        dgvRelayMapping.Rows[rowIndex].Cells["RelayState"].Style.ForeColor = Color.Gray;
                    }

                    // Highlight reserved relays with different background
                    if (relayId <= 11)
                    {
                        dgvRelayMapping.Rows[rowIndex].DefaultCellStyle.BackColor = Color.LightGray;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading relay mapping: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Gets the display name for a relay, showing descriptive names for reserved relays (1-11).
        /// </summary>
        private static string GetRelayDisplayName(int relayId)
        {
            return relayId switch
            {
                1 => "Relay 1 - System Running",
                2 => "Relay 2 - System Health",
                3 => "Relay 3 - Common Alarm",
                4 => "Relay 4 - CH1 Fiber Break",
                5 => "Relay 5 - CH2 Fiber Break",
                6 => "Relay 6 - CH3 Fiber Break",
                7 => "Relay 7 - CH4 Fiber Break",
                8 => "Relay 8 - CH5 Fiber Break",
                9 => "Relay 9 - CH6 Fiber Break",
                10 => "Relay 10 - CH7 Fiber Break",
                11 => "Relay 11 - CH8 Fiber Break",
                _ => $"Relay {relayId}"
            };
        }

        /// <summary>
        /// Gets the purpose/assignment description for a relay.
        /// Reserved relays (1-11) show their system function, others show assigned zones.
        /// </summary>
        private static string GetRelayPurpose(int relayId, Dictionary<int, List<string>> relayMapping)
        {
            return relayId switch
            {
                1 => "[RESERVED] ON when FRMC is running",
                2 => "[RESERVED] ON when system health fault detected",
                3 => "[RESERVED] ON when any alarm is active in the system",
                4 => "[RESERVED] ON when Channel 1 fiber break detected",
                5 => "[RESERVED] ON when Channel 2 fiber break detected",
                6 => "[RESERVED] ON when Channel 3 fiber break detected",
                7 => "[RESERVED] ON when Channel 4 fiber break detected",
                8 => "[RESERVED] ON when Channel 5 fiber break detected",
                9 => "[RESERVED] ON when Channel 6 fiber break detected",
                10 => "[RESERVED] ON when Channel 7 fiber break detected",
                11 => "[RESERVED] ON when Channel 8 fiber break detected",
                _ => relayMapping.TryGetValue(relayId, out var zones) && zones.Any()
                    ? string.Join(", ", zones)
                    : "Not assigned"
            };
        }
    }
}
