
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using FRCM.Services; // Add this for IConfigurationService

namespace FRCM
{
    public partial class ZoneControl : UserControl
    {
        public event EventHandler? ZonesSaved;

        private readonly ChannelClient _channelClient;
        private readonly string _currentChannel;
        private readonly string _currentZoneName;
        private readonly IConfigurationService _configurationService; // New field
        private readonly string _loginRole;

        private DataGridView? dgvProperties;
        private Button? btnApply, btnRead;
        private Button? btnDeleteZone;

        private List<int> _availableRelays = new();
        private int? _originalAssignedRelayId = null;  // Track original relay for validation

        private enum PropRow
        {
            Name = 0,
            EnableZone = 1,
            ZoneId = 2,
            StartPoint = 3,
            EndPoint = 4,
            MaxTemp = 5,
            MinTemp = 6,
            PreAlarm = 7,
            RoR = 8,
            Deviation = 9,
            AssignedRelay = 10
            // Note: Relay trigger checkboxes removed - relay now triggers on ANY enabled alarm for this zone
        }

        public ZoneControl(string channelName, string zoneName, ChannelClient channelClient, IConfigurationService configurationService, string loginRole = "User")
        {
            _currentChannel = channelName.Trim();
            _currentZoneName = zoneName;
            _channelClient = channelClient;
            _configurationService = configurationService; // Assign the service
            _loginRole = loginRole ?? "User";

            InitializeComponent(); // Call InitializeComponent here
            BuildUI();
            SetupGrid();
            HookEvents();
            LoadZoneFromCacheOrDefaults();
            LoadAvailableRelaysFromFrmc(); // ← ADD

        }

        private void InitializeComponent()
        {
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.SuspendLayout();
            this.Name = "ZoneControl";
            this.ResumeLayout(false);
        }

        private async void LoadAvailableRelaysFromFrmc()
        {
            try
            {
                _availableRelays = await _channelClient.GetAvailableRelaysAsync();
                PopulateRelayDropdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Failed to load available relays from FRMC.\n" + ex.Message,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }


        // ---------------- UI ----------------

        private void HookEvents()
        {
            if (dgvProperties != null)
            {
                dgvProperties.CurrentCellDirtyStateChanged += (s, e) =>
                {
                    if (dgvProperties.IsCurrentCellDirty)
                        dgvProperties.CommitEdit(DataGridViewDataErrorContexts.Commit);
                };

                dgvProperties.CellValidating += DgvProperties_CellValidating;

                dgvProperties.CellValueChanged += (s, e) =>
                {
                    if (e.RowIndex == (int)PropRow.EnableZone && e.ColumnIndex == 2)
                    {
                        bool enabled = Convert.ToBoolean(
                            dgvProperties.Rows[(int)PropRow.EnableZone].Cells[2].Value ?? false
                        );

                        // Auto-update column 1 (value column) to show True/False text
                        dgvProperties.Rows[(int)PropRow.EnableZone].Cells[1].Value = enabled ? "True" : "False";

                        UpdateRowsVisibility(enabled);
                    }
                };
            }
        }

        private void DgvProperties_CellValidating(
    object? sender,
    DataGridViewCellValidatingEventArgs e)
        {
            if (dgvProperties == null)
                return;

            // Validate only Name row and Value column
            if (e.RowIndex != (int)PropRow.Name || e.ColumnIndex != 1)
                return;

            string enteredName = e.FormattedValue?.ToString()?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(enteredName))
            {
                MessageBox.Show(
                    "Zone Name cannot be empty",
                    "Validation Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                e.Cancel = true;
                return;
            }

            var zonesForChannel =
                _configurationService.GetZoneConfig(_currentChannel);

            if (zonesForChannel == null)
                return;

            // Get current zone ID
            int currentZoneId = 0;

            var zoneIdValue =
                dgvProperties.Rows[(int)PropRow.ZoneId]
                .Cells[1]
                .Value?
                .ToString();

            if (!string.IsNullOrEmpty(zoneIdValue))
            {
                if (zoneIdValue.Contains("."))
                {
                    var parts = zoneIdValue.Split('.');

                    if (parts.Length == 2)
                    {
                        int.TryParse(parts[1], out currentZoneId);
                    }
                }
                else
                {
                    int.TryParse(zoneIdValue, out currentZoneId);
                }
            }

            bool duplicateExists = zonesForChannel.Any(z =>
                z.ZoneId != currentZoneId &&
                string.Equals(
                    z.Name?.Trim(),
                    enteredName,
                    StringComparison.OrdinalIgnoreCase));

            if (duplicateExists)
            {
                MessageBox.Show(
                    $"Zone Name '{enteredName}' already exists in this channel",
                    "Validation Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                e.Cancel = true;
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // Ensure all rows are scaled correctly for high-DPI 55-inch screens
            if (dgvProperties != null)
            {
                // Force a re-calculation of all row heights now that DPI is fully resolved
                dgvProperties.AutoResizeRows(DataGridViewAutoSizeRowsMode.AllCells);
            }
        }

        private void BuildUI()
        {
            Dock = DockStyle.Fill;

            // Calculate DPI-scaled header and row heights for 55-inch screens
            float scalingFactor = this.DeviceDpi / 96f;
            float fontScaling = FontSizeHelper.GetAutoScaling(this);
            int scaledHeaderHeight = (int)(36 * scalingFactor);
            int scaledRowHeight = (int)(28 * scalingFactor);

            dgvProperties = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                ColumnHeadersVisible = true, // Explicitly show headers
                EnableHeadersVisualStyles = false, // Required for custom header styles
                // FIX: Use DisableResizing for headers to avoid first-row scaling bug in WinForms
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = Math.Max(24, scaledHeaderHeight),
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                RowTemplate = { Height = scaledRowHeight, MinimumHeight = Math.Max(24, scaledRowHeight) }, // Dynamic height based on screen DPI
                AllowUserToOrderColumns = false
            };

            // Ensure consistent font, padding, and centered checkboxes for large screens
            dgvProperties.DefaultCellStyle.Font = new Font("Segoe UI", 8F * fontScaling);
            dgvProperties.DefaultCellStyle.Padding = new Padding((int)(3 * scalingFactor));
            dgvProperties.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dgvProperties.RowsDefaultCellStyle.WrapMode = DataGridViewTriState.True;

            // Apply bold font and grey background to headers for visibility
            dgvProperties.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8F * fontScaling, FontStyle.Bold);
            dgvProperties.ColumnHeadersDefaultCellStyle.BackColor = Color.LightGray;

            // Handle DataError to show meaningful messages to users
            dgvProperties.DataError += (s, e) =>
            {
                e.ThrowException = false; // Suppress the default exception dialog

                string fieldName = "Unknown field";
                string expectedFormat = "a valid value";

                switch (e.RowIndex)
                {
                    case (int)PropRow.Name:
                        fieldName = "Zone Name";
                        expectedFormat = "a text value (e.g., 'Zone 1', 'Fire Zone A')";
                        break;
                    case (int)PropRow.EnableZone:
                        fieldName = "Enable Zone";
                        expectedFormat = "checked (enabled) or unchecked (disabled)";
                        break;
                    case (int)PropRow.ZoneId:
                        fieldName = "Zone ID";
                        expectedFormat = "a positive integer (read-only)";
                        break;
                    case (int)PropRow.StartPoint:
                        fieldName = "Start Point";
                        expectedFormat = "a number in meters (0 or greater, less than End Point)";
                        break;
                    case (int)PropRow.EndPoint:
                        fieldName = "End Point";
                        expectedFormat = "a number in meters (greater than Start Point, within Channel Length)";
                        break;
                    case (int)PropRow.MaxTemp:
                        fieldName = "Max Temperature";
                        expectedFormat = "a number in °C (typically 40 - 200°C)";
                        break;
                    case (int)PropRow.MinTemp:
                        fieldName = "Min Temperature";
                        expectedFormat = "a number in °C (typically -40 to 40°C, less than Max Temp)";
                        break;
                    case (int)PropRow.PreAlarm:
                        fieldName = "Pre-Alarm Temperature";
                        expectedFormat = "a number in °C (less than Max Temp)";
                        break;
                    case (int)PropRow.RoR:
                        fieldName = "Rate of Rise";
                        expectedFormat = "a number in °C/min (typically 1 - 50°C/min)";
                        break;
                    case (int)PropRow.Deviation:
                        fieldName = "Deviation";
                        expectedFormat = "a number in °C (typically 1 - 50°C)";
                        break;
                    case (int)PropRow.AssignedRelay:
                        fieldName = "Assigned Relay";
                        expectedFormat = "select a relay from the dropdown or 'None'";
                        break;
                }

                MessageBox.Show(
                    $"Invalid value entered for '{fieldName}'.\n\n" +
                    $"Expected: {expectedFormat}\n\n" +
                    $"Please correct the value and try again.",
                    "Invalid Input",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            };

            Panel bottomPanel = new Panel
            {
                Height = (int)(56 * scalingFactor),
                Dock = DockStyle.Bottom
            };

            btnRead = new Button
            {
                Text = "Read"
            };

            btnApply = new Button
            {
                Text = "Apply"
            };

            btnDeleteZone = new Button
            {
                Text = "Delete Zone"
            };

            btnRead.Click += BtnRead_Click;
            btnApply.Click += BtnApply_Click;
            btnDeleteZone.Click += BtnDeleteZone_Click;

            bottomPanel.Controls.Add(btnRead);
            bottomPanel.Controls.Add(btnApply);
            bottomPanel.Controls.Add(btnDeleteZone);

            bottomPanel.Resize += (s, e) =>
            {
                int panelWidth = bottomPanel.Width;
                int panelHeight = bottomPanel.Height;

                int buttonWidth = (int)(panelWidth * 0.20);
                int buttonHeight = (int)(34 * scalingFactor);
                int spacing = (int)(panelWidth * 0.05);
                int top = (int)(10 * scalingFactor); // Consistent space from the grid edge

                if (btnRead != null)
                {
                    btnRead.Width = buttonWidth;
                    btnRead.Height = buttonHeight;
                    btnRead.Top = top;
                    btnRead.Font = new Font("Segoe UI", 9 * fontScaling);
                }
                if (btnApply != null)
                {
                    btnApply.Width = buttonWidth;
                    btnApply.Height = buttonHeight;
                    btnApply.Top = top;
                    btnApply.Font = new Font("Segoe UI", 9 * fontScaling);
                }
                if (btnDeleteZone != null)
                {
                    btnDeleteZone.Width = buttonWidth;
                    btnDeleteZone.Height = buttonHeight;
                    btnDeleteZone.Top = top;
                    btnDeleteZone.Font = new Font("Segoe UI", 9 * fontScaling);
                }

                int totalWidth = (btnRead?.Width ?? 0) + (btnApply?.Width ?? 0) + (btnDeleteZone?.Width ?? 0) + (spacing * 2);
                int startX = (panelWidth - totalWidth) / 2;

                if (btnRead != null)
                {
                    btnRead.Left = startX;
                }

                if (btnApply != null)
                {
                    btnApply.Left = startX + (btnRead?.Width ?? 0) + spacing;
                }

                if (btnDeleteZone != null)
                {
                    btnDeleteZone.Left = (btnApply?.Right ?? 0) + spacing;
                }
            };

            Controls.Add(dgvProperties);
            Controls.Add(bottomPanel);

            // Admin guard: disable editing for non-admin users
            if (!string.Equals(_loginRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                if (btnApply != null) btnApply.Enabled = false;
                if (btnDeleteZone != null) btnDeleteZone.Enabled = false;
            }
        }

        private void SetupGrid()
        {
            if (dgvProperties == null) return;
            dgvProperties.Columns.Clear();

            dgvProperties.Columns.Add("Property", "Property Name");
            dgvProperties.Columns["Property"].ReadOnly = true;
            dgvProperties.Columns["Property"].SortMode = DataGridViewColumnSortMode.NotSortable;

            dgvProperties.Columns.Add("Value", "Value");
            dgvProperties.Columns["Value"].SortMode = DataGridViewColumnSortMode.NotSortable;

            dgvProperties.Columns.Add(new DataGridViewCheckBoxColumn
            {
                HeaderText = "Enable",
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            dgvProperties.Rows.Clear();

            dgvProperties.Rows.Add("Name", _currentZoneName, false);
            dgvProperties.Rows.Add("Enable Zone", false, true);
            dgvProperties.Rows.Add("Zone ID", "", false);
            dgvProperties.Rows.Add("Start Point", 0.0, false);
            dgvProperties.Rows.Add("End Point", 0.0, false);
            dgvProperties.Rows.Add("Max Temp (°C)", 0.0, false);
            dgvProperties.Rows.Add("Min Temp (°C)", 0.0, false);
            dgvProperties.Rows.Add("PreAlarm (°C)", 0.0, false);
            dgvProperties.Rows.Add("Rate of Rise (°C/min)", 0.0, false);
            dgvProperties.Rows.Add("Deviation (°C)", 0.0, false);
            dgvProperties.Rows.Add("Assigned Relay", "None", false);
            // Note: Relay trigger checkboxes removed - relay triggers on ANY enabled alarm for this zone

            // Zone name is editable by user - will sync to FRMC when saved
            // Zone ID is read-only (auto-assigned based on zone position)
            dgvProperties.Rows[(int)PropRow.ZoneId].Cells[1].ReadOnly = true;
            dgvProperties.Rows[(int)PropRow.ZoneId].Cells[1].Style.BackColor = Color.LightGray;

            // Make Assigned Relay a ComboBox - populate from system config
            PopulateRelayDropdown();

            for (int i = 0; i < dgvProperties.Rows.Count; i++)
            {
                bool allowCheckbox =
                    i == (int)PropRow.EnableZone ||
                    i == (int)PropRow.MaxTemp ||
                    i == (int)PropRow.MinTemp ||
                    i == (int)PropRow.PreAlarm ||
                    i == (int)PropRow.RoR ||
                    i == (int)PropRow.Deviation;

                if (!allowCheckbox)
                {
                    dgvProperties.Rows[i].Cells[2] = new DataGridViewTextBoxCell { Value = "" };
                }
            }

            UpdateRowsVisibility(false);
        }

        private void PopulateRelayDropdown()
        {
            if (dgvProperties == null) return;

            string? currentValue =
                dgvProperties.Rows[(int)PropRow.AssignedRelay].Cells[1].Value?.ToString();

            var relayCell = new DataGridViewComboBoxCell();
            relayCell.Items.Add("None");

            foreach (var relay in _availableRelays)
            {
                relayCell.Items.Add(relay.ToString());
            }

            // FIX: Always add currently assigned relay if not already in list
            // This prevents losing relay assignment when editing a zone that already has a relay
            if (!string.IsNullOrEmpty(currentValue) &&
                currentValue != "None" &&
                !relayCell.Items.Contains(currentValue))
            {
                relayCell.Items.Add(currentValue);
            }

            dgvProperties.Rows[(int)PropRow.AssignedRelay].Cells[1] = relayCell;

            // FIX: Always restore the current value (no longer need Contains check)
            if (!string.IsNullOrEmpty(currentValue))
            {
                dgvProperties.Rows[(int)PropRow.AssignedRelay].Cells[1].Value = currentValue;
            }
        }


        // ---------------- LOAD ----------------

        private void LoadZoneFromCacheOrDefaults()
        {
            // Use the configuration service to check if the channel has zones
            var zonesForChannel = _configurationService.GetZoneConfig(_currentChannel);

            if (zonesForChannel != null && zonesForChannel.Any())
                LoadSelectedZone();
            else
                LoadDefaults();
        }

        private void LoadSelectedZone()
        {
            if (dgvProperties == null) return;

            var zonesForChannel = _configurationService.GetZoneConfig(_currentChannel);

            if (zonesForChannel == null || !zonesForChannel.Any())
            {
                LoadDefaults();
                return;
            }

            ZoneInfo? zone = null;

            // First try: Find by matching name (supports custom zone names like "vvv")
            zone = zonesForChannel.FirstOrDefault(z => z.Name == _currentZoneName);

            // Second try: If grid has Zone ID populated (after initial load), use that for lookup
            // This handles the case where zone name changed after save
            if (zone == null)
            {
                var zoneIdValue = dgvProperties.Rows[(int)PropRow.ZoneId].Cells[1].Value;
                if (zoneIdValue != null)
                {
                    string zoneIdStr = zoneIdValue.ToString() ?? "";
                    int targetZoneId = 0;

                    // Handle composite format "ChannelId.ZoneId" (e.g., "1.1")
                    if (zoneIdStr.Contains("."))
                    {
                        var parts = zoneIdStr.Split('.');
                        if (parts.Length == 2 && int.TryParse(parts[1], out int parsedZoneId))
                        {
                            targetZoneId = parsedZoneId;
                        }
                    }
                    else if (zoneIdValue is int intId)
                    {
                        targetZoneId = intId;
                    }
                    else if (int.TryParse(zoneIdStr, out int parsedId))
                    {
                        targetZoneId = parsedId;
                    }

                    if (targetZoneId > 0)
                    {
                        zone = zonesForChannel.FirstOrDefault(z => z.ZoneId == targetZoneId);
                    }
                }
            }

            // Third try: Parse zone name and find by index (fallback for "Zone 1", "Zone 2" format)
            if (zone == null)
            {
                int index = ParseInt(_currentZoneName) - 1;
                if (index >= 0 && index < zonesForChannel.Count)
                {
                    zone = zonesForChannel[index];
                }
            }

            // If still not found, load defaults
            if (zone == null)
            {
                LoadDefaults();
                return;
            }

            // Load zone name from FRMC (user can customize this)
            dgvProperties.Rows[(int)PropRow.Name].Cells[1].Value = zone.Name;

            // Display composite zone ID (ChannelId.ZoneId format)
            dgvProperties.Rows[(int)PropRow.ZoneId].Cells[1].Value = zone.CompositeZoneId;
            dgvProperties.Rows[(int)PropRow.EnableZone].Cells[2].Value = zone.Enabled;
            dgvProperties.Rows[(int)PropRow.EnableZone].Cells[1].Value = zone.Enabled ? "True" : "False";

            dgvProperties.Rows[(int)PropRow.StartPoint].Cells[1].Value = zone.StartPoint;
            dgvProperties.Rows[(int)PropRow.EndPoint].Cells[1].Value = zone.EndPoint;

            dgvProperties.Rows[(int)PropRow.MaxTemp].Cells[1].Value = zone.MaxTemp;
            dgvProperties.Rows[(int)PropRow.MinTemp].Cells[1].Value = zone.MinTemp;
            dgvProperties.Rows[(int)PropRow.PreAlarm].Cells[1].Value = zone.PreAlarm;
            dgvProperties.Rows[(int)PropRow.RoR].Cells[1].Value = zone.RoRThreshold;
            dgvProperties.Rows[(int)PropRow.Deviation].Cells[1].Value = zone.DeviationThreshold;

            dgvProperties.Rows[(int)PropRow.MaxTemp].Cells[2].Value = zone.IsMaxTempEnabled;
            dgvProperties.Rows[(int)PropRow.MinTemp].Cells[2].Value = zone.IsMinTempEnabled;
            dgvProperties.Rows[(int)PropRow.PreAlarm].Cells[2].Value = zone.IsPreAlarmEnabled;
            dgvProperties.Rows[(int)PropRow.RoR].Cells[2].Value = zone.IsRateOfRiseEnabled;
            dgvProperties.Rows[(int)PropRow.Deviation].Cells[2].Value = zone.IsDeviationEnabled;

            // Relay settings - relay triggers on ANY enabled alarm for this zone
            _originalAssignedRelayId = zone.AssignedRelayId;  // Store original for validation

            // FIX: Ensure the assigned relay value is in the dropdown before setting it
            string relayValueToSet = zone.AssignedRelayId.HasValue ? zone.AssignedRelayId.Value.ToString() : "None";
            var relayCell = dgvProperties.Rows[(int)PropRow.AssignedRelay].Cells[1] as DataGridViewComboBoxCell;
            if (relayCell != null && !relayCell.Items.Contains(relayValueToSet) && relayValueToSet != "None")
            {
                relayCell.Items.Add(relayValueToSet);
            }
            dgvProperties.Rows[(int)PropRow.AssignedRelay].Cells[1].Value = relayValueToSet;

            UpdateRowsVisibility(zone.Enabled);
        }


        private void LoadDefaults()
        {
            if (dgvProperties == null) return;

            var zonesForChannel = _configurationService.GetZoneConfig(_currentChannel);
            var existingZone = zonesForChannel?.FirstOrDefault(z => z.Name == _currentZoneName);

            if (existingZone != null)
            {
                // ✅ Zone exists in FRMC → safe to show ID
                dgvProperties.Rows[(int)PropRow.Name].Cells[1].Value = existingZone.Name;
                dgvProperties.Rows[(int)PropRow.ZoneId].Cells[1].Value = existingZone.CompositeZoneId;
            }
            else
            {
                // ✅ New zone → ID NOT assigned yet
                dgvProperties.Rows[(int)PropRow.Name].Cells[1].Value = _currentZoneName;
                dgvProperties.Rows[(int)PropRow.ZoneId].Cells[1].Value = "Not assigned yet";
            }

            dgvProperties.Rows[(int)PropRow.EnableZone].Cells[2].Value = false;
            UpdateRowsVisibility(false);
        }

        // ---------------- APPLY / READ ----------------

        private async void BtnRead_Click(object? sender, EventArgs e)
        {
            await LoadFromFrmc();
            MessageBox.Show("Zones reloaded from FRMC.");
        }

        private async Task LoadFromFrmc()
        {
            int uiChannel = ParseChannel(_currentChannel);
            var ch = await _channelClient.GetChannelConfigAsync(uiChannel);
            if (ch == null) return;

            var zonesForChannel = new List<ZoneInfo>();
            for (int i = 0; i < ch.NumberOfZones; i++)
            {
                var cfg = await _channelClient.GetZoneConfigAsync(uiChannel, i);
                if (cfg != null)
                    zonesForChannel.Add(cfg);
            }

            // Update the configuration service with the newly loaded zones
            // TODO: Add a method to IConfigurationService to update zones for a channel
            _configurationService.GetZones()[_currentChannel] = zonesForChannel;


            LoadSelectedZone();
        }



        private async Task<string> ValidateZoneConfiguration()
        {
            if (dgvProperties == null) return "Grid not initialized";

            try
            {
                // ---------------- ZONE NAME ----------------
                var nameValue = dgvProperties.Rows[(int)PropRow.Name].Cells[1].Value?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(nameValue))
                    return "Zone Name cannot be empty";

                // ---------------- DUPLICATE ZONE NAME CHECK ----------------
                var zonesForChannel = _configurationService.GetZoneConfig(_currentChannel);

                // Get current zone ID
                int currentZoneId = 0;

                if (zonesForChannel != null)
                {
                    var zoneIdValue =
                        dgvProperties.Rows[(int)PropRow.ZoneId].Cells[1].Value?.ToString();

                    if (!string.IsNullOrEmpty(zoneIdValue))
                    {
                        // Handle composite ID format like "1.2"
                        if (zoneIdValue.Contains("."))
                        {
                            var parts = zoneIdValue.Split('.');

                            if (parts.Length == 2)
                            {
                                int.TryParse(parts[1], out currentZoneId);
                            }
                        }
                        else
                        {
                            int.TryParse(zoneIdValue, out currentZoneId);
                        }
                    }

                    bool duplicateExists = zonesForChannel.Any(z =>
                        z.ZoneId != currentZoneId &&
                        string.Equals(
                            z.Name?.Trim(),
                            nameValue,
                            StringComparison.OrdinalIgnoreCase));

                    if (duplicateExists)
                    {
                        return $"Zone Name '{nameValue}' already exists in this channel";
                    }
                }

                // ---------------- ENABLE CHECK ----------------
                bool isEnabled = Convert.ToBoolean(
                    dgvProperties.Rows[(int)PropRow.EnableZone].Cells[2].Value ?? false);

                if (!isEnabled)
                    return string.Empty; // Skip rest if zone is disabled

                // ---------------- CHANNEL LENGTH ----------------
                double channelLength = 0;
                try
                {
                    var channelConfig = _configurationService.GetChannelConfig(_currentChannel);
                    if (channelConfig != null)
                        channelLength = channelConfig.Length;
                }
                catch { }

                // ---------------- START POINT ----------------
                if (!double.TryParse(
                        dgvProperties.Rows[(int)PropRow.StartPoint].Cells[1].Value?.ToString(),
                        out double startPoint))
                    return "Start Point must be a valid number";

                if (startPoint < 0)
                    return "Start Point must be greater than or equal to 0 meters";

                if (channelLength > 0 && startPoint >= channelLength)
                    return $"Start Point must be less than Channel Length ({channelLength} m)";

                // ---------------- END POINT ----------------
                if (!double.TryParse(
                        dgvProperties.Rows[(int)PropRow.EndPoint].Cells[1].Value?.ToString(),
                        out double endPoint))
                    return "End Point must be a valid number";

                if (endPoint <= startPoint)
                    return "End Point must be greater than Start Point";

                if (channelLength > 0 && endPoint > channelLength)
                    return $"End Point cannot exceed Channel Length ({channelLength} m)";

                if ((endPoint - startPoint) < 1.0)
                    return "Zone length must be at least 1 meter";


                // ---------------- EXACT SAME RANGE CHECK ----------------
                if (zonesForChannel != null)
                {
                    var existingZone = zonesForChannel.FirstOrDefault(z =>
                    {
                        // Skip current zone
                        if (z.ZoneId == currentZoneId)
                            return false;

                        // Only block exact same range
                        return z.StartPoint == startPoint &&
                               z.EndPoint == endPoint;
                    });

                    if (existingZone != null)
                    {
                        return
                            $"Channel '{_currentChannel}' - Zone '{existingZone.Name}' " +
                            $"already uses range {existingZone.StartPoint} - {existingZone.EndPoint}";
                    }
                }

                // ---------------- TEMPERATURE LIMITS ----------------
                const double MIN_TEMP_LIMIT = -273.15;
                const double MAX_TEMP_LIMIT = 1500.0;

                if (!double.TryParse(
                        dgvProperties.Rows[(int)PropRow.MaxTemp].Cells[1].Value?.ToString(),
                        out double maxTemp))
                    return "Max Temperature must be a valid number";

                if (!double.TryParse(
                        dgvProperties.Rows[(int)PropRow.MinTemp].Cells[1].Value?.ToString(),
                        out double minTemp))
                    return "Min Temperature must be a valid number";

                if (!double.TryParse(
                        dgvProperties.Rows[(int)PropRow.PreAlarm].Cells[1].Value?.ToString(),
                        out double preAlarm))
                    return "PreAlarm Temperature must be a valid number";

                if (maxTemp < MIN_TEMP_LIMIT || maxTemp > MAX_TEMP_LIMIT)
                    return $"Max Temperature must be between {MIN_TEMP_LIMIT}°C and {MAX_TEMP_LIMIT}°C";

                if (minTemp < MIN_TEMP_LIMIT || minTemp > MAX_TEMP_LIMIT)
                    return $"Min Temperature must be between {MIN_TEMP_LIMIT}°C and {MAX_TEMP_LIMIT}°C";

                if (preAlarm < MIN_TEMP_LIMIT || preAlarm > MAX_TEMP_LIMIT)
                    return $"PreAlarm Temperature must be between {MIN_TEMP_LIMIT}°C and {MAX_TEMP_LIMIT}°C";

                bool maxTempEnabled = Convert.ToBoolean(
                    dgvProperties.Rows[(int)PropRow.MaxTemp].Cells[2].Value ?? false);
                bool minTempEnabled = Convert.ToBoolean(
                    dgvProperties.Rows[(int)PropRow.MinTemp].Cells[2].Value ?? false);
                bool preAlarmEnabled = Convert.ToBoolean(
                    dgvProperties.Rows[(int)PropRow.PreAlarm].Cells[2].Value ?? false);

                if (maxTempEnabled && minTempEnabled && maxTemp <= minTemp)
                    return "Max Temperature must be greater than Min Temperature when both are enabled";

                if (preAlarmEnabled)
                {
                    if (maxTempEnabled && preAlarm >= maxTemp)
                        return "PreAlarm Temperature must be less than Max Temperature";

                    if (minTempEnabled && preAlarm <= minTemp)
                        return "PreAlarm Temperature must be greater than Min Temperature";
                }

                // ---------------- RATE OF RISE ----------------
                if (!double.TryParse(
                        dgvProperties.Rows[(int)PropRow.RoR].Cells[1].Value?.ToString(),
                        out double ror))
                    return "Rate of Rise must be a valid number";

                bool rorEnabled = Convert.ToBoolean(
                    dgvProperties.Rows[(int)PropRow.RoR].Cells[2].Value ?? false);

                if (rorEnabled && ror <= 0)
                    return "Rate of Rise must be greater than 0 °C/min when enabled";

                if (ror > 1000)
                    return "Rate of Rise cannot exceed 1000 °C/min";

                // ---------------- DEVIATION ----------------
                if (!double.TryParse(
                        dgvProperties.Rows[(int)PropRow.Deviation].Cells[1].Value?.ToString(),
                        out double deviation))
                    return "Deviation must be a valid number";

                bool deviationEnabled = Convert.ToBoolean(
                    dgvProperties.Rows[(int)PropRow.Deviation].Cells[2].Value ?? false);

                if (deviationEnabled && deviation <= 0)
                    return "Deviation must be greater than 0 °C when enabled";

                if (deviation > 500)
                    return "Deviation cannot exceed 500 °C";

                // ---------------- ASSIGNED RELAY ----------------
                var relayValue =
                    dgvProperties.Rows[(int)PropRow.AssignedRelay].Cells[1].Value?.ToString();

                if (!string.IsNullOrEmpty(relayValue) && relayValue != "None")
                {
                    if (!int.TryParse(relayValue, out int relayId))
                        return "Assigned Relay must be a valid number or 'None'";

                    // FIX: Allow original assigned relay (not in available list because already assigned to this zone)
                    bool isOriginalRelay = _originalAssignedRelayId.HasValue && _originalAssignedRelayId.Value == relayId;

                    // Only validate relay availability if the relay list has been loaded
                    // Skip validation if list is null/empty (not yet loaded from FRMC)
                    // Also skip validation if this is the originally assigned relay
                    if (!isOriginalRelay && _availableRelays != null && _availableRelays.Count > 0 && !_availableRelays.Contains(relayId))
                        return $"Selected relay ({relayId}) is not available. Available relays: {string.Join(", ", _availableRelays)}";
                }

                return string.Empty; // ✅ All validations passed
            }
            catch (Exception ex)
            {
                return $"Validation error: {ex.Message}";
            }
        }


        private async void BtnApply_Click(object? sender, EventArgs e)
        {
            // ✅ FIX 1: Commit DataGridView edits (solves double-click issue)
            if (dgvProperties != null)
            {
                if (dgvProperties.IsCurrentCellInEditMode)
                {
                    dgvProperties.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }

                dgvProperties.EndEdit();
            }

            this.Validate();

            // 🔐 Admin check (unchanged)
            if (!string.Equals(_loginRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Access Denied. Only Admin users can modify zone configuration.",
                    "Access Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // ✅ Validation (unchanged)
            string validationError = await ValidateZoneConfiguration();
            if (!string.IsNullOrEmpty(validationError))
            {
                MessageBox.Show(validationError, "Validation Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // ✅ Save zone data (unchanged)
            SaveToStaged();

            var parent = this.FindForm() as FormChannelZoneConfig;
            if (parent != null)
            {
                // 🔥 FIX 2: IMPORTANT — do NOT skip SaveCurrentState
                await parent.ApplyAllStagedChangesAsync(skipSaveCurrentState: false);

                // ✅ Update cache (unchanged)
                UpdateLocalCacheWithCurrentZone();

                // ✅ Refresh UI (unchanged)
                LoadSelectedZone();

                // ✅ Notify parent (unchanged)
                ZonesSaved?.Invoke(this, EventArgs.Empty);
            }
        }
        /// <summary>
        /// Updates the local cache with the current zone's UI values.
        /// Called after FRMC confirms a successful save - avoids reloading all zones.
        /// </summary>
        private void UpdateLocalCacheWithCurrentZone()
        {
            if (dgvProperties == null) return;

            var zonesForChannel = _configurationService.GetZones()
                .GetOrAdd(_currentChannel, new List<ZoneInfo>());

            // Get ZoneId from grid to find the correct zone
            var zoneIdValue = dgvProperties.Rows[(int)PropRow.ZoneId].Cells[1].Value;
            if (zoneIdValue == null) return;

            int zoneId;
            string zoneIdStr = zoneIdValue.ToString() ?? "";

            if (zoneIdStr.Contains("."))
            {
                var parts = zoneIdStr.Split('.');
                if (parts.Length != 2 || !int.TryParse(parts[1], out zoneId))
                    return;
            }
            else if (!int.TryParse(zoneIdStr, out zoneId))
            {
                return;
            }

            var existingZone = zonesForChannel.FirstOrDefault(z => z.ZoneId == zoneId);
            if (existingZone != null)
            {
                // Update cache with current UI values (FRMC already confirmed these)
                existingZone.Name = dgvProperties.Rows[(int)PropRow.Name].Cells[1].Value?.ToString()?.Trim();
                existingZone.Enabled = Convert.ToBoolean(dgvProperties.Rows[(int)PropRow.EnableZone].Cells[2].Value);
                existingZone.StartPoint = GetDouble(PropRow.StartPoint);
                existingZone.EndPoint = GetDouble(PropRow.EndPoint);
                existingZone.MaxTemp = GetDouble(PropRow.MaxTemp);
                existingZone.MinTemp = GetDouble(PropRow.MinTemp);
                existingZone.PreAlarm = GetDouble(PropRow.PreAlarm);
                existingZone.RoRThreshold = GetDouble(PropRow.RoR);
                existingZone.DeviationThreshold = GetDouble(PropRow.Deviation);
                existingZone.IsMaxTempEnabled = Convert.ToBoolean(dgvProperties.Rows[(int)PropRow.MaxTemp].Cells[2].Value);
                existingZone.IsMinTempEnabled = Convert.ToBoolean(dgvProperties.Rows[(int)PropRow.MinTemp].Cells[2].Value);
                existingZone.IsPreAlarmEnabled = Convert.ToBoolean(dgvProperties.Rows[(int)PropRow.PreAlarm].Cells[2].Value);
                existingZone.IsRateOfRiseEnabled = Convert.ToBoolean(dgvProperties.Rows[(int)PropRow.RoR].Cells[2].Value);
                existingZone.IsDeviationEnabled = Convert.ToBoolean(dgvProperties.Rows[(int)PropRow.Deviation].Cells[2].Value);

                // Relay assignment
                var relayValue = dgvProperties.Rows[(int)PropRow.AssignedRelay].Cells[1].Value?.ToString();
                existingZone.AssignedRelayId =
                    string.IsNullOrEmpty(relayValue) || relayValue == "None"
                        ? null
                        : int.TryParse(relayValue, out var r) ? r : null;
            }
        }

        internal void SaveToStaged()
        {
            // Get staged list or create one by copying global list
            if (!_configurationService.GetStagedZones().TryGetValue(_currentChannel, out var stagedZones))
            {
                var globalZones = _configurationService.GetZoneConfig(_currentChannel) ?? new List<ZoneInfo>();
                // Deep copy global to staged
                stagedZones = globalZones.Select(z => new ZoneInfo
                {
                    ChannelId = z.ChannelId,
                    ZoneId = z.ZoneId,
                    Name = z.Name,
                    Enabled = z.Enabled,
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
                    AssignedRelayId = z.AssignedRelayId
                }).ToList();
                _configurationService.GetStagedZones()[_currentChannel] = stagedZones;
            }

            // Get ZoneId from grid
            var zoneIdValue = dgvProperties.Rows[(int)PropRow.ZoneId].Cells[1].Value;
            if (zoneIdValue == null) return;

            int zoneId;
            string zoneIdStr = zoneIdValue.ToString() ?? "";

            if (zoneIdStr.Contains("."))
            {
                var parts = zoneIdStr.Split('.');
                if (parts.Length != 2 || !int.TryParse(parts[1], out zoneId))
                    return;
            }
            else if (!int.TryParse(zoneIdStr, out zoneId))
            {
                return;
            }

            // Find or create in staged list
            var stagedZone = stagedZones.FirstOrDefault(z => z.ZoneId == zoneId);

            if (stagedZone == null)
            {
                // 🔥 FIX: Do NOT recreate deleted zones
                return;
            }

            // Update staged with UI values
            stagedZone.Name = dgvProperties.Rows[(int)PropRow.Name].Cells[1].Value?.ToString()?.Trim();
            stagedZone.Enabled = Convert.ToBoolean(dgvProperties.Rows[(int)PropRow.EnableZone].Cells[2].Value);

            stagedZone.StartPoint = GetDouble(PropRow.StartPoint);
            stagedZone.EndPoint = GetDouble(PropRow.EndPoint);
            stagedZone.MaxTemp = GetDouble(PropRow.MaxTemp);
            stagedZone.MinTemp = GetDouble(PropRow.MinTemp);
            stagedZone.PreAlarm = GetDouble(PropRow.PreAlarm);
            stagedZone.RoRThreshold = GetDouble(PropRow.RoR);
            stagedZone.DeviationThreshold = GetDouble(PropRow.Deviation);

            stagedZone.IsMaxTempEnabled = Convert.ToBoolean(dgvProperties.Rows[(int)PropRow.MaxTemp].Cells[2].Value);
            stagedZone.IsMinTempEnabled = Convert.ToBoolean(dgvProperties.Rows[(int)PropRow.MinTemp].Cells[2].Value);
            stagedZone.IsPreAlarmEnabled = Convert.ToBoolean(dgvProperties.Rows[(int)PropRow.PreAlarm].Cells[2].Value);
            stagedZone.IsRateOfRiseEnabled = Convert.ToBoolean(dgvProperties.Rows[(int)PropRow.RoR].Cells[2].Value);
            stagedZone.IsDeviationEnabled = Convert.ToBoolean(dgvProperties.Rows[(int)PropRow.Deviation].Cells[2].Value);

            var relayValue = dgvProperties.Rows[(int)PropRow.AssignedRelay].Cells[1].Value?.ToString();
            stagedZone.AssignedRelayId =
                string.IsNullOrEmpty(relayValue) || relayValue == "None"
                    ? null
                    : int.TryParse(relayValue, out var r) ? r : null;
        }


        private async void BtnDeleteZone_Click(object? sender, EventArgs e)
        {
            if (!string.Equals(_loginRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Access Denied. Only Admin users can delete zones.",
                    "Access Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var zonesForChannel =
                _configurationService.GetZones()
                    .GetOrAdd(_currentChannel, new List<ZoneInfo>());

            // Minimum 1 zone validation
            if (zonesForChannel.Count <= 1)
            {
                MessageBox.Show(
                    "Each channel must have at least one zone.\n\n" +
                    "You cannot delete the last remaining zone.",
                    "Minimum Zone Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            if (MessageBox.Show(
                $"Are you sure you want to delete '{_currentZoneName}'?",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            var zone = zonesForChannel.FirstOrDefault(z => z.Name == _currentZoneName);
            if (zone == null)
            {
                MessageBox.Show("Zone not found.", "Error");
                return;
            }

            // Create updated zone list
            var updatedZones = zonesForChannel
                .Where(z => z.ZoneId != zone.ZoneId)
                .ToList();

            // 1️⃣ Send updated zones to FRMC
            bool success = await _channelClient
                .SetChannelZonesPreserveIdsAsync(_currentChannel, updatedZones);

            if (!success)
            {
                MessageBox.Show("Failed to delete zone in FRMC.");
                return;
            }

            // 2️⃣ Update channel zone count in FRMC
            if (_configurationService.GetChannels()
                .TryGetValue(_currentChannel, out var channelCfg))
            {
                channelCfg.NumberOfZones = updatedZones.Count;

                bool cfgOk = await _channelClient.SetChannelConfigAsync(channelCfg);
                if (!cfgOk)
                {
                    MessageBox.Show("Zone deleted but failed to update channel config.");
                    return;
                }

                // 🔥 Ensure global cache updated
                _configurationService.GetChannels()[_currentChannel] = channelCfg;
            }

            // 3️⃣ Update global zone cache
            // 3️⃣ Update global zone cache
            _configurationService.GetZones()[_currentChannel] = updatedZones;

            // 🔥 FIX: Update stagedZones ALSO (this is the main fix)
            _configurationService.GetStagedZones()[_currentChannel] = updatedZones.ToList();

            // Optional: clear stagedChannels only
            _configurationService.GetStagedChannels().TryRemove(_currentChannel, out _);

            // 🔥 Clear main form cache to prevent stale data for the deleted zone
            if (Application.OpenForms["Form1"] is Form1 mainForm)
            {
                mainForm.ResetZoneStateCache();
            }

            MessageBox.Show("Zone deleted successfully.");

            ZonesSaved?.Invoke(this, EventArgs.Empty);
        }

        private double GetDouble(PropRow row)
            => Convert.ToDouble(dgvProperties.Rows[(int)row].Cells[1].Value ?? 0);

        private void UpdateRowsVisibility(bool enabled)
        {
            // Hide/show zone configuration rows (Start, End, temps, etc.)
            for (int i = (int)PropRow.StartPoint; i <= (int)PropRow.Deviation; i++)
                dgvProperties.Rows[i].Visible = enabled;

            // Hide/show relay control row (only visible when zone is enabled)
            // Note: Relay triggers on ANY enabled alarm for this zone - no per-alarm-type checkboxes
            dgvProperties.Rows[(int)PropRow.AssignedRelay].Visible = enabled;
        }

        private int ParseChannel(string s)
        {
            var m = System.Text.RegularExpressions.Regex.Match(s ?? "", @"\d+");
            return m.Success ? int.Parse(m.Value) : 1;
        }

        private int ParseInt(string s)
        {
            var m = System.Text.RegularExpressions.Regex.Match(s ?? "", @"\d+");
            return m.Success ? int.Parse(m.Value) : 0;
        }


    }
}
