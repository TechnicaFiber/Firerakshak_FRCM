using System;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FRCM
{
    public class UserManagementForm : Form
    {
        private readonly CustomWebSocketClient _webSocketClient;
        private DataGridView? dgvUsers;
        private Button? btnRefresh;
        private Button? btnAddUser;
        private Button? btnDeleteUser;
        private Button? btnClose;
        private GroupBox? grpAddUser;
        private TextBox? txtNewUsername;
        private TextBox? txtNewPassword;
        private ComboBox? cmbNewRole;
        private Button? btnCreateUser;
        private Label? lblStatus;

        public UserManagementForm(CustomWebSocketClient webSocketClient)
        {
            _webSocketClient = webSocketClient;
            this.WindowState = FormWindowState.Maximized;
            InitializeComponents();
            _ = LoadUsersAsync(); // Load users on form open
        }

        private void InitializeComponents()
        {
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.AutoScaleMode = AutoScaleMode.Dpi;

            // Form settings
            this.Text = "User Management - FRMC";
            this.Size = new Size(1100, 750);
            this.StartPosition = FormStartPosition.CenterParent;
            this.WindowState = FormWindowState.Maximized;
            this.Font = new Font("Segoe UI", 9F);
            // Make form resizable
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.MinimumSize = new Size(1000, 650);

            // ===== Main Layout =====
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(10)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Grid
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));  // Action Buttons
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // GroupBox
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));  // Status/Close

            // ===== DataGridView =====
            //dgvUsers = new DataGridView
            //{
            //    Dock = DockStyle.Fill,
            //    AllowUserToAddRows = false,
            //    AllowUserToDeleteRows = false,
            //    ReadOnly = true,
            //    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            //    MultiSelect = false,
            //    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            //    BackgroundColor = Color.White,
            //    BorderStyle = BorderStyle.Fixed3D
            //};

            dgvUsers = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,

                RowHeadersVisible = false,
                ColumnHeadersVisible = true,

                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,

                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,

                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.Fixed3D,

                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,

                RowTemplate = { MinimumHeight = 35 }
            };

            dgvUsers.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
            dgvUsers.DefaultCellStyle.Padding = new Padding(3);
            dgvUsers.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dgvUsers.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dgvUsers.RowsDefaultCellStyle.WrapMode = DataGridViewTriState.True;

            dgvUsers.ColumnHeadersDefaultCellStyle.Font =
                new Font("Segoe UI", 10F, FontStyle.Bold);

            dgvUsers.DataError += (s, e) =>
            {
                e.ThrowException = false;
            };

            dgvUsers.Columns.Add("Username", "Username");
            dgvUsers.Columns.Add("Role", "Role");

            // ===== Action Buttons Layout =====
            var buttonsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                AutoSize = true
            };
            buttonsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));  // Spacer
            buttonsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20)); // Refresh
            buttonsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20)); // Delete



            btnRefresh = new Button { Text = "🔄 Refresh", Dock = DockStyle.Fill, Margin = new Padding(3), Height = 45, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            btnRefresh.Click += async (s, e) => await LoadUsersAsync();
            btnDeleteUser = new Button { Text = "🗑️ Delete User", Dock = DockStyle.Fill, Margin = new Padding(3), Height = 45, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.DarkRed };
            btnDeleteUser.Click += async (s, e) => await DeleteSelectedUserAsync();

            buttonsLayout.Controls.Add(btnRefresh, 1, 0);
            buttonsLayout.Controls.Add(btnDeleteUser, 2, 0);

            // ===== Add User GroupBox =====
            grpAddUser = new GroupBox
            {
                Text = "Add New User",
                Dock = DockStyle.Fill,
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            var addUserLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 7,
                RowCount = 1,
                AutoSize = true,
                Padding = new Padding(15)
            };
            addUserLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Username Label
            addUserLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25)); // Username Text
            addUserLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Password Label
            addUserLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25)); // Password Text
            addUserLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Role Label
            addUserLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25)); // Role Combo
            addUserLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25)); // Create Button

            // Controls inside GroupBox
            Label lblUsername = new Label { Text = "Username:", Anchor = AnchorStyles.Left, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9F) };
            txtNewUsername = new TextBox { Dock = DockStyle.Fill, MaxLength = 50 };
            Label lblPassword = new Label { Text = "Password:", Anchor = AnchorStyles.Left, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9F) };
            txtNewPassword = new TextBox { Dock = DockStyle.Fill, PasswordChar = '*', MaxLength = 128 };
            Label lblRole = new Label { Text = "Role:", Anchor = AnchorStyles.Left, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9F) };
            cmbNewRole = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbNewRole.Items.AddRange(new object[] { "Admin", "User" });
            cmbNewRole.SelectedIndex = 1;
            // Create User button
            btnCreateUser = new Button { Text = "➕ Create User", Dock = DockStyle.Fill, Height = 35, Font = new Font("Segoe UI", 9F, FontStyle.Bold), BackColor = Color.LightGreen, Margin = new Padding(10, 0, 0, 0) };
            btnCreateUser.Click += async (s, e) => await AddNewUserAsync();
            
            addUserLayout.Controls.Add(lblUsername, 0, 0);
            addUserLayout.Controls.Add(txtNewUsername, 1, 0);
            addUserLayout.Controls.Add(lblPassword, 2, 0);
            addUserLayout.Controls.Add(txtNewPassword, 3, 0);
            addUserLayout.Controls.Add(lblRole, 4, 0);
            addUserLayout.Controls.Add(cmbNewRole, 5, 0);
            addUserLayout.Controls.Add(btnCreateUser, 6, 0);
            grpAddUser.Controls.Add(addUserLayout);

            // ===== Status Bar Layout =====
            var statusLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true
            };
            statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 80)); // Status Label
            statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20)); // Close Button

            lblStatus = new Label { Text = "Ready", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9F), ForeColor = Color.Blue };
            btnClose = new Button { Text = "Close", Dock = DockStyle.Fill, Height = 45, DialogResult = DialogResult.OK };

            statusLayout.Controls.Add(lblStatus, 0, 0);
            statusLayout.Controls.Add(btnClose, 1, 0);

            // ===== Add all sections to main layout =====
            mainLayout.Controls.Add(dgvUsers, 0, 0);
            mainLayout.Controls.Add(buttonsLayout, 0, 1);
            mainLayout.Controls.Add(grpAddUser, 0, 2);
            mainLayout.Controls.Add(statusLayout, 0, 3);

            this.Controls.Add(mainLayout);
        }

        private async Task LoadUsersAsync()
        {
            try
            {
                SetStatus("Loading users...", Color.Blue);
                dgvUsers?.Rows.Clear();

                var response = await _webSocketClient.ListUsersAsync();

                if (!response.Success)
                {
                    SetStatus($"Failed: {response.Message}", Color.Red);
                    MessageBox.Show($"Failed to retrieve user list:\n\n{response.Message}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                foreach (var user in response.Users)
                {
                    dgvUsers?.Rows.Add(user.Username, user.RoleName);
                }

                SetStatus($"Loaded {response.Users.Count} user(s)", Color.Green);
            }
            catch (Exception ex)
            {
                SetStatus($"Error: {ex.Message}", Color.Red);
                MessageBox.Show($"Error loading users:\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task AddNewUserAsync()
        {
            try
            {
                string username = txtNewUsername?.Text.Trim() ?? "";
                string password = txtNewPassword?.Text.Trim() ?? "";
                string role = cmbNewRole?.SelectedItem?.ToString() ?? "User";

                if (string.IsNullOrEmpty(username))
                {
                    MessageBox.Show("Please enter a username.", "Validation Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtNewUsername?.Focus();
                    return;
                }

                if (!Regex.IsMatch(username, @"^[a-zA-Z0-9_]+$"))
                {
                    MessageBox.Show("Username can only contain letters, numbers, and underscores.\nSpaces and special characters are not allowed.",
                        "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtNewUsername?.Focus();
                    return;
                }

                if (string.IsNullOrEmpty(password))
                {
                    MessageBox.Show("Please enter a password.", "Validation Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtNewPassword?.Focus();
                    return;
                }

                if (password.Length < 4)
                {
                    MessageBox.Show("Password must be at least 4 characters long.", "Validation Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtNewPassword?.Focus();
                    return;
                }

                var confirmResult = MessageBox.Show(
                    $"Create new user?\n\nUsername: {username}\nRole: {role}",
                    "Confirm",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (confirmResult != DialogResult.Yes)
                    return;

                SetStatus($"Creating user '{username}'...", Color.Blue);

                var response = await _webSocketClient.AddUserAsync(username, password, role);

                if (response.Success)
                {
                    SetStatus($"User '{username}' created successfully", Color.Green);
                    MessageBox.Show($"User '{username}' has been created successfully!",
                        "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    // Clear input fields
                    if (txtNewUsername != null) txtNewUsername.Text = "";
                    if (txtNewPassword != null) txtNewPassword.Text = "";
                    if (cmbNewRole != null) cmbNewRole.SelectedIndex = 1;

                    // Refresh user list
                    await LoadUsersAsync();
                }
                else
                {
                    SetStatus($"Failed to create user: {response.Message}", Color.Red);
                    MessageBox.Show($"Failed to create user:\n\n{response.Message}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Error: {ex.Message}", Color.Red);
                MessageBox.Show($"Error creating user:\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task DeleteSelectedUserAsync()
        {
            try
            {
                if (dgvUsers?.SelectedRows.Count == 0)
                {
                    MessageBox.Show("Please select a user to delete.", "No Selection",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                string username = dgvUsers?.SelectedRows[0].Cells[0].Value.ToString() ?? "";

                if (string.IsNullOrEmpty(username))
                    return;

                var confirmResult = MessageBox.Show(
                    $"Are you sure you want to delete user '{username}'?\n\nThis action cannot be undone.",
                    "Confirm Delete",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirmResult != DialogResult.Yes)
                    return;

                SetStatus($"Deleting user '{username}'...", Color.Blue);

                var response = await _webSocketClient.DeleteUserAsync(username);

                if (response.Success)
                {
                    SetStatus($"User '{username}' deleted successfully", Color.Green);
                    MessageBox.Show($"User '{username}' has been deleted successfully!",
                        "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    // Refresh user list
                    await LoadUsersAsync();
                }
                else
                {
                    SetStatus($"Failed to delete user: {response.Message}", Color.Red);
                    MessageBox.Show($"Failed to delete user:\n\n{response.Message}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Error: {ex.Message}", Color.Red);
                MessageBox.Show($"Error deleting user:\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetStatus(string message, Color color)
        {
            if (lblStatus != null)
            {
                lblStatus.Text = message;
                lblStatus.ForeColor = color;
            }
        }
    }
}
