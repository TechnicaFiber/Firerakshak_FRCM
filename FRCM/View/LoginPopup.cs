using System;
using System.Drawing;
using System.Windows.Forms;

namespace FRCM
{
    public class LoginPopup : Form
    {
        private TextBox? txtUsername;
        private TextBox? txtPassword;
        private Button? btnAdmin;
        private Button? btnUser;
        private Button? btnOK;
        private Button? btnCancel;
        private Label? lblUsername;
        private Label? lblPassword;
        private Label? lblRole;

        private string selectedRole = "";

        public string Username => txtUsername?.Text ?? "";
        public string Password => txtPassword?.Text ?? "";
        public string Role => selectedRole;

        public LoginPopup()
        {
            // DPI-safe
            this.AutoScaleMode = AutoScaleMode.Dpi;

            this.Text = "Fire Rakshak - Login";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.Font = new Font("Segoe UI", 11F);

            // Let dialog size itself correctly on all DPI
            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.MinimumSize = new Size(320, 280);

            InitializeLayout();
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
                Padding = new Padding(15)
            };

            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // ===== COMPANY LABEL =====
            var lblCompany = new Label
            {
                Text = "Technica Fiber Tech Pvt. Ltd.",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 80, 150),
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleCenter,
                Anchor = AnchorStyles.None, // Changed from DockStyle.Top to AnchorStyles.None for centering
                Margin = new Padding(0, 0, 0, 15)
            };

            // ===== CONTENT PANEL =====
            var contentPanel = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 4,
                Anchor = AnchorStyles.None // Center the whole content grid
            };

            contentPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            contentPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            for (int i = 0; i < 4; i++)
                contentPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var labelFont = new Font("Segoe UI", 11F, FontStyle.Bold);
            var textFont = new Font("Segoe UI", 11F);

            // ===== ROLE =====
            lblRole = new Label
            {
                Text = "Login as:",
                Font = labelFont,
                AutoSize = true,
                Anchor = AnchorStyles.Right, // Align label to the right of its cell
                Margin = new Padding(0, 8, 10, 8)
            };

            var rolePanel = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Anchor = AnchorStyles.Left, // Align buttons to the left of their cell
                Margin = new Padding(0, 5, 0, 5)
            };

            // Use same min-width (e.g. 100) for Admin/User
            btnAdmin = CreateButton("Admin", BtnAdmin_Click, 100);
            btnUser = CreateButton("User", BtnUser_Click, 100);

            rolePanel.Controls.Add(btnAdmin);
            rolePanel.Controls.Add(btnUser);

            // ===== USERNAME =====
            lblUsername = new Label
            {
                Text = "Username:",
                Font = labelFont,
                AutoSize = true,
                Anchor = AnchorStyles.Right,
                Margin = new Padding(0, 8, 10, 8)
            };

            txtUsername = new TextBox
            {
                Enabled = false,
                Font = textFont,
                Dock = DockStyle.Fill, // Stretch to match the width of buttons
                MinimumSize = new Size(220, 32),
                Margin = new Padding(0, 5, 0, 5),
                MaxLength = 50
            };

            // ===== PASSWORD =====
            lblPassword = new Label
            {
                Text = "Password:",
                Font = labelFont,
                AutoSize = true,
                Anchor = AnchorStyles.Right,
                Margin = new Padding(0, 8, 10, 8)
            };

            txtPassword = new TextBox
            {
                Enabled = false,
                Font = textFont,
                PasswordChar = '*',
                Dock = DockStyle.Fill, // Stretch to match the width of buttons
                MinimumSize = new Size(220, 32),
                Margin = new Padding(0, 5, 0, 5),
                MaxLength = 128
            };

            var buttonPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                Anchor = AnchorStyles.Left, // Keep aligned with the left of the column
                Margin = new Padding(0, 20, 0, 0),
                WrapContents = false
            };

            // Use same min-width (e.g. 100) for Login/Cancel
            btnOK = CreateButton("Login", BtnOK_Click, 100);
            btnCancel = CreateButton("Cancel", BtnCancel_Click, 100);

            buttonPanel.Controls.Add(btnOK);
            buttonPanel.Controls.Add(btnCancel);


            // ===== ADD CONTROLS =====
            contentPanel.Controls.Add(lblRole, 0, 0);
            contentPanel.Controls.Add(rolePanel, 1, 0);
            contentPanel.Controls.Add(lblUsername, 0, 1);
            contentPanel.Controls.Add(txtUsername, 1, 1);
            contentPanel.Controls.Add(lblPassword, 0, 2);
            contentPanel.Controls.Add(txtPassword, 1, 2);
            contentPanel.Controls.Add(buttonPanel, 1, 3); // Placed in Column 1 to align with inputs
            // contentPanel.SetColumnSpan(buttonPanel, 2); // Removed column span to keep it in Column 1

            mainPanel.Controls.Add(lblCompany, 0, 0);
            mainPanel.Controls.Add(contentPanel, 0, 1);

            Controls.Add(mainPanel);

            AcceptButton = btnOK;
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
                MinimumSize = new Size(minWidth, 32), // Forces equal width
                Padding = new Padding(10, 0, 10, 0),
                Margin = new Padding(5, 0, 5, 0)
            };
            btn.Click += onClick;
            return btn;
        }

        private void BtnAdmin_Click(object? sender, EventArgs e)
        {
            selectedRole = "Admin";
            EnableLoginFields();
            btnAdmin!.BackColor = Color.LightGreen;
            btnUser!.BackColor = Color.LightGray;
        }

        private void BtnUser_Click(object? sender, EventArgs e)
        {
            selectedRole = "User";
            EnableLoginFields();
            btnUser!.BackColor = Color.LightGreen;
            btnAdmin!.BackColor = Color.LightGray;
        }

        private void EnableLoginFields()
        {
            txtUsername!.Enabled = true;
            txtPassword!.Enabled = true;
            txtUsername.Clear();
            txtPassword.Clear();
            txtUsername.Focus();
        }

        private void BtnOK_Click(object? sender, EventArgs e)
        {
            ValidateCredentials();
        }

        private void BtnCancel_Click(object? sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void ValidateCredentials()
        {
            if (string.IsNullOrEmpty(selectedRole))
            {
                MessageBox.Show("Please select a role first (Admin/User)!", "Info",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                MessageBox.Show("Please enter both Username and Password!", "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;
        }
    }
}
