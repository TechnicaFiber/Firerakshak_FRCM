using System;
using System.Drawing;
using System.Windows.Forms;
using FRCM.Models;
using FRCM.Services;

namespace FRCM
{
    public partial class FormSystemConfig : Form
    {
        private readonly ChannelClient _channelClient;
        private readonly string _loginRole;
        private DataGridView? dgvConfig;
        private Button? btnSave, btnCancel;

        public FormSystemConfig(ChannelClient channelClient, string loginRole)
        {
            _channelClient = channelClient;
            _loginRole = loginRole;

            this.WindowState = FormWindowState.Maximized;
            InitializeComponent();
            BuildUI();
            LoadConfiguration();
        }

        private void InitializeComponent()
        {
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.AutoScaleMode = AutoScaleMode.Dpi;

            float scalingFactor = this.DeviceDpi / 96f;

            this.Text = "System Configuration";
            this.Size = new Size((int)(850 * scalingFactor), (int)(600 * scalingFactor));
            this.StartPosition = FormStartPosition.CenterParent;
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.MinimumSize = new Size((int)(700 * scalingFactor), (int)(500 * scalingFactor));
        }

        private void BuildUI()
        {
            // Calculate DPI-scaled sizes
            float scalingFactor = this.DeviceDpi / 96f;
            float fontScaling = FontSizeHelper.GetAutoScaling(this);
            int scaledHeaderHeight = (int)(40 * scalingFactor);
            int scaledButtonHeight = (int)(26 * scalingFactor);

            // Main layout
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Grid
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));    // Buttons

            dgvConfig = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = false,
                RowHeadersVisible = false,
                ColumnHeadersVisible = true,

                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,

                SelectionMode = DataGridViewSelectionMode.CellSelect,
                MultiSelect = false,

                BackgroundColor = SystemColors.Window,
                AllowUserToResizeRows = false,

                EditMode = DataGridViewEditMode.EditOnEnter,
                
                // Fix: Force header height to scale for 55-inch screens
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = scaledHeaderHeight,

                RowTemplate = { MinimumHeight = (int)(30 * scalingFactor) }
            };

            // Improve readability for large displays like 55-inch screens
            dgvConfig.Font = new Font("Segoe UI", 9F * fontScaling);
            dgvConfig.DefaultCellStyle.Font = dgvConfig.Font;
            dgvConfig.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 11F * fontScaling, FontStyle.Bold);
            dgvConfig.DefaultCellStyle.Padding = new Padding((int)(3 * scalingFactor));
            dgvConfig.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dgvConfig.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dgvConfig.RowsDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dgvConfig.EnableHeadersVisualStyles = false;
            dgvConfig.ColumnHeadersDefaultCellStyle.BackColor = Color.LightGray;

            dgvConfig.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (dgvConfig.IsCurrentCellDirty)
                    dgvConfig.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            // Setup columns
            dgvConfig.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Setting", Name = "Setting", FillWeight = 40, ReadOnly = true });
            dgvConfig.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Value", Name = "Value", FillWeight = 25 });
            dgvConfig.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Unit", Name = "Unit", FillWeight = 15, ReadOnly = true });
            dgvConfig.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Description", Name = "Description", FillWeight = 45, ReadOnly = true });

            // Button layout
            var buttonLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 1,
                Padding = new Padding(10, 2, 10, 2)
            };
            buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35)); // Left Spacer
            buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15)); // Save Button
            buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 10)); // Gap
            buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15)); // Cancel Button
            buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35)); // Right Spacer

            btnCancel = new Button { Text = "Cancel", Anchor = AnchorStyles.Left | AnchorStyles.Right, Height = scaledButtonHeight, Font = new Font("Segoe UI", 8 * fontScaling) };
            btnCancel.Click += BtnCancel_Click;

            btnSave = new Button { Text = "Save", Anchor = AnchorStyles.Left | AnchorStyles.Right, Height = scaledButtonHeight, Font = new Font("Segoe UI", 8 * fontScaling) };
            btnSave.Click += BtnSave_Click;

            buttonLayout.Controls.Add(btnSave, 1, 0);
            buttonLayout.Controls.Add(btnCancel, 3, 0);

            // Add to main layout
            mainLayout.Controls.Add(dgvConfig, 0, 0);
            mainLayout.Controls.Add(buttonLayout, 0, 1);
            this.Controls.Add(mainLayout);

            // Admin guard: make read-only for non-admin users
            if (!string.Equals(_loginRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                if (dgvConfig != null) dgvConfig.ReadOnly = true;
                if (btnSave != null) btnSave.Enabled = false;
            }
        }

        private async void LoadConfiguration()
        {
            try
            {
                // Load decimation config first
                var (success, mediumKm, largeKm, mediumFactor, largeFactor) = await _channelClient.GetDecimationConfigAsync();
                if (success)
                {
                    _decimationMediumKm = mediumKm;
                    _decimationLargeKm = largeKm;
                    _decimationMediumFactor = mediumFactor;
                    _decimationLargeFactor = largeFactor;
                }

                var config = await _channelClient.GetSystemConfigAsync();
                if (config != null && dgvConfig != null)
                {
                    PopulateGrid(config);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading configuration: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Decimation config cached values
        private double _decimationMediumKm = 3;
        private double _decimationLargeKm = 6;
        private int _decimationMediumFactor = 5;
        private int _decimationLargeFactor = 10;
        private SystemConfiguration? _originalConfig;

        private void PopulateGrid(SystemConfiguration config)
        {
            _originalConfig = config;
            if (dgvConfig == null) return;

            dgvConfig.Rows.Clear();

            dgvConfig.Rows.Add("Max Number of Relays", config.NumberOfRelays.ToString(), "-", "Maximum relay pool size (1-64)");
            dgvConfig.Rows.Add("Max Zones per Channel", config.MaxZonesPerChannel.ToString(), "-", "Maximum zones per channel (1-1000)");
            dgvConfig.Rows.Add("DTS Polling Interval", config.DtsPollingInterval.ToString(), "ms", "Data query rate from DTS device");
            dgvConfig.Rows.Add("Alarm Evaluation Interval", config.AlarmEvaluationInterval.ToString(), "ms", "Alarm check frequency");
            dgvConfig.Rows.Add("Alarm Broadcast Interval", config.AlarmStatusBroadcastInterval.ToString(), "ms", "Alarm status update rate");
            dgvConfig.Rows.Add("Health Broadcast Interval", config.HealthStatusBroadcastInterval.ToString(), "ms", "Health status update rate");
            dgvConfig.Rows.Add("Auto Alarm Clear Timeout", config.AutoAlarmClearTimeout.ToString(), "sec", "Automatic alarm clear delay");
            dgvConfig.Rows.Add("ROR Window Duration", config.RateOfRiseWindowSeconds.ToString(), "sec", "Rate of Rise calculation window (30-120s recommended)");
            dgvConfig.Rows.Add("DTS Point Resolution", config.DtsPointResolutionMeters.ToString("F2"), "m", "Distance between measurement points (0.25, 0.4, 0.5, 1.0)");

            // Decimation settings (for live data streaming optimization)
            dgvConfig.Rows.Add("Decimation Medium Threshold", _decimationMediumKm.ToString("F1"), "km", "Channels >= this length use medium decimation");
            dgvConfig.Rows.Add("Decimation Large Threshold", _decimationLargeKm.ToString("F1"), "km", "Channels >= this length use large decimation");
            dgvConfig.Rows.Add("Decimation Medium Factor", _decimationMediumFactor.ToString(), "1:N", "Take 1 in N points for medium channels");
            dgvConfig.Rows.Add("Decimation Large Factor", _decimationLargeFactor.ToString(), "1:N", "Take 1 in N points for large channels");
        }

        private async void BtnSave_Click(object? sender, EventArgs e)
        {
            try
            {
                if (!string.Equals(_loginRole, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Access Denied. Only Admin users can modify system configuration.",
                        "Access Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (dgvConfig == null) return;

                // Validate and extract values
                if (!int.TryParse(dgvConfig.Rows[0].Cells["Value"].Value?.ToString(), out int relays) || relays < 1 || relays > 64)
                {
                    MessageBox.Show("Number of relays must be between 1 and 64", "Validation Error");
                    return;
                }

                if (!int.TryParse(dgvConfig.Rows[1].Cells["Value"].Value?.ToString(), out int zones) || zones < 1 || zones > 1000)
                {
                    MessageBox.Show("Max zones per channel must be between 1 and 1000", "Validation Error");
                    return;
                }

                if (!int.TryParse(dgvConfig.Rows[2].Cells["Value"].Value?.ToString(), out int polling) || polling < 100)
                {
                    MessageBox.Show("DTS polling interval must be at least 100 ms", "Validation Error");
                    return;
                }

                if (!int.TryParse(dgvConfig.Rows[3].Cells["Value"].Value?.ToString(), out int alarmEval) || alarmEval < 100)
                {
                    MessageBox.Show("Alarm evaluation interval must be at least 100 ms", "Validation Error");
                    return;
                }

                if (!int.TryParse(dgvConfig.Rows[4].Cells["Value"].Value?.ToString(), out int alarmBroadcast) || alarmBroadcast < 100)
                {
                    MessageBox.Show("Alarm broadcast interval must be at least 100 ms", "Validation Error");
                    return;
                }

                if (!int.TryParse(dgvConfig.Rows[5].Cells["Value"].Value?.ToString(), out int healthBroadcast) || healthBroadcast < 100)
                {
                    MessageBox.Show("Health broadcast interval must be at least 100 ms", "Validation Error");
                    return;
                }

                if (!int.TryParse(dgvConfig.Rows[6].Cells["Value"].Value?.ToString(), out int timeout) || timeout < 0)
                {
                    MessageBox.Show("Auto alarm clear timeout must be 0 or greater", "Validation Error");
                    return;
                }

                if (!int.TryParse(dgvConfig.Rows[7].Cells["Value"].Value?.ToString(), out int rorWindow) || rorWindow < 10 || rorWindow > 300)
                {
                    MessageBox.Show("ROR Window Duration must be between 10 and 300 seconds.\n\nRecommended: 30-120 seconds\n- Shorter (30s) = faster detection, more noise\n- Longer (90-120s) = more stable, slower detection",
                        "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Validate DTS Point Resolution
                if (!double.TryParse(dgvConfig.Rows[8].Cells["Value"].Value?.ToString(), out double dtsPointResolution) || dtsPointResolution < 0.1 || dtsPointResolution > 2.0)
                {
                    MessageBox.Show("DTS Point Resolution must be between 0.1 and 2.0 meters.\n\nCommon values: 0.25m, 0.4m, 0.5m, 1.0m\n\nThis must match the DTS device configuration.",
                        "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Validate decimation settings
                if (!double.TryParse(dgvConfig.Rows[9].Cells["Value"].Value?.ToString(), out double decMediumKm) || decMediumKm < 0.5 || decMediumKm > 20)
                {
                    MessageBox.Show("Decimation Medium Threshold must be between 0.5 and 20 km", "Validation Error");
                    return;
                }

                if (!double.TryParse(dgvConfig.Rows[10].Cells["Value"].Value?.ToString(), out double decLargeKm) || decLargeKm < 1 || decLargeKm > 30)
                {
                    MessageBox.Show("Decimation Large Threshold must be between 1 and 30 km", "Validation Error");
                    return;
                }

                if (decLargeKm <= decMediumKm)
                {
                    MessageBox.Show("Large Threshold must be greater than Medium Threshold", "Validation Error");
                    return;
                }

                if (!int.TryParse(dgvConfig.Rows[11].Cells["Value"].Value?.ToString(), out int decMediumFactor) || decMediumFactor < 1 || decMediumFactor > 20)
                {
                    MessageBox.Show("Decimation Medium Factor must be between 1 and 20\n(1 = no decimation, 5 = take 1 in 5 points)", "Validation Error");
                    return;
                }

                if (!int.TryParse(dgvConfig.Rows[12].Cells["Value"].Value?.ToString(), out int decLargeFactor) || decLargeFactor < 1 || decLargeFactor > 50)
                {
                    MessageBox.Show("Decimation Large Factor must be between 1 and 50\n(1 = no decimation, 10 = take 1 in 10 points)", "Validation Error");
                    return;
                }

                var config = new SystemConfiguration
                {
                    NumberOfRelays = relays,
                    MaxZonesPerChannel = zones,
                    DtsPollingInterval = polling,
                    AlarmEvaluationInterval = alarmEval,
                    AlarmStatusBroadcastInterval = alarmBroadcast,
                    HealthStatusBroadcastInterval = healthBroadcast,
                    AutoAlarmClearTimeout = timeout,
                    RateOfRiseWindowSeconds = rorWindow,
                    DtsPointResolutionMeters = dtsPointResolution,
                    // Preserve health thresholds and log settings
                    CpuLoadThreshold = _originalConfig?.CpuLoadThreshold ?? 90.0,
                    MemoryLoadThreshold = _originalConfig?.MemoryLoadThreshold ?? 90.0,
                    DtsModuleTempHighThreshold = _originalConfig?.DtsModuleTempHighThreshold ?? 70.0,
                    DtsModuleTempLowThreshold = _originalConfig?.DtsModuleTempLowThreshold ?? -10.0,
                    DtsCommunicationTimeoutSeconds = _originalConfig?.DtsCommunicationTimeoutSeconds ?? 10,
                    HealthMonitoringIntervalSeconds = _originalConfig?.HealthMonitoringIntervalSeconds ?? 120,
                    MinimumLogLevel = _originalConfig?.MinimumLogLevel ?? "Information",
                    LogRetentionEnabled = _originalConfig?.LogRetentionEnabled ?? true,
                    LogTotalQuotaMB = _originalConfig?.LogTotalQuotaMB ?? 10240,
                    LogHighWatermarkMB = _originalConfig?.LogHighWatermarkMB ?? 9216,
                    LogLowWatermarkMB = _originalConfig?.LogLowWatermarkMB ?? 7680,
                    LogCheckIntervalHours = _originalConfig?.LogCheckIntervalHours ?? 12
                };

                var (success, errorMessage) = await _channelClient.SetSystemConfigAsync(config);

                // Save decimation config separately
                bool decimationSuccess = await _channelClient.SetDecimationConfigAsync(decMediumKm, decLargeKm, decMediumFactor, decLargeFactor);

                if (success)
                {
                    // Check if relay count changed - this is the ONLY parameter that requires restart
                    bool relayCountChanged = false;
                    try
                    {
                        var currentConfig = await _channelClient.GetSystemConfigAsync();
                        relayCountChanged = currentConfig != null && currentConfig.NumberOfRelays != relays;
                    }
                    catch { }

                    string decimationNote = decimationSuccess
                        ? "• Decimation settings applied immediately"
                        : "• Warning: Decimation settings failed to save";

                    if (relayCountChanged)
                    {
                        MessageBox.Show("✅ System configuration saved successfully.\n\n" +
                            "⚠️ FRMC RESTART REQUIRED:\n" +
                            "Number of Relays was changed. FRMC must be restarted for this change to take effect.\n\n" +
                            "Other changes (intervals, timeouts, ROR window) are applied immediately.\n" +
                            decimationNote,
                            "Success - Restart Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    else
                    {
                        MessageBox.Show("✅ System configuration saved successfully.\n\n" +
                            "All changes applied immediately - no restart required.\n\n" +
                            "• Broadcast intervals updated in real-time\n" +
                            "• ROR window duration active on next calculation\n" +
                            "• Timeout settings effective immediately\n" +
                            decimationNote,
                            "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }

                    this.DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    string message = string.IsNullOrEmpty(errorMessage)
                        ? "Failed to save system configuration."
                        : $"Failed to save system configuration.\n\n{errorMessage}";

                    MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving configuration: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnCancel_Click(object? sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
