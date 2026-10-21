using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Text.Json;
using System.Windows.Forms;
using FRCM.Services;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace FRCM.View
{
    public partial class FormIpConfiguration : Form
    {
        public class InterfaceInfo
        {
            public string MacAddress { get; set; } = "";
            public string InterfaceName { get; set; } = "";
            public string Description { get; set; } = "";
            public string IpAddress { get; set; } = "";
            public string Status { get; set; } = "";

            public override string ToString()
            {
                return $"{InterfaceName} ({MacAddress}) - {IpAddress} [{Status}]";
            }
        }

        public static FormIpConfiguration? Instance { get; private set; }

        private readonly CustomWebSocketClient _wsClient;
        private readonly string _loginRole;

        // ================= NETRA1 UI =================
        private TextBox txtCurrentIP;
        private TextBox txtNewIP;
        private TextBox txtOutput;
        private Button btnGetIP;
        private Button btnSetIP;
        private Button btnCancel;
        private TextBox txtCurrentSubnet;
        private TextBox txtNewSubnet;

        private TextBox txtCurrentGateway;
        private TextBox txtNewGateway;

        // ================= NETRA2 UI =================
        private TextBox txtCurrentNetraIP;
        private TextBox txtNewNetraIP;
        private Button btnGetNetraIP;
        private Button btnSetNetraIP;
        private TextBox txtCurrentNetraSubnet;
        private TextBox txtNewNetraSubnet;

        private TextBox txtCurrentNetraGateway;
        private TextBox txtNewNetraGateway;

        // ================= PORT CONFIG UI =================
        private ComboBox cmbNetra1Port;
        private ComboBox cmbNetra2Port;
        private Button btnRefreshPorts;
        private Button btnSavePortConfig;
        private List<InterfaceInfo> _availableInterfaces = new();


        // ================= DTS CONNECTION UI =================
        private TextBox txtDtsIP;
        private TextBox txtDtsPort;
        private Button btnSaveDtsConnection;

        // ================= PERSISTED VALUES =================
        private static string _lastCurrentFrmcIp = "";
        private static string _lastNewFrmcIp = "";
        private static string _lastCurrentNetraIp = "";
        private static string _lastNewNetraIp = "";
        private string _dtscmIp = "";

        public event Action? IpConfigurationClosed;
        public event Action<string>? IpOperationStatus;

        public FormIpConfiguration(CustomWebSocketClient wsClient, string loginRole)
        {
            _wsClient = wsClient ?? throw new ArgumentNullException(nameof(wsClient));
            _loginRole = loginRole;

            Instance = this;
            LoadDtscmIp();

            InitializeComponent();
            InitializeLayout();

            this.WindowState = FormWindowState.Maximized;

            this.Load += (s, e) =>
            {
                FRCM.Services.FontSizeHelper.UpdateControlRecursive(
                    this,
                    FRCM.Services.FontSizeHelper.CurrentMultiplier
                );
                LoadDtsConnectionSettings();
            };
        }

        // ================= HELPERS =================
        private Label CreateAlignedLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 6, 0, 6)
            };
        }

        private Button CreateButton(string text, bool enabled = true)
        {
            return new Button
            {
                Text = text,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 6, 12, 6),
                Enabled = enabled,
                Margin = new Padding(4),
                UseVisualStyleBackColor = true
            };
        }


        // ================= UI LAYOUT =================
        private void InitializeLayout()
        {
            Text = "Netra IP Configuration";
            Size = new Size(1100, 850);
            MinimumSize = new Size(850, 650);
            StartPosition = FormStartPosition.CenterScreen;

            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
                ColumnCount = 2,
                RowCount = 5
            };

            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            Controls.Add(root);

            // ================= NETRA1 IP =================
            btnGetIP = CreateButton("Get IP");
            btnGetIP.Click += BtnGetIP_Click;

            btnSetIP = CreateButton("Set IP", _loginRole == "Admin");
            btnSetIP.Click += BtnSetPersistentIP_Click;

            txtCurrentIP = new TextBox { ReadOnly = true, Dock = DockStyle.Fill, Margin = new Padding(4) };
            txtNewIP = new TextBox { Dock = DockStyle.Fill, MaxLength = 15, Margin = new Padding(4) };

            var netra1Layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 7,
                Padding = new Padding(8),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            netra1Layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));    // Labels
            netra1Layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F)); // Buttons (Fixed width)
            netra1Layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); // Textboxes

            netra1Layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            netra1Layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            netra1Layout.Controls.Add(CreateAlignedLabel("Current IP:"), 0, 0);
            netra1Layout.Controls.Add(btnGetIP, 1, 0);
            netra1Layout.Controls.Add(txtCurrentIP, 2, 0);

            netra1Layout.Controls.Add(CreateAlignedLabel("New IP:"), 0, 1);
            netra1Layout.Controls.Add(btnSetIP, 1, 1);
            netra1Layout.Controls.Add(txtNewIP, 2, 1);

            // Subnet Mask
            netra1Layout.Controls.Add(CreateAlignedLabel("Current Subnet:"), 0, 3);
            txtCurrentSubnet = new TextBox { ReadOnly = true, Dock = DockStyle.Fill };
            netra1Layout.Controls.Add(txtCurrentSubnet, 2, 3);

            netra1Layout.Controls.Add(CreateAlignedLabel("New Subnet:"), 0, 4);
            txtNewSubnet = new TextBox { Dock = DockStyle.Fill };
            netra1Layout.Controls.Add(txtNewSubnet, 2, 4);

            // Gateway
            netra1Layout.Controls.Add(CreateAlignedLabel("Current Gateway:"), 0, 5);
            txtCurrentGateway = new TextBox { ReadOnly = true, Dock = DockStyle.Fill };
            netra1Layout.Controls.Add(txtCurrentGateway, 2, 5);

            netra1Layout.Controls.Add(CreateAlignedLabel("New Gateway:"), 0, 6);
            txtNewGateway = new TextBox { Dock = DockStyle.Fill };
            netra1Layout.Controls.Add(txtNewGateway, 2, 6);

            var grpNetra1 = new GroupBox
            {
                Text = "Netra1 IP",
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(5)
            };
            grpNetra1.Controls.Add(netra1Layout);

            root.Controls.Add(grpNetra1, 0, 0);

            // ================= NETRA2 IP =================
            btnGetNetraIP = CreateButton("Get IP");
            btnGetNetraIP.Click += BtnGetNetraIP_Click;

            btnSetNetraIP = CreateButton("Set IP", _loginRole == "Admin");
            btnSetNetraIP.Click += BtnSetPersistentNetraIP_Click;

            txtCurrentNetraIP = new TextBox { ReadOnly = true, Dock = DockStyle.Fill, Margin = new Padding(4) };
            txtNewNetraIP = new TextBox { Dock = DockStyle.Fill, MaxLength = 15, Margin = new Padding(4) };

            var netra2Layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 7,
                Padding = new Padding(8),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            netra2Layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));    // Labels
            netra2Layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F)); // Buttons (Fixed width for consistency)
            netra2Layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); // Textboxes (Fills remainder)

            netra2Layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            netra2Layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            netra2Layout.Controls.Add(CreateAlignedLabel("Current IP:"), 0, 0);
            netra2Layout.Controls.Add(btnGetNetraIP, 1, 0);
            netra2Layout.Controls.Add(txtCurrentNetraIP, 2, 0);

            netra2Layout.Controls.Add(CreateAlignedLabel("New IP:"), 0, 1);
            netra2Layout.Controls.Add(btnSetNetraIP, 1, 1);
            netra2Layout.Controls.Add(txtNewNetraIP, 2, 1);

            // Subnet
            netra2Layout.Controls.Add(CreateAlignedLabel("Current Subnet:"), 0, 3);
            txtCurrentNetraSubnet = new TextBox { ReadOnly = true, Dock = DockStyle.Fill };
            netra2Layout.Controls.Add(txtCurrentNetraSubnet, 2, 3);

            netra2Layout.Controls.Add(CreateAlignedLabel("New Subnet:"), 0, 4);
            txtNewNetraSubnet = new TextBox { Dock = DockStyle.Fill };
            netra2Layout.Controls.Add(txtNewNetraSubnet, 2, 4);

            // Gateway
            netra2Layout.Controls.Add(CreateAlignedLabel("Current Gateway:"), 0, 5);
            txtCurrentNetraGateway = new TextBox { ReadOnly = true, Dock = DockStyle.Fill };
            netra2Layout.Controls.Add(txtCurrentNetraGateway, 2, 5);

            netra2Layout.Controls.Add(CreateAlignedLabel("New Gateway:"), 0, 6);
            txtNewNetraGateway = new TextBox { Dock = DockStyle.Fill };
            netra2Layout.Controls.Add(txtNewNetraGateway, 2, 6);

            var grpNetra2 = new GroupBox
            {
                Text = "Netra2 IP",
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(5)
            };
            grpNetra2.Controls.Add(netra2Layout);

            root.Controls.Add(grpNetra2, 1, 0);

            // ================= PORT CONFIGURATION (Admin Only) =================
            var portConfigLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 2,
                Padding = new Padding(8),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            portConfigLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            portConfigLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            portConfigLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            portConfigLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            portConfigLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            portConfigLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Row 0: Refresh and Save buttons
            btnRefreshPorts = CreateButton("Refresh Available Ports", _loginRole == "Admin");
            btnRefreshPorts.Click += BtnRefreshPorts_Click;

            btnSavePortConfig = CreateButton("Save Port Configuration", _loginRole == "Admin");
            btnSavePortConfig.Click += BtnSavePortConfig_Click;

            portConfigLayout.Controls.Add(btnRefreshPorts, 0, 0);
            portConfigLayout.SetColumnSpan(btnRefreshPorts, 2);
            portConfigLayout.Controls.Add(btnSavePortConfig, 2, 0);
            portConfigLayout.SetColumnSpan(btnSavePortConfig, 2);

            // Row 1: NETRA1 dropdown
            portConfigLayout.Controls.Add(CreateAlignedLabel("NETRA1 Port:"), 0, 1);
            cmbNetra1Port = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = _loginRole == "Admin",
                Margin = new Padding(4),
                Font = new Font("Segoe UI", 10F) // Slightly larger font to match button height
            };
            portConfigLayout.Controls.Add(cmbNetra1Port, 1, 1);

            // Row 1: NETRA2 dropdown
            portConfigLayout.Controls.Add(CreateAlignedLabel("NETRA2 Port:"), 2, 1);
            cmbNetra2Port = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = _loginRole == "Admin",
                Margin = new Padding(4),
                Font = new Font("Segoe UI", 10F) // Slightly larger font to match button height
            };
            portConfigLayout.Controls.Add(cmbNetra2Port, 3, 1);

            var grpPortConfig = new GroupBox
            {
                Text = "Port Configuration (Admin Only - Initial Setup)",
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(5)
            };
            grpPortConfig.Controls.Add(portConfigLayout);

            root.Controls.Add(grpPortConfig, 0, 1);
            root.SetColumnSpan(grpPortConfig, 2);

            // ================= DTS CONNECTION SETTINGS =================
            var dtsConnectionLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 1,
                Padding = new Padding(8),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            dtsConnectionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));    // "DTS IP Address:"
            dtsConnectionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F)); // txtDtsIP
            dtsConnectionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));    // "DTS Port:"
            dtsConnectionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F)); // txtDtsPort
            dtsConnectionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));    // btnSaveDtsConnection

            dtsConnectionLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            txtDtsIP = new TextBox { Dock = DockStyle.Fill, MaxLength = 15, Margin = new Padding(4) };
            txtDtsPort = new TextBox { Dock = DockStyle.Fill, MaxLength = 5, Margin = new Padding(4) };
            btnSaveDtsConnection = CreateButton("Save DTS Settings", _loginRole == "Admin");
            btnSaveDtsConnection.Click += BtnSaveDtsConnection_Click;

            dtsConnectionLayout.Controls.Add(CreateAlignedLabel("DTS IP Address:"), 0, 0);
            dtsConnectionLayout.Controls.Add(txtDtsIP, 1, 0);
            dtsConnectionLayout.Controls.Add(CreateAlignedLabel("DTS Port:"), 2, 0);
            dtsConnectionLayout.Controls.Add(txtDtsPort, 3, 0);
            dtsConnectionLayout.Controls.Add(btnSaveDtsConnection, 4, 0);

            var grpDtsConnection = new GroupBox
            {
                Text = "DTS Connection Configuration (Admin Only)",
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(5)
            };
            grpDtsConnection.Controls.Add(dtsConnectionLayout);

            root.Controls.Add(grpDtsConnection, 0, 2);
            root.SetColumnSpan(grpDtsConnection, 2);

            // ================= OUTPUT =================
            txtOutput = new TextBox
            {
                Multiline = true,
                Dock = DockStyle.Fill,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9F),
                Margin = new Padding(8)
            };

            root.Controls.Add(txtOutput, 0, 3);
            root.SetColumnSpan(txtOutput, 2);

            // ================= CLOSE =================
            btnCancel = CreateButton("Close");
            btnCancel.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);

            btnCancel.Click += (_, __) =>
            {
                Hide();
                IpConfigurationClosed?.Invoke();
            };

            var closeButtonLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            closeButtonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
            closeButtonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));
            closeButtonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));

            closeButtonLayout.Controls.Add(btnCancel, 2, 0);

            root.Controls.Add(closeButtonLayout, 0, 4);
            root.SetColumnSpan(closeButtonLayout, 2);

            RestoreValues();
        }
        // ================= GET NETRA1 IP =================

        private async void BtnGetIP_Click(object sender, EventArgs e)
        {
            AppendOutput("Requesting Netra1 IP via WebSocket...");
            IpOperationStatus?.Invoke("Requesting Netra1 IP...");

            var msg = new WebSocketMessage
            {
                MessageType = "GetNetra1IP",
                Payload = JsonSerializer.SerializeToElement(new { })
            };

            await _wsClient.SendAsync(JsonSerializer.Serialize(msg));
        }

        //public void SetCurrentIp(string ip)
        //{
        //    if (InvokeRequired)
        //    {
        //        Invoke(new Action(() => SetCurrentIp(ip)));
        //        return;
        //    }

        //    _lastCurrentFrmcIp = ip;
        //    txtCurrentIP.Text = ip;
        //    AppendOutput($"Current Netra1 IP: {ip}");
        //    IpOperationStatus?.Invoke($"Netra1 IP received: {ip}");
        //}

        public void SetCurrentIp(string ip, string subnet, string gateway)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => SetCurrentIp(ip, subnet, gateway)));
                return;
            }

            _lastCurrentFrmcIp = ip;
            txtCurrentIP.Text = ip;
            txtCurrentSubnet.Text = subnet;
            txtCurrentGateway.Text = gateway;

            AppendOutput($"IP: {ip}, Subnet: {subnet}, Gateway: {gateway}");
            IpOperationStatus?.Invoke($"Netra1 Config received: {ip} | {subnet} | {gateway}");
        }
        // ================= SET NETRA1 IP =================

        public void OnSetIpSuccess()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(OnSetIpSuccess));
                return;
            }

            AppendOutput("Netra1 IP updated successfully");
            IpOperationStatus?.Invoke("Netra1 IP updated successfully");

            MessageBox.Show(
                "Netra1 IP updated successfully.",
                "Success",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }


        public void OnSetIpFailure(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnSetIpFailure(message)));
                return;
            }

            AppendOutput($"Failed to update Netra1 IP: {message}");
            IpOperationStatus?.Invoke($"Netra1 IP update failed: {message}");

            MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        // ================= OUTPUT =================

        private void AppendOutput(string text)
        {
            txtOutput.AppendText($"{DateTime.Now:HH:mm:ss} - {text}{Environment.NewLine}");
        }

        private void RestoreValues()
        {
            txtCurrentIP.Text = _lastCurrentFrmcIp;
            txtNewIP.Text = _lastNewFrmcIp;

            txtCurrentNetraIP.Text = _lastCurrentNetraIp;
            txtNewNetraIP.Text = _lastNewNetraIp;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private async void BtnGetNetraIP_Click(object sender, EventArgs e)
        {
            AppendOutput("Requesting Netra2 IP via WebSocket...");
            IpOperationStatus?.Invoke("Requesting Netra2 IP...");

            var msg = new WebSocketMessage
            {
                MessageType = "GetNetra2IP",
                Payload = JsonSerializer.SerializeToElement(new { })
            };

            await _wsClient.SendAsync(JsonSerializer.Serialize(msg));
        }

        public void SetCurrentNetra2Ip(string ip, string subnet, string gateway)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => SetCurrentNetra2Ip(ip, subnet, gateway)));
                return;
            }

            _lastCurrentNetraIp = ip;

            txtCurrentNetraIP.Text = ip;
            txtCurrentNetraSubnet.Text = subnet;
            txtCurrentNetraGateway.Text = gateway;

            AppendOutput($"Netra2 -> IP: {ip}, Subnet: {subnet}, Gateway: {gateway}");
            IpOperationStatus?.Invoke($"Netra2 Config received: {ip} | {subnet} | {gateway}");
        }
        public void OnSetNetra2IpSuccess()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(OnSetNetra2IpSuccess));
                return;
            }

            AppendOutput("Netra2 IP updated successfully");
            IpOperationStatus?.Invoke("Netra2 IP updated successfully");

            MessageBox.Show(
                "Netra2 IP updated successfully.",
                "Success",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }
        private void LoadDtscmIp()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

                if (!File.Exists(configPath))
                    return;

                var json = File.ReadAllText(configPath);
                var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("FrmcConfiguration", out var frmcConfig))
                {
                    if (frmcConfig.TryGetProperty("DefaultIpAddress", out var ip))
                    {
                        _dtscmIp = ip.GetString() ?? "";
                    }
                }
            }
            catch
            {
                _dtscmIp = "";
            }
        }

        public void OnSetNetra2IpFailure(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnSetNetra2IpFailure(message)));
                return;
            }

            AppendOutput($"Failed to update Netra2 IP: {message}");
            IpOperationStatus?.Invoke($"Netra2 IP update failed: {message}");

            MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        // ================= PORT CONFIGURATION =================

        private async void BtnRefreshPorts_Click(object? sender, EventArgs e)
        {
            AppendOutput("Fetching available ethernet ports via WebSocket...");

            var msg = new WebSocketMessage
            {
                MessageType = "GetAvailableInterfaces",
                Payload = JsonSerializer.SerializeToElement(new { })
            };

            await _wsClient.SendAsync(JsonSerializer.Serialize(msg));
        }

        private async void BtnSavePortConfig_Click(object? sender, EventArgs e)
        {
            if (_loginRole != "Admin")
            {
                MessageBox.Show("Only Admin users can configure ports.",
                    "Access Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var netra1 = cmbNetra1Port.SelectedItem as InterfaceInfo;
            var netra2 = cmbNetra2Port.SelectedItem as InterfaceInfo;

            if (netra1 == null || netra2 == null)
            {
                MessageBox.Show("Please select both NETRA1 and NETRA2 ports.",
                    "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (netra1.MacAddress == netra2.MacAddress && !string.IsNullOrEmpty(netra1.MacAddress))
            {
                MessageBox.Show("NETRA1 and NETRA2 cannot use the same port.",
                    "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // ================= CHECK DTS / DTSCM RESERVED IPS =================
            List<string> restrictedIps = new List<string>();

            bool netra1IsDts = netra1.IpAddress.StartsWith("192.168.24.", StringComparison.OrdinalIgnoreCase);
            bool netra2IsDts = netra2.IpAddress.StartsWith("192.168.24.", StringComparison.OrdinalIgnoreCase);

            if (netra1IsDts)
                restrictedIps.Add($"{netra1.IpAddress} (DTS)");

            if (netra2IsDts)
                restrictedIps.Add($"{netra2.IpAddress} (DTS)");

            if (netra1.IpAddress == _dtscmIp)
                restrictedIps.Add($"{netra1.IpAddress} (DTSCM)");

            if (netra2.IpAddress == _dtscmIp)
                restrictedIps.Add($"{netra2.IpAddress} (DTSCM)");

            if (restrictedIps.Count > 0)
            {
                string ipList = string.Join("\n", restrictedIps);

                string message =
                    "You are trying to change DTS / DTSCM reserved IP(s).\n\n" +
                    $"IP Address(es):\n{ipList}\n\n" +
                    "Changing these IPs may break DTS communication.\n\n" +
                    "Do you really want to continue?";

                var result = MessageBox.Show(
                    message,
                    "Confirm IP Change",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.No)
                {
                    AppendOutput("User cancelled port configuration.");
                    return;
                }
            }
            // ================================================================

            AppendOutput($"Configuring ports: NETRA1={netra1.InterfaceName}, NETRA2={netra2.InterfaceName}");

            var msg = new WebSocketMessage
            {
                MessageType = "ConfigureEthernetRoles",
                Payload = JsonSerializer.SerializeToElement(new
                {
                    Netra1Mac = netra1.MacAddress,
                    Netra2Mac = netra2.MacAddress,
                    Netra1Name = netra1.InterfaceName,
                    Netra2Name = netra2.InterfaceName
                })
            };

            await _wsClient.SendAsync(JsonSerializer.Serialize(msg));
        }

        public void OnPortConfigurationFailure(string error)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnPortConfigurationFailure(error)));
                return;
            }

            AppendOutput($"Port configuration failed: {error}");
            MessageBox.Show(error, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private async void BtnSetPersistentIP_Click(object? sender, EventArgs e)
        {
            await HandleSetPersistentIpAsync(true);
        }

        private async void BtnSetPersistentNetraIP_Click(object? sender, EventArgs e)
        {
            await HandleSetPersistentIpAsync(false);
        }

        private async Task HandleSetPersistentIpAsync(bool isNetra1)
        {
            if (_loginRole != "Admin")
            {
                MessageBox.Show("Only Admin users can change IP configuration.",
                    "Access Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedPort = isNetra1 
                ? cmbNetra1Port.SelectedItem as InterfaceInfo 
                : cmbNetra2Port.SelectedItem as InterfaceInfo;

            if (selectedPort == null)
            {
                MessageBox.Show("Please select a port in the Port Configuration section first.",
                    "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string newIp = isNetra1 ? txtNewIP.Text.Trim() : txtNewNetraIP.Text.Trim();
            string subnet = isNetra1 ? txtNewSubnet.Text.Trim() : txtNewNetraSubnet.Text.Trim();
            string gateway = isNetra1 ? txtNewGateway.Text.Trim() : txtNewNetraGateway.Text.Trim();

            if (string.IsNullOrWhiteSpace(newIp) || !IsValidIPv4(newIp))
            {
                MessageBox.Show("Invalid IP Address");
                return;
            }

            if (!IsValidIPv4(subnet))
            {
                MessageBox.Show("Invalid Subnet Mask");
                return;
            }

            if (!IsValidIPv4(gateway))
            {
                MessageBox.Show("Invalid Gateway");
                return;
            }

            if (!ConfirmRestrictedIpChange(newIp))
            {
                AppendOutput("User cancelled persistent IP change.");
                return;
            }

            AppendOutput($"Setting Persistent IP for {selectedPort.InterfaceName} to {newIp}...");

            var msg = new WebSocketMessage
            {
                MessageType = "SetPersistentIp",
                Payload = JsonSerializer.SerializeToElement(new
                {
                    InterfaceName = selectedPort.InterfaceName,
                    IpAddress = newIp,
                    SubnetMask = subnet,
                    Gateway = gateway
                })
            };

            await _wsClient.SendAsync(JsonSerializer.Serialize(msg));
        }

        public void OnSetPersistentIpSuccess(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnSetPersistentIpSuccess(message)));
                return;
            }

            AppendOutput($"Success: {message}");
            IpOperationStatus?.Invoke($"Persistent IP configuration: {message}");

            MessageBox.Show(
                message,
                "Success",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }

        public void OnSetPersistentIpFailure(string error)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnSetPersistentIpFailure(error)));
                return;
            }

            AppendOutput($"Error: {error}");
            IpOperationStatus?.Invoke($"Persistent IP failure: {error}");

            MessageBox.Show(error, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private bool IsValidIPv4(string value)
        {
            var parts = value.Split('.');
            return parts.Length == 4 && parts.All(p => byte.TryParse(p, out _));
        }

        public void OnAvailableInterfacesReceived(JsonElement payload)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnAvailableInterfacesReceived(payload)));
                return;
            }

            _availableInterfaces.Clear();
            cmbNetra1Port.Items.Clear();
            cmbNetra2Port.Items.Clear();

            if (!payload.TryGetProperty("Interfaces", out var interfaces))
            {
                AppendOutput("No interfaces found in response");
                return;
            }

            string currentNetra1Mac = "";
            string currentNetra2Mac = "";

            if (payload.TryGetProperty("CurrentConfig", out var config))
            {
                currentNetra1Mac = config.TryGetProperty("Netra1Mac", out var n1) ? n1.GetString() ?? "" : "";
                currentNetra2Mac = config.TryGetProperty("Netra2Mac", out var n2) ? n2.GetString() ?? "" : "";
            }

            foreach (var iface in interfaces.EnumerateArray())
            {
                var info = new InterfaceInfo
                {
                    MacAddress = iface.TryGetProperty("MacAddress", out var mac) ? (mac.GetString() ?? "").ToUpper() : "",
                    InterfaceName = iface.TryGetProperty("InterfaceName", out var name) ? name.GetString() ?? "" : "",
                    Description = iface.TryGetProperty("Description", out var desc) ? desc.GetString() ?? "" : "",
                    IpAddress = iface.TryGetProperty("IpAddress", out var ip) ? ip.GetString() ?? "" : "",
                    Status = iface.TryGetProperty("Status", out var status) ? status.GetString() ?? "" : ""
                };

                _availableInterfaces.Add(info);
                cmbNetra1Port.Items.Add(info);
                cmbNetra2Port.Items.Add(info);

                AppendOutput($"  Found: {info.MacAddress} - {info.Description} | IP: {info.IpAddress} | Status: {info.Status}");
            }

            // Select current config
            for (int i = 0; i < _availableInterfaces.Count; i++)
            {
                if (_availableInterfaces[i].MacAddress.Equals(currentNetra1Mac, StringComparison.OrdinalIgnoreCase))
                    cmbNetra1Port.SelectedIndex = i;
                if (_availableInterfaces[i].MacAddress.Equals(currentNetra2Mac, StringComparison.OrdinalIgnoreCase))
                    cmbNetra2Port.SelectedIndex = i;
            }

            AppendOutput($"Found {_availableInterfaces.Count} ethernet port(s)");
        }

        public void OnPortConfigurationSuccess(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnPortConfigurationSuccess(message)));
                return;
            }

            AppendOutput($"Port configuration saved: {message}");
            MessageBox.Show("Port configuration saved successfully.",
                "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private bool ConfirmRestrictedIpChange(string ip)
        {
            bool isDtsNetwork = ip.StartsWith("192.168.24.", StringComparison.OrdinalIgnoreCase);

            if (!isDtsNetwork && ip != _dtscmIp)
                return true;

            string ipLabel = isDtsNetwork ? $"{ip} (DTS)" : $"{ip} (DTSCM)";

            string message =
                "You are trying to change a DTS / DTSCM reserved IP.\n\n" +
                $"IP Address: {ipLabel}\n\n" +
                "Changing this IP may break DTS communication.\n\n" +
                "Do you really want to continue?";

            var result = MessageBox.Show(
                message,
                "Confirm IP Change",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            return result == DialogResult.Yes;
        }

        private async void LoadDtsConnectionSettings()
        {
            try
            {
                AppendOutput("Requesting DTS connection settings via WebSocket...");
                var msg = new WebSocketMessage
                {
                    MessageType = "GetDtsConnection",
                    Payload = JsonSerializer.SerializeToElement(new { })
                };
                await _wsClient.SendAsync(JsonSerializer.Serialize(msg));
            }
            catch (Exception ex)
            {
                AppendOutput($"Failed to request DTS settings: {ex.Message}");
            }
        }

        private async void BtnSaveDtsConnection_Click(object? sender, EventArgs e)
        {
            if (_loginRole != "Admin")
            {
                MessageBox.Show("Only Admin users can change DTS Connection settings.",
                    "Access Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string ip = txtDtsIP.Text.Trim();
            string portStr = txtDtsPort.Text.Trim();

            if (string.IsNullOrWhiteSpace(ip) || !IsValidIPv4(ip))
            {
                MessageBox.Show("Please enter a valid DTS IP Address.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!int.TryParse(portStr, out int port) || port < 1 || port > 65535)
            {
                MessageBox.Show("Please enter a valid Port (1 - 65535).", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var firstConfirm = MessageBox.Show(
                $"Are you sure you want to change the DTS connection settings to IP: {ip}, Port: {port}?",
                "Confirm Save Settings - Step 1 of 2",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (firstConfirm != DialogResult.Yes)
            {
                AppendOutput("Save DTS settings cancelled at first confirmation.");
                return;
            }

            var secondConfirm = MessageBox.Show(
                "Changing the DTS connection settings will modify the FRMC configuration file and force a DTS reconnection.\n\nAre you absolutely sure you want to proceed?",
                "Confirm Save Settings - Step 2 of 2",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (secondConfirm != DialogResult.Yes)
            {
                AppendOutput("Save DTS settings cancelled at second confirmation.");
                return;
            }

            AppendOutput($"Saving DTS Connection configuration: IP={ip}, Port={port}...");

            var msg = new WebSocketMessage
            {
                MessageType = "UpdateDtsConnection",
                Payload = JsonSerializer.SerializeToElement(new
                {
                    IpAddress = ip,
                    Port = port
                })
            };

            await _wsClient.SendAsync(JsonSerializer.Serialize(msg));
        }

        public void SetDtsConnection(string ip, int port)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => SetDtsConnection(ip, port)));
                return;
            }

            txtDtsIP.Text = ip;
            txtDtsPort.Text = port.ToString();
            AppendOutput($"DTS Connection settings received: IP = {ip}, Port = {port}");
        }

        public void OnUpdateDtsConnectionSuccess()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(OnUpdateDtsConnectionSuccess));
                return;
            }

            AppendOutput("DTS Connection configuration updated successfully.");
            MessageBox.Show("DTS Connection configuration updated successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public void OnUpdateDtsConnectionFailure(string error)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnUpdateDtsConnectionFailure(error)));
                return;
            }

            AppendOutput($"Failed to update DTS Connection configuration: {error}");
            MessageBox.Show($"Failed to update DTS Connection configuration: {error}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

    }
}
