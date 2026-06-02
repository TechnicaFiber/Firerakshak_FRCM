using System;
using System.Drawing;
using System.Windows.Forms;
using FRCM.Models;
using FRCM.Services;

namespace FRCM.View
{
    /// <summary>
    /// Form for configuring system health thresholds.
    /// Changes are persisted and trigger audit trail logging.
    /// </summary>
    public partial class FormHealthThresholds : Form
    {
        private readonly ChannelClient _channelClient;
        private readonly string _loginRole;

        // Input controls
        private NumericUpDown? nudCpuThreshold;
        private NumericUpDown? nudMemoryThreshold;
        private NumericUpDown? nudTempHighThreshold;
        private NumericUpDown? nudTempLowThreshold;
        private NumericUpDown? nudCommTimeout;
        private NumericUpDown? nudMonitoringInterval;

        // Buttons
        private Button? btnSave;
        private Button? btnCancel;
        private Button? btnRestoreDefaults;

        // Status
        private Label? lblStatus;

        // Original values for change detection
        private SystemConfiguration? _originalConfig;

        public FormHealthThresholds(ChannelClient channelClient, string loginRole = "User")
        {
            _channelClient = channelClient;
            _loginRole = loginRole ?? "User";
            InitializeComponent();
            BuildUI();

            this.Shown += async (s, e) => await LoadCurrentThresholds();
        }

        private void InitializeComponent()
        {
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.AutoScaleMode = AutoScaleMode.Dpi;

            float scalingFactor = this.DeviceDpi / 96f;

            this.Text = "Health Threshold Configuration";
            this.Size = new Size((int)(520 * scalingFactor), (int)(580 * scalingFactor));
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Padding = new Padding((int)(10 * scalingFactor));
        }

        private void BuildUI()
        {
            float scalingFactor = this.DeviceDpi / 96f;
            float fontScaling = FontSizeHelper.GetAutoScaling(this);

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding((int)(5 * scalingFactor))
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(150 * scalingFactor))); // System Resources
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(150 * scalingFactor))); // DTS Thresholds
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(90 * scalingFactor)));  // Monitoring
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(25 * scalingFactor)));  // Status
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(40 * scalingFactor)));  // Buttons

            int scaledButtonHeight = (int)(26 * scalingFactor);

            // ========== System Resources GroupBox ==========
            var grpSystemResources = new GroupBox
            {
                Text = "System Resource Thresholds",
                Dock = DockStyle.Fill,
                Padding = new Padding((int)(10 * scalingFactor)),
                Font = new Font("Segoe UI", 9 * fontScaling, FontStyle.Bold)
            };

            var resourcesTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                Padding = new Padding((int)(5 * scalingFactor))
            };
            resourcesTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            resourcesTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            resourcesTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));

            // CPU Threshold
            resourcesTable.Controls.Add(CreateLabel("CPU Usage Threshold:", false), 0, 0);
            nudCpuThreshold = CreateNumericUpDown(1, 100, 90, 1);
            resourcesTable.Controls.Add(nudCpuThreshold, 1, 0);
            resourcesTable.Controls.Add(CreateLabel("%", true), 2, 0);

            // Memory Threshold
            resourcesTable.Controls.Add(CreateLabel("Memory Usage Threshold:", false), 0, 1);
            nudMemoryThreshold = CreateNumericUpDown(1, 100, 90, 1);
            resourcesTable.Controls.Add(nudMemoryThreshold, 1, 1);
            resourcesTable.Controls.Add(CreateLabel("%", true), 2, 1);

            grpSystemResources.Controls.Add(resourcesTable);

            // ========== DTS Thresholds GroupBox ==========
            var grpDtsThresholds = new GroupBox
            {
                Text = "DTS Hardware Thresholds",
                Dock = DockStyle.Fill,
                Padding = new Padding((int)(10 * scalingFactor)),
                Font = new Font("Segoe UI", 9 * fontScaling, FontStyle.Bold)
            };

            var dtsTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 3,
                Padding = new Padding((int)(5 * scalingFactor))
            };
            dtsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            dtsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            dtsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));

            // Temp High
            dtsTable.Controls.Add(CreateLabel("Temperature High:", false), 0, 0);
            nudTempHighThreshold = CreateNumericUpDown(30, 100, 70, 1);
            dtsTable.Controls.Add(nudTempHighThreshold, 1, 0);
            dtsTable.Controls.Add(CreateLabel("°C", true), 2, 0);

            // Temp Low
            dtsTable.Controls.Add(CreateLabel("Temperature Low:", false), 0, 1);
            nudTempLowThreshold = CreateNumericUpDown(-50, 20, -10, 1);
            dtsTable.Controls.Add(nudTempLowThreshold, 1, 1);
            dtsTable.Controls.Add(CreateLabel("°C", true), 2, 1);

            // Comm Timeout
            dtsTable.Controls.Add(CreateLabel("Communication Timeout:", false), 0, 2);
            nudCommTimeout = CreateNumericUpDown(5, 120, 10, 1);
            dtsTable.Controls.Add(nudCommTimeout, 1, 2);
            dtsTable.Controls.Add(CreateLabel("sec", true), 2, 2);

            grpDtsThresholds.Controls.Add(dtsTable);

            // ========== Monitoring GroupBox ==========
            var grpMonitoring = new GroupBox
            {
                Text = "Monitoring Settings",
                Dock = DockStyle.Fill,
                Padding = new Padding((int)(10 * scalingFactor)),
                Font = new Font("Segoe UI", 9 * fontScaling, FontStyle.Bold)
            };

            var monitoringTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding((int)(5 * scalingFactor))
            };
            monitoringTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            monitoringTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            monitoringTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));

            monitoringTable.Controls.Add(CreateLabel("Health Check Interval:", false), 0, 0);
            nudMonitoringInterval = CreateNumericUpDown(10, 3600, 120, 10);
            monitoringTable.Controls.Add(nudMonitoringInterval, 1, 0);
            monitoringTable.Controls.Add(CreateLabel("sec", true), 2, 0);

            grpMonitoring.Controls.Add(monitoringTable);

            // ========== Status Label ==========
            lblStatus = new Label
            {
                Text = "",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 8 * fontScaling)
            };

            // ========== Button Panel ==========
            var buttonPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 1
            };
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 5));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 5));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

            btnRestoreDefaults = new Button
            {
                Text = "Defaults",
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Height = scaledButtonHeight,
                Margin = new Padding((int)(2 * scalingFactor)),
                Font = new Font("Segoe UI", 8 * fontScaling)
            };
            btnRestoreDefaults.Click += BtnRestoreDefaults_Click;

            btnSave = new Button
            {
                Text = "Save",
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Height = scaledButtonHeight,
                Margin = new Padding((int)(2 * scalingFactor)),
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8 * fontScaling, FontStyle.Bold)
            };
            btnSave.Click += async (s, e) => await SaveThresholds();

            btnCancel = new Button
            {
                Text = "Cancel",
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Height = scaledButtonHeight,
                Margin = new Padding((int)(2 * scalingFactor)),
                Font = new Font("Segoe UI", 8 * fontScaling)
            };
            btnCancel.Click += (s, e) => Close();

            buttonPanel.Controls.Add(btnRestoreDefaults, 0, 0);
            buttonPanel.Controls.Add(btnSave, 2, 0);
            buttonPanel.Controls.Add(btnCancel, 4, 0);

            // Add all to main layout
            mainLayout.Controls.Add(grpSystemResources, 0, 0);
            mainLayout.Controls.Add(grpDtsThresholds, 0, 1);
            mainLayout.Controls.Add(grpMonitoring, 0, 2);
            mainLayout.Controls.Add(lblStatus, 0, 3);
            mainLayout.Controls.Add(buttonPanel, 0, 4);

            this.Controls.Add(mainLayout);

            // Admin guard: disable editing for non-admin users
            if (!string.Equals(_loginRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                if (btnSave != null) btnSave.Enabled = false;
                if (btnRestoreDefaults != null) btnRestoreDefaults.Enabled = false;
                if (nudCpuThreshold != null) nudCpuThreshold.Enabled = false;
                if (nudMemoryThreshold != null) nudMemoryThreshold.Enabled = false;
                if (nudTempHighThreshold != null) nudTempHighThreshold.Enabled = false;
                if (nudTempLowThreshold != null) nudTempLowThreshold.Enabled = false;
                if (nudCommTimeout != null) nudCommTimeout.Enabled = false;
                if (nudMonitoringInterval != null) nudMonitoringInterval.Enabled = false;
            }
        }

        private Label CreateLabel(string text, bool isUnit)
        {
            float fontScaling = FontSizeHelper.GetAutoScaling(this);
            return new Label
            {
                Text = text,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = isUnit ? Color.Gray : Color.Black,
                Font = new Font("Segoe UI", 9 * fontScaling, FontStyle.Regular)
            };
        }

        private NumericUpDown CreateNumericUpDown(decimal min, decimal max, decimal value, decimal increment)
        {
            float fontScaling = FontSizeHelper.GetAutoScaling(this);
            float scalingFactor = this.DeviceDpi / 96f;
            return new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                Value = value,
                Increment = increment,
                DecimalPlaces = 0,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding((int)(3 * scalingFactor)),
                Font = new Font("Segoe UI", 9 * fontScaling)
            };
        }

        private async System.Threading.Tasks.Task LoadCurrentThresholds()
        {
            try
            {
                UpdateStatus("Loading current thresholds...", Color.Gray);

                var config = await _channelClient.GetSystemConfigAsync();

                if (config == null)
                {
                    UpdateStatus("Failed to load configuration", Color.Red);
                    return;
                }

                _originalConfig = config;

                // Populate controls
                if (nudCpuThreshold != null) nudCpuThreshold.Value = (decimal)config.CpuLoadThreshold;
                if (nudMemoryThreshold != null) nudMemoryThreshold.Value = (decimal)config.MemoryLoadThreshold;
                if (nudTempHighThreshold != null) nudTempHighThreshold.Value = (decimal)config.DtsModuleTempHighThreshold;
                if (nudTempLowThreshold != null) nudTempLowThreshold.Value = (decimal)config.DtsModuleTempLowThreshold;
                if (nudCommTimeout != null) nudCommTimeout.Value = config.DtsCommunicationTimeoutSeconds;
                if (nudMonitoringInterval != null) nudMonitoringInterval.Value = config.HealthMonitoringIntervalSeconds;

                UpdateStatus("Ready", Color.Green);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error: {ex.Message}", Color.Red);
            }
        }

        private async System.Threading.Tasks.Task SaveThresholds()
        {
            try
            {
                if (!string.Equals(_loginRole, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Access Denied. Only Admin users can modify health thresholds.",
                        "Access Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (_originalConfig == null)
                {
                    UpdateStatus("No configuration loaded", Color.Red);
                    return;
                }

                UpdateStatus("Saving thresholds...", Color.Gray);

                // Update config with new values
                _originalConfig.CpuLoadThreshold = (double)(nudCpuThreshold?.Value ?? 90);
                _originalConfig.MemoryLoadThreshold = (double)(nudMemoryThreshold?.Value ?? 90);
                _originalConfig.DtsModuleTempHighThreshold = (double)(nudTempHighThreshold?.Value ?? 70);
                _originalConfig.DtsModuleTempLowThreshold = (double)(nudTempLowThreshold?.Value ?? -10);
                _originalConfig.DtsCommunicationTimeoutSeconds = (int)(nudCommTimeout?.Value ?? 10);
                _originalConfig.HealthMonitoringIntervalSeconds = (int)(nudMonitoringInterval?.Value ?? 120);

                // Send to FRMC
                var (success, errorMessage) = await _channelClient.SetSystemConfigAsync(_originalConfig);

                if (success)
                {
                    UpdateStatus("Saved successfully!", Color.Green);
                    MessageBox.Show(
                        "Health thresholds updated successfully.\n\nChanges logged to audit trail.",
                        "Success",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    UpdateStatus($"Failed: {errorMessage}", Color.Red);
                    MessageBox.Show(
                        $"Failed to save: {errorMessage}",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error: {ex.Message}", Color.Red);
            }
        }

        private void BtnRestoreDefaults_Click(object? sender, EventArgs e)
        {
            if (nudCpuThreshold != null) nudCpuThreshold.Value = 90;
            if (nudMemoryThreshold != null) nudMemoryThreshold.Value = 90;
            if (nudTempHighThreshold != null) nudTempHighThreshold.Value = 70;
            if (nudTempLowThreshold != null) nudTempLowThreshold.Value = -10;
            if (nudCommTimeout != null) nudCommTimeout.Value = 10;
            if (nudMonitoringInterval != null) nudMonitoringInterval.Value = 120;

            UpdateStatus("Defaults restored. Click Save to apply.", Color.Orange);
        }

        private void UpdateStatus(string message, Color color)
        {
            if (lblStatus != null)
            {
                lblStatus.Text = message;
                lblStatus.ForeColor = color;
            }
        }
    }
}
