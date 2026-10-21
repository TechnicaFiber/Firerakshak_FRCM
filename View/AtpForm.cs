using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using FRCM.Models;
using FRCM.Services;

namespace FRCM.View
{
    public partial class AtpForm : Form
    {
        private DataGridView dgvAtpTable = null!;
        private Button btnRunAll = null!;
        private Button btnTogglePeriodic = null!;
        private Button btnClear = null!;
        private Button btnExport = null!;
        private Button btnClose = null!;
        private ComboBox cboMcuPort = null!;
        private ComboBox cboMcuBaud = null!;
        private Button btnConnectMcu = null!;
        private Label lblMcuStatus = null!;
        private double _tolerance = 2.0;
        private bool _isPeriodicRunning = false;

        private readonly IConfigurationService? _configurationService;
        private readonly ITemperatureDataService? _temperatureDataService;
        private readonly ChannelClient? _channelClient;
        private readonly ISensorCommunicationService _sensorCommService;

        private static readonly Color DisabledCellBg = Color.FromArgb(245, 246, 248);
        private static readonly Color DisabledCellFg = Color.FromArgb(160, 165, 175);
        private static readonly Color PassColor = Color.FromArgb(40, 167, 69);
        private static readonly Color FailColor = Color.FromArgb(220, 53, 69);

        public AtpForm(
            IConfigurationService? configurationService = null,
            ITemperatureDataService? temperatureDataService = null,
            ChannelClient? channelClient = null,
            ISensorCommunicationService? sensorCommService = null)
        {
            _configurationService = configurationService;
            _temperatureDataService = temperatureDataService;
            _channelClient = channelClient;
            _sensorCommService = sensorCommService ?? new SensorCommunicationService(enableSimulation: false);

            InitializeComponent();
            InitializeLayout();
            WireUpEvents();

            this.FormClosing += async (s, e) =>
            {
                if (_isPeriodicRunning)
                {
                    await _sensorCommService.StopPeriodicAsync();
                }
                await _sensorCommService.DisconnectAsync();
            };

            this.Load += (s, e) =>
            {
                FontSizeHelper.UpdateControlRecursive(this, FontSizeHelper.CurrentMultiplier);
                InitializeFixedPortRows();
            };
        }

        private void InitializeComponent()
        {
            this.Text = "ATP – Acceptance Test Procedure";
            this.Size = new Size(1250, 620);
            this.MinimumSize = new Size(950, 440);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(248, 249, 250);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        }

        private List<string> GetConfiguredChannels()
        {
            var list = new List<string>();
            if (_configurationService != null)
            {
                var chDict = _configurationService.GetChannels();
                if (chDict != null && chDict.Count > 0)
                {
                    // Filter for only enabled channels
                    var enabledChannels = chDict
                        .Where(c => c.Value != null && c.Value.IsEnabled)
                        .OrderBy(c => c.Value.ChannelId)
                        .ToList();

                    // If enabled channels exist, use them; otherwise fallback to all configured channels
                    var source = enabledChannels.Count > 0 
                        ? enabledChannels 
                        : chDict.OrderBy(c => c.Value?.ChannelId ?? 0).ToList();

                    foreach (var kvp in source)
                    {
                        list.Add(kvp.Key);
                    }
                }
            }

            if (list.Count == 0)
            {
                for (int i = 1; i <= 8; i++) list.Add($"Channel {i}");
            }
            return list;
        }

        private List<string> GetZonesForChannel(string channelKey)
        {
            var list = new List<string>();
            if (_configurationService != null)
            {
                var zoneDict = _configurationService.GetZones();
                if (zoneDict != null && zoneDict.TryGetValue(channelKey, out var zones) && zones != null && zones.Count > 0)
                {
                    // Filter for enabled zones for this channel
                    var enabledZones = zones.Where(z => z.Enabled).ToList();
                    var source = enabledZones.Count > 0 ? enabledZones : zones;

                    foreach (var z in source)
                    {
                        string name = !string.IsNullOrWhiteSpace(z.Name) ? z.Name : $"Zone {z.ZoneId}";
                        list.Add(name);
                    }
                }
            }

            if (list.Count == 0)
            {
                for (int z = 1; z <= 10; z++) list.Add($"Zone {z}");
            }
            return list;
        }

        private List<string> GetAllConfiguredZones()
        {
            var set = new HashSet<string>();
            if (_configurationService != null)
            {
                var zoneDict = _configurationService.GetZones();
                var chDict = _configurationService.GetChannels();

                if (zoneDict != null)
                {
                    foreach (var kvp in zoneDict)
                    {
                        // Only include zones from enabled channels if channel configs exist
                        if (chDict != null && chDict.TryGetValue(kvp.Key, out var chCfg) && !chCfg.IsEnabled)
                        {
                            continue;
                        }

                        if (kvp.Value != null)
                        {
                            var enabledZones = kvp.Value.Where(z => z.Enabled).ToList();
                            var source = enabledZones.Count > 0 ? enabledZones : kvp.Value;

                            foreach (var z in source)
                            {
                                string name = !string.IsNullOrWhiteSpace(z.Name) ? z.Name : $"Zone {z.ZoneId}";
                                set.Add(name);
                            }
                        }
                    }
                }
            }

            if (set.Count == 0)
            {
                for (int z = 1; z <= 10; z++) set.Add($"Zone {z}");
            }
            return set.ToList();
        }

        private void InitializeLayout()
        {
            var mainContainer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(16, 18, 16, 14),
                BackColor = Color.FromArgb(248, 249, 250)
            };
            mainContainer.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // Header Section
            mainContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));     // Center DataGridView Table
            mainContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));     // Bottom Action Buttons

            // 1. Header Section
            var pnlHeader = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 14),
                WrapContents = false
            };

            var lblTitle = new Label
            {
                Text = "Test Records",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 3)
            };

            var lblSubtitle = new Label
            {
                Text = "Configure the Port, Channel, Zone, and Distance Point. Click 'Run Test' to obtain temperature readings and evaluate status.",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(108, 117, 125),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8)
            };

            // Microcontroller Port Connection Toolbar
            var pnlConnectBar = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 2, 0, 0)
            };

            var lblMcuPort = new Label
            {
                Text = "MCU Port:",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                Margin = new Padding(0, 6, 6, 0)
            };

            cboMcuPort = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 100,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Margin = new Padding(0, 2, 12, 0)
            };
            RefreshMcuPorts();
            cboMcuPort.DropDown += (s, e) => RefreshMcuPorts();

            var lblBaud = new Label
            {
                Text = "Baud:",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                Margin = new Padding(0, 6, 6, 0)
            };

            cboMcuBaud = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 95,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Margin = new Padding(0, 2, 12, 0)
            };
            cboMcuBaud.Items.AddRange(new object[] { "9600", "19200", "38400", "57600", "115200" });
            cboMcuBaud.SelectedItem = "115200";

            btnConnectMcu = new Button
            {
                Text = "Connect",
                Width = 90,
                Height = 27,
                BackColor = Color.FromArgb(0, 122, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 1, 14, 0),
                Cursor = Cursors.Hand
            };
            btnConnectMcu.FlatAppearance.BorderSize = 0;

            lblMcuStatus = new Label
            {
                Text = "🔴 Disconnected",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(220, 53, 69),
                Margin = new Padding(0, 6, 0, 0)
            };

            pnlConnectBar.Controls.Add(lblMcuPort);
            pnlConnectBar.Controls.Add(cboMcuPort);
            pnlConnectBar.Controls.Add(lblBaud);
            pnlConnectBar.Controls.Add(cboMcuBaud);
            pnlConnectBar.Controls.Add(btnConnectMcu);
            pnlConnectBar.Controls.Add(lblMcuStatus);

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);
            pnlHeader.Controls.Add(pnlConnectBar);
            mainContainer.Controls.Add(pnlHeader, 0, 0);

            // 2. Tabular Column DataGridView
            dgvAtpTable = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.RowHeaderSelect,
                MultiSelect = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                GridColor = Color.FromArgb(222, 226, 230),
                RowHeadersVisible = true,
                RowHeadersWidth = 48,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(33, 37, 41),
                EnableHeadersVisualStyles = false,
                Margin = new Padding(0, 0, 0, 12)
            };

            dgvAtpTable.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 243, 245);
            dgvAtpTable.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(33, 37, 41);
            dgvAtpTable.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            dgvAtpTable.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvAtpTable.ColumnHeadersHeight = 36;
            dgvAtpTable.RowTemplate.Height = 32;
            dgvAtpTable.TopLeftHeaderCell.Value = "#";

            // 1. Port Column (Fixed Port 1, Port 2, Port 3)
            var colPort = new DataGridViewTextBoxColumn
            {
                Name = "colPort",
                HeaderText = "Port",
                FillWeight = 60,
                ReadOnly = true
            };

            // 2. Channel ComboBox Column
            var colChannelId = new DataGridViewComboBoxColumn
            {
                Name = "colChannelId",
                HeaderText = "Channel",
                FillWeight = 60,
                FlatStyle = FlatStyle.Flat,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox
            };
            var configuredChannels = GetConfiguredChannels();
            foreach (var ch in configuredChannels) colChannelId.Items.Add(ch);

            // 3. Zone ComboBox Column
            var colZoneId = new DataGridViewComboBoxColumn
            {
                Name = "colZoneId",
                HeaderText = "Zone",
                FillWeight = 60,
                FlatStyle = FlatStyle.Flat,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox
            };
            var allZones = GetAllConfiguredZones();
            foreach (var z in allZones) colZoneId.Items.Add(z);

            // 4. Start (m) Column (from database, ReadOnly)
            var colStart = new DataGridViewTextBoxColumn
            {
                Name = "colStart",
                HeaderText = "Start (m)",
                FillWeight = 50,
                ReadOnly = true
            };

            // 5. End (m) Column (from database, ReadOnly)
            var colEnd = new DataGridViewTextBoxColumn
            {
                Name = "colEnd",
                HeaderText = "End (m)",
                FillWeight = 50,
                ReadOnly = true
            };

            // 6. Distance Point (m) Column (user configurable)
            var colDistance = new DataGridViewTextBoxColumn
            {
                Name = "colDistance",
                HeaderText = "Distance Point (m)",
                FillWeight = 75
            };

            // 7. Microcontroller Temp Column (populated upon test run)
            var colMicroTemp = new DataGridViewTextBoxColumn
            {
                Name = "colMicroTemp",
                HeaderText = "MC Temp (°C)",
                FillWeight = 65,
                ReadOnly = true
            };

            // 8. Fibre Temp Column (populated upon test run)
            var colFibreTemp = new DataGridViewTextBoxColumn
            {
                Name = "colFibreTemp",
                HeaderText = "Fibre Temp (°C)",
                FillWeight = 65,
                ReadOnly = true
            };

            // 9. Difference Column (Calculated)
            var colDiff = new DataGridViewTextBoxColumn
            {
                Name = "colDifference",
                HeaderText = "Diff (°C)",
                FillWeight = 55,
                ReadOnly = true
            };

            // 10. Status Column (Calculated PASS/FAIL)
            var colPassFail = new DataGridViewTextBoxColumn
            {
                Name = "colPassFail",
                HeaderText = "Status",
                FillWeight = 55,
                ReadOnly = true
            };

            // 11. Action Button Column ("Run Test")
            var colAction = new DataGridViewButtonColumn
            {
                Name = "colAction",
                HeaderText = "Action",
                Text = "Run Test",
                UseColumnTextForButtonValue = true,
                FillWeight = 60,
                FlatStyle = FlatStyle.Standard
            };

            dgvAtpTable.Columns.AddRange(new DataGridViewColumn[]
            {
                colPort,
                colChannelId,
                colZoneId,
                colStart,
                colEnd,
                colDistance,
                colMicroTemp,
                colFibreTemp,
                colDiff,
                colPassFail,
                colAction
            });

            // Center Alignments
            foreach (DataGridViewColumn col in dgvAtpTable.Columns)
            {
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }

            mainContainer.Controls.Add(dgvAtpTable, 0, 1);

            // 3. Bottom Action Bar
            var pnlBottom = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Margin = new Padding(0)
            };

            btnClose = new Button
            {
                Text = "Close",
                Width = 90,
                Height = 34,
                UseVisualStyleBackColor = true,
                Margin = new Padding(10, 0, 0, 0),
                Cursor = Cursors.Hand
            };

            btnExport = new Button
            {
                Text = "Export CSV",
                Width = 110,
                Height = 34,
                UseVisualStyleBackColor = true,
                Margin = new Padding(10, 0, 0, 0),
                Cursor = Cursors.Hand
            };

            btnClear = new Button
            {
                Text = "Clear Table",
                Width = 105,
                Height = 34,
                UseVisualStyleBackColor = true,
                Margin = new Padding(10, 0, 0, 0),
                Cursor = Cursors.Hand
            };

            btnRunAll = new Button
            {
                Text = "Run All Tests",
                Width = 120,
                Height = 34,
                BackColor = Color.FromArgb(0, 122, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(10, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            btnRunAll.FlatAppearance.BorderSize = 0;

            btnTogglePeriodic = new Button
            {
                Text = "Start Periodic",
                Width = 125,
                Height = 34,
                BackColor = Color.FromArgb(40, 167, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            btnTogglePeriodic.FlatAppearance.BorderSize = 0;

            pnlBottom.Controls.Add(btnClose);
            pnlBottom.Controls.Add(btnExport);
            pnlBottom.Controls.Add(btnClear);
            pnlBottom.Controls.Add(btnRunAll);
            pnlBottom.Controls.Add(btnTogglePeriodic);

            mainContainer.Controls.Add(pnlBottom, 0, 2);

            this.Controls.Add(mainContainer);
        }

        private void WireUpEvents()
        {
            dgvAtpTable.CellValueChanged += DgvAtpTable_CellValueChanged;
            dgvAtpTable.DefaultValuesNeeded += DgvAtpTable_DefaultValuesNeeded;
            dgvAtpTable.RowPostPaint += DgvAtpTable_RowPostPaint;
            dgvAtpTable.CellContentClick += DgvAtpTable_CellContentClick;
            dgvAtpTable.DataError += (s, e) => { e.ThrowException = false; };

            dgvAtpTable.CellEnter += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.RowIndex < dgvAtpTable.Rows.Count && !dgvAtpTable.Rows[e.RowIndex].IsNewRow)
                {
                    var row = dgvAtpTable.Rows[e.RowIndex];
                    if (row.Cells["colZoneId"] is DataGridViewComboBoxCell zoneCell)
                    {
                        string ch = row.Cells["colChannelId"].Value?.ToString() ?? "";
                        var zones = GetZonesForChannel(ch);
                        if (zoneCell.Items.Count != zones.Count)
                        {
                            zoneCell.Items.Clear();
                            foreach (var z in zones) zoneCell.Items.Add(z);
                        }
                    }
                }
            };

            btnConnectMcu.Click += async (s, e) =>
            {
                if (!_sensorCommService.IsConnected)
                {
                    string selectedPort = cboMcuPort.SelectedItem?.ToString() ?? "";
                    int.TryParse(cboMcuBaud.SelectedItem?.ToString(), out int baud);
                    if (baud <= 0) baud = 115200;

                    if (string.IsNullOrWhiteSpace(selectedPort))
                    {
                        MessageBox.Show("Please select a valid COM port.", "Port Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    btnConnectMcu.Enabled = false;
                    btnConnectMcu.Text = "Connecting...";

                    bool connected = await _sensorCommService.ConnectAsync(selectedPort, baud);
                    btnConnectMcu.Enabled = true;

                    if (connected)
                    {
                        btnConnectMcu.Text = "Disconnect";
                        btnConnectMcu.BackColor = Color.FromArgb(108, 117, 125);
                        lblMcuStatus.Text = $"🟢 Connected ({_sensorCommService.ConnectedPort})";
                        lblMcuStatus.ForeColor = Color.FromArgb(40, 167, 69);
                        cboMcuPort.Enabled = false;
                        cboMcuBaud.Enabled = false;
                    }
                    else
                    {
                        btnConnectMcu.Text = "Connect";
                        btnConnectMcu.BackColor = Color.FromArgb(0, 122, 255);
                        lblMcuStatus.Text = "🔴 Disconnected";
                        lblMcuStatus.ForeColor = Color.FromArgb(220, 53, 69);
                        MessageBox.Show($"Failed to connect to {selectedPort}.\n\nPlease ensure the device/simulator is plugged into {selectedPort} and not open in another application.", "Connection Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    await _sensorCommService.DisconnectAsync();
                    btnConnectMcu.Text = "Connect";
                    btnConnectMcu.BackColor = Color.FromArgb(0, 122, 255);
                    lblMcuStatus.Text = "🔴 Disconnected";
                    lblMcuStatus.ForeColor = Color.FromArgb(220, 53, 69);
                    cboMcuPort.Enabled = true;
                    cboMcuBaud.Enabled = true;
                }
            };

            btnRunAll.Click += async (s, e) => await RunAllTestsAsync();
            btnTogglePeriodic.Click += async (s, e) => await TogglePeriodicMonitoringAsync();
            btnClear.Click += (s, e) => ClearTable();
            btnExport.Click += (s, e) => ExportToCsv();
            btnClose.Click += (s, e) => this.Close();
        }

        private void RefreshMcuPorts()
        {
            try
            {
                string? current = cboMcuPort.SelectedItem?.ToString();
                cboMcuPort.Items.Clear();
                var ports = System.IO.Ports.SerialPort.GetPortNames();
                foreach (var p in ports) cboMcuPort.Items.Add(p);
                if (cboMcuPort.Items.Count > 0)
                {
                    if (!string.IsNullOrEmpty(current) && cboMcuPort.Items.Contains(current))
                    {
                        cboMcuPort.SelectedItem = current;
                    }
                    else
                    {
                        cboMcuPort.SelectedIndex = 0;
                    }
                }
            }
            catch { }
        }

        private void InitializeFixedPortRows()
        {
            try
            {
                dgvAtpTable.Rows.Clear();

                var channels = GetConfiguredChannels();
                string defaultChannel = channels.Count > 0 ? channels[0] : "Channel 1";
                var zones = GetZonesForChannel(defaultChannel);
                string defaultZone = zones.Count > 0 ? zones[0] : "Zone 1";
                var zoneInfo = GetZoneInfo(defaultChannel, defaultZone);

                // Initialize exactly the 3 PT100 sensor ports (Port 1, Port 2, Port 3)
                for (int portId = 1; portId <= 3; portId++)
                {
                    int rowIdx = dgvAtpTable.Rows.Add();
                    var row = dgvAtpTable.Rows[rowIdx];

                    row.Cells["colPort"].Value = $"Port {portId}";
                    row.Cells["colChannelId"].Value = defaultChannel;
                    row.Cells["colZoneId"].Value = defaultZone;

                    if (zoneInfo != null)
                    {
                        row.Cells["colStart"].Value = zoneInfo.StartPoint.ToString("0.##");
                        row.Cells["colEnd"].Value = zoneInfo.EndPoint.ToString("0.##");
                        row.Cells["colDistance"].ToolTipText = $"Enter point between {zoneInfo.StartPoint:0.##}m and {zoneInfo.EndPoint:0.##}m";
                    }
                    else
                    {
                        row.Cells["colStart"].Value = "0";
                        row.Cells["colEnd"].Value = "0";
                    }

                    row.Cells["colDistance"].Value = "";

                    if (row.Cells["colZoneId"] is DataGridViewComboBoxCell zoneCell)
                    {
                        zoneCell.Items.Clear();
                        foreach (var z in zones) zoneCell.Items.Add(z);
                    }

                    SetCellsDisabledState(row, true);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing 3 fixed port rows: {ex.Message}");
            }
        }

        private ZoneInfo? GetZoneInfo(string channelKey, string zoneDisplayName)
        {
            if (_configurationService != null && !string.IsNullOrWhiteSpace(channelKey) && !string.IsNullOrWhiteSpace(zoneDisplayName))
            {
                var zoneDict = _configurationService.GetZones();
                if (zoneDict != null && zoneDict.TryGetValue(channelKey, out var zones) && zones != null)
                {
                    return zones.FirstOrDefault(z =>
                        (!string.IsNullOrWhiteSpace(z.Name) && z.Name.Equals(zoneDisplayName, StringComparison.OrdinalIgnoreCase)) ||
                        $"Zone {z.ZoneId}".Equals(zoneDisplayName, StringComparison.OrdinalIgnoreCase) ||
                        z.ZoneId.ToString() == zoneDisplayName);
                }
            }
            return null;
        }

        private void SetCellsDisabledState(DataGridViewRow row, bool disabled)
        {
            Color bg = disabled ? DisabledCellBg : Color.White;
            Color fg = disabled ? DisabledCellFg : Color.FromArgb(33, 37, 41);

            string[] targetCols = new[] { "colMicroTemp", "colFibreTemp", "colDifference", "colPassFail" };
            foreach (var colName in targetCols)
            {
                if (row.Cells[colName] != null)
                {
                    row.Cells[colName].Style.BackColor = bg;
                    row.Cells[colName].Style.ForeColor = fg;
                    if (disabled && (row.Cells[colName].Value == null || string.IsNullOrWhiteSpace(row.Cells[colName].Value.ToString())))
                    {
                        row.Cells[colName].Value = "--";
                    }
                }
            }
        }

        private void DgvAtpTable_DefaultValuesNeeded(object? sender, DataGridViewRowEventArgs e)
        {
            var channels = GetConfiguredChannels();
            string defaultChannel = channels.Count > 0 ? channels[0] : "Channel 1";
            var zones = GetZonesForChannel(defaultChannel);
            string defaultZone = zones.Count > 0 ? zones[0] : "Zone 1";

            e.Row.Cells["colPort"].Value = "Port 1";
            e.Row.Cells["colChannelId"].Value = defaultChannel;
            e.Row.Cells["colZoneId"].Value = defaultZone;

            // Auto-populate Start (m) and End (m) from database
            var zoneInfo = GetZoneInfo(defaultChannel, defaultZone);
            if (zoneInfo != null)
            {
                e.Row.Cells["colStart"].Value = zoneInfo.StartPoint.ToString("0.##");
                e.Row.Cells["colEnd"].Value = zoneInfo.EndPoint.ToString("0.##");
                e.Row.Cells["colDistance"].ToolTipText = $"Enter point between {zoneInfo.StartPoint:0.##}m and {zoneInfo.EndPoint:0.##}m";
            }
            else
            {
                e.Row.Cells["colStart"].Value = "0";
                e.Row.Cells["colEnd"].Value = "0";
            }

            e.Row.Cells["colDistance"].Value = "";

            // Grey out temperature fields initially
            SetCellsDisabledState(e.Row, true);

            if (e.Row.Cells["colZoneId"] is DataGridViewComboBoxCell zoneCell)
            {
                zoneCell.Items.Clear();
                foreach (var z in zones) zoneCell.Items.Add(z);
            }
        }

        private void DgvAtpTable_RowPostPaint(object? sender, DataGridViewRowPostPaintEventArgs e)
        {
            // Draw row index number in the row header automatically
            string rowIdx = (e.RowIndex + 1).ToString();
            var headerBounds = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, dgvAtpTable.RowHeadersWidth, e.RowBounds.Height);
            TextRenderer.DrawText(
                e.Graphics,
                rowIdx,
                dgvAtpTable.Font,
                headerBounds,
                dgvAtpTable.RowHeadersDefaultCellStyle.ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter
            );
        }

        private async void DgvAtpTable_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dgvAtpTable.Rows.Count) return;
            if (dgvAtpTable.Rows[e.RowIndex].IsNewRow) return;

            // Click on "Run Test" button column
            if (e.ColumnIndex == dgvAtpTable.Columns["colAction"]?.Index)
            {
                await RunTestForRowAsync(e.RowIndex);
            }
        }

        private byte ParseSensorId(string? portName)
        {
            if (string.IsNullOrWhiteSpace(portName)) return 1;
            var match = Regex.Match(portName, @"\d+");
            if (match.Success && byte.TryParse(match.Value, out byte id) && id > 0)
            {
                return id;
            }
            return 1;
        }

        private async Task<double?> GetDtsFibreTemperatureAsync(int channelId, string zoneDisplayName, double distancePoint)
        {
            // 1. Try fetching detailed point data from TemperatureDataService
            if (_temperatureDataService != null)
            {
                var channelPoints = _temperatureDataService.GetChannelData(channelId);
                if (channelPoints != null && channelPoints.Length > 0)
                {
                    var nearest = channelPoints.OrderBy(p => Math.Abs(p.Position - distancePoint)).FirstOrDefault();
                    if (nearest != null)
                    {
                        return nearest.Temperature;
                    }
                }
            }

            // 2. Try fetching live Zone State from ChannelClient (DTS Simulator / Backend)
            if (_channelClient != null)
            {
                try
                {
                    var zoneStates = await _channelClient.GetZoneStateAsync(channelId);
                    if (zoneStates != null && zoneStates.Count > 0)
                    {
                        var matchingZone = zoneStates.FirstOrDefault(z =>
                            (distancePoint >= z.StartPoint && distancePoint <= z.EndPoint) ||
                            (!string.IsNullOrWhiteSpace(z.Name) && z.Name.Equals(zoneDisplayName, StringComparison.OrdinalIgnoreCase)) ||
                            $"Zone {z.ZoneId}".Equals(zoneDisplayName, StringComparison.OrdinalIgnoreCase));

                        if (matchingZone != null && matchingZone.AverageTemperature > 0)
                        {
                            return matchingZone.AverageTemperature;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ATP] ChannelClient zone state error: {ex.Message}");
                }
            }

            return null;
        }

        private async Task RunTestForRowAsync(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= dgvAtpTable.Rows.Count) return;
            var row = dgvAtpTable.Rows[rowIndex];
            if (row.IsNewRow) return;

            if (!_sensorCommService.IsConnected)
            {
                MessageBox.Show("Microcontroller is not connected.\n\nPlease select your MCU Port at the top (e.g. COM1, COM2, COM3) and click 'Connect' before running tests.", "Microcontroller Not Connected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string chStr = row.Cells["colChannelId"].Value?.ToString() ?? "";
            string zoneStr = row.Cells["colZoneId"].Value?.ToString() ?? "";
            string distStr = row.Cells["colDistance"].Value?.ToString()?.Trim() ?? "";

            var zoneInfo = GetZoneInfo(chStr, zoneStr);

            // 1. Validate Distance Point
            if (!double.TryParse(distStr, out double distancePoint))
            {
                MessageBox.Show("Please configure a valid Distance Point (m) before running the test.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                row.Cells["colDistance"].Style.BackColor = Color.FromArgb(255, 235, 235);
                return;
            }

            if (zoneInfo != null && (distancePoint < zoneInfo.StartPoint || distancePoint > zoneInfo.EndPoint))
            {
                MessageBox.Show($"Distance Point ({distancePoint}m) is outside the selected zone's range ({zoneInfo.StartPoint:0.##}m – {zoneInfo.EndPoint:0.##}m).", "Out of Range", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                row.Cells["colDistance"].Style.BackColor = Color.FromArgb(255, 235, 235);
                return;
            }

            // Show running state
            row.Cells["colPassFail"].Value = "Reading...";
            row.Cells["colPassFail"].Style.ForeColor = Color.Blue;
            row.Cells["colPassFail"].Style.Font = new Font(dgvAtpTable.Font, FontStyle.Italic);

            try
            {
                // 2. Fetch Microcontroller Temperature via Protocol Request (0x01)
                string portStr = row.Cells["colPort"].Value?.ToString() ?? "Port 1";
                byte sensorId = ParseSensorId(portStr);

                var readings = await _sensorCommService.RequestTemperaturesAsync(new[] { sensorId });
                var reading = readings.FirstOrDefault(r => r.SensorId == sensorId) ?? readings.FirstOrDefault();
                if (reading == null)
                {
                    throw new InvalidOperationException($"No temperature reading returned for Port {sensorId}.");
                }
                double mcTemp = reading.Temperature;

                // 3. Fetch Fibre Temperature from DTS Simulator (point or zone state)
                int channelId = 1;
                var match = Regex.Match(chStr, @"\d+");
                if (match.Success) int.TryParse(match.Value, out channelId);

                double? fibreTempOpt = await GetDtsFibreTemperatureAsync(channelId, zoneStr, distancePoint);

                // 4. Calculate Difference and Status
                SetCellsDisabledState(row, false);
                row.Cells["colMicroTemp"].Value = mcTemp.ToString("F2");

                if (fibreTempOpt.HasValue)
                {
                    double fibreTemp = fibreTempOpt.Value;
                    double diff = Math.Abs(mcTemp - fibreTemp);
                    bool isPass = diff <= _tolerance;

                    row.Cells["colFibreTemp"].Value = fibreTemp.ToString("F2");
                    row.Cells["colDifference"].Value = diff.ToString("F2");
                    row.Cells["colPassFail"].Value = isPass ? "PASS" : "FAIL";
                    row.Cells["colPassFail"].Style.ForeColor = isPass ? PassColor : FailColor;
                    row.Cells["colPassFail"].Style.Font = new Font(dgvAtpTable.Font, FontStyle.Bold);
                }
                else
                {
                    row.Cells["colFibreTemp"].Value = "NO DATA";
                    row.Cells["colDifference"].Value = "--";
                    row.Cells["colPassFail"].Value = "NO FIBRE DATA";
                    row.Cells["colPassFail"].Style.ForeColor = Color.FromArgb(255, 140, 0);
                    row.Cells["colPassFail"].Style.Font = new Font(dgvAtpTable.Font, FontStyle.Bold);
                }
            }
            catch (Exception ex)
            {
                row.Cells["colMicroTemp"].Value = "--";
                row.Cells["colFibreTemp"].Value = "--";
                row.Cells["colDifference"].Value = "--";
                row.Cells["colPassFail"].Value = "ERROR";
                row.Cells["colPassFail"].Style.ForeColor = FailColor;
                SetCellsDisabledState(row, true);
                MessageBox.Show($"Failed to execute test for Row #{rowIndex + 1}:\n\n{ex.Message}", "Communication Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task RunAllTestsAsync()
        {
            if (!_sensorCommService.IsConnected)
            {
                MessageBox.Show("Microcontroller is not connected.\n\nPlease select your MCU Port at the top (e.g. COM1, COM2, COM3) and click 'Connect' before running tests.", "Microcontroller Not Connected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var validRows = new List<(int rowIndex, DataGridViewRow row, byte sensorId, int channelId, string zoneStr, double distancePoint)>();

            for (int i = 0; i < dgvAtpTable.Rows.Count; i++)
            {
                var row = dgvAtpTable.Rows[i];
                if (row.IsNewRow) continue;

                string distStr = row.Cells["colDistance"].Value?.ToString()?.Trim() ?? "";
                if (!double.TryParse(distStr, out double dist)) continue;

                string chStr = row.Cells["colChannelId"].Value?.ToString() ?? "";
                string zoneStr = row.Cells["colZoneId"].Value?.ToString() ?? "";
                int channelId = 1;
                var match = Regex.Match(chStr, @"\d+");
                if (match.Success) int.TryParse(match.Value, out channelId);

                string portStr = row.Cells["colPort"].Value?.ToString() ?? "Port 1";
                byte sensorId = ParseSensorId(portStr);

                validRows.Add((i, row, sensorId, channelId, zoneStr, dist));
            }

            if (validRows.Count == 0)
            {
                MessageBox.Show("No valid configured test records found (please configure at least one row with a valid Distance Point).", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            btnRunAll.Enabled = false;
            btnRunAll.Text = "Running...";

            try
            {
                // Send single batch request containing all required Sensor IDs (Protocol 0x01)
                var sensorIds = validRows.Select(r => r.sensorId).Distinct().ToList();
                var readings = await _sensorCommService.RequestTemperaturesAsync(sensorIds);
                var readingMap = readings.ToDictionary(r => r.SensorId, r => r.Temperature);

                foreach (var item in validRows)
                {
                    if (!readingMap.TryGetValue(item.sensorId, out double mcTemp))
                    {
                        throw new InvalidOperationException($"No temperature reading returned for Port {item.sensorId}.");
                    }

                    double? fibreTempOpt = await GetDtsFibreTemperatureAsync(item.channelId, item.zoneStr, item.distancePoint);

                    SetCellsDisabledState(item.row, false);
                    item.row.Cells["colMicroTemp"].Value = mcTemp.ToString("F2");

                    if (fibreTempOpt.HasValue)
                    {
                        double fibreTemp = fibreTempOpt.Value;
                        double diff = Math.Abs(mcTemp - fibreTemp);
                        bool isPass = diff <= _tolerance;

                        item.row.Cells["colFibreTemp"].Value = fibreTemp.ToString("F2");
                        item.row.Cells["colDifference"].Value = diff.ToString("F2");
                        item.row.Cells["colPassFail"].Value = isPass ? "PASS" : "FAIL";
                        item.row.Cells["colPassFail"].Style.ForeColor = isPass ? PassColor : FailColor;
                        item.row.Cells["colPassFail"].Style.Font = new Font(dgvAtpTable.Font, FontStyle.Bold);
                    }
                    else
                    {
                        item.row.Cells["colFibreTemp"].Value = "NO DATA";
                        item.row.Cells["colDifference"].Value = "--";
                        item.row.Cells["colPassFail"].Value = "NO FIBRE DATA";
                        item.row.Cells["colPassFail"].Style.ForeColor = Color.FromArgb(255, 140, 0);
                        item.row.Cells["colPassFail"].Style.Font = new Font(dgvAtpTable.Font, FontStyle.Bold);
                    }
                }
            }
            catch (Exception ex)
            {
                foreach (var item in validRows)
                {
                    item.row.Cells["colMicroTemp"].Value = "--";
                    item.row.Cells["colFibreTemp"].Value = "--";
                    item.row.Cells["colDifference"].Value = "--";
                    item.row.Cells["colPassFail"].Value = "ERROR";
                    item.row.Cells["colPassFail"].Style.ForeColor = FailColor;
                    SetCellsDisabledState(item.row, true);
                }
                MessageBox.Show($"Batch test execution failed:\n\n{ex.Message}", "Communication Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnRunAll.Enabled = true;
                btnRunAll.Text = "Run All Tests";
            }
        }

        private async Task TogglePeriodicMonitoringAsync()
        {
            if (!_isPeriodicRunning)
            {
                if (!_sensorCommService.IsConnected)
                {
                    MessageBox.Show("Microcontroller is not connected.\n\nPlease select your MCU Port at the top (e.g. COM1, COM2, COM3) and click 'Connect' before starting periodic monitoring.", "Microcontroller Not Connected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var sensorIds = new List<byte>();
                for (int i = 0; i < dgvAtpTable.Rows.Count; i++)
                {
                    var row = dgvAtpTable.Rows[i];
                    if (row.IsNewRow) continue;

                    string distStr = row.Cells["colDistance"].Value?.ToString()?.Trim() ?? "";
                    if (double.TryParse(distStr, out double dist) && dist > 0)
                    {
                        string portStr = row.Cells["colPort"].Value?.ToString() ?? "Port 1";
                        sensorIds.Add(ParseSensorId(portStr));
                    }
                }

                if (sensorIds.Count == 0)
                {
                    MessageBox.Show("No configured test ports found.\n\nPlease enter a valid Distance Point (m) for at least one port before starting periodic monitoring.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                bool started = await _sensorCommService.StartPeriodicAsync(sensorIds.Distinct());
                if (started)
                {
                    _isPeriodicRunning = true;
                    btnTogglePeriodic.Text = "Stop Periodic";
                    btnTogglePeriodic.BackColor = Color.FromArgb(220, 53, 69);
                    _sensorCommService.PeriodicDataReceived += SensorComm_PeriodicDataReceived;
                }
            }
            else
            {
                await _sensorCommService.StopPeriodicAsync();
                _sensorCommService.PeriodicDataReceived -= SensorComm_PeriodicDataReceived;
                _isPeriodicRunning = false;
                btnTogglePeriodic.Text = "Start Periodic";
                btnTogglePeriodic.BackColor = Color.FromArgb(40, 167, 69);
            }
        }

        private void SensorComm_PeriodicDataReceived(object? sender, List<SensorReading> readings)
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;

            this.BeginInvoke(new Action(async () =>
            {
                if (dgvAtpTable.IsDisposed) return;

                var readingMap = readings.ToDictionary(r => r.SensorId, r => r.Temperature);

                for (int i = 0; i < dgvAtpTable.Rows.Count; i++)
                {
                    var row = dgvAtpTable.Rows[i];
                    if (row.IsNewRow) continue;

                    string portStr = row.Cells["colPort"].Value?.ToString() ?? "Port 1";
                    byte sensorId = ParseSensorId(portStr);

                    if (readingMap.TryGetValue(sensorId, out double mcTemp))
                    {
                        string distStr = row.Cells["colDistance"].Value?.ToString()?.Trim() ?? "";
                        double.TryParse(distStr, out double dist);

                        string chStr = row.Cells["colChannelId"].Value?.ToString() ?? "";
                        string zoneStr = row.Cells["colZoneId"].Value?.ToString() ?? "";
                        int channelId = 1;
                        var match = Regex.Match(chStr, @"\d+");
                        if (match.Success) int.TryParse(match.Value, out channelId);

                        double? fibreTempOpt = await GetDtsFibreTemperatureAsync(channelId, zoneStr, dist);

                        SetCellsDisabledState(row, false);
                        row.Cells["colMicroTemp"].Value = mcTemp.ToString("F2");

                        if (fibreTempOpt.HasValue)
                        {
                            double fibreTemp = fibreTempOpt.Value;
                            double diff = Math.Abs(mcTemp - fibreTemp);
                            bool isPass = diff <= _tolerance;

                            row.Cells["colFibreTemp"].Value = fibreTemp.ToString("F2");
                            row.Cells["colDifference"].Value = diff.ToString("F2");
                            row.Cells["colPassFail"].Value = isPass ? "PASS" : "FAIL";
                            row.Cells["colPassFail"].Style.ForeColor = isPass ? PassColor : FailColor;
                            row.Cells["colPassFail"].Style.Font = new Font(dgvAtpTable.Font, FontStyle.Bold);
                        }
                        else
                        {
                            row.Cells["colFibreTemp"].Value = "NO DATA";
                            row.Cells["colDifference"].Value = "--";
                            row.Cells["colPassFail"].Value = "NO FIBRE DATA";
                            row.Cells["colPassFail"].Style.ForeColor = Color.FromArgb(255, 140, 0);
                            row.Cells["colPassFail"].Style.Font = new Font(dgvAtpTable.Font, FontStyle.Bold);
                        }
                    }
                }
            }));
        }

        private void DgvAtpTable_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dgvAtpTable.Rows.Count) return;

            var row = dgvAtpTable.Rows[e.RowIndex];
            if (row.IsNewRow) return;

            // Handle Channel change -> dynamically update available Zones and fetch Start/End points
            if (e.ColumnIndex == dgvAtpTable.Columns["colChannelId"]?.Index)
            {
                string ch = row.Cells["colChannelId"].Value?.ToString() ?? "";
                if (row.Cells["colZoneId"] is DataGridViewComboBoxCell zoneCell)
                {
                    var zones = GetZonesForChannel(ch);
                    zoneCell.Items.Clear();
                    foreach (var z in zones) zoneCell.Items.Add(z);

                    if (zoneCell.Value == null || !zones.Contains(zoneCell.Value.ToString() ?? ""))
                    {
                        zoneCell.Value = zones.Count > 0 ? zones[0] : "";
                    }

                    string selectedZone = zoneCell.Value?.ToString() ?? "";
                    var zoneInfo = GetZoneInfo(ch, selectedZone);
                    if (zoneInfo != null)
                    {
                        row.Cells["colStart"].Value = zoneInfo.StartPoint.ToString("0.##");
                        row.Cells["colEnd"].Value = zoneInfo.EndPoint.ToString("0.##");
                        row.Cells["colDistance"].Value = "";
                        row.Cells["colDistance"].ToolTipText = $"Enter point between {zoneInfo.StartPoint:0.##}m and {zoneInfo.EndPoint:0.##}m";
                        row.Cells["colDistance"].Style.BackColor = Color.White;
                        row.Cells["colDistance"].Style.ForeColor = dgvAtpTable.ForeColor;
                    }

                    SetCellsDisabledState(row, true);
                }
            }
            // Handle Zone change -> fetch Start (m) and End (m) points from database
            else if (e.ColumnIndex == dgvAtpTable.Columns["colZoneId"]?.Index)
            {
                string ch = row.Cells["colChannelId"].Value?.ToString() ?? "";
                string selectedZone = row.Cells["colZoneId"].Value?.ToString() ?? "";
                var zoneInfo = GetZoneInfo(ch, selectedZone);
                if (zoneInfo != null)
                {
                    row.Cells["colStart"].Value = zoneInfo.StartPoint.ToString("0.##");
                    row.Cells["colEnd"].Value = zoneInfo.EndPoint.ToString("0.##");
                    row.Cells["colDistance"].Value = "";
                    row.Cells["colDistance"].ToolTipText = $"Enter point between {zoneInfo.StartPoint:0.##}m and {zoneInfo.EndPoint:0.##}m";
                    row.Cells["colDistance"].Style.BackColor = Color.White;
                    row.Cells["colDistance"].Style.ForeColor = dgvAtpTable.ForeColor;
                }

                SetCellsDisabledState(row, true);
            }
            // Handle Distance Point edit -> validate against Start and End boundaries
            else if (e.ColumnIndex == dgvAtpTable.Columns["colDistance"]?.Index)
            {
                string ch = row.Cells["colChannelId"].Value?.ToString() ?? "";
                string selectedZone = row.Cells["colZoneId"].Value?.ToString() ?? "";
                var zoneInfo = GetZoneInfo(ch, selectedZone);
                string distStr = row.Cells["colDistance"].Value?.ToString()?.Trim() ?? "";

                if (string.IsNullOrEmpty(distStr))
                {
                    row.Cells["colDistance"].Style.BackColor = Color.White;
                    row.Cells["colDistance"].Style.ForeColor = dgvAtpTable.ForeColor;
                    if (zoneInfo != null)
                    {
                        row.Cells["colDistance"].ToolTipText = $"Enter point between {zoneInfo.StartPoint:0.##}m and {zoneInfo.EndPoint:0.##}m";
                    }
                }
                else if (zoneInfo != null && double.TryParse(distStr, out double dist))
                {
                    if (dist < zoneInfo.StartPoint || dist > zoneInfo.EndPoint)
                    {
                        row.Cells["colDistance"].Style.BackColor = Color.FromArgb(255, 235, 235);
                        row.Cells["colDistance"].Style.ForeColor = Color.DarkRed;
                        row.Cells["colDistance"].ToolTipText = $"⚠ Point {dist}m is outside zone range ({zoneInfo.StartPoint:0.##}m – {zoneInfo.EndPoint:0.##}m)";
                    }
                    else
                    {
                        row.Cells["colDistance"].Style.BackColor = Color.White;
                        row.Cells["colDistance"].Style.ForeColor = dgvAtpTable.ForeColor;
                        row.Cells["colDistance"].ToolTipText = $"Valid point between {zoneInfo.StartPoint:0.##}m and {zoneInfo.EndPoint:0.##}m";
                    }
                }
            }
            // Recalculate Difference and Pass/Fail if temperatures are edited manually
            else if (e.ColumnIndex == dgvAtpTable.Columns["colMicroTemp"]?.Index || e.ColumnIndex == dgvAtpTable.Columns["colFibreTemp"]?.Index)
            {
                string microVal = row.Cells["colMicroTemp"].Value?.ToString()?.Trim() ?? "";
                string fibreVal = row.Cells["colFibreTemp"].Value?.ToString()?.Trim() ?? "";

                bool hasMicro = double.TryParse(microVal, out double microTemp);
                bool hasFibre = double.TryParse(fibreVal, out double fibreTemp);

                if (hasMicro && hasFibre)
                {
                    double diff = Math.Abs(microTemp - fibreTemp);
                    row.Cells["colDifference"].Value = diff.ToString("F2");

                    bool isPass = diff <= _tolerance;
                    row.Cells["colPassFail"].Value = isPass ? "PASS" : "FAIL";
                    row.Cells["colPassFail"].Style.ForeColor = isPass ? PassColor : FailColor;
                    row.Cells["colPassFail"].Style.Font = new Font(dgvAtpTable.Font, FontStyle.Bold);
                }
            }
        }

        private void ClearTable()
        {
            if (dgvAtpTable.Rows.Count == 0 || (dgvAtpTable.Rows.Count == 1 && dgvAtpTable.Rows[0].IsNewRow)) return;

            var confirm = MessageBox.Show("Are you sure you want to reset all test rows?", "Confirm Reset", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm == DialogResult.Yes)
            {
                InitializeFixedPortRows();
            }
        }

        private void ExportToCsv()
        {
            if (dgvAtpTable.Rows.Count == 0 || (dgvAtpTable.Rows.Count == 1 && dgvAtpTable.Rows[0].IsNewRow))
            {
                MessageBox.Show("No records to export.", "Empty Table", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                FileName = $"ATP_Report_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                Title = "Export ATP Records"
            };

            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    var sb = new StringBuilder();

                    // Header (excluding the Action button column)
                    var headers = new List<string> { "\"#\"" };
                    foreach (DataGridViewColumn col in dgvAtpTable.Columns)
                    {
                        if (col.Name == "colAction") continue;
                        headers.Add($"\"{col.HeaderText}\"");
                    }
                    sb.AppendLine(string.Join(",", headers));

                    // Rows
                    int rNum = 1;
                    foreach (DataGridViewRow row in dgvAtpTable.Rows)
                    {
                        if (row.IsNewRow) continue;
                        var cells = new List<string> { $"\"{rNum++}\"" };
                        foreach (DataGridViewColumn col in dgvAtpTable.Columns)
                        {
                            if (col.Name == "colAction") continue;
                            string val = row.Cells[col.Index].Value?.ToString() ?? "";
                            cells.Add($"\"{val.Replace("\"", "\"\"")}\"");
                        }
                        sb.AppendLine(string.Join(",", cells));
                    }

                    File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show($"Report exported successfully to:\n{sfd.FileName}", "Export Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to export report:\n\n{ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
