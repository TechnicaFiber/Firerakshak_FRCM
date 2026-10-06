using System;
using System.Drawing;
using System.Windows.Forms;

namespace FRCM
{
    public class LoadingForm : Form
    {
        private Label? lblStatus;
        private ProgressBar? progressBar;

        public LoadingForm()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.AutoScaleMode = AutoScaleMode.Dpi;

            // Form settings
            this.Text = "FRCM - Loading";
            this.Size = new Size(450, 200);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.FromArgb(45, 45, 48);
            this.Font = new Font("Segoe UI", 10F);

            // Main panel
            Panel panel = new Panel
            {
                Location = new Point(2, 2),
                Size = new Size(446, 196),
                BackColor = Color.FromArgb(30, 30, 30),
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(panel);

            // Title label
            Label lblTitle = new Label
            {
                Text = "FRCM - DTS Configuration Manager",
                Location = new Point(20, 30),
                Size = new Size(406, 30),
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter
            };
            panel.Controls.Add(lblTitle);

            // Status label
            lblStatus = new Label
            {
                Text = "Initializing...",
                Location = new Point(20, 80),
                Size = new Size(406, 25),
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.LightGray,
                TextAlign = ContentAlignment.MiddleCenter
            };
            panel.Controls.Add(lblStatus);

            // Progress bar
            progressBar = new ProgressBar
            {
                Location = new Point(70, 120),
                Size = new Size(306, 25),
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30
            };
            panel.Controls.Add(progressBar);
        }

        public void UpdateStatus(string status)
        {
            if (lblStatus != null && !lblStatus.IsDisposed)
            {
                if (lblStatus.InvokeRequired)
                {
                    // Use BeginInvoke to avoid UI thread deadlock
                    lblStatus.BeginInvoke(new Action(() => lblStatus.Text = status));
                }
                else
                {
                    lblStatus.Text = status;
                }
            }
        }

        public void SetProgress(int percentage)
        {
            if (progressBar != null && !progressBar.IsDisposed)
            {
                if (progressBar.InvokeRequired)
                {
                    // Use BeginInvoke to avoid UI thread deadlock
                    progressBar.BeginInvoke(new Action(() =>
                    {
                        progressBar.Style = ProgressBarStyle.Blocks;
                        progressBar.Value = Math.Min(100, Math.Max(0, percentage));
                    }));
                }
                else
                {
                    progressBar.Style = ProgressBarStyle.Blocks;
                    progressBar.Value = Math.Min(100, Math.Max(0, percentage));
                }
            }
        }
    }
}
