using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using FRCM.Services;
using OfficeOpenXml;

namespace FRCM
{
    public partial class ChannelControl : UserControl
    {
        private readonly string _channelName;
        private readonly IConfigurationService _configurationService; // Use the new service
        private readonly string _loginRole;
        private readonly ChannelClient _channelClient;

        public event EventHandler? ChannelSaved;

        private DataGridView? dataGridViewChannel;
        private Button? btnRead, btnApply;

        private const int ROW_CHANNEL_NAME = 0;
        private const int ROW_LENGTH = 1;
        private const int ROW_SCAN_PERIOD = 2;
        private const int ROW_CORRECTION_LENGTH = 3;
        private const int ROW_NUMBER_OF_ZONES = 4;
        private const int ROW_ENABLE_CHANNEL = 5;


        public ChannelControl(
    string channelName,
    IConfigurationService configurationService, // Changed parameter
    string loginRole,
    ChannelClient channelClient)
        {
            _channelName = channelName ?? throw new ArgumentNullException(nameof(channelName));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService)); // Assign the new service
            _loginRole = loginRole ?? string.Empty;
            _channelClient = channelClient ?? throw new ArgumentNullException(nameof(channelClient));

            InitializeComponent();
            BuildUI();
            SetupChannelGrid();
            LoadChannelData();
        }

        private void InitializeComponent()
        {
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.SuspendLayout();
            this.Name = "ChannelConfigurationControl";
            this.ResumeLayout(false);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // Ensure all rows are scaled correctly for high-DPI 55-inch screens
            if (dataGridViewChannel != null)
            {
                // Force a re-calculation of all row heights now that DPI is fully resolved
                dataGridViewChannel.AutoResizeRows(DataGridViewAutoSizeRowsMode.AllCells);
            }
        }

        private void BuildUI()
        {
            this.Dock = DockStyle.Fill;
            this.Controls.Clear();

            // Calculate DPI-scaled header and row heights for 55-inch screens
            float scalingFactor = this.DeviceDpi / 96f;
            float fontScaling = FontSizeHelper.GetAutoScaling(this);
            int scaledHeaderHeight = (int)(40 * scalingFactor); 
            int scaledRowHeight = (int)(30 * scalingFactor);    

            dataGridViewChannel = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                ColumnHeadersVisible = true,
                EnableHeadersVisualStyles = false, // Required for custom header styles
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                RowTemplate = { Height = scaledRowHeight, MinimumHeight = scaledRowHeight }, // Consistent baseline
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                EditMode = DataGridViewEditMode.EditOnEnter,
                // FIX: Use DisableResizing for headers to avoid first-row scaling bug in WinForms
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = scaledHeaderHeight
            };

            // Ensure consistent font and padding for better scaling on 55-inch screens
            dataGridViewChannel.DefaultCellStyle.Font = new Font("Segoe UI", 8F * fontScaling);
            dataGridViewChannel.DefaultCellStyle.Padding = new Padding((int)(3 * scalingFactor));
            dataGridViewChannel.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewChannel.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dataGridViewChannel.RowsDefaultCellStyle.WrapMode = DataGridViewTriState.True;

            // Apply bold font and grey background to headers for visibility
            dataGridViewChannel.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8F * fontScaling, FontStyle.Bold);
            dataGridViewChannel.ColumnHeadersDefaultCellStyle.BackColor = Color.LightGray;

            dataGridViewChannel.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (dataGridViewChannel.IsCurrentCellDirty)
                    dataGridViewChannel.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            dataGridViewChannel.CellValueChanged += (s, e) =>
            {
                if (e.RowIndex == ROW_ENABLE_CHANNEL && e.ColumnIndex == 1)
                {
                    // Get the checkbox value (this is a DataGridViewCheckBoxCell)
                    bool enabled = Convert.ToBoolean(
                        dataGridViewChannel.Rows[ROW_ENABLE_CHANNEL].Cells[1].Value ?? false
                    );

                    // Note: For checkbox cells, the value IS the boolean, so no text update needed here
                    // The checkbox itself displays the state visually

                    UpdateUIBasedOnEnable();
                }
            };

            // Handle DataError to show meaningful messages to users
            dataGridViewChannel.DataError += (s, e) =>
            {
                e.ThrowException = false; // Suppress the default exception dialog

                string fieldName = "Unknown field";
                string expectedFormat = "a valid value";

                switch (e.RowIndex)
                {
                    case ROW_CHANNEL_NAME:
                        fieldName = "Channel Name";
                        expectedFormat = "a text value (e.g., 'Channel 1')";
                        break;
                    case ROW_LENGTH:
                        fieldName = "Length";
                        expectedFormat = "a positive integer (1 - 100,000 meters)";
                        break;
                    case ROW_SCAN_PERIOD:
                        fieldName = "Scan Period";
                        expectedFormat = "a positive integer (1 - 3,600 seconds)";
                        break;
                    case ROW_CORRECTION_LENGTH:
                        fieldName = "Correction Length";
                        expectedFormat = "a number (0 or greater, less than Channel Length)";
                        break;
                    case ROW_NUMBER_OF_ZONES:
                        fieldName = "Number of Zones";
                        expectedFormat = "a positive integer (0 - 50)";
                        break;
                    case ROW_ENABLE_CHANNEL:
                        fieldName = "Enable Channel";
                        expectedFormat = "checked (enabled) or unchecked (disabled)";
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

            this.Controls.Add(dataGridViewChannel);

            Panel bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = (int)(52 * scalingFactor)
            };

            btnRead = new Button { Text = "Read" };
            btnApply = new Button { Text = "Apply" };

            bottomPanel.Controls.AddRange(new Control[] { btnRead, btnApply });

            bottomPanel.Resize += (s, e) =>
            {
                int panelWidth = bottomPanel.Width;
                int panelHeight = bottomPanel.Height;

                int buttonWidth = (int)(panelWidth * 0.25);
                int buttonHeight = (int)(34 * scalingFactor); 
                int spacing = (int)(panelWidth * 0.12);
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


                int totalWidth = (btnRead?.Width ?? 0) + (btnApply?.Width ?? 0) + spacing;
                int startX = (panelWidth - totalWidth) / 2;

                if (btnRead != null)
                {
                    btnRead.Left = startX;
                }

                if (btnApply != null)
                {
                    btnApply.Left = startX + (btnRead?.Width ?? 0) + spacing;
                }
            };

            btnRead.Click += BtnRead_Click;
            btnApply.Click += BtnApply_Click;

            // Admin guard: disable Apply for non-admin users
            if (!_loginRole.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                btnApply.Enabled = false;
            }

            this.Controls.Add(bottomPanel);
        }

        private void SetupChannelGrid()
        {
            if (dataGridViewChannel == null) return;
            dataGridViewChannel.Columns.Clear();
            dataGridViewChannel.AutoGenerateColumns = false;

            dataGridViewChannel.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Property Name",
                ReadOnly = true
            });
            dataGridViewChannel.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Property Value"
            });


            dataGridViewChannel.Rows.Add("Channel Name", "");
            dataGridViewChannel.Rows[ROW_CHANNEL_NAME].Cells[1].ReadOnly = true;
            dataGridViewChannel.Rows[ROW_CHANNEL_NAME].Cells[1].Style.BackColor = Color.LightGray;
            dataGridViewChannel.Rows.Add("Length(m)", "");
            dataGridViewChannel.Rows.Add("Scan Period(sec)", "");
            dataGridViewChannel.Rows.Add("Correction Length(m)", "");
            dataGridViewChannel.Rows.Add("Number of Zones", "");

            int enableRow = dataGridViewChannel.Rows.Add("Enable Channel", false);
            dataGridViewChannel.Rows[enableRow].Cells[1] = new DataGridViewCheckBoxCell();

        }
        private ChannelConfigurationInfo? GetInfo()
        {
            // Retrieve from central configuration service
            return _configurationService.GetChannelConfig(_channelName);
        }
        private void LoadChannelData()
        {
            if (dataGridViewChannel == null) return;
            var info = GetInfo();
            bool isAdmin = _loginRole.Equals("Admin", StringComparison.OrdinalIgnoreCase);

            dataGridViewChannel.Rows[ROW_CHANNEL_NAME].Cells[1].Value = _channelName;



            if (info != null)
            {
                dataGridViewChannel.Rows[ROW_LENGTH].Cells[1].Value = info.Length;
                dataGridViewChannel.Rows[ROW_SCAN_PERIOD].Cells[1].Value = info.ScanPeriod;
                dataGridViewChannel.Rows[ROW_CORRECTION_LENGTH].Cells[1].Value = info.CorrectionLength;
                dataGridViewChannel.Rows[ROW_NUMBER_OF_ZONES].Cells[1].Value = info.NumberOfZones;
                dataGridViewChannel.Rows[ROW_ENABLE_CHANNEL].Cells[1].Value = info.IsEnabled;
            }
            else
            {
                dataGridViewChannel.Rows[ROW_LENGTH].Cells[1].Value = 0;
                dataGridViewChannel.Rows[ROW_SCAN_PERIOD].Cells[1].Value = 0;
                dataGridViewChannel.Rows[ROW_CORRECTION_LENGTH].Cells[1].Value = 0;
                dataGridViewChannel.Rows[ROW_NUMBER_OF_ZONES].Cells[1].Value = 0;
                dataGridViewChannel.Rows[ROW_ENABLE_CHANNEL].Cells[1].Value = false;
            }


            dataGridViewChannel.Rows[ROW_NUMBER_OF_ZONES].Cells[1].ReadOnly = !isAdmin;
            dataGridViewChannel.Rows[ROW_NUMBER_OF_ZONES].Cells[1].Style.BackColor =
                isAdmin ? Color.White : Color.LightGray;
            dataGridViewChannel.Rows[ROW_ENABLE_CHANNEL].Cells[1].ReadOnly = !isAdmin;
            UpdateUIBasedOnEnable();
        }
        internal void SaveToStaged()
        {
            if (dataGridViewChannel == null) return;

            // Get current config (staged or global) to preserve other properties
            var current = _configurationService.GetChannelConfig(_channelName);

            var stagedConfig = new ChannelConfigurationInfo
            {
                Name = _channelName,
                ChannelId = current?.ChannelId ?? (ExtractChannelIdFromName(_channelName) - 1),
                FrequencyOfScanningSeconds = current?.FrequencyOfScanningSeconds ?? 0
            };

            // Capture current UI values
            stagedConfig.Length = Convert.ToDouble(dataGridViewChannel.Rows[ROW_LENGTH].Cells[1].Value ?? 0);
            stagedConfig.ScanPeriod = Convert.ToInt32(dataGridViewChannel.Rows[ROW_SCAN_PERIOD].Cells[1].Value ?? 0);
            stagedConfig.CorrectionLength = Convert.ToDouble(dataGridViewChannel.Rows[ROW_CORRECTION_LENGTH].Cells[1].Value ?? 0.0);

            // Capture target total zones from UI
            int targetTotal = Convert.ToInt32(
                dataGridViewChannel.Rows[ROW_NUMBER_OF_ZONES].Cells[1].Value ?? 0
            );

            stagedConfig.NumberOfZones = targetTotal;
            stagedConfig.IsEnabled = Convert.ToBoolean(dataGridViewChannel.Rows[ROW_ENABLE_CHANNEL].Cells[1].Value ?? false);

            // Store in staged memory
            _configurationService.GetStagedChannels()[_channelName] = stagedConfig;
        }

        //private void SaveToSource()
        //{
        //    if (dataGridViewChannel == null) return;
        //    var currentConfig = _configurationService.GetChannelConfig(_channelName);
        //    if (currentConfig == null)
        //    {
        //        // This means there's no existing config. We should create a new one.
        //        currentConfig = new ChannelConfigurationInfo
        //        {
        //            Name = _channelName,
        //            // Assuming ChannelId can be derived or is part of _channelName
        //            ChannelId = ExtractChannelIdFromName(_channelName) - 1 // Convert UI channel to FRMC ID
        //        };
        //    }


        //    currentConfig.Name = _channelName;
        //    currentConfig.Length = Convert.ToInt32(dataGridViewChannel.Rows[ROW_LENGTH].Cells[1].Value ?? 0);
        //    currentConfig.ScanPeriod = Convert.ToInt32(dataGridViewChannel.Rows[ROW_SCAN_PERIOD].Cells[1].Value ?? 0);
        //    //currentConfig.CorrectionLength = Convert.ToInt32(dataGridViewChannel.Rows[ROW_CORRECTION_LENGTH].Cells[1].Value ?? 0);
        //    currentConfig.CorrectionLength =Convert.ToDouble(dataGridViewChannel.Rows[ROW_CORRECTION_LENGTH].Cells[1].Value ?? 0.0);
        //    currentConfig.NumberOfZones = Convert.ToInt32(dataGridViewChannel.Rows[ROW_NUMBER_OF_ZONES].Cells[1].Value ?? 0);
        //    currentConfig.IsEnabled = Convert.ToBoolean(dataGridViewChannel.Rows[ROW_ENABLE_CHANNEL].Cells[1].Value ?? false);


        //    // TODO: Add a method to IConfigurationService to update/add channel configuration
        //    // _configurationService.UpdateChannelConfig(_channelName, currentConfig);
        //    _configurationService.GetChannels()[_channelName] = currentConfig; // Direct update for now, will be replaced
        //}

        private async Task ReadFromFrmcAndPopulateAsync()
        {
            try
            {
                // Extract UI channel number (1,2,3,...)
                int uiChannel = ExtractChannelIdFromName(_channelName);

                // ✔️ DO NOT subtract 1 here
                // The ChannelClient will convert UI → FRMC internally
                int frmcChannel = uiChannel;

                // Make request
                var frmcCfg = await _channelClient.GetChannelConfigAsync(frmcChannel);

                // Null check
                if (frmcCfg == null)
                {
                    MessageBox.Show(
                        $"FRMC did not return configuration for {_channelName}.",
                        "No Data",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    return;
                }

                // Update central configuration service
                // TODO: Replace with a proper _configurationService.UpdateChannelConfig method
                _configurationService.GetChannels()[_channelName] = frmcCfg;

                // ✅ ALSO read all zones for this channel from FRMC
                //var zonesForChannel = new List<ZoneInfo>();

                //for (int zoneId = 1; zoneId <= frmcCfg.NumberOfZones; zoneId++)
                //{
                //    var zoneCfg = await _channelClient.GetZoneConfigAsync(frmcChannel, zoneId);
                //    if (zoneCfg != null)
                //        zonesForChannel.Add(zoneCfg);
                //}

                //// Store zones in central configuration service
                //_configurationService.GetZones()[_channelName] = zonesForChannel;



                // Refresh UI
                LoadChannelData();



                string msg =
                $"Channel: {_channelName}\n" +
                $"Length: {frmcCfg.Length} m\n" +
                $"Correction Length: {frmcCfg.CorrectionLength} m\n" +
                $"Scan Period: {frmcCfg.ScanPeriod} sec\n" +
                $"Number of Zones: {frmcCfg.NumberOfZones}\n" +
                $"Enabled: {(frmcCfg.IsEnabled ? "Yes" : "No")}";


                MessageBox.Show(
                    msg,
                    "FRMC Channel Config Loaded",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to read data from FRMC:\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }



        private void UpdateUIBasedOnEnable()
        {
            if (dataGridViewChannel == null || btnApply == null) return;

            btnApply.Enabled = true;
        }

        private async void BtnRead_Click(object? sender, EventArgs e)
        {
            await ReadFromFrmcAndPopulateAsync();
        }
        private async void BtnApply_Click(object? sender, EventArgs e)
        {
            if (!_loginRole.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Access Denied. Only Admin users can modify channel configuration.",
                    "Access Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Validate inputs before saving
            string validationError = await ValidateChannelConfigurationAsync();
            if (!string.IsNullOrEmpty(validationError))
            {
                MessageBox.Show(validationError, "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SaveToStaged(); // Save current state to staging memory

            var parent = this.FindForm() as FormChannelZoneConfig;
            if (parent != null)
            {
                await parent.ApplyAllStagedChangesAsync(skipSaveCurrentState: true);

                // 🔥 FORCE staging clear to ensure LoadChannelData pulls from updated global cache
                _configurationService.GetStagedChannels().TryRemove(_channelName, out _);
                _configurationService.GetStagedZones().TryRemove(_channelName, out _);

                LoadChannelData(); // Refresh UI with current data
            }
        }
        private async Task<string> ValidateChannelConfigurationAsync()
        {
            if (dataGridViewChannel == null) return "Grid not initialized";

            try
            {
                // Validate Channel Length
                var lengthValue = dataGridViewChannel.Rows[ROW_LENGTH].Cells[1].Value;
                if (lengthValue == null || !double.TryParse(lengthValue.ToString(), out double length))
                {
                    return "Channel Length must be a valid number";
                }

                if (length <= 0)
                {
                    return "Channel Length must be greater than 0 meters";
                }

                if (length > 100000)
                {
                    return "Channel Length cannot exceed 100,000 meters";
                }

                // Validate Scan Period
                var scanPeriodValue = dataGridViewChannel.Rows[ROW_SCAN_PERIOD].Cells[1].Value;
                if (scanPeriodValue == null || !int.TryParse(scanPeriodValue.ToString(), out int scanPeriod))
                {
                    return "Scan Period must be a valid integer";
                }

                if (scanPeriod <= 0)
                {
                    return "Scan Period must be greater than 0 seconds";
                }

                if (scanPeriod > 3600)
                {
                    return "Scan Period cannot exceed 3600 seconds (1 hour)";
                }

                // Validate Correction Length
                var correctionValue =
                    dataGridViewChannel.Rows[ROW_CORRECTION_LENGTH].Cells[1].Value;

                if (correctionValue == null ||
                    !double.TryParse(correctionValue.ToString(), out double correction))
                {
                    return "Correction Length must be a valid number";
                }

                if (correction < 0)
                {
                    return "Correction Length cannot be negative";
                }

                if (correction > length)
                {
                    return "Correction Length cannot be greater than Channel Length";
                }


                // Validate Number of Zones
                var zonesValue = dataGridViewChannel.Rows[ROW_NUMBER_OF_ZONES].Cells[1].Value;
                if (zonesValue == null || !int.TryParse(zonesValue.ToString(), out int numberOfZones))
                {
                    return "Number of Zones must be a valid integer";
                }

                //if (numberOfZones < 0)
                //{
                //    return "Number of Zones cannot be negative";
                //}

                if (numberOfZones < 1)
                {
                    return "Each channel must have at least 1 zone";
                }

                // Get system configuration to check max zones limit
                int maxZones = 50; // Default
                try
                {
                    var systemConfig = await _channelClient.GetSystemConfigAsync();
                    if (systemConfig != null)
                    {
                        maxZones = systemConfig.MaxZonesPerChannel;
                    }
                }
                catch
                {
                    // Use default if can't fetch system config
                }

                if (numberOfZones > maxZones)
                {
                    return $"Number of Zones cannot exceed {maxZones} (system limit)";
                }

                return string.Empty; // All validation passed
            }


            catch (Exception ex)
            {
                return $"Validation error: {ex.Message}";
            }
        }
        private static int ExtractChannelIdFromName(string name)
        {
            var m = Regex.Match(name, @"\d+");
            return (m.Success && int.TryParse(m.Value, out int n)) ? n : 1;
        }

        private int GetNextZoneNumberFromExistingNames(List<ZoneInfo> zones)
        {
            int max = 0;

            foreach (var z in zones)
            {
                if (string.IsNullOrWhiteSpace(z.Name))
                    continue;

                var match = Regex.Match(z.Name, @"\d+");
                if (match.Success && int.TryParse(match.Value, out int n))
                {
                    if (n > max)
                        max = n;
                }
            }

            return max + 1;
        }

    }
}
