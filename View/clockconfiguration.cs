using System;
using System.Drawing;
using System.Windows.Forms;

namespace FRCM
{
    public class ClockConfigurationPopup : Form
    {
        private readonly CustomWebSocketClient _wsClient;

        private Label lblDeviceTime;
        private TextBox txtDeviceTime;
        private Label lblLag;
        private TextBox txtLag;
        private Button btnGetTime;
        private Button btnSetTime;
        private DateTimePicker dateTimePicker;
        private TextBox txtOutput;

        public DateTime SelectedTime { get; private set; }

        // ✅ Constructor used from menu
        public ClockConfigurationPopup(CustomWebSocketClient wsClient)
        {
            _wsClient = wsClient ?? throw new ArgumentNullException(nameof(wsClient));

            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;

            Text = "FRMC Clock Configuration";
            Size = new Size(680, 500);
            MinimumSize = new Size(650, 450);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9F);

            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;

            WindowState = FormWindowState.Normal;

            InitializeLayout();
        }

        private void InitializeLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 1,
                RowCount = 2
            };
            // Top section for controls, bottom section for the output log
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); 
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            // =============================
            // GRID (Configuration Section)
            // =============================
            var grid = new TableLayoutPanel
            {
                ColumnCount = 3,
                RowCount = 3,
                Dock = DockStyle.Top,
                Height = 140, // Fixed total height to keep it compact
                Margin = new Padding(0, 0, 0, 10)
            };

            // Column 1: Labels
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180)); 
            // Column 2: Buttons
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30)); 
            // Column 3: Inputs
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));

            // Use Percent for rows to ensure "Get Time" and "Set Time" buttons are EXACTLY the same height
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.34f));

            // -------- Row 1 : Current Time --------
            grid.Controls.Add(new Label { Text = "Current FRMC Time:", AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);

            btnGetTime = new Button { Text = "Get Time", Dock = DockStyle.Fill, Margin = new Padding(5) };
            btnGetTime.Click += BtnGetTime_Click;
            grid.Controls.Add(btnGetTime, 1, 0);

            txtDeviceTime = new TextBox { ReadOnly = true, Dock = DockStyle.Fill, Margin = new Padding(5, 10, 0, 0) };
            grid.Controls.Add(txtDeviceTime, 2, 0);

            // -------- Row 2 : Lag --------
            grid.Controls.Add(new Label { Text = "Lag (seconds):", AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
            
            txtLag = new TextBox { ReadOnly = true, Dock = DockStyle.Fill, Margin = new Padding(5, 10, 0, 0) };
            grid.Controls.Add(txtLag, 2, 1);

            // -------- Row 3 : Set Time --------
            grid.Controls.Add(new Label { Text = "Set New Time:", AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 2);

            btnSetTime = new Button { Text = "Set Time", Dock = DockStyle.Fill, Margin = new Padding(5) };
            btnSetTime.Click += BtnSetTime_Click;
            grid.Controls.Add(btnSetTime, 1, 2);

            dateTimePicker = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy-MM-dd HH:mm:ss",
                Dock = DockStyle.Fill,
                Margin = new Padding(5, 10, 0, 0)
            };
            grid.Controls.Add(dateTimePicker, 2, 2);

            root.Controls.Add(grid, 0, 0);

            // =============================
            // OUTPUT LOG
            // =============================
            txtOutput = new TextBox
            {
                Multiline = true,
                Dock = DockStyle.Fill,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9F),
                BackColor = Color.White
            };

            root.Controls.Add(txtOutput, 0, 1);
        }

        // =========================
        // BUTTON HANDLERS
        // =========================
        private async void BtnGetTime_Click(object sender, EventArgs e)
        {
            try
            {
                AppendOutput("Fetching FRMC time...");

                DateTime? frmcTime = await _wsClient.GetFrmcTimeAsync();
                if (!frmcTime.HasValue)
                {
                    AppendOutput("❌ Failed to fetch FRMC time.");
                    return;
                }

                DateTime localTime = DateTime.Now;
                TimeSpan lag = localTime - frmcTime.Value;

                txtDeviceTime.Text = frmcTime.Value.ToString("yyyy-MM-dd HH:mm:ss");
                txtLag.Text = Math.Abs(lag.TotalSeconds).ToString("F1");
                dateTimePicker.Value = frmcTime.Value;

                AppendOutput($"✅ FRMC time : {frmcTime:yyyy-MM-dd HH:mm:ss}");
                AppendOutput($"🕒 Local time: {localTime:yyyy-MM-dd HH:mm:ss}");
                AppendOutput($"⏱️ Lag       : {lag.TotalSeconds:F1} seconds");

                if (Math.Abs(lag.TotalSeconds) > 30)
                    AppendOutput("⚠️ FRMC clock is out of sync. Consider correcting it.");
            }
            catch (Exception ex)
            {
                AppendOutput($"❌ Error fetching time: {ex.Message}");
            }
        }
        private async void BtnSetTime_Click(object sender, EventArgs e)
        {
            try
            {
                DateTime newTime = dateTimePicker.Value; // LOCAL time

                var confirm = MessageBox.Show(
                    $"Do you want to set FRMC time to:\n{newTime:yyyy-MM-dd HH:mm:ss} ?",
                    "Confirm Time Set",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (confirm != DialogResult.Yes)
                {
                    AppendOutput("⏹️ Time set cancelled by user.");
                    return;
                }

                AppendOutput("Setting FRMC time...");

                string result = await _wsClient.SetFrmcTimeAsync(newTime);

                SelectedTime = newTime;
                DialogResult = DialogResult.OK;

                txtDeviceTime.Text = newTime.ToString("yyyy-MM-dd HH:mm:ss");
                txtLag.Text = "0";

                AppendOutput($"🕒 FRMC time set to {newTime:yyyy-MM-dd HH:mm:ss}");
                AppendOutput($"📡 Server response: {result}");
            }
            catch (Exception ex)
            {
                AppendOutput($"❌ Error setting time: {ex.Message}");
            }
        }
        private void AppendOutput(string text)
        {
            txtOutput.AppendText($"{DateTime.Now:HH:mm:ss} - {text}{Environment.NewLine}");
        }
    }
}
