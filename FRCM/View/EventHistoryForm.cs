using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using FRCM.Services;

namespace FRCM.View
{
    public partial class EventHistoryForm : Form
    {
        private readonly CustomWebSocketClient _wsClient;
        private DataGridView dgvEvents;
        private DateTimePicker dtpFrom;
        private DateTimePicker dtpTo;
        private Button btnLoad;
        private Button btnExport;
        private Label lblStatus;
        private Label lblFrom;
        private Label lblTo;

        public EventHistoryForm(CustomWebSocketClient wsClient)
        {
            _wsClient = wsClient;
            this.WindowState = FormWindowState.Maximized;
            InitializeComponent();
            LoadLast7DaysEvents();
        }

        //private void InitializeComponent()
        //{
        //    this.Text = "Event History - Alarm Events";
        //    this.Size = new Size(1200, 700);
        //    this.StartPosition = FormStartPosition.CenterParent;

        //    // Create controls
        //    lblFrom = new Label
        //    {
        //        Text = "From:",
        //        Location = new Point(20, 20),
        //        AutoSize = true
        //    };

        //    dtpFrom = new DateTimePicker
        //    {
        //        Location = new Point(70, 17),
        //        Width = 200,
        //        Format = DateTimePickerFormat.Custom,
        //        CustomFormat = "yyyy-MM-dd HH:mm:ss",
        //        Value = DateTime.Now.AddDays(-7)
        //    };

        //    lblTo = new Label
        //    {
        //        Text = "To:",
        //        Location = new Point(290, 20),
        //        AutoSize = true
        //    };

        //    dtpTo = new DateTimePicker
        //    {
        //        Location = new Point(320, 17),
        //        Width = 200,
        //        Format = DateTimePickerFormat.Custom,
        //        CustomFormat = "yyyy-MM-dd HH:mm:ss",
        //        Value = DateTime.Now
        //    };

        //    btnLoad = new Button
        //    {
        //        Text = "Load Events",
        //        Location = new Point(540, 15),
        //        Width = 100,
        //        Height = 30
        //    };
        //    btnLoad.Click += BtnLoad_Click;

        //    btnExport = new Button
        //    {
        //        Text = "Export to CSV",
        //        Location = new Point(660, 15),
        //        Width = 120,
        //        Height = 30
        //    };
        //    btnExport.Click += BtnExport_Click;

        //    lblStatus = new Label
        //    {
        //        Text = "Ready",
        //        Location = new Point(20, 55),
        //        AutoSize = true,
        //        ForeColor = Color.Blue
        //    };

        //    // DataGridView
        //    dgvEvents = new DataGridView
        //    {
        //        Location = new Point(20, 85),
        //        Size = new Size(1150, 550),
        //        ReadOnly = true,
        //        AllowUserToAddRows = false,
        //        AllowUserToDeleteRows = false,
        //        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        //        MultiSelect = false,
        //        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        //    };

        //    // Add columns
        //    dgvEvents.Columns.Add(new DataGridViewTextBoxColumn { Name = "Timestamp", HeaderText = "Timestamp", Width = 150 });
        //    dgvEvents.Columns.Add(new DataGridViewTextBoxColumn { Name = "ChannelId", HeaderText = "Channel", Width = 80 });
        //    dgvEvents.Columns.Add(new DataGridViewTextBoxColumn { Name = "ZoneId", HeaderText = "Zone", Width = 80 });
        //    dgvEvents.Columns.Add(new DataGridViewTextBoxColumn { Name = "ZoneName", HeaderText = "Zone Name", Width = 120 });
        //    dgvEvents.Columns.Add(new DataGridViewTextBoxColumn { Name = "AlarmType", HeaderText = "Alarm Type", Width = 120 });
        //    dgvEvents.Columns.Add(new DataGridViewTextBoxColumn { Name = "IsActive", HeaderText = "Active", Width = 80 });
        //    dgvEvents.Columns.Add(new DataGridViewTextBoxColumn { Name = "TriggerValue", HeaderText = "Trigger Value", Width = 100 });
        //    dgvEvents.Columns.Add(new DataGridViewTextBoxColumn { Name = "BreachPosition", HeaderText = "Breach Position", Width = 120 });
        //    dgvEvents.Columns.Add(new DataGridViewTextBoxColumn { Name = "AvgTemp", HeaderText = "Avg Temp", Width = 90 });
        //    dgvEvents.Columns.Add(new DataGridViewTextBoxColumn { Name = "MaxTemp", HeaderText = "Max Temp", Width = 90 });

        //    // Add controls to form
        //    this.Controls.Add(lblFrom);
        //    this.Controls.Add(dtpFrom);
        //    this.Controls.Add(lblTo);
        //    this.Controls.Add(dtpTo);
        //    this.Controls.Add(btnLoad);
        //    this.Controls.Add(btnExport);
        //    this.Controls.Add(lblStatus);
        //    this.Controls.Add(dgvEvents);
        //}
                private void InitializeComponent()
                {
                    this.Text = "Event History - Alarm Events";
                    this.Size = new Size(1200, 700);
                    this.StartPosition = FormStartPosition.CenterParent;
                    this.MinimumSize = new Size(900, 500);
                    this.AutoScaleDimensions = new SizeF(96F, 96F);
                    this.AutoScaleMode = AutoScaleMode.Dpi;
        
                    // ===== Main Layout =====
                    var mainLayout = new TableLayoutPanel
                    {
                        Dock = DockStyle.Fill,
                        ColumnCount = 1,
                        RowCount = 2
                    };
                    mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        
                    // ===== Top Controls Layout =====
                    var controlsLayout = new TableLayoutPanel
                    {
                        Dock = DockStyle.Fill,
                        ColumnCount = 7,
                        RowCount = 2,
                        Padding = new Padding(10)
                    };
                    controlsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // From
                    controlsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35)); // From Picker
                    controlsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // To
                    controlsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35)); // To Picker
                    controlsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15)); // Load Button
                    controlsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15)); // Export Button
                    controlsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20)); // Spacer
        
                    controlsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
                    controlsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        
                    // ===== Controls Initialization =====
                    lblFrom = new Label { Text = "From:", Anchor = AnchorStyles.Left, AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
                    dtpFrom = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd HH:mm:ss", Value = DateTime.Now.AddDays(-7), Dock = DockStyle.Fill, Margin = new Padding(3) };
                    lblTo = new Label { Text = "To:", Anchor = AnchorStyles.Left, AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
                    dtpTo = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd HH:mm:ss", Value = DateTime.Now, Dock = DockStyle.Fill, Margin = new Padding(3) };
                    btnLoad = new Button { Text = "Load Events", Dock = DockStyle.Fill, Margin = new Padding(3) };
                    btnLoad.Click += BtnLoad_Click;
                    btnExport = new Button { Text = "Export to CSV", Dock = DockStyle.Fill, Margin = new Padding(3) };
                    btnExport.Click += BtnExport_Click;
                    lblStatus = new Label { Text = "Ready", Anchor = AnchorStyles.Left, AutoSize = true, ForeColor = Color.Blue, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        
                    // ===== Add Controls to Top Layout =====
                    controlsLayout.Controls.Add(lblFrom, 0, 0);
                    controlsLayout.Controls.Add(dtpFrom, 1, 0);
                    controlsLayout.Controls.Add(lblTo, 2, 0);
                    controlsLayout.Controls.Add(dtpTo, 3, 0);
                    controlsLayout.Controls.Add(btnLoad, 4, 0);
                    controlsLayout.Controls.Add(btnExport, 5, 0);
                    controlsLayout.Controls.Add(lblStatus, 0, 1);
                    controlsLayout.SetColumnSpan(lblStatus, 6);
        
                    // ===== DataGridView =====
                    dgvEvents = new DataGridView
                    {
                        Dock = DockStyle.Fill,
                        ReadOnly = true,
                        AllowUserToAddRows = false,
                        AllowUserToDeleteRows = false,
                        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                        MultiSelect = false,
                        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                        AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
                    };
                    dgvEvents.Columns.Add(new DataGridViewTextBoxColumn { Name = "Timestamp", HeaderText = "Timestamp" });
                    dgvEvents.Columns.Add(new DataGridViewTextBoxColumn { Name = "ChannelId", HeaderText = "Channel" });
                    dgvEvents.Columns.Add(new DataGridViewTextBoxColumn { Name = "ZoneId", HeaderText = "Zone" });
                    dgvEvents.Columns.Add(new DataGridViewTextBoxColumn { Name = "ZoneName", HeaderText = "Zone Name" });
                    dgvEvents.Columns.Add(new DataGridViewTextBoxColumn { Name = "AlarmType", HeaderText = "Alarm Type" });
                    dgvEvents.Columns.Add(new DataGridViewTextBoxColumn { Name = "IsActive", HeaderText = "Active" });
                    dgvEvents.Columns.Add(new DataGridViewTextBoxColumn { Name = "TriggerValue", HeaderText = "Trigger Value" });
                    dgvEvents.Columns.Add(new DataGridViewTextBoxColumn { Name = "BreachPosition", HeaderText = "Breach Position" });
                    dgvEvents.Columns.Add(new DataGridViewTextBoxColumn { Name = "AvgTemp", HeaderText = "Avg Temp" });
                    dgvEvents.Columns.Add(new DataGridViewTextBoxColumn { Name = "MaxTemp", HeaderText = "Max Temp" });
        
                    // ===== Add Panels to Main Layout =====
                    mainLayout.Controls.Add(controlsLayout, 0, 0);
                    mainLayout.Controls.Add(dgvEvents, 0, 1);
        
                    this.Controls.Add(mainLayout);
                }


        private async void LoadLast7DaysEvents()
        {
            await LoadEvents();
        }

        private async void BtnLoad_Click(object? sender, EventArgs e)
        {
            await LoadEvents();
        }

        private async Task LoadEvents()
        {
            try
            {
                btnLoad.Enabled = false;
                lblStatus.Text = "Loading events...";
                lblStatus.ForeColor = Color.Blue;

                if (dtpFrom.Value > dtpTo.Value)
                {
                    lblStatus.Text = "Invalid date range";
                    lblStatus.ForeColor = Color.Red;
                    MessageBox.Show("'From' date cannot be later than 'To' date.",
                        "Invalid Date Range", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var request = new
                {
                    MessageType = "get_event_history",
                    Payload = new
                    {
                        From = dtpFrom.Value.ToUniversalTime(),
                        To = dtpTo.Value.ToUniversalTime()
                    }
                };

                string requestJson = JsonSerializer.Serialize(request);
                var response = await _wsClient.SendCommandAsync("get_event_history", request.Payload);

                if (!response.HasValue)
                {
                    lblStatus.Text = "Error: No response from server";
                    lblStatus.ForeColor = Color.Red;
                    return;
                }

                if (response.Value.TryGetProperty("Payload", out var payload) &&
                    payload.TryGetProperty("Success", out var success) && success.GetBoolean())
                {
                    dgvEvents.Rows.Clear();

                    if (payload.TryGetProperty("Events", out var eventsArray))
                    {
                        int count = 0;
                        foreach (var ev in eventsArray.EnumerateArray())
                        {
                            var timestamp = ev.GetProperty("Timestamp").GetDateTime().ToLocalTime();
                            var channelId = ev.GetProperty("ChannelId").GetInt32();
                            var zoneId = ev.GetProperty("ZoneId").GetInt32();
                            var zoneName = ev.GetProperty("ZoneName").GetString() ?? "";
                            var alarmType = ev.GetProperty("AlarmType").GetString() ?? "";
                            var isActive = ev.GetProperty("IsActive").GetBoolean();
                            var triggerValue = ev.TryGetProperty("TriggerValue", out var tv) && tv.ValueKind != JsonValueKind.Null
                                ? tv.GetDouble().ToString("F3")
                                : "N/A";
                            var breachPosition = ev.TryGetProperty("BreachPosition", out var bp) && bp.ValueKind != JsonValueKind.Null
                                ? bp.GetDouble().ToString("F3")
                                : "N/A";

                            string avgTemp = "N/A";
                            string maxTemp = "N/A";

                            if (ev.TryGetProperty("ZoneStateSnapshot", out var snapshot))
                            {
                                avgTemp = snapshot.GetProperty("AverageTemperature").GetDouble().ToString("F3");
                                maxTemp = snapshot.GetProperty("MaxTemperature").GetDouble().ToString("F3");
                            }

                            dgvEvents.Rows.Add(
                                timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                                channelId,
                                zoneId,
                                zoneName,
                                alarmType,
                                isActive ? "Yes" : "No",
                                triggerValue,
                                breachPosition,
                                avgTemp,
                                maxTemp
                            );

                            count++;
                        }

                        // Check if results were limited
                        bool isLimited = false;
                        int maxEntries = 0;
                        if (payload.TryGetProperty("IsLimited", out var isLimitedProp) && isLimitedProp.GetBoolean())
                        {
                            isLimited = true;
                            if (payload.TryGetProperty("MaxEntries", out var maxEntriesProp))
                            {
                                maxEntries = maxEntriesProp.GetInt32();
                            }
                        }

                        if (isLimited)
                        {
                            lblStatus.Text = $"Loaded {count} event(s) - LIMITED TO {maxEntries} ENTRIES (date range has more data)";
                            lblStatus.ForeColor = Color.Orange;
                            MessageBox.Show(
                                $"The event history results have been limited to {maxEntries} entries.\n\n" +
                                "The selected date range contains more data than can be displayed.\n" +
                                "Consider using a shorter date range for complete results.",
                                "Results Limited",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                        }
                        else
                        {
                            lblStatus.Text = $"Loaded {count} event(s) successfully";
                            lblStatus.ForeColor = Color.Green;
                        }
                    }
                    else
                    {
                        lblStatus.Text = "No events found";
                        lblStatus.ForeColor = Color.Orange;
                    }
                }
                else
                {
                    string message = "Unknown error";
                    if (response.Value.TryGetProperty("Payload", out var errorPayload) &&
                        errorPayload.TryGetProperty("Message", out var msg))
                    {
                        message = msg.GetString() ?? "Unknown error";
                    }
                    lblStatus.Text = $"Error: {message}";
                    lblStatus.ForeColor = Color.Red;
                    MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Error: {ex.Message}";
                lblStatus.ForeColor = Color.Red;
                MessageBox.Show($"Failed to load events:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnLoad.Enabled = true;
            }
        }

        private void BtnExport_Click(object? sender, EventArgs e)
        {
            try
            {
                if (dgvEvents.Rows.Count == 0)
                {
                    MessageBox.Show("No data to export", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (var sfd = new SaveFileDialog())
                {
                    sfd.Filter = "CSV files (*.csv)|*.csv";
                    sfd.FileName = $"EventHistory_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        var csv = new System.Text.StringBuilder();

                        // Header
                        var headers = dgvEvents.Columns.Cast<DataGridViewColumn>().Select(c => c.HeaderText);
                        csv.AppendLine(string.Join(",", headers));

                        // Rows
                        foreach (DataGridViewRow row in dgvEvents.Rows)
                        {
                            var cells = row.Cells.Cast<DataGridViewCell>().Select(c => $"\"{c.Value}\"");
                            csv.AppendLine(string.Join(",", cells));
                        }

                        System.IO.File.WriteAllText(sfd.FileName, csv.ToString());
                        MessageBox.Show($"Exported {dgvEvents.Rows.Count} events to:\n{sfd.FileName}", "Export Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
