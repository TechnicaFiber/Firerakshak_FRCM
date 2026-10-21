//using System;
//using System.Drawing;
//using System.Windows.Forms;

//namespace FRCM
//{
//    /// <summary>
//    /// Shown whenever the FRMC connection is lost.
//    /// Retry  → keeps trying to reconnect until it succeeds (no re-press needed).
//    /// Close  → exits the DTSCM application.
//    /// </summary>
//    public class ConnectingPopup : Form
//    {
//        private Label  lblMessage  = null!;
//        private Label  lblStatus   = null!;
//        private Button btnRetry    = null!;
//        private Button btnClose    = null!;

//        /// <summary>Fired once when the user clicks Retry.</summary>
//        public event EventHandler? RetryRequested;

//        public ConnectingPopup()
//        {
//            BuildForm();
//        }

//        private void BuildForm()
//        {
//            this.AutoScaleDimensions = new SizeF(96F, 96F);
//            this.AutoScaleMode       = AutoScaleMode.Dpi;
//            this.Text                = "FRMC Connection Lost";
//            this.Size                = new Size(400, 200);
//            this.StartPosition       = FormStartPosition.CenterScreen;
//            this.FormBorderStyle     = FormBorderStyle.FixedDialog;
//            this.MaximizeBox         = false;
//            this.MinimizeBox         = false;
//            this.ControlBox          = false;   // no X button
//            this.ShowInTaskbar       = false;
//            this.TopMost             = true;
//            this.BackColor           = Color.White;
//            this.Font                = new Font("Segoe UI", 10F);

//            lblMessage = new Label
//            {
//                Text      = "Connection to FRMC has been lost.",
//                AutoSize  = false,
//                Size      = new Size(360, 28),
//                Location  = new Point(20, 25),
//                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
//                ForeColor = Color.FromArgb(30, 30, 30),
//                TextAlign = ContentAlignment.MiddleCenter
//            };

//            lblStatus = new Label
//            {
//                Text      = "Press Retry to reconnect or Close to exit.",
//                AutoSize  = false,
//                Size      = new Size(360, 24),
//                Location  = new Point(20, 62),
//                Font      = new Font("Segoe UI", 9F),
//                ForeColor = Color.Gray,
//                TextAlign = ContentAlignment.MiddleCenter
//            };

//            btnRetry = new Button
//            {
//                Text     = "Retry",
//                Size     = new Size(110, 34),
//                Location = new Point(90, 112),
//                Font     = new Font("Segoe UI", 10F)
//            };
//            btnRetry.Click += (_, _) =>
//            {
//                // Disable retry button — user does not need to press again
//                btnRetry.Enabled    = false;
//                lblStatus.Text      = "Connecting...";
//                lblStatus.ForeColor = Color.FromArgb(0, 100, 180);
//                RetryRequested?.Invoke(this, EventArgs.Empty);
//            };

//            btnClose = new Button
//            {
//                Text     = "Close",
//                Size     = new Size(110, 34),
//                Location = new Point(210, 112),
//                Font     = new Font("Segoe UI", 10F)
//            };
//            btnClose.Click += (_, _) => Application.Exit();

//            this.Controls.Add(lblMessage);
//            this.Controls.Add(lblStatus);
//            this.Controls.Add(btnRetry);
//            this.Controls.Add(btnClose);
//        }

//        /// <summary>Closes the popup safely from any thread.</summary>
//        public void SafeClose()
//        {
//            if (this.IsDisposed) return;
//            if (this.InvokeRequired)
//                this.BeginInvoke(new Action(SafeClose));
//            else
//                this.Close();
//        }
//    }
//}
using System;
using System.Drawing;
using System.Windows.Forms;

namespace FRCM
{
    /// <summary>
    /// Shown whenever the FRMC connection is lost.
    /// Retry  → keeps trying to reconnect until it succeeds.
    /// Close  → exits the DTSCM application.
    /// </summary>
    public class ConnectingPopup : Form
    {
        private Label lblMessage = null!;
        private Label lblStatus = null!;
        private Button btnRetry = null!;
        private Button btnClose = null!;

        /// <summary>
        /// Fired once when the user clicks Retry.
        /// </summary>
        public event EventHandler? RetryRequested;

        public ConnectingPopup()
        {
            BuildForm();
        }

        private void BuildForm()
        {
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;

            Text = "FRMC Connection Lost";
            ClientSize = new Size(400, 180);

            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;

            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false;
            ShowInTaskbar = false;
            TopMost = true;

            BackColor = Color.White;
            Font = new Font("Segoe UI", 10F);

            lblMessage = new Label
            {
                Text = "Connection to FRMC has been lost.",
                AutoSize = false,
                Size = new Size(360, 34),
                Location = new Point(20, 24),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 30, 30),
                TextAlign = ContentAlignment.MiddleCenter
            };

            lblStatus = new Label
            {
                Text = "Press Retry to reconnect or Close to exit.",
                AutoSize = false,
                Size = new Size(360, 24),
                Location = new Point(20, 66),
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = Color.DimGray,
                TextAlign = ContentAlignment.MiddleCenter
            };

            btnRetry = new Button
            {
                Text = "Retry",
                Size = new Size(110, 36),
                Location = new Point(85, 120),
                Font = new Font("Segoe UI", 9.5F)
            };

            btnRetry.Click += (_, _) =>
            {
                btnRetry.Enabled = false;
                btnRetry.Text = "Retrying...";
                lblStatus.Text = "Trying to reconnect to FRMC...";
                lblStatus.ForeColor = Color.FromArgb(0, 102, 204);

                RetryRequested?.Invoke(this, EventArgs.Empty);
            };

            btnClose = new Button
            {
                Text = "Close",
                Size = new Size(110, 36),
                Location = new Point(205, 120),
                Font = new Font("Segoe UI", 9.5F)
            };

            btnClose.Click += (_, _) =>
            {
                Application.Exit();
            };

            Controls.Add(lblMessage);
            Controls.Add(lblStatus);
            Controls.Add(btnRetry);
            Controls.Add(btnClose);
        }

        /// <summary>
        /// Closes the popup safely from any thread.
        /// </summary>
        public void SafeClose()
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
                BeginInvoke(new Action(SafeClose));
            else
                Close();
        }
    }
}