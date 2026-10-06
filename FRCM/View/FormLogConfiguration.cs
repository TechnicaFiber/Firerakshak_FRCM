using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using FRCM.Models;
using FRCM.Services;

namespace FRCM.View
{
    /// <summary>
    /// Dedicated form for configuring log retention, disk quota management, and archiving intervals.
    /// Changes are persisted to FRMC, updated in appsettings.json, and logged to the audit trail.
    /// </summary>
    public partial class FormLogConfiguration : Form
    {
        private readonly ChannelClient _channelClient;
        private readonly string _loginRole;

        // Input controls
        private CheckBox? chkRetentionEnabled;
        private NumericUpDown? nudTotalQuota;
        private NumericUpDown? nudHighWatermark;
        private NumericUpDown? nudLowWatermark;
        private NumericUpDown? nudCheckInterval;

        // Buttons
        private Button? btnSave;
        private Button? btnCancel;
        private Button? btnRestoreDefaults;

        // Status
        private Label? lblStatus;

        // Original configuration for change tracking
        private SystemConfiguration? _originalConfig;

        public FormLogConfiguration(ChannelClient channelClient, string loginRole = "User")
        {
            _channelClient = channelClient;
            _loginRole = loginRole ?? "User";
            InitializeComponent();
            BuildUI();

            this.Shown += async (s, e) => await LoadCurrentLogConfiguration();
        }

        private void InitializeComponent()
        {
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.AutoScaleMode = AutoScaleMode.Dpi;

            float scalingFactor = this.DeviceDpi / 96f;

            this.Text = "Log Retention Configuration";
            this.Size = new Size((int)(560 * scalingFactor), (int)(420 * scalingFactor));
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Padding = new Padding((int)(12 * scalingFactor));
        }

        private void BuildUI()
        {
            float scalingFactor = this.DeviceDpi / 96f;
            float fontScaling = FontSizeHelper.GetAutoScaling(this);

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(0)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));                         // GroupBox
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(28 * scalingFactor)));  // Status Label
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(42 * scalingFactor)));  // Buttons

            int scaledButtonHeight = (int)(34 * scalingFactor);

            // ==================== Log Retention & Storage GroupBox ====================
            var grpRetention = new GroupBox
            {
                Text = "Log Retention & Storage Management",
                Dock = DockStyle.Fill,
                Padding = new Padding((int)(12 * scalingFactor)),
                Font = new Font("Segoe UI", 9.5F * fontScaling, FontStyle.Bold)
            };

            var retentionTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 5,
                Padding = new Padding((int)(4 * scalingFactor))
            };
            retentionTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));
            retentionTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            retentionTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));

            for (int i = 0; i < 5; i++)
            {
                retentionTable.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
            }

            // 1. Retention Enabled
            retentionTable.Controls.Add(CreateLabel("Automated Archiving & Retention:", false), 0, 0);
            chkRetentionEnabled = new CheckBox
            {
                Text = "Enabled",
                Checked = true,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F * fontScaling, FontStyle.Regular),
                ForeColor = Color.Black
            };
            chkRetentionEnabled.CheckedChanged += (s, e) => UpdateRetentionControlStates();
            retentionTable.Controls.Add(chkRetentionEnabled, 1, 0);
            retentionTable.Controls.Add(new Label(), 2, 0);

            // 2. Total Quota
            retentionTable.Controls.Add(CreateLabel("Total Storage Quota:", false), 0, 1);
            nudTotalQuota = CreateNumericUpDown(100, 1048576, 10240, 100);
            retentionTable.Controls.Add(nudTotalQuota, 1, 1);
            retentionTable.Controls.Add(CreateLabel("MB", true), 2, 1);

            // 3. High Watermark
            retentionTable.Controls.Add(CreateLabel("High Watermark (Pruning Trigger):", false), 0, 2);
            nudHighWatermark = CreateNumericUpDown(50, 1048576, 9216, 100);
            retentionTable.Controls.Add(nudHighWatermark, 1, 2);
            retentionTable.Controls.Add(CreateLabel("MB", true), 2, 2);

            // 4. Low Watermark
            retentionTable.Controls.Add(CreateLabel("Low Watermark (Pruning Target):", false), 0, 3);
            nudLowWatermark = CreateNumericUpDown(20, 1048576, 7680, 100);
            retentionTable.Controls.Add(nudLowWatermark, 1, 3);
            retentionTable.Controls.Add(CreateLabel("MB", true), 2, 3);

            // 5. Check Interval
            retentionTable.Controls.Add(CreateLabel("Archiving Check Interval:", false), 0, 4);
            nudCheckInterval = CreateNumericUpDown(1, 72, 12, 1);
            retentionTable.Controls.Add(nudCheckInterval, 1, 4);
            retentionTable.Controls.Add(CreateLabel("hours", true), 2, 4);

            grpRetention.Controls.Add(retentionTable);

            // ==================== Status Label ====================
            lblStatus = new Label
            {
                Text = "Loading configuration...",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8.5F * fontScaling, FontStyle.Italic),
                ForeColor = Color.Gray,
                TextAlign = ContentAlignment.MiddleLeft
            };

            // ==================== Buttons Layout ====================
            var buttonLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 1,
                Margin = new Padding(0)
            };
            buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F)); // Defaults
            buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F)); // Spacer
            buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F)); // Save
            buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, (int)(10 * scalingFactor))); // Gap
            buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F)); // Cancel
            buttonLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            btnRestoreDefaults = new Button
            {
                Text = "Restore Defaults",
                Dock = DockStyle.Fill,
                Height = scaledButtonHeight,
                BackColor = SystemColors.Control,
                ForeColor = Color.Black,
                UseVisualStyleBackColor = true,
                Font = new Font("Segoe UI", 9F * fontScaling, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            btnRestoreDefaults.Click += BtnRestoreDefaults_Click;

            btnSave = new Button
            {
                Text = "Save",
                Dock = DockStyle.Fill,
                Height = scaledButtonHeight,
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F * fontScaling, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += async (s, e) => await SaveLogConfiguration();

            btnCancel = new Button
            {
                Text = "Cancel",
                Dock = DockStyle.Fill,
                Height = scaledButtonHeight,
                BackColor = SystemColors.Control,
                ForeColor = Color.Black,
                UseVisualStyleBackColor = true,
                Font = new Font("Segoe UI", 9F * fontScaling, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            btnCancel.Click += (s, e) =>
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };

            buttonLayout.Controls.Add(btnRestoreDefaults, 0, 0);
            buttonLayout.Controls.Add(new Panel(), 1, 0);
            buttonLayout.Controls.Add(btnSave, 2, 0);
            buttonLayout.Controls.Add(new Panel(), 3, 0);
            buttonLayout.Controls.Add(btnCancel, 4, 0);

            // Assemble main layout
            mainLayout.Controls.Add(grpRetention, 0, 0);
            mainLayout.Controls.Add(lblStatus, 0, 1);
            mainLayout.Controls.Add(buttonLayout, 0, 2);

            this.Controls.Add(mainLayout);

            // Apply Admin role restrictions
            ApplyRolePermissions();
        }

        private void UpdateRetentionControlStates()
        {
            bool enabled = chkRetentionEnabled?.Checked ?? true;
            if (nudTotalQuota != null) nudTotalQuota.Enabled = enabled;
            if (nudHighWatermark != null) nudHighWatermark.Enabled = enabled;
            if (nudLowWatermark != null) nudLowWatermark.Enabled = enabled;
            if (nudCheckInterval != null) nudCheckInterval.Enabled = enabled;
        }

        private void ApplyRolePermissions()
        {
            bool isAdmin = string.Equals(_loginRole, "Admin", StringComparison.OrdinalIgnoreCase);

            if (!isAdmin)
            {
                if (chkRetentionEnabled != null) chkRetentionEnabled.Enabled = false;
                if (nudTotalQuota != null) nudTotalQuota.Enabled = false;
                if (nudHighWatermark != null) nudHighWatermark.Enabled = false;
                if (nudLowWatermark != null) nudLowWatermark.Enabled = false;
                if (nudCheckInterval != null) nudCheckInterval.Enabled = false;
                if (btnSave != null) btnSave.Enabled = false;
                if (btnRestoreDefaults != null) btnRestoreDefaults.Enabled = false;

                UpdateStatus("Read-only mode (Admin role required to modify)", Color.DarkOrange);
            }
        }

        private Label CreateLabel(string text, bool isUnit)
        {
            float fontScaling = FontSizeHelper.GetAutoScaling(this);
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = isUnit ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", (isUnit ? 8.5F : 9F) * fontScaling, isUnit ? FontStyle.Italic : FontStyle.Regular),
                ForeColor = isUnit ? Color.Gray : Color.Black
            };
        }

        private NumericUpDown CreateNumericUpDown(decimal min, decimal max, decimal defaultValue, decimal increment)
        {
            float fontScaling = FontSizeHelper.GetAutoScaling(this);
            return new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                Value = defaultValue,
                Increment = increment,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F * fontScaling, FontStyle.Regular),
                TextAlign = HorizontalAlignment.Right
            };
        }

        private async Task LoadCurrentLogConfiguration()
        {
            try
            {
                UpdateStatus("Loading configuration from FRMC...", Color.Gray);

                _originalConfig = await _channelClient.GetSystemConfigAsync();

                if (_originalConfig != null)
                {
                    if (chkRetentionEnabled != null)
                        chkRetentionEnabled.Checked = _originalConfig.LogRetentionEnabled;

                    if (nudTotalQuota != null && _originalConfig.LogTotalQuotaMB > 0)
                        nudTotalQuota.Value = Math.Clamp(_originalConfig.LogTotalQuotaMB, nudTotalQuota.Minimum, nudTotalQuota.Maximum);

                    if (nudHighWatermark != null && _originalConfig.LogHighWatermarkMB > 0)
                        nudHighWatermark.Value = Math.Clamp(_originalConfig.LogHighWatermarkMB, nudHighWatermark.Minimum, nudHighWatermark.Maximum);

                    if (nudLowWatermark != null && _originalConfig.LogLowWatermarkMB > 0)
                        nudLowWatermark.Value = Math.Clamp(_originalConfig.LogLowWatermarkMB, nudLowWatermark.Minimum, nudLowWatermark.Maximum);

                    if (nudCheckInterval != null && _originalConfig.LogCheckIntervalHours > 0)
                        nudCheckInterval.Value = Math.Clamp(_originalConfig.LogCheckIntervalHours, nudCheckInterval.Minimum, nudCheckInterval.Maximum);

                    UpdateRetentionControlStates();
                    UpdateStatus("Configuration loaded successfully.", Color.Green);
                }
                else
                {
                    UpdateStatus("Could not load configuration from FRMC.", Color.Red);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error loading configuration: {ex.Message}", Color.Red);
            }
        }

        private async Task SaveLogConfiguration()
        {
            try
            {
                if (!string.Equals(_loginRole, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Access Denied. Only administrators can modify log configuration.",
                        "Access Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (_originalConfig == null)
                {
                    UpdateStatus("No configuration loaded.", Color.Red);
                    return;
                }

                // Validation
                decimal high = nudHighWatermark?.Value ?? 9216;
                decimal low = nudLowWatermark?.Value ?? 7680;
                decimal total = nudTotalQuota?.Value ?? 10240;

                if (high <= low)
                {
                    MessageBox.Show("Log High Watermark must be greater than Log Low Watermark.",
                        "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (total < high)
                {
                    MessageBox.Show("Total Storage Quota must be greater than or equal to High Watermark.",
                        "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                UpdateStatus("Saving log configuration to FRMC...", Color.Gray);

                _originalConfig.LogRetentionEnabled = chkRetentionEnabled?.Checked ?? true;
                _originalConfig.LogTotalQuotaMB = (long)total;
                _originalConfig.LogHighWatermarkMB = (long)high;
                _originalConfig.LogLowWatermarkMB = (long)low;
                _originalConfig.LogCheckIntervalHours = (int)(nudCheckInterval?.Value ?? 12);

                var (success, errorMessage) = await _channelClient.SetSystemConfigAsync(_originalConfig);

                if (success)
                {
                    UpdateStatus("Saved successfully!", Color.Green);
                    MessageBox.Show("✅ Log Retention configuration saved successfully.\n\n" +
                                    "• Retention thresholds updated and synced to appsettings.json.",
                                    "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    UpdateStatus($"Failed: {errorMessage}", Color.Red);
                    MessageBox.Show($"Failed to save log configuration:\n\n{errorMessage}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error saving configuration: {ex.Message}", Color.Red);
                MessageBox.Show($"Error saving configuration:\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnRestoreDefaults_Click(object? sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to restore default log retention values?\n\n" +
                "• Retention: Enabled\n" +
                "• Total Quota: 10240 MB (10 GB)\n" +
                "• High Watermark: 9216 MB (9 GB)\n" +
                "• Low Watermark: 7680 MB (7.5 GB)\n" +
                "• Check Interval: 12 hours",
                "Restore Defaults",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                if (chkRetentionEnabled != null) chkRetentionEnabled.Checked = true;
                if (nudTotalQuota != null) nudTotalQuota.Value = 10240;
                if (nudHighWatermark != null) nudHighWatermark.Value = 9216;
                if (nudLowWatermark != null) nudLowWatermark.Value = 7680;
                if (nudCheckInterval != null) nudCheckInterval.Value = 12;

                UpdateRetentionControlStates();
                UpdateStatus("Defaults restored. Click 'Save' to apply.", Color.Blue);
            }
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
