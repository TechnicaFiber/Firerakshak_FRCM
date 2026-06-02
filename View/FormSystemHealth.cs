using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;
using FRCM.Models;
using FRCM.Services;
using FRCM.View;

namespace FRCM
{
    public partial class FormSystemHealth : Form
    {
        private readonly ChannelClient _channelClient;
        private readonly CommandMessageProcessor? _commandProcessor;
        private readonly string _loginRole;
        private DataGridView? dgvHealthParams;
        private DataGridView? dgvActiveFaults;
        private Button? btnRefresh, btnClose, btnConfigureThresholds;
        private Label? lblOverallStatus;
        private Label? lblLastUpdate;
        private Panel? pnlOverallStatus;

        public FormSystemHealth(ChannelClient channelClient, CommandMessageProcessor? commandProcessor = null, string loginRole = "User")
        {
            _channelClient = channelClient;
            _commandProcessor = commandProcessor;
            _loginRole = loginRole ?? "User";

            this.WindowState = FormWindowState.Maximized;
            InitializeComponent();
            BuildUI();

            // Subscribe to real-time health fault events if processor is available
            if (_commandProcessor != null)
            {
                _commandProcessor.OnHealthFaultDetected += OnHealthFaultDetected;
                _commandProcessor.OnHealthFaultCleared += OnHealthFaultCleared;
            }

            // Initial refresh
            this.Shown += async (s, e) => await RefreshHealth();
        }

        /// <summary>
        /// Handles real-time health fault detected events from FRMC.
        /// </summary>
        private void OnHealthFaultDetected(JsonElement payload)
        {
            // Refresh the health display on the UI thread
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => _ = RefreshHealth()));
            }
            else
            {
                _ = RefreshHealth();
            }
        }

        /// <summary>
        /// Handles real-time health fault cleared events from FRMC.
        /// </summary>
        private void OnHealthFaultCleared(JsonElement payload)
        {
            // Refresh the health display on the UI thread
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => _ = RefreshHealth()));
            }
            else
            {
                _ = RefreshHealth();
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // Unsubscribe from events to prevent memory leaks
            if (_commandProcessor != null)
            {
                _commandProcessor.OnHealthFaultDetected -= OnHealthFaultDetected;
                _commandProcessor.OnHealthFaultCleared -= OnHealthFaultCleared;
            }
            base.OnFormClosed(e);
        }

        private void InitializeComponent()
        {
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.AutoScaleMode = AutoScaleMode.Dpi;

            float scalingFactor = this.DeviceDpi / 96f;

            this.Text = "System Health Monitor";
            this.Size = new Size((int)(850 * scalingFactor), (int)(650 * scalingFactor));
            this.StartPosition = FormStartPosition.CenterParent;
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimumSize = new Size((int)(650 * scalingFactor), (int)(500 * scalingFactor));
        }

        private void BuildUI()
        {
            // Calculate DPI-scaled sizes
            float scalingFactor = this.DeviceDpi / 96f;
            float fontScaling = FontSizeHelper.GetAutoScaling(this);
            int scaledRowHeight = (int)(30 * scalingFactor); // Scalable baseline for grid rows
            int scaledHeaderHeight = (int)(40 * scalingFactor);
            int scaledButtonHeight = (int)(22 * scalingFactor);
            int scaledButtonPanelHeight = (int)(40 * scalingFactor);
            int scaledStatusHeight = (int)(24 * scalingFactor);

            // Main layout TableLayoutPanel
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(10)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(50 * scalingFactor)));   // Overall Status
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));    // Health Parameters
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));    // Active Faults
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, scaledStatusHeight));   // Last Update + Auto Refresh
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, scaledButtonPanelHeight));   // Buttons

            // ========== Overall Status Panel ==========
            pnlOverallStatus = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 0, 10)
            };

            lblOverallStatus = new Label
            {
                Text = "System Health: Checking...",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 14 * fontScaling, FontStyle.Bold),
                ForeColor = Color.Gray
            };
            pnlOverallStatus.Controls.Add(lblOverallStatus);

            // ========== Health Parameters Section ==========
            var healthParamsPanel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 5) };

            var lblHealthParams = new Label
            {
                Text = "DTS Hardware Parameters",
                Dock = DockStyle.Top,
                Height = (int)(24 * scalingFactor),
                Font = new Font("Segoe UI", 10 * fontScaling, FontStyle.Bold),
                ForeColor = Color.Black
            };

            dgvHealthParams = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,

                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,

                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = SystemColors.Window,
                AllowUserToResizeRows = false,

                // Fix: Force header height to scale for 55-inch screens
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = scaledHeaderHeight,

                EditMode = DataGridViewEditMode.EditOnEnter,

                RowTemplate = { MinimumHeight = scaledRowHeight },
                BorderStyle = BorderStyle.FixedSingle
            };

            // Ensure consistent padding and font for large screens
            dgvHealthParams.Font = new Font("Segoe UI", 8F * fontScaling);
            dgvHealthParams.DefaultCellStyle.Font = dgvHealthParams.Font;
            dgvHealthParams.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F * fontScaling, FontStyle.Bold);
            dgvHealthParams.DefaultCellStyle.Padding = new Padding((int)(2 * scalingFactor));
            dgvHealthParams.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dgvHealthParams.RowsDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dgvHealthParams.EnableHeadersVisualStyles = false;
            dgvHealthParams.ColumnHeadersDefaultCellStyle.BackColor = Color.LightGray;
            dgvHealthParams.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.LightGray;
            dgvHealthParams.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.Black;
            dgvHealthParams.ColumnHeadersDefaultCellStyle.ForeColor = Color.Black;

            // Setup columns for health parameters
            dgvHealthParams.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Parameter",
                Name = "Parameter",
                FillWeight = 40
            });

            dgvHealthParams.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Value",
                Name = "Value",
                FillWeight = 30
            });

            dgvHealthParams.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Unit",
                Name = "Unit",
                FillWeight = 15
            });

            dgvHealthParams.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Status",
                Name = "Status",
                FillWeight = 15
            });

            healthParamsPanel.Controls.Add(dgvHealthParams);
            healthParamsPanel.Controls.Add(lblHealthParams);

            // ========== Active Faults Section ==========
            var faultsPanel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 5, 0, 5) };

            var lblFaults = new Label
            {
                Text = "Active System Health Faults (Controls Relay 2)",
                Dock = DockStyle.Top,
                Height = (int)(24 * scalingFactor),
                Font = new Font("Segoe UI", 10 * fontScaling, FontStyle.Bold),
                ForeColor = Color.DarkRed
            };

            dgvActiveFaults = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                
                // Fix: Force header height to scale for 55-inch screens
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = scaledHeaderHeight,

                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = SystemColors.Window,
                AllowUserToResizeRows = false,
                RowTemplate = { MinimumHeight = scaledRowHeight },
                BorderStyle = BorderStyle.FixedSingle
            };

            // Ensure consistent padding and font for large screens
            dgvActiveFaults.Font = new Font("Segoe UI", 8F * fontScaling);
            dgvActiveFaults.DefaultCellStyle.Font = dgvActiveFaults.Font;
            dgvActiveFaults.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F * fontScaling, FontStyle.Bold);
            dgvActiveFaults.DefaultCellStyle.Padding = new Padding((int)(2 * scalingFactor));
            dgvActiveFaults.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dgvActiveFaults.RowsDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dgvActiveFaults.EnableHeadersVisualStyles = false;
            dgvActiveFaults.ColumnHeadersDefaultCellStyle.BackColor = Color.LightGray;
            dgvActiveFaults.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.LightGray;
            dgvActiveFaults.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.Black;
            dgvActiveFaults.ColumnHeadersDefaultCellStyle.ForeColor = Color.Black;

            // Setup columns for active faults
            dgvActiveFaults.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Fault Type",
                Name = "FaultType",
                FillWeight = 25
            });

            dgvActiveFaults.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Description",
                Name = "Description",
                FillWeight = 35
            });

            dgvActiveFaults.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Current Value",
                Name = "CurrentValue",
                FillWeight = 15
            });

            dgvActiveFaults.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Threshold",
                Name = "Threshold",
                FillWeight = 15
            });

            dgvActiveFaults.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Since",
                Name = "Since",
                FillWeight = 10
            });

            faultsPanel.Controls.Add(dgvActiveFaults);
            faultsPanel.Controls.Add(lblFaults);

            // ========== Last Update Status ==========
            var statusPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 1
            };
            statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            lblLastUpdate = new Label
            {
                Text = "Click 'Refresh Now' to update",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 8 * fontScaling)
            };

            statusPanel.Controls.Add(lblLastUpdate, 0, 0);

            // ========== Button Layout ==========
            var buttonLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                Padding = new Padding(0, 5, 0, 0)
            };
            
            // Initial column styles
            buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 10));
            buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));

            mainLayout.Resize += (s, e) => {
                // When form is very wide (e.g. maximized), cap the button width but let it grow
                // We adjust the percentage vs absolute balance
                if (mainLayout.Width > 1200 * scalingFactor)
                {
                    // For very large screens, use a mix that keeps buttons large but not absurd
                    float sidePadding = (mainLayout.Width - (1000 * scalingFactor)) / 2;
                    buttonLayout.Padding = new Padding((int)sidePadding, 5, (int)sidePadding, 0);
                }
                else
                {
                    buttonLayout.Padding = new Padding(0, 5, 0, 0);
                }
            };

            btnConfigureThresholds = new Button
            {
                Text = "Configure Thresholds",
                Dock = DockStyle.Fill,
                Margin = new Padding(3),
                Height = scaledButtonHeight,
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9 * fontScaling)
            };
            btnConfigureThresholds.Click += BtnConfigureThresholds_Click;

            btnRefresh = new Button
            {
                Text = "Refresh Now",
                Dock = DockStyle.Fill,
                Margin = new Padding(3),
                Height = scaledButtonHeight,
                Font = new Font("Segoe UI", 9 * fontScaling)
            };
            btnRefresh.Click += async (s, e) => await RefreshHealth();

            btnClose = new Button
            {
                Text = "Close",
                Dock = DockStyle.Fill,
                Margin = new Padding(3),
                Height = scaledButtonHeight,
                Font = new Font("Segoe UI", 9 * fontScaling)
            };
            btnClose.Click += (s, e) => Close();

            buttonLayout.Controls.Add(btnConfigureThresholds, 0, 0);
            buttonLayout.Controls.Add(btnRefresh, 1, 0);
            buttonLayout.Controls.Add(btnClose, 3, 0);

            // Add all to main layout
            mainLayout.Controls.Add(pnlOverallStatus, 0, 0);
            mainLayout.Controls.Add(healthParamsPanel, 0, 1);
            mainLayout.Controls.Add(faultsPanel, 0, 2);
            mainLayout.Controls.Add(statusPanel, 0, 3);
            mainLayout.Controls.Add(buttonLayout, 0, 4);

            this.Controls.Add(mainLayout);

            // No auto-refresh - user must click Refresh button
        }

        private void BtnConfigureThresholds_Click(object? sender, EventArgs e)
        {
            using var form = new FormHealthThresholds(_channelClient, _loginRole);
            form.ShowDialog(this);

            // Refresh after closing configuration form to show any changes
            _ = RefreshHealth();
        }

        private async System.Threading.Tasks.Task RefreshHealth()
        {
            try
            {
                // Fetch health parameters and active faults in parallel
                var healthTask = _channelClient.GetHealthParametersAsync();
                var faultsTask = _channelClient.GetActiveHealthFaultsAsync();

                await System.Threading.Tasks.Task.WhenAll(healthTask, faultsTask);

                var health = healthTask.Result;
                var faults = faultsTask.Result;

                if (health == null)
                {
                    UpdateOverallStatus(false, "Unable to connect to FRMC");
                    return;
                }

                // Update Health Parameters grid
                UpdateHealthParametersGrid(health);

                // Update Active Faults grid
                UpdateActiveFaultsGrid(faults);

                // Update Overall Status
                bool hasActiveFaults = faults != null && faults.Count > 0;
                if (hasActiveFaults)
                {
                    UpdateOverallStatus(false, $"FAULT - {faults!.Count} active issue(s) detected");
                }
                else
                {
                    UpdateOverallStatus(true, "OK - All systems operational");
                }

                // Update last refresh time
                if (lblLastUpdate != null)
                {
                    lblLastUpdate.Text = $"Last Update: {DateTime.Now:HH:mm:ss}";
                }
            }
            catch (Exception ex)
            {
                UpdateOverallStatus(false, $"Error: {ex.Message}");
            }
        }

        private void UpdateOverallStatus(bool isHealthy, string message)
        {
            if (lblOverallStatus == null || pnlOverallStatus == null) return;

            lblOverallStatus.Text = $"System Health: {message}";

            if (isHealthy)
            {
                lblOverallStatus.ForeColor = Color.White;
                pnlOverallStatus.BackColor = Color.Green;
            }
            else
            {
                lblOverallStatus.ForeColor = Color.White;
                pnlOverallStatus.BackColor = Color.Red;
            }
        }

        private void UpdateHealthParametersGrid(HealthParametersInfo health)
        {
            if (dgvHealthParams == null) return;

            dgvHealthParams.Rows.Clear();

            // === DTS Hardware Parameters ===

            // Lamp Status
            AddHealthRow("Lamp Status",
                health.LampStatus ? "ON" : "OFF",
                "-",
                health.LampStatus ? "OK" : "FAULT",
                health.LampStatus);

            // DTS Communication
            AddHealthRow("DTS Communication",
                health.DtsResponsive ? "Connected" : "Disconnected",
                "-",
                health.DtsResponsive ? "OK" : "FAULT",
                health.DtsResponsive);

            // Module Temperature (threshold: 70°C high, warning at 60°C)
            bool tempOK = health.ModuleTemperature < 60;
            bool tempWarning = health.ModuleTemperature >= 60 && health.ModuleTemperature < 70;
            string tempStatus = tempOK ? "OK" : (tempWarning ? "WARNING" : "FAULT");
            AddHealthRow("Module Temperature",
                $"{health.ModuleTemperature:F1}",
                "°C",
                tempStatus,
                tempOK,
                tempWarning);

            // Laser Current
            bool laserOK = health.LaserCurrent > 0;
            AddHealthRow("Laser Current",
                $"{health.LaserCurrent:F2}",
                "mA",
                laserOK ? "OK" : "OFF",
                laserOK);

            // Pump Current
            bool pumpOK = health.PumpCurrent > 0;
            AddHealthRow("Pump Current",
                $"{health.PumpCurrent:F2}",
                "mA",
                pumpOK ? "OK" : "OFF",
                pumpOK);

            // APD Value
            AddHealthRow("APD Value",
                $"{health.ApdValue:F2}",
                "mV",
                "-",
                true);

            // === System Resource Parameters ===

            // CPU Usage (threshold: 90%)
            bool cpuOK = health.CpuUsage < 80;
            bool cpuWarning = health.CpuUsage >= 80 && health.CpuUsage < 90;
            string cpuStatus = cpuOK ? "OK" : (cpuWarning ? "WARNING" : "HIGH");
            AddHealthRow("CPU Usage",
                $"{health.CpuUsage:F1}",
                "%",
                cpuStatus,
                cpuOK,
                cpuWarning);

            // Memory Usage (threshold: 90%)
            bool memOK = health.MemoryUsage < 80;
            bool memWarning = health.MemoryUsage >= 80 && health.MemoryUsage < 90;
            string memStatus = memOK ? "OK" : (memWarning ? "WARNING" : "HIGH");
            string memValue = health.MemoryTotalGB > 0
                ? $"{health.MemoryUsage:F1} ({health.MemoryUsedGB:F2}/{health.MemoryTotalGB:F1} GB)"
                : $"{health.MemoryUsage:F1}";
            AddHealthRow("Memory Usage",
                memValue,
                "%",
                memStatus,
                memOK,
                memWarning);

            // Last DTS Check
            AddHealthRow("Last DTS Check",
                health.LastCheck != DateTime.MinValue ? health.LastCheck.ToString("HH:mm:ss") : "Never",
                "-",
                "-",
                true);
        }

        private void AddHealthRow(string parameter, string value, string unit, string status, bool isOK, bool isWarning = false)
        {
            if (dgvHealthParams == null) return;

            int row = dgvHealthParams.Rows.Add(parameter, value, unit, status);

            // Color coding
            Color foreColor;
            if (isWarning)
            {
                foreColor = Color.Orange;
            }
            else if (isOK)
            {
                foreColor = Color.Green;
            }
            else
            {
                foreColor = Color.Red;
            }

            dgvHealthParams.Rows[row].Cells["Status"].Style.ForeColor = foreColor;
            dgvHealthParams.Rows[row].Cells["Status"].Style.Font = new Font(dgvHealthParams.Font, FontStyle.Bold);
        }

        private void UpdateActiveFaultsGrid(List<SystemHealthFaultInfo>? faults)
        {
            if (dgvActiveFaults == null) return;

            dgvActiveFaults.Rows.Clear();

            if (faults == null || faults.Count == 0)
            {
                // Show "No active faults" message
                int row = dgvActiveFaults.Rows.Add("None", "No active system health faults", "-", "-", "-");
                dgvActiveFaults.Rows[row].DefaultCellStyle.ForeColor = Color.Green;
                dgvActiveFaults.Rows[row].DefaultCellStyle.Font = new Font(dgvActiveFaults.Font, FontStyle.Italic);
                return;
            }

            foreach (var fault in faults)
            {
                string currentValue = fault.CurrentValue != 0 ? $"{fault.CurrentValue:F1}" : "-";
                string threshold = fault.ThresholdValue != 0 ? $"{fault.ThresholdValue:F1}" : "-";
                string since = fault.FirstDetectedAt != DateTime.MinValue
                    ? fault.FirstDetectedAt.ToLocalTime().ToString("HH:mm:ss")
                    : "-";

                int row = dgvActiveFaults.Rows.Add(
                    fault.FaultTypeName,
                    fault.Description ?? GetDefaultDescription(fault.FaultType),
                    currentValue,
                    threshold,
                    since
                );

                // All faults are red
                dgvActiveFaults.Rows[row].DefaultCellStyle.ForeColor = Color.Red;
                dgvActiveFaults.Rows[row].DefaultCellStyle.Font = new Font(dgvActiveFaults.Font, FontStyle.Bold);
            }
        }


        private string GetDefaultDescription(int faultType)
        {
            return faultType switch
            {
                1 => "DTS device not responding",
                2 => "DTS module temperature too high",
                3 => "DTS module temperature too low",
                4 => "CPU utilization exceeds threshold",
                5 => "Memory usage exceeds threshold",
                6 => "Disk space critically low",
                7 => "Laser/pump is off",
                99 => "General system fault",
                _ => "Unknown fault"
            };
        }

    }
}
