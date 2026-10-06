using Newtonsoft.Json.Linq;
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace FRCM.View
{
    public class ServerIpEntryForm : Form
    {
        private TextBox? txtIpAddress;
        private Button? btnConnect;
        private Button? btnCancel;
        private Label? lblIpAddress;
        private Label? lblCompany;

        public string IpAddress => txtIpAddress?.Text ?? "";

        public ServerIpEntryForm()
        {
            // DPI-safe
            this.AutoScaleMode = AutoScaleMode.Dpi;

            this.Text = "Fire Rakshak - Server Connection";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = true;
            this.Font = new Font("Segoe UI", 11F);

            // Let dialog size itself correctly on all DPI
            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.MinimumSize = new Size(400, 250);

            InitializeLayout();
            LoadCurrentIp();
        }

        private void InitializeLayout()
        {
            SuspendLayout();

            // ===== MAIN PANEL =====
            var mainPanel = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(20)
            };

            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // ===== COMPANY LABEL =====
            lblCompany = new Label
            {
                Text = "Technica Fiber Tech Pvt. Ltd.",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 80, 150),
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleCenter,
                Anchor = AnchorStyles.None,
                Margin = new Padding(0, 0, 0, 20)
            };

            // ===== CONTENT PANEL =====
            var contentPanel = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 2,
                Anchor = AnchorStyles.None
            };

            contentPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            contentPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var labelFont = new Font("Segoe UI", 11F, FontStyle.Bold);
            var textFont = new Font("Segoe UI", 11F);

            // ===== IP ADDRESS =====
            lblIpAddress = new Label
            {
                Text = "FRMC Server IP:",
                Font = labelFont,
                AutoSize = true,
                Anchor = AnchorStyles.Right,
                Margin = new Padding(0, 0, 10, 20)
            };

            txtIpAddress = new TextBox
            {
                Font = textFont,
                MinimumSize = new Size(200, 32),
                Margin = new Padding(0, 0, 0, 20),
                MaxLength = 15
            };

            var buttonPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                Anchor = AnchorStyles.Right,
                Margin = new Padding(0, 10, 0, 0),
                WrapContents = false
            };

            btnConnect = CreateButton("Connect", BtnConnect_Click, 110);
            btnCancel = CreateButton("Exit", BtnCancel_Click, 110);

            buttonPanel.Controls.Add(btnConnect);
            buttonPanel.Controls.Add(btnCancel);

            // ===== ADD CONTROLS =====
            contentPanel.Controls.Add(lblIpAddress, 0, 0);
            contentPanel.Controls.Add(txtIpAddress, 1, 0);
            contentPanel.Controls.Add(buttonPanel, 1, 1);

            mainPanel.Controls.Add(lblCompany, 0, 0);
            mainPanel.Controls.Add(contentPanel, 0, 1);

            Controls.Add(mainPanel);

            AcceptButton = btnConnect;
            CancelButton = btnCancel;

            ResumeLayout(false);
        }

        // ===== BUTTON FACTORY (DPI SAFE) =====
        private Button CreateButton(string text, EventHandler onClick, int minWidth = 90)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(minWidth, 35),
                Padding = new Padding(10, 0, 10, 0),
                Margin = new Padding(5, 0, 5, 0)
            };
            btn.Click += onClick;
            return btn;
        }

        private void LoadCurrentIp()
        {
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var obj = JObject.Parse(json);
                    var ip = obj["FrmcConfiguration"]?["DefaultIpAddress"]?.ToString();
                    if (!string.IsNullOrEmpty(ip) && txtIpAddress != null)
                    {
                        txtIpAddress.Text = ip;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading IP from config: {ex.Message}");
            }
        }

        private void BtnConnect_Click(object? sender, EventArgs e)
        {
            string ip = IpAddress.Trim();
            if (string.IsNullOrEmpty(ip))
            {
                MessageBox.Show("Please enter a valid IP address.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Simple IPv4 validation
            var parts = ip.Split('.');
            if (ip.ToLower() != "localhost" && (parts.Length != 4 || !Array.TrueForAll(parts, p => byte.TryParse(p, out _))))
            {
                 MessageBox.Show("Please enter a valid IPv4 address (e.g., 192.168.1.100) or 'localhost'.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SaveIpToConfig(ip);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void BtnCancel_Click(object? sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Application.Exit();
        }

        private void SaveIpToConfig(string ip)
        {
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var obj = JObject.Parse(json);

                    if (obj["FrmcConfiguration"] == null)
                        obj["FrmcConfiguration"] = new JObject();

                    obj["FrmcConfiguration"]!["DefaultIpAddress"] = ip;

                    File.WriteAllText(configPath, obj.ToString(Newtonsoft.Json.Formatting.Indented));
                }
                else
                {
                    // Create minimal appsettings.json if it doesn't exist
                    var obj = new JObject();
                    obj["FrmcConfiguration"] = new JObject();
                    obj["FrmcConfiguration"]!["DefaultIpAddress"] = ip;
                    File.WriteAllText(configPath, obj.ToString(Newtonsoft.Json.Formatting.Indented));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save configuration: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
