using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using FRCM.Services;

namespace FRCM.View
{
    public partial class FormDtsCalibration : Form
    {
        public static FormDtsCalibration? Instance { get; private set; }

        private readonly CustomWebSocketClient _wsClient;
        private readonly string _loginRole;

        private const int ChannelCount = 8;
        private CheckBox[] _chkChannels = new CheckBox[ChannelCount];
        private DataGridView _dgvLengthFreq = null!;
        private DataGridView _dgvAccumulation = null!;
        private Button _btnRead = null!;
        private Button _btnSetup = null!;

        // Stored raw INI buffer from hardware for non-destructive updates
        private string _lastReadIniPayload = string.Empty;

        public event Action? DtsCalibrationClosed;
        public event Action<string>? DtsOperationStatus;

        public FormDtsCalibration(CustomWebSocketClient wsClient, string loginRole = "Admin")
        {
            _wsClient = wsClient ?? throw new ArgumentNullException(nameof(wsClient));
            _loginRole = loginRole;

            Instance = this;

            InitializeComponent();
            InitializeLayout();

            this.FormClosed += (s, e) =>
            {
                Instance = null;
                DtsCalibrationClosed?.Invoke();
            };

            this.Load += (s, e) =>
            {
                FontSizeHelper.UpdateControlRecursive(this, FontSizeHelper.CurrentMultiplier);
            };

            this.Shown += async (s, e) =>
            {
                await BtnRead_Click();
            };
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Text = "DTS Hardware Configuration";
            this.Size = new Size(820, 520);
            this.MinimumSize = new Size(760, 480);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = SystemColors.Control;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.ResumeLayout(false);
        }

        private void InitializeLayout()
        {
            var mainContainer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(12),
                BackColor = SystemColors.Control
            };

            mainContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));  // Channel checkboxes
            mainContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Tables (length/frequency & accumulation)
            mainContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));  // Buttons row
            mainContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            // ----------------------------------------------------
            // SECTION 1: Channel Checkboxes (Channels 1 to 8)
            // ----------------------------------------------------
            var channelGroupPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 6)
            };

            var lblChannelTitle = new Label
            {
                Text = "Channel",
                ForeColor = Color.FromArgb(200, 60, 40),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Location = new Point(4, 2),
                AutoSize = true
            };
            channelGroupPanel.Controls.Add(lblChannelTitle);

            var channelRowLayout = new TableLayoutPanel
            {
                Location = new Point(4, 24),
                Height = 42,
                Width = 480,
                ColumnCount = ChannelCount,
                RowCount = 2,
                Margin = new Padding(0)
            };

            for (int i = 0; i < ChannelCount; i++)
            {
                channelRowLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / ChannelCount));
            }
            channelRowLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18F));
            channelRowLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));

            for (int i = 0; i < ChannelCount; i++)
            {
                int chNum = i + 1;
                var lblNum = new Label
                {
                    Text = chNum.ToString(),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock = DockStyle.Fill,
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Regular)
                };
                channelRowLayout.Controls.Add(lblNum, i, 0);

                var chk = new CheckBox
                {
                    Dock = DockStyle.Fill,
                    CheckAlign = ContentAlignment.MiddleCenter,
                    Checked = true,
                    Tag = chNum
                };
                _chkChannels[i] = chk;
                channelRowLayout.Controls.Add(chk, i, 1);
            }

            channelGroupPanel.Controls.Add(channelRowLayout);
            mainContainer.Controls.Add(channelGroupPanel, 0, 0);

            // ----------------------------------------------------
            // SECTION 2: Dual Grid (Length/Frequency & Accumulation times)
            // ----------------------------------------------------
            var tablesLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0)
            };
            tablesLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tablesLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tablesLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // Left Grid Box: length/frequency
            var pnlLeft = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
            var lblLeft = new Label
            {
                Text = "length/frequency",
                ForeColor = Color.FromArgb(200, 60, 40),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 24
            };
            _dgvLengthFreq = CreateStyledGrid();
            _dgvLengthFreq.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "",
                Name = "ColChannel",
                Width = 65,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _dgvLengthFreq.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Cable length",
                Name = "ColCableLength",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _dgvLengthFreq.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Frequency(Hz)",
                Name = "ColFrequency",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            pnlLeft.Controls.Add(_dgvLengthFreq);
            pnlLeft.Controls.Add(lblLeft);
            tablesLayout.Controls.Add(pnlLeft, 0, 0);

            // Right Grid Box: Accumulation times
            var pnlRight = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
            var lblRight = new Label
            {
                Text = "Accumulation times",
                ForeColor = Color.FromArgb(200, 60, 40),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 24
            };
            _dgvAccumulation = CreateStyledGrid();
            _dgvAccumulation.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "",
                Name = "ColChannelAcc",
                Width = 65,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _dgvAccumulation.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Calculating time(s)",
                Name = "ColCalcTime",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            pnlRight.Controls.Add(_dgvAccumulation);
            pnlRight.Controls.Add(lblRight);
            tablesLayout.Controls.Add(pnlRight, 1, 0);

            mainContainer.Controls.Add(tablesLayout, 0, 1);

            // Default initial rows for 8 channels
            for (int i = 1; i <= ChannelCount; i++)
            {
                double defaultLength = (i == 1) ? 5000 : (i == 2 ? 3000 : 6000);
                int defaultFreq = 14000;
                int defaultCalcTime = 6;

                _dgvLengthFreq.Rows.Add($"CH {i}", defaultLength.ToString("0"), defaultFreq.ToString());
                _dgvAccumulation.Rows.Add($"CH {i}", defaultCalcTime.ToString());
            }

            // ----------------------------------------------------
            // SECTION 3: Bottom Action Buttons (Read & Setup)
            // ----------------------------------------------------
            var buttonPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0, 8, 0, 0)
            };
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
            buttonPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _btnRead = new Button
            {
                Text = "Read",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5F),
                UseVisualStyleBackColor = true,
                Height = 34
            };
            _btnRead.Click += async (s, e) => await BtnRead_Click();
            buttonPanel.Controls.Add(_btnRead, 0, 0);

            _btnSetup = new Button
            {
                Text = "Setup",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5F),
                UseVisualStyleBackColor = true,
                Height = 34
            };
            _btnSetup.Click += async (s, e) => await BtnSetup_Click();
            buttonPanel.Controls.Add(_btnSetup, 2, 0);

            mainContainer.Controls.Add(buttonPanel, 0, 2);

            this.Controls.Add(mainContainer);
        }

        private DataGridView CreateStyledGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                GridColor = Color.LightGray,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                MultiSelect = false,
                EnableHeadersVisualStyles = true,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
                RowTemplate = { Height = 26 }
            };

            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.ColumnHeadersDefaultCellStyle.BackColor = SystemColors.ControlLight;
            grid.ColumnHeadersHeight = 30;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            grid.DefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            grid.DefaultCellStyle.SelectionBackColor = SystemColors.Highlight;
            grid.DefaultCellStyle.SelectionForeColor = Color.White;

            return grid;
        }

        #region Protocol Frames & INI Mapping (ADR 0xFF + CMD 0x01 / 0x02)

        /// <summary>
        /// Parses raw DTS INI payload and updates the GUI checkboxes and tables.
        /// </summary>
        private void ParseIniPayload(string iniText)
        {
            if (string.IsNullOrWhiteSpace(iniText)) return;
            _lastReadIniPayload = iniText;

            // 1. Channel Enable Bitmask (chscancfg) from [global] section
            var chscanMatch = Regex.Match(iniText, @"chscancfg\s*=\s*(\d+)", RegexOptions.IgnoreCase);
            if (chscanMatch.Success && int.TryParse(chscanMatch.Groups[1].Value, out int bitmask))
            {
                for (int i = 0; i < ChannelCount; i++)
                {
                    _chkChannels[i].Checked = (bitmask & (1 << i)) != 0;
                }
            }

            // 2. Individual Channel Sections [ch1_basic] .. [ch8_basic]
            for (int i = 1; i <= ChannelCount; i++)
            {
                string sectionHeader = $"[ch{i}_basic]";
                int sectionStart = iniText.IndexOf(sectionHeader, StringComparison.OrdinalIgnoreCase);
                if (sectionStart >= 0)
                {
                    int nextSection = iniText.IndexOf("[", sectionStart + sectionHeader.Length);
                    string sectionContent = (nextSection > sectionStart)
                        ? iniText.Substring(sectionStart, nextSection - sectionStart)
                        : iniText.Substring(sectionStart);

                    // Sensing Length: chlen
                    var chlenMatch = Regex.Match(sectionContent, @"chlen\s*=\s*(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
                    if (chlenMatch.Success)
                    {
                        _dgvLengthFreq.Rows[i - 1].Cells["ColCableLength"].Value = chlenMatch.Groups[1].Value;
                    }

                    // Optical Pulse Frequency: pulsefreq
                    var freqMatch = Regex.Match(sectionContent, @"pulsefreq\s*=\s*(\d+)", RegexOptions.IgnoreCase);
                    int pulseFreq = 14000;
                    if (freqMatch.Success && int.TryParse(freqMatch.Groups[1].Value, out int f))
                    {
                        pulseFreq = f;
                        _dgvLengthFreq.Rows[i - 1].Cells["ColFrequency"].Value = f.ToString();
                    }

                    // Accumulation / Calculating Time: avgnum = calc_time * pulsefreq
                    var avgMatch = Regex.Match(sectionContent, @"avgnum\s*=\s*(\d+)", RegexOptions.IgnoreCase);
                    if (avgMatch.Success && long.TryParse(avgMatch.Groups[1].Value, out long avgnum))
                    {
                        int calcSeconds = (pulseFreq > 0) ? (int)Math.Round((double)avgnum / pulseFreq) : 6;
                        if (calcSeconds <= 0) calcSeconds = 6;
                        _dgvAccumulation.Rows[i - 1].Cells["ColCalcTime"].Value = calcSeconds.ToString();
                    }
                }
            }
        }

        /// <summary>
        /// Builds modified INI text from _lastReadIniPayload and writes it via Command 0x01 (ADR 0xFF, CMD 0x01).
        /// Preserves unchecked channels, coefficients, and other sections.
        /// </summary>
        private string BuildModifiedIni()
        {
            if (string.IsNullOrWhiteSpace(_lastReadIniPayload))
            {
                throw new InvalidOperationException("No existing DTS configuration available. Please read the configuration from DTS first.");
            }

            string ini = _lastReadIniPayload;

            // 1. Calculate and update chscancfg bitmask in [global] section
            int bitmask = 0;
            for (int i = 0; i < ChannelCount; i++)
            {
                if (_chkChannels[i].Checked)
                    bitmask |= (1 << i);
            }

            if (Regex.IsMatch(ini, @"chscancfg\s*=\s*\d+", RegexOptions.IgnoreCase))
            {
                ini = Regex.Replace(ini, @"chscancfg\s*=\s*\d+", $"chscancfg = {bitmask}", RegexOptions.IgnoreCase);
            }
            else if (ini.Contains("[global]", StringComparison.OrdinalIgnoreCase))
            {
                ini = Regex.Replace(ini, @"\[global\]", $"[global]\nchscancfg = {bitmask}", RegexOptions.IgnoreCase);
            }

            // 2. Update each channel section [ch1_basic] .. [ch8_basic] for CHECKED channels only
            for (int i = 1; i <= ChannelCount; i++)
            {
                if (!_chkChannels[i - 1].Checked)
                {
                    // Preserve unchecked channels as-is
                    continue;
                }

                string sectionHeader = $"[ch{i}_basic]";
                string lengthStr = _dgvLengthFreq.Rows[i - 1].Cells["ColCableLength"].Value?.ToString() ?? "6000";
                string freqStr = _dgvLengthFreq.Rows[i - 1].Cells["ColFrequency"].Value?.ToString() ?? "14000";
                string calcTimeStr = _dgvAccumulation.Rows[i - 1].Cells["ColCalcTime"].Value?.ToString() ?? "6";

                double.TryParse(lengthStr, out double cableLength);
                if (cableLength <= 0) cableLength = 6000;

                int.TryParse(freqStr, out int freq);
                if (freq <= 0) freq = 14000;

                int.TryParse(calcTimeStr, out int calcTime);
                if (calcTime <= 0) calcTime = 6;

                long avgnum = (long)Math.Round((double)calcTime * freq);

                int sectionStart = ini.IndexOf(sectionHeader, StringComparison.OrdinalIgnoreCase);
                if (sectionStart >= 0)
                {
                    int nextSection = ini.IndexOf("[", sectionStart + sectionHeader.Length);
                    string sectionContent = (nextSection > sectionStart)
                        ? ini.Substring(sectionStart, nextSection - sectionStart)
                        : ini.Substring(sectionStart);

                    string updatedSection = sectionContent;

                    // Replace chlen
                    if (Regex.IsMatch(updatedSection, @"chlen\s*=\s*[\d\.]+", RegexOptions.IgnoreCase))
                    {
                        updatedSection = Regex.Replace(updatedSection, @"chlen\s*=\s*[\d\.]+", $"chlen = {cableLength:0}", RegexOptions.IgnoreCase);
                    }
                    else
                    {
                        updatedSection += $"\nchlen = {cableLength:0}";
                    }

                    // Replace pulsefreq
                    if (Regex.IsMatch(updatedSection, @"pulsefreq\s*=\s*\d+", RegexOptions.IgnoreCase))
                    {
                        updatedSection = Regex.Replace(updatedSection, @"pulsefreq\s*=\s*\d+", $"pulsefreq = {freq}", RegexOptions.IgnoreCase);
                    }
                    else
                    {
                        updatedSection += $"\npulsefreq = {freq}";
                    }

                    // Replace avgnum
                    if (Regex.IsMatch(updatedSection, @"avgnum\s*=\s*\d+", RegexOptions.IgnoreCase))
                    {
                        updatedSection = Regex.Replace(updatedSection, @"avgnum\s*=\s*\d+", $"avgnum = {avgnum}", RegexOptions.IgnoreCase);
                    }
                    else
                    {
                        updatedSection += $"\navgnum = {avgnum}";
                    }

                    ini = ini.Remove(sectionStart, sectionContent.Length).Insert(sectionStart, updatedSection);
                }
                else
                {
                    // If section didn't exist in INI, append it
                    ini += $"\n{sectionHeader}\nchlen = {cableLength:0}\navgnum = {avgnum}\npulsefreq = {freq}\n";
                }
            }

            return ini;
        }

        #endregion

        private async Task BtnRead_Click()
        {
            try
            {
                _btnRead.Enabled = false;
                this.Cursor = Cursors.WaitCursor;
                DtsOperationStatus?.Invoke("Reading DTS Configuration (CMD 0x02)...");

                // Flow: DTSCM -> FRMC -> DTS (CMD 0x02 Query)
                var resp = await _wsClient.SendCommandAsync("GetDtsCalibration", new { });
                if (resp.HasValue && resp.Value.TryGetProperty("Payload", out var payload))
                {
                    if (payload.TryGetProperty("IniPayload", out var iniProp))
                    {
                        string rawIni = iniProp.GetString() ?? "";
                        if (!string.IsNullOrEmpty(rawIni))
                        {
                            ParseIniPayload(rawIni);
                        }
                    }
                }

                DtsOperationStatus?.Invoke("Reading DTS configuration completed successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to read DTS parameters: {ex.Message}", "Read Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.Cursor = Cursors.Default;
                _btnRead.Enabled = true;
            }
        }

        private async Task BtnSetup_Click()
        {
            try
            {
                // 1. Check prerequisite: _lastReadIniPayload must be available
                if (string.IsNullOrWhiteSpace(_lastReadIniPayload))
                {
                    MessageBox.Show(this, "No existing DTS configuration available. Please read the configuration from DTS first.", "Configuration Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // 2. Validate at least one channel is selected
                bool anyChecked = _chkChannels.Any(c => c.Checked);
                if (!anyChecked)
                {
                    MessageBox.Show(this, "Please select at least one channel to enable.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // 3. Validate values for all checked channels
                for (int i = 0; i < ChannelCount; i++)
                {
                    if (_chkChannels[i].Checked)
                    {
                        int chNum = i + 1;
                        string lengthStr = _dgvLengthFreq.Rows[i].Cells["ColCableLength"].Value?.ToString() ?? "";
                        string freqStr = _dgvLengthFreq.Rows[i].Cells["ColFrequency"].Value?.ToString() ?? "";
                        string calcTimeStr = _dgvAccumulation.Rows[i].Cells["ColCalcTime"].Value?.ToString() ?? "";

                        if (!double.TryParse(lengthStr, out double len) || len <= 0)
                        {
                            MessageBox.Show(this, $"Channel {chNum}: Cable length must be a valid number greater than 0.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        if (!int.TryParse(freqStr, out int freq) || freq <= 0)
                        {
                            MessageBox.Show(this, $"Channel {chNum}: Frequency must be a valid integer greater than 0 (e.g., 14000).", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        if (!int.TryParse(calcTimeStr, out int calcTime) || calcTime <= 0)
                        {
                            MessageBox.Show(this, $"Channel {chNum}: Calculating time must be a valid integer greater than 0.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                    }
                }

                _btnSetup.Enabled = false;
                this.Cursor = Cursors.WaitCursor;
                DtsOperationStatus?.Invoke("Writing DTS Configuration (CMD 0x01)...");

                // 4. Build non-destructively modified INI
                string modifiedIni = BuildModifiedIni();

                // 5. Flow: DTSCM -> FRMC (WebSocket) -> DTS (WRITE CMD 0x01 + INI Payload)
                var response = await _wsClient.SendCommandAsync("SetDtsCalibration", new
                {
                    IniPayload = modifiedIni
                });

                if (response.HasValue && response.Value.TryGetProperty("Payload", out var payload))
                {
                    bool isSuccess = payload.TryGetProperty("Success", out var successProp) && successProp.GetBoolean();
                    if (isSuccess)
                    {
                        _lastReadIniPayload = modifiedIni;
                        DtsOperationStatus?.Invoke("DTS hardware configuration setup completed successfully.");
                        MessageBox.Show(this, "Set successfully", "Set successfully", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        string errorMsg = payload.TryGetProperty("Error", out var errProp) ? errProp.GetString() ?? "Unknown error" : "Failed to write DTS configuration";
                        MessageBox.Show(this, $"Failed to setup DTS hardware configuration: {errorMsg}", "Setup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        DtsOperationStatus?.Invoke($"Setup failed: {errorMsg}");
                    }
                }
                else
                {
                    MessageBox.Show(this, "No response received from FRMC middleware service.", "Communication Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    DtsOperationStatus?.Invoke("Setup failed: No response from middleware service.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to setup DTS hardware configuration: {ex.Message}", "Setup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                DtsOperationStatus?.Invoke($"Setup error: {ex.Message}");
            }
            finally
            {
                this.Cursor = Cursors.Default;
                _btnSetup.Enabled = true;
            }
        }
    }
}
