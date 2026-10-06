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
    public partial class AuditTrailForm : Form
    {
        private readonly CustomWebSocketClient _wsClient;
        private DataGridView dgvAudit;
        private DateTimePicker dtpFrom;
        private DateTimePicker dtpTo;
        private Button btnLoad;
        private Button btnExport;
        private Label lblStatus;
        private Label lblFrom;
        private Label lblTo;
        private ComboBox cboConfigType;
        private Label lblConfigType;

        public AuditTrailForm(CustomWebSocketClient wsClient)
        {
            _wsClient = wsClient;
            this.WindowState = FormWindowState.Maximized;
            InitializeComponent();
            //LoadLast7DaysAudit();
        }

     
        private void InitializeComponent()
        {
            this.Text = "Audit Trail - Configuration Changes";
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
                ColumnCount = 9,
                RowCount = 2,
                Padding = new Padding(10)
            };
            controlsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // From Label
            controlsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25)); // From Picker
            controlsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // To Label
            controlsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25)); // To Picker
            controlsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Type Label
            controlsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20)); // Type Combo
            controlsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15)); // Load Button
            controlsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15)); // Export Button
            controlsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20)); // Spacer

            controlsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            controlsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            // ===== Controls Initialization =====
            lblFrom = new Label { Text = "From:", Anchor = AnchorStyles.Left, AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            dtpFrom = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd HH:mm:ss", Dock = DockStyle.Fill, Margin = new Padding(3) };
            lblTo = new Label { Text = "To:", Anchor = AnchorStyles.Left, AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            dtpTo = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd HH:mm:ss", Dock = DockStyle.Fill, Margin = new Padding(3) };
            lblConfigType = new Label { Text = "Type:", Anchor = AnchorStyles.Left, AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            cboConfigType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, Margin = new Padding(3) };
            cboConfigType.Items.AddRange(new object[] { "All", "Channel", "Zone", "System", "User", "Network" });
            cboConfigType.SelectedIndex = 0;
            btnLoad = new Button { Text = "Load Audit", Dock = DockStyle.Fill, Margin = new Padding(3) };
            btnLoad.Click += BtnLoad_Click;
            btnExport = new Button { Text = "Export to Excel", Dock = DockStyle.Fill, Margin = new Padding(3) };
            btnExport.Click += BtnExport_Click;
            lblStatus = new Label { Text = "Ready", Anchor = AnchorStyles.Left, AutoSize = true, ForeColor = Color.Blue, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };

            // ===== Add Controls to Top Layout =====
            controlsLayout.Controls.Add(lblFrom, 0, 0);
            controlsLayout.Controls.Add(dtpFrom, 1, 0);
            controlsLayout.Controls.Add(lblTo, 2, 0);
            controlsLayout.Controls.Add(dtpTo, 3, 0);
            controlsLayout.Controls.Add(lblConfigType, 4, 0);
            controlsLayout.Controls.Add(cboConfigType, 5, 0);
            controlsLayout.Controls.Add(btnLoad, 6, 0);
            controlsLayout.Controls.Add(btnExport, 7, 0);
            controlsLayout.Controls.Add(lblStatus, 0, 1);
            controlsLayout.SetColumnSpan(lblStatus, 8);
            
            // ===== DataGridView =====
            dgvAudit = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                ScrollBars = ScrollBars.Both
            };
            dgvAudit.Columns.Add(new DataGridViewTextBoxColumn { Name = "Timestamp", HeaderText = "Timestamp" });
            dgvAudit.Columns.Add(new DataGridViewTextBoxColumn { Name = "ChangedBy", HeaderText = "Changed By" });
            dgvAudit.Columns.Add(new DataGridViewTextBoxColumn { Name = "ConfigType", HeaderText = "Config Type" });
            dgvAudit.Columns.Add(new DataGridViewTextBoxColumn { Name = "Key", HeaderText = "Configuration Key" });
            dgvAudit.Columns.Add(new DataGridViewTextBoxColumn { Name = "OldValue", HeaderText = "Old Value", MinimumWidth = 300 });
            dgvAudit.Columns.Add(new DataGridViewTextBoxColumn { Name = "NewValue", HeaderText = "New Value", MinimumWidth = 300 });
            dgvAudit.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dgvAudit.RowsDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dgvAudit.RowTemplate.MinimumHeight = 40;

            // ===== Add Panels to Main Layout =====
            mainLayout.Controls.Add(controlsLayout, 0, 0);
            mainLayout.Controls.Add(dgvAudit, 0, 1);
            this.Controls.Add(mainLayout);
        }

        private string MakeGridFriendly(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            // Add line break after each comma
            return text.Replace(",", ",\n");
        }

        //private async void LoadLast7DaysAudit()
        //{
        //    await LoadAuditTrail();
        //}

        private async void BtnLoad_Click(object? sender, EventArgs e)
        {
            await LoadAuditTrail();
        }

        //    private async Task LoadAuditTrail()
        //    {
        //        try
        //        {
        //            btnLoad.Enabled = false;
        //            lblStatus.Text = "Loading audit trail...";
        //            lblStatus.ForeColor = Color.Blue;

        //            var request = new
        //            {
        //                MessageType = "get_audit_trail",
        //                //Payload = new
        //                //{
        //                //    From = dtpFrom.Value.ToUniversalTime(),
        //                //    To = dtpTo.Value.ToUniversalTime()
        //                //}
        //                object payload;

        //            if (dtpFrom.Checked || dtpTo.Checked)
        //            {
        //                payload = new
        //                {
        //                    From = dtpFrom.Checked
        //                        ? dtpFrom.Value.ToUniversalTime()
        //                        : (DateTime?)null,

        //                    To = dtpTo.Checked
        //                        ? dtpTo.Value.ToUniversalTime()
        //                        : (DateTime?)null
        //                };
        //            }
        //            else
        //            {
        //                payload = new { }; // 🔥 NO DATE FILTER → ALL DAYS
        //            }

        //            var response = await _wsClient.SendCommandAsync("get_audit_trail", payload);

        //        };

        //            string requestJson = JsonSerializer.Serialize(request);
        //            var response = await _wsClient.SendCommandAsync("get_audit_trail", request.Payload);

        //            if (!response.HasValue)
        //            {
        //                lblStatus.Text = "Error: No response from server";
        //                lblStatus.ForeColor = Color.Red;
        //                return;
        //            }

        //            if (response.Value.TryGetProperty("Payload", out var payload) &&
        //                payload.TryGetProperty("Success", out var success) && success.GetBoolean())
        //            {
        //                dgvAudit.SuspendLayout();

        //                dgvAudit.Rows.Clear();

        //                if (payload.TryGetProperty("AuditTrail", out var auditArray))
        //                {
        //                    int count = 0;
        //                    string filterType = cboConfigType.SelectedItem?.ToString() ?? "All";

        //                    foreach (var entry in auditArray.EnumerateArray())
        //                    {
        //                        // Use TryGetProperty for all fields to handle missing properties gracefully
        //                        DateTime timestamp = DateTime.Now;
        //                        if (entry.TryGetProperty("Timestamp", out var tsProp))
        //                        {
        //                            timestamp = tsProp.GetDateTime().ToLocalTime();
        //                        }

        //                        string changedBy = "Unknown";
        //                        if (entry.TryGetProperty("ChangedBy", out var cbProp))
        //                        {
        //                            changedBy = cbProp.GetString() ?? "Unknown";
        //                        }

        //                        string configType = "";
        //                        // Try both "ConfigurationType" (from ISystemConfigurationManager) and "ConfigType" (from Core.Models)
        //                        if (entry.TryGetProperty("ConfigurationType", out var ctProp) ||
        //                            entry.TryGetProperty("ConfigType", out ctProp))
        //                        {
        //                            configType = ctProp.GetString() ?? "";
        //                        }

        //                        string key = "";
        //                        if (entry.TryGetProperty("Key", out var keyProp))
        //                        {
        //                            key = keyProp.GetString() ?? "";
        //                        }

        //                        //var oldValue = entry.TryGetProperty("OldValue", out var ov) ? ov.ToString() : "N/A";
        //                        //var newValue = entry.TryGetProperty("NewValue", out var nv) ? nv.ToString() : "N/A";
        //                        var oldValue = entry.TryGetProperty("OldValue", out var ov)
        //? MakeGridFriendly(ov.ToString())
        //: "N/A";

        //                        var newValue = entry.TryGetProperty("NewValue", out var nv)
        //                            ? MakeGridFriendly(nv.ToString())
        //                            : "N/A";


        //                        // Apply filter
        //                        if (filterType != "All" && configType != filterType)
        //                            continue;

        //                        dgvAudit.Rows.Add(
        //                            timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
        //                            changedBy,
        //                            configType,
        //                            key,
        //                            oldValue,
        //                            newValue
        //                        );

        //                        count++;
        //                    }

        //                    dgvAudit.AutoResizeRows(DataGridViewAutoSizeRowsMode.AllCells);

        //                    // ✅ RESUME LAYOUT ONCE
        //                    dgvAudit.ResumeLayout();
        //                    dgvAudit.ClearSelection();

        //                    // Check if results were limited
        //                    bool isLimited = false;
        //                    int maxEntries = 0;
        //                    if (payload.TryGetProperty("IsLimited", out var isLimitedProp) && isLimitedProp.GetBoolean())
        //                    {
        //                        isLimited = true;
        //                        if (payload.TryGetProperty("MaxEntries", out var maxEntriesProp))
        //                        {
        //                            maxEntries = maxEntriesProp.GetInt32();
        //                        }
        //                    }

        //                    if (isLimited)
        //                    {
        //                        lblStatus.Text = $"Loaded {count} audit entry(ies) - LIMITED TO {maxEntries} ENTRIES (date range has more data)";
        //                        lblStatus.ForeColor = Color.Orange;
        //                        MessageBox.Show(
        //                            $"The audit trail results have been limited to {maxEntries} entries.\n\n" +
        //                            "The selected date range contains more data than can be displayed.\n" +
        //                            "Consider using a shorter date range for complete results.",
        //                            "Results Limited",
        //                            MessageBoxButtons.OK,
        //                            MessageBoxIcon.Warning);
        //                    }
        //                    else
        //                    {
        //                        lblStatus.Text = $"Loaded {count} audit entry(ies) successfully";
        //                        lblStatus.ForeColor = Color.Green;
        //                    }
        //                }
        //                else
        //                {
        //                    lblStatus.Text = "No audit entries found";
        //                    lblStatus.ForeColor = Color.Orange;
        //                }
        //            }
        //            else
        //            {
        //                string message = "Unknown error";
        //                if (response.Value.TryGetProperty("Payload", out var errorPayload) &&
        //                    errorPayload.TryGetProperty("Message", out var msg))
        //                {
        //                    message = msg.GetString() ?? "Unknown error";
        //                }
        //                lblStatus.Text = $"Error: {message}";
        //                lblStatus.ForeColor = Color.Red;

        //                if (message == "Admin authentication required")
        //                {
        //                    MessageBox.Show("Access Denied\n\nOnly administrators can view the audit trail.",
        //                        "Admin Access Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        //                }
        //                else
        //                {
        //                    MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //                }
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            lblStatus.Text = $"Error: {ex.Message}";
        //            lblStatus.ForeColor = Color.Red;
        //            MessageBox.Show($"Failed to load audit trail:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //        }
        //        finally
        //        {
        //            btnLoad.Enabled = true;
        //        }
        //    }
        private async Task LoadAuditTrail()
        {
            try
            {
                btnLoad.Enabled = false;
                lblStatus.Text = "Loading audit trail...";
                lblStatus.ForeColor = Color.Blue;

                // ✅ Validate date range
                if (dtpFrom.Checked && dtpTo.Checked && dtpFrom.Value > dtpTo.Value)
                {
                    MessageBox.Show(
                        "From date cannot be later than To date.",
                        "Invalid Date Range",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                // ✅ Build payload CONDITIONALLY
                object payload;

                if (dtpFrom.Checked || dtpTo.Checked)
                {
                    payload = new
                    {
                        From = dtpFrom.Checked
                            ? dtpFrom.Value.ToUniversalTime()
                            : (DateTime?)null,

                        To = dtpTo.Checked
                            ? dtpTo.Value.ToUniversalTime()
                            : (DateTime?)null
                    };
                }
                else
                {
                    payload = new { }; // 🔥 NO DATE FILTER → ALL DAYS
                }

                // ✅ SINGLE WebSocket call
                var response = await _wsClient.SendCommandAsync("get_audit_trail", payload);

                if (!response.HasValue)
                {
                    lblStatus.Text = "Error: No response from server";
                    lblStatus.ForeColor = Color.Red;
                    return;
                }

                if (response.Value.TryGetProperty("Payload", out var responsePayload) &&
                    responsePayload.TryGetProperty("Success", out var success) &&
                    success.GetBoolean())
                {
                    dgvAudit.SuspendLayout();
                    dgvAudit.Rows.Clear();

                    if (responsePayload.TryGetProperty("AuditTrail", out var auditArray))
                    {
                        int count = 0;
                        string filterType = cboConfigType.SelectedItem?.ToString() ?? "All";

                        foreach (var entry in auditArray.EnumerateArray())
                        {
                            DateTime timestamp = entry.TryGetProperty("Timestamp", out var ts)
                                ? ts.GetDateTime().ToLocalTime()
                                : DateTime.Now;

                            string changedBy = entry.TryGetProperty("ChangedBy", out var cb)
                                ? cb.GetString() ?? "Unknown"
                                : "Unknown";

                            string configType = entry.TryGetProperty("ConfigurationType", out var ct) ||
                                                entry.TryGetProperty("ConfigType", out ct)
                                ? ct.GetString() ?? ""
                                : "";

                            if (filterType != "All" && configType != filterType)
                                continue;

                            string key = entry.TryGetProperty("Key", out var k) ? k.GetString() ?? "" : "";

                            string oldValue = entry.TryGetProperty("OldValue", out var ov)
                                ? MakeGridFriendly(ov.ToString())
                                : "N/A";

                            string newValue = entry.TryGetProperty("NewValue", out var nv)
                                ? MakeGridFriendly(nv.ToString())
                                : "N/A";

                            dgvAudit.Rows.Add(
                                timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                                changedBy,
                                configType,
                                key,
                                oldValue,
                                newValue
                            );

                            count++;
                        }

                        dgvAudit.AutoResizeRows(DataGridViewAutoSizeRowsMode.AllCells);
                        dgvAudit.ResumeLayout();
                        dgvAudit.ClearSelection();

                        lblStatus.Text = $"Loaded {count} audit entry(ies) successfully";
                        lblStatus.ForeColor = Color.Green;
                    }
                    else
                    {
                        lblStatus.Text = "No audit entries found";
                        lblStatus.ForeColor = Color.Orange;
                    }
                }
                else
                {
                    lblStatus.Text = "Failed to load audit trail";
                    lblStatus.ForeColor = Color.Red;
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Error: {ex.Message}";
                lblStatus.ForeColor = Color.Red;
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnLoad.Enabled = true;
            }
        }

        private void ExportAuditTrailAsExcel()
        {
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "Excel Workbook (*.xlsx)|*.xlsx";
                sfd.FileName = $"AuditTrail_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

                if (sfd.ShowDialog() != DialogResult.OK)
                    return;

                using (var package = new OfficeOpenXml.ExcelPackage())
                {
                    var ws = package.Workbook.Worksheets.Add("AuditTrail");

                    // Headers
                    for (int c = 0; c < dgvAudit.Columns.Count; c++)
                    {
                        ws.Cells[1, c + 1].Value = dgvAudit.Columns[c].HeaderText;
                        ws.Cells[1, c + 1].Style.Font.Bold = true;
                    }

                    // Data
                    for (int r = 0; r < dgvAudit.Rows.Count; r++)
                    {
                        for (int c = 0; c < dgvAudit.Columns.Count; c++)
                        {
                            ws.Cells[r + 2, c + 1].Value =
                                dgvAudit.Rows[r].Cells[c].Value?.ToString();
                        }
                    }

                    // ✅ REQUIRED FORMATTING
                    ws.Cells.Style.WrapText = true;
                    ws.Cells.Style.VerticalAlignment =
                        OfficeOpenXml.Style.ExcelVerticalAlignment.Top;

                    ws.Column(1).Width = 22; // Timestamp
                    ws.Column(2).Width = 20; // User
                    ws.Column(3).Width = 15; // Type
                    ws.Column(4).Width = 30; // Key
                    ws.Column(5).Width = 45; // Old Value
                    ws.Column(6).Width = 45; // New Value

                    ws.Row(1).Height = 35;
                    ws.DefaultRowHeight = 15;

                    package.SaveAs(new FileInfo(sfd.FileName));
                }

                MessageBox.Show(
                    "Audit trail exported successfully.",
                    "Export Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(sfd.FileName)
                    {
                        UseShellExecute = true
                    });
            }
        }

        private void BtnExport_Click(object? sender, EventArgs e)
        {
            if (dgvAudit.Rows.Count == 0)
            {
                MessageBox.Show("No data to export", "Export",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ExportAuditTrailAsExcel(); // 👈 internal change only
        }

    }
}
