using System;
using System.Drawing;
using System.Windows.Forms;

namespace FRCM.View
{
    public partial class AboutForm : Form
    {
        private Panel? card; // Main centered UI card panel

        public AboutForm()
        {
            InitializeComponent();
            BuildResponsiveUI();
        }

        private void BuildResponsiveUI()
        {
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.AutoScaleMode = AutoScaleMode.Dpi;

            // ---------- WINDOW SETTINGS ----------
            this.Text = "About";
            this.Size = new Size(680, 480);
            this.MinimumSize = new Size(640, 450);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.BackColor = Color.White;

            // ---------- MAIN FORM LAYOUT (for border spacing) ----------
            TableLayoutPanel mainFormLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 1,
                Padding = new Padding(10) // Space for the border
            };

            // ---------- BORDERED PANEL ----------
            Panel borderedPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(15) // Internal padding for content
            };

            // ---------- CONTENT LAYOUT (TableLayoutPanel) ----------
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2
            };

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45)); // Logo column
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55)); // Text column

            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Description
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60)); // Footer

            borderedPanel.Controls.Add(layout); // Add content layout to bordered panel

            // ---------- LOGO ----------
            PictureBox logo = new PictureBox
            {
                Image = Properties.Resources.Logo,
                SizeMode = PictureBoxSizeMode.Zoom,
                Anchor = AnchorStyles.Top | AnchorStyles.Left, // Align to top left of cell
                Size = new Size(260, 260), // Further increased logo size
                Margin = new Padding(20, 30, 10, 0)
            };

            layout.Controls.Add(logo, 0, 0);

            // ---------- DESCRIPTION ----------
            FlowLayoutPanel descPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                WrapContents = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left, // Align to top left of cell
                Margin = new Padding(20, 30, 0, 0)
            };

            Label product = new Label
            {
                Text = "Fire Rakshak",
                Font = new Font("Segoe UI", 22, FontStyle.Bold),
                AutoSize = true
            };

            Label subTitle = new Label
            {
                Text = "Optical Fiber Intrusion Detection System",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 5, 0, 15)
            };

            Label version = new Label
            {
                Text = "Version 1.0",
                Font = new Font("Segoe UI", 11),
                AutoSize = true
            };

            Label desc = new Label
            {
                Text = "Channel Monitoring Configuration Software",
                Font = new Font("Segoe UI", 11),
                AutoSize = true
            };

            descPanel.Controls.Add(product);
            descPanel.Controls.Add(subTitle);
            descPanel.Controls.Add(version);
            descPanel.Controls.Add(desc);

            layout.Controls.Add(descPanel, 1, 0);

            // ---------- FOOTER ----------
            Label footer = new Label
            {
                Text = "© 2025 Technica Fiber Tech. All Rights Reserved.",
                Font = new Font("Segoe UI", 10, FontStyle.Italic),
                Dock = DockStyle.Fill, // Fill the cell
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 10, 0, 0) // Adjust margin as needed
            };

            layout.Controls.Add(footer, 0, 1);
            layout.SetColumnSpan(footer, 2);

            // Add bordered panel to main form layout
            mainFormLayout.Controls.Add(borderedPanel, 0, 0);
            this.Controls.Add(mainFormLayout); // Add main form layout to the form
        }
    }
}
