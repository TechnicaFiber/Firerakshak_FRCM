using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using FRCM.Services;

namespace FRCM.View
{
    public partial class FormUsbConfiguration : Form
    {
        public static FormUsbConfiguration? Instance { get; private set; }

        private readonly CustomWebSocketClient _wsClient;
        private readonly string _loginRole;

        private ListBox lstAuthorizedIds;
        private ListBox lstConnectedDrives;
        private Button btnRead;
        private Button btnAdd;
        private Button btnDelete;
        private Button btnSave;
        private Button btnClose;
        private RichTextBox txtOutput;

        public event Action? UsbConfigurationClosed;
        public event Action<string>? UsbOperationStatus;

        public FormUsbConfiguration(CustomWebSocketClient wsClient, string loginRole)
        {
            _wsClient = wsClient ?? throw new ArgumentNullException(nameof(wsClient));
            _loginRole = loginRole;

            Instance = this;

            InitializeComponent();
            InitializeLayout();

            this.WindowState = FormWindowState.Normal;
            this.StartPosition = FormStartPosition.CenterParent;

            this.Load += (s, e) =>
            {
                FRCM.Services.FontSizeHelper.UpdateControlRecursive(
                    this,
                    FRCM.Services.FontSizeHelper.CurrentMultiplier
                );
            };
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Text = "USB Configuration";
            this.Size = new Size(800, 600);
            this.MinimumSize = new Size(600, 500);
            this.BackColor = Color.White;
            this.ResumeLayout(false);
        }

        private void InitializeLayout()
        {
            var mainPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(20) };
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); 
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 40));  
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 60)); 
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); 
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 40));  

            var lblHeading = new Label { Text = "Available Device IDs", Font = new Font("Segoe UI", 10, FontStyle.Bold), AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            mainPanel.Controls.Add(lblHeading, 0, 0);

            lstAuthorizedIds = new ListBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10), SelectionMode = SelectionMode.One };
            mainPanel.Controls.Add(lstAuthorizedIds, 0, 1);

            var buttonPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 7, RowCount = 1 };
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); 
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); 

            btnRead = CreateButton("Read");
            btnAdd = CreateButton("Scan");
            btnDelete = CreateButton("Delete");
            btnSave = CreateButton("Save");
            btnClose = CreateButton("Close");

            btnRead.Click += async (s, e) => await LoadAuthorizedIds();
            btnAdd.Click += async (s, e) => await BtnAdd_Click();
            btnDelete.Click += BtnDelete_Click;
            btnSave.Click += async (s, e) => await BtnSave_Click();
            btnClose.Click += (s, e) => this.Close();

            buttonPanel.Controls.Add(btnRead, 1, 0);
            buttonPanel.Controls.Add(btnAdd, 2, 0);
            buttonPanel.Controls.Add(btnDelete, 3, 0);
            buttonPanel.Controls.Add(btnSave, 4, 0);
            buttonPanel.Controls.Add(btnClose, 5, 0);

            foreach (Control btn in buttonPanel.Controls) btn.Anchor = AnchorStyles.None;
            mainPanel.Controls.Add(buttonPanel, 0, 2);

            var lblLogsHeading = new Label { Text = "Logs", Font = new Font("Segoe UI", 10, FontStyle.Bold), AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            mainPanel.Controls.Add(lblLogsHeading, 0, 3);

            txtOutput = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.FromArgb(240, 240, 240), Font = new Font("Consolas", 9) };
            mainPanel.Controls.Add(txtOutput, 0, 4);

            this.Controls.Add(mainPanel);
        }

        private Button CreateButton(string text) { return new Button { Text = text, Width = 100, Height = 35, UseVisualStyleBackColor = true, Margin = new Padding(0, 0, 10, 0) }; }

        private void AppendOutput(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (InvokeRequired) { Invoke(new Action(() => AppendOutput(text))); return; }
            txtOutput.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
            txtOutput.ScrollToCaret();
            UsbOperationStatus?.Invoke(text);
        }

        private async Task LoadAuthorizedIds(bool silent = false)
        {
            try
            {
                if (!silent) AppendOutput("Reading authorized USB IDs from server...");
                var response = await _wsClient.SendCommandAsync("USBConfigurationCommand", new { SubCommand = "Read" });
                if (response.HasValue && response.Value.TryGetProperty("Payload", out var payload))
                {
                    if (payload.TryGetProperty("Success", out var success) && success.GetBoolean())
                    {
                        if (payload.TryGetProperty("AuthorizedUsbSerialIds", out var idsProp))
                        {
                            var ids = new List<string>();
                            foreach (var id in idsProp.EnumerateArray()) ids.Add(id.GetString() ?? "");
                            SetAuthorizedIds(ids);
                            if (!silent) AppendOutput($"Loaded {ids.Count} authorized IDs.");
                        }
                    }
                    else if (payload.TryGetProperty("Error", out var error)) { if (!silent) AppendOutput($"Error: {error.GetString()}"); }
                }
            }
            catch (Exception ex) { if (!silent) AppendOutput($"Failed to read configuration: {ex.Message}"); }
        }

        public void SetAuthorizedIds(List<string> ids)
        {
            if (InvokeRequired) { Invoke(new Action(() => SetAuthorizedIds(ids))); return; }
            lstAuthorizedIds.Items.Clear();
            if (ids != null) { foreach (var id in ids.Where(i => !string.IsNullOrEmpty(i))) lstAuthorizedIds.Items.Add(id); }
        }

        private async Task BtnAdd_Click()
        {
            AppendOutput("Scanning for USB devices on local machine...");
            var devices = await GetLocalUsbSerials();

            if (devices.Count == 0) 
            { 
                MessageBox.Show("No USB devices detected on your Windows PC.", "Scan Result", MessageBoxButtons.OK, MessageBoxIcon.Information); 
                return; 
            }

            using (var form = new Form())
            {
                var list = new ListBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9) };
                foreach (var id in devices) list.Items.Add(id);
                var btnOk = new Button { Text = "Add Selected", Dock = DockStyle.Bottom, Height = 40, UseVisualStyleBackColor = true };
                btnOk.Click += (s, ev) => { form.DialogResult = DialogResult.OK; form.Close(); };
                form.Text = "Select USB to Add (Local PC)"; 
                form.Controls.Add(list); 
                form.Controls.Add(btnOk); 
                form.Size = new Size(500, 400); 
                form.StartPosition = FormStartPosition.CenterParent; 
                form.FormBorderStyle = FormBorderStyle.FixedDialog;

                if (form.ShowDialog() == DialogResult.OK && list.SelectedItem != null)
                {
                    string serial = list.SelectedItem.ToString()!.Trim();
                    if (!string.IsNullOrEmpty(serial))
                    {
                        bool alreadyExists = lstAuthorizedIds.Items.Cast<string>()
                            .Any(item => string.Equals(item, serial, StringComparison.OrdinalIgnoreCase));

                        if (alreadyExists)
                        {
                            MessageBox.Show($"Device {serial} is already present.", "Duplicate Device", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        else
                        {
                            lstAuthorizedIds.Items.Add(serial);
                            AppendOutput($"Added local serial: {serial}");
                        }
                    }
                }
            }
        }

        private async Task<List<string>> GetLocalUsbSerials()
        {
            var detectedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                // COMBINED SCAN: 
                // 1. PnP scan gets the full 128-char serial for SanDisk.
                // 2. DiskDrive scan gets the '123456789' for Raspberry Pi.
                string psScript = @"
                    $results = @();
                    # Get Instance IDs from Mass Storage
                    $results += Get-PnpDevice -Status OK -Class 'USB' | Where-Object { $_.FriendlyName -match 'Mass Storage' } | Select-Object -ExpandProperty InstanceId | ForEach-Object { 
                        $s = $_.Split('\')[-1]; if ($s.Contains('&')) { $s = $s.Split('&')[0] }; $s 
                    };
                    # Get SerialNumbers from Disk Drives
                    $results += Get-CimInstance Win32_DiskDrive | Where-Object { $_.InterfaceType -eq 'USB' } | Select-Object -ExpandProperty SerialNumber;
                    
                    $results | ForEach-Object { $_.Trim() } | Where-Object { $_.Length -gt 2 } | Select-Object -Unique
                ";

                var psi = new ProcessStartInfo 
                { 
                    FileName = "powershell", 
                    Arguments = $"-NoProfile -Command \"{psScript.Replace("\"", "\\\"")}\"", 
                    RedirectStandardOutput = true, 
                    UseShellExecute = false, 
                    CreateNoWindow = true 
                };

                using var process = Process.Start(psi);
                if (process != null) 
                { 
                    string output = await process.StandardOutput.ReadToEndAsync(); 
                    await process.WaitForExitAsync(); 
                    foreach (var line in output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string id = line.Trim();
                        if (!string.IsNullOrEmpty(id)) detectedIds.Add(id);
                    }
                }
            }
            catch (Exception ex) { AppendOutput($"Local scan error: {ex.Message}"); }

            var list = detectedIds.ToList();
            // Final step: If a short ID is just the start of a long ID, keep only the long one.
            return list.Where(id => !list.Any(other => other.Length > id.Length && other.StartsWith(id, StringComparison.OrdinalIgnoreCase))).ToList();
        }

        private void BtnDelete_Click(object? sender, EventArgs e)
        {
            if (lstAuthorizedIds.SelectedItem == null) return;
            string serial = lstAuthorizedIds.SelectedItem.ToString()!;
            lstAuthorizedIds.Items.Remove(serial); AppendOutput($"Removed: {serial}");
        }

        private async Task BtnSave_Click()
        {
            if (_loginRole != "Admin") { MessageBox.Show("Admin role required to save.", "Access Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            try
            {
                AppendOutput("Saving configuration to server...");
                var ids = lstAuthorizedIds.Items.Cast<string>().ToList();
                var response = await _wsClient.SendCommandAsync("USBConfigurationCommand", new { SubCommand = "Save", AuthorizedUsbSerialIds = ids });
                if (response.HasValue && response.Value.TryGetProperty("Payload", out var payload))
                {
                    if (payload.TryGetProperty("Success", out var success) && success.GetBoolean()) { AppendOutput("Configuration saved successfully."); MessageBox.Show("Saved successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information); }
                    else { string err = payload.TryGetProperty("Error", out var e) ? e.GetString()! : "Unknown error"; AppendOutput($"Save failed: {err}"); MessageBox.Show($"Save failed: {err}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                }
            }
            catch (Exception ex) { AppendOutput($"Save error: {ex.Message}"); }
        }

        protected override void OnFormClosing(FormClosingEventArgs e) { base.OnFormClosing(e); UsbConfigurationClosed?.Invoke(); Instance = null; }
    }
}
