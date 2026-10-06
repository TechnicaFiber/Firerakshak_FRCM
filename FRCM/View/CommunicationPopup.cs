using System;
using System.Drawing;
using System.Net;
using System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FRCM
{
    public class CommunicationPopup : Form
    {
        private TextBox? txtOutput;
        private Label? lblCurrentIP;
        private TextBox? txtCurrentIP;
        private Button? btnGetIP;
        private Label? lblNewIP;
        private TextBox? txtNewIP;
        private Button? btnSetIP;

        public string SelectedProtocol => "WebSocket";

        private string? currentFRMCIP;

        public string NewIP => txtNewIP?.Text.Trim() ?? "";
        public string? CurrentIP => currentFRMCIP;

        public CommunicationPopup()
        {
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.AutoScaleMode = AutoScaleMode.Dpi;

            Text = "FRMC IP Configuration";
            Size = new Size(560, 400);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            LoadDefaultIpFromSettings();  
            InitializeLayout();            

            if(txtCurrentIP != null) txtCurrentIP.Text = currentFRMCIP; 
            AppendOutput($"📌 Default FRMC IP loaded: {currentFRMCIP}");
        }

        private void LoadDefaultIpFromSettings()
        {
            try
            {
                var config = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();

                currentFRMCIP = config["FrmcConfiguration:DefaultIpAddress"] ?? "192.168.0.100";
            }
            catch
            {
                currentFRMCIP = "192.168.0.100"; 
            }
        }

        private void SaveIpToSettings(string ip)
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                string json = File.ReadAllText(configPath);

                var root = JsonNode.Parse(json)?.AsObject();
                if (root == null) return;

                if (root["FrmcConfiguration"] is JsonObject frmcConfig)
                {
                    frmcConfig["DefaultIpAddress"] = ip;
                }
                else
                {
                    root["FrmcConfiguration"] = new JsonObject
                    {
                        ["DefaultIpAddress"] = ip,
                        ["Port"] = 4000
                    };
                }

                string updatedJson = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, updatedJson);

                AppendOutput($"💾 IP saved to appsettings.json: {ip}");
            }
            catch (Exception ex)
            {
                AppendOutput($"❌ Failed to save IP: {ex.Message}");
            }
        }

        private void InitializeLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                ColumnCount = 1,
                RowCount = 2
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            var ipPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 100
            };

            lblCurrentIP = new Label
            {
                Text = "Current FRMC IP:",
                Location = new Point(20, 15),
                AutoSize = true
            };

            btnGetIP = new Button
            {
                Text = "Get IP",
                Location = new Point(160, 10),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            txtCurrentIP = new TextBox
            {
                Location = new Point(260, 12),
                Width = 250,
                ReadOnly = true
            };

            lblNewIP = new Label
            {
                Text = "New FRMC IP:",
                Location = new Point(20, 55),
                AutoSize = true
            };

            btnSetIP = new Button
            {
                Text = "Set IP",
                Location = new Point(160, 50),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            txtNewIP = new TextBox
            {
                Location = new Point(260, 52),
                Width = 250,
                MaxLength = 45
            };

            btnGetIP.Click += BtnGetIP_Click;
            btnSetIP.Click += BtnSetIP_Click;

            ipPanel.Controls.AddRange(new Control[]
            {
                lblCurrentIP, btnGetIP, txtCurrentIP,
                lblNewIP, btnSetIP, txtNewIP
            });

            root.Controls.Add(ipPanel, 0, 0);

            txtOutput = new TextBox
            {
                Multiline = true,
                Dock = DockStyle.Fill,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9.5F),
                BackColor = Color.White,
                BorderStyle = BorderStyle.Fixed3D,
                WordWrap = true
            };

            root.Controls.Add(txtOutput, 0, 1);
        }

        private void BtnGetIP_Click(object? sender, EventArgs e)
        {
            if (txtCurrentIP != null) txtCurrentIP.Text = currentFRMCIP;
            AppendOutput($"✅ Current FRMC IP fetched: {currentFRMCIP}");
        }

        private void BtnSetIP_Click(object? sender, EventArgs e)
        {
            string newIP = NewIP;

            if (string.IsNullOrEmpty(newIP))
            {
                MessageBox.Show("Please enter a new IP address.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!IPAddress.TryParse(newIP, out _))
            {
                MessageBox.Show("Invalid IP format. Please enter a valid IP (e.g., 192.168.1.50).", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            currentFRMCIP = newIP;
            if(txtCurrentIP != null) txtCurrentIP.Text = currentFRMCIP;
            AppendOutput($"🔄 FRMC IP successfully set to: {currentFRMCIP}");

            SaveIpToSettings(currentFRMCIP); // ✅ Save to appsettings.json
        }

        private void AppendOutput(string text)
        {
            if (txtOutput != null) txtOutput.AppendText($"{DateTime.Now:HH:mm:ss} - {text}{Environment.NewLine}");
        }
    }
}