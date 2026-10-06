using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using FRCM.Services;
using ScottPlot;
using ScottPlot.WinForms;
using FontStyle = System.Drawing.FontStyle;
using Label = System.Windows.Forms.Label;
using Font = System.Drawing.Font;
using Color = System.Drawing.Color;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace FRCM.View
{
    public partial class FormTemperatureCorrection : Form
    {
        public static FormTemperatureCorrection? Instance { get; private set; }

        private readonly CustomWebSocketClient _wsClient;
        private readonly string _loginRole;

        private const int TotalChannels = 8;
        private int _selectedChannel = 1;

        // UI Controls
        private DataGridView _dgvCoefficients = null!;
        private NumericUpDown _numPosition = null!;
        private NumericUpDown _numTemp = null!;
        private Button _btnAdd = null!;
        private Button _btnCorrect = null!;
        private ComboBox _cboChannel = null!;
        private FormsPlot _formsPlot = null!;

        // Data storage
        private string _rawIniPayload = string.Empty;
        private readonly Dictionary<int, List<CorrectionPoint>> _channelPoints = new();
        private readonly Dictionary<int, (double[] Distances, double[] Temperatures)> _channelTempCurves = new();

        public event Action? TemperatureCorrectionClosed;
        public event Action<string>? OperationStatus;

        public class CorrectionPoint
        {
            public double Position { get; set; }
            public double CoeffA { get; set; }
            public double CoeffB { get; set; }
            public double TargetTemp { get; set; }
            public double ReserveCoeff { get; set; } = 0.001700;
        }

        public FormTemperatureCorrection(CustomWebSocketClient wsClient, string loginRole = "Admin")
        {
            _wsClient = wsClient ?? throw new ArgumentNullException(nameof(wsClient));
            _loginRole = loginRole;

            Instance = this;

            InitializeComponent();
            InitializeLayout();
            SetupPlot();

            this.FormClosed += (s, e) =>
            {
                Instance = null;
                TemperatureCorrectionClosed?.Invoke();
            };

            this.Load += (s, e) =>
            {
                FontSizeHelper.UpdateControlRecursive(this, FontSizeHelper.CurrentMultiplier);
            };

            this.Shown += async (s, e) =>
            {
                await LoadConfigurationFromHardwareAsync();
            };
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Text = "Temperature correction";
            this.Size = new Size(950, 680);
            this.MinimumSize = new Size(820, 580);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = System.Drawing.Color.FromArgb(240, 240, 240);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.ResumeLayout(false);
        }

        private void InitializeLayout()
        {
            var mainContainer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(12),
                BackColor = System.Drawing.Color.FromArgb(240, 240, 240)
            };

            mainContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 160F)); // Top configuration & inputs panel
            mainContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));  // Channel selector bar
            mainContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Graph region
            mainContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            // ====================================================================
            // SECTION 1: TOP PANEL (Table on Left, Inputs + Add/Correct on Right)
            // ====================================================================
            var topPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 8)
            };
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68F)); // Table
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32F)); // Inputs & Buttons
            topPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // 1.1 Left Side: DataGridView Table
            var pnlTable = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = System.Drawing.Color.White
            };

            _dgvCoefficients = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = true,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                BackgroundColor = System.Drawing.Color.White,
                BorderStyle = BorderStyle.None,
                GridColor = System.Drawing.Color.FromArgb(225, 225, 225),
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                EnableHeadersVisualStyles = false,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
                RowTemplate = { Height = 24 }
            };

            _dgvCoefficients.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            _dgvCoefficients.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _dgvCoefficients.ColumnHeadersDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(245, 245, 245);
            _dgvCoefficients.ColumnHeadersDefaultCellStyle.ForeColor = System.Drawing.Color.Black;
            _dgvCoefficients.ColumnHeadersHeight = 28;
            _dgvCoefficients.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            _dgvCoefficients.DefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            _dgvCoefficients.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _dgvCoefficients.DefaultCellStyle.SelectionBackColor = System.Drawing.Color.FromArgb(51, 153, 255);
            _dgvCoefficients.DefaultCellStyle.SelectionForeColor = System.Drawing.Color.White;

            _dgvCoefficients.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "position",
                Name = "ColPosition",
                Width = 85,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _dgvCoefficients.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "coefficient A",
                Name = "ColCoeffA",
                Width = 145,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _dgvCoefficients.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "coefficient B",
                Name = "ColCoeffB",
                Width = 145,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _dgvCoefficients.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "temp",
                Name = "ColTemp",
                Width = 75,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _dgvCoefficients.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Reserve coefficient",
                Name = "ColReserve",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            pnlTable.Controls.Add(_dgvCoefficients);
            topPanel.Controls.Add(pnlTable, 0, 0);

            // 1.2 Right Side: Inputs & Buttons Panel
            var pnlInputs = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 4, 4, 4),
                BackColor = System.Drawing.Color.FromArgb(240, 240, 240)
            };

            var inputsBorderPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 65,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = System.Drawing.Color.Transparent
            };

            var lblPosition = new Label
            {
                Text = "position",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Location = new Point(10, 8),
                AutoSize = true
            };
            _numPosition = new NumericUpDown
            {
                Location = new Point(62, 6),
                Width = 55,
                Minimum = 0,
                Maximum = 50000,
                Value = 0
            };

            var lblTemp = new Label
            {
                Text = "temp",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Location = new Point(125, 8),
                AutoSize = true
            };
            _numTemp = new NumericUpDown
            {
                Location = new Point(165, 6),
                Width = 50,
                Minimum = -50,
                Maximum = 300,
                Value = 0
            };

            _btnAdd = new Button
            {
                Text = "Add",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Location = new Point(222, 5),
                Size = new Size(58, 25),
                UseVisualStyleBackColor = true
            };
            _btnAdd.Click += (s, e) => BtnAdd_Click();

            inputsBorderPanel.Controls.Add(lblPosition);
            inputsBorderPanel.Controls.Add(_numPosition);
            inputsBorderPanel.Controls.Add(lblTemp);
            inputsBorderPanel.Controls.Add(_numTemp);
            inputsBorderPanel.Controls.Add(_btnAdd);

            _btnCorrect = new Button
            {
                Text = "Correct",
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                Location = new Point(40, 80),
                Size = new Size(210, 36),
                BackColor = System.Drawing.Color.FromArgb(215, 228, 242),
                FlatStyle = FlatStyle.Standard,
                UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand
            };
            _btnCorrect.Click += async (s, e) => await BtnCorrect_Click();

            pnlInputs.Controls.Add(inputsBorderPanel);
            pnlInputs.Controls.Add(_btnCorrect);
            topPanel.Controls.Add(pnlInputs, 1, 0);

            mainContainer.Controls.Add(topPanel, 0, 0);

            // ====================================================================
            // SECTION 2: CHANNEL SELECTOR (e.g. CH1 Dropdown)
            // ====================================================================
            var channelBar = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(4, 2, 4, 2),
                BackColor = System.Drawing.Color.FromArgb(240, 240, 240)
            };

            _cboChannel = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = System.Drawing.Color.FromArgb(180, 40, 20),
                Location = new Point(8, 4),
                Width = 85
            };

            for (int i = 1; i <= TotalChannels; i++)
            {
                _cboChannel.Items.Add($"CH{i}");
            }
            _cboChannel.SelectedIndex = 0;
            _cboChannel.SelectedIndexChanged += (s, e) =>
            {
                _selectedChannel = _cboChannel.SelectedIndex + 1;
                UpdateGridForSelectedChannel();
                UpdatePlotForSelectedChannel();
            };

            channelBar.Controls.Add(_cboChannel);
            mainContainer.Controls.Add(channelBar, 0, 1);

            // ====================================================================
            // SECTION 3: GRAPH REGION (ScottPlot FormsPlot)
            // ====================================================================
            var pnlGraph = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = System.Drawing.Color.White
            };

            _formsPlot = new FormsPlot
            {
                Dock = DockStyle.Fill,
                DisplayScale = 1F
            };

            pnlGraph.Controls.Add(_formsPlot);
            mainContainer.Controls.Add(pnlGraph, 0, 2);

            this.Controls.Add(mainContainer);
        }

        private void SetupPlot()
        {
            _formsPlot.Plot.Clear();

            // Styling matching screenshot
            _formsPlot.Plot.Axes.SetLimitsX(0, 7000);
            _formsPlot.Plot.Axes.SetLimitsY(0, 100);

            _formsPlot.Plot.XLabel("Distance (m)");
            _formsPlot.Plot.YLabel("Temperature (°C)");

            _formsPlot.Plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#D0DDF0");
            _formsPlot.Plot.Grid.MajorLineWidth = 1;

            _formsPlot.Plot.FigureBackground.Color = ScottPlot.Color.FromHex("#FFFFFF");
            _formsPlot.Plot.DataBackground.Color = ScottPlot.Color.FromHex("#FFFFFF");

            _formsPlot.Refresh();
        }

        #region Configuration Reading & Parsing

        public async Task LoadConfigurationFromHardwareAsync()
        {
            try
            {
                OperationStatus?.Invoke("Reading DTS configuration...");
                this.Cursor = Cursors.WaitCursor;

                var resp = await _wsClient.SendCommandAsync("GetDtsCalibration", new { });
                if (resp.HasValue && resp.Value.TryGetProperty("Payload", out var payload))
                {
                    if (payload.TryGetProperty("IniPayload", out var iniProp))
                    {
                        string rawIni = iniProp.GetString() ?? "";
                        if (!string.IsNullOrEmpty(rawIni))
                        {
                            ParseIniPayload(rawIni);
                            OperationStatus?.Invoke("DTS configuration loaded.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to load configuration: {ex.Message}", "Read Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.Cursor = Cursors.Default;
                UpdateGridForSelectedChannel();
                UpdatePlotForSelectedChannel();
            }
        }

        private void ParseIniPayload(string iniText)
        {
            if (string.IsNullOrWhiteSpace(iniText)) return;
            _rawIniPayload = iniText;
            _channelPoints.Clear();

            for (int ch = 1; ch <= TotalChannels; ch++)
            {
                var pointsList = new List<CorrectionPoint>();
                string sectionHeader = $"[ch{ch}_revisecoef]";
                int sectionStart = iniText.IndexOf(sectionHeader, StringComparison.OrdinalIgnoreCase);

                if (sectionStart >= 0)
                {
                    int nextSection = iniText.IndexOf("[", sectionStart + sectionHeader.Length);
                    string sectionContent = (nextSection > sectionStart)
                        ? iniText.Substring(sectionStart, nextSection - sectionStart)
                        : iniText.Substring(sectionStart);

                    var lines = sectionContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("[") || trimmed.StartsWith("//") || trimmed.StartsWith("#"))
                            continue;

                        // Match n1 = 50, -0.0000000086, -0.0000543162, 30.0000000000, 0.0017500000
                        var match = Regex.Match(trimmed, @"n\d+\s*=\s*([^,\s]+)\s*,\s*([^,\s]+)\s*,\s*([^,\s]+)\s*,\s*([^,\s]+)\s*,\s*([^,\s]+)", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            double.TryParse(match.Groups[1].Value, out double pos);
                            double.TryParse(match.Groups[2].Value, out double coeffA);
                            double.TryParse(match.Groups[3].Value, out double coeffB);
                            double.TryParse(match.Groups[4].Value, out double temp);
                            double.TryParse(match.Groups[5].Value, out double reserve);

                            pointsList.Add(new CorrectionPoint
                            {
                                Position = pos,
                                CoeffA = coeffA,
                                CoeffB = coeffB,
                                TargetTemp = temp,
                                ReserveCoeff = reserve
                            });
                        }
                    }
                }

                _channelPoints[ch] = pointsList;
            }
        }

        private void UpdateGridForSelectedChannel()
        {
            _dgvCoefficients.Rows.Clear();

            if (_channelPoints.TryGetValue(_selectedChannel, out var points))
            {
                foreach (var pt in points)
                {
                    _dgvCoefficients.Rows.Add(
                        pt.Position.ToString("0"),
                        pt.CoeffA.ToString("0.##########"),
                        pt.CoeffB.ToString("0.##########"),
                        pt.TargetTemp.ToString("0"),
                        pt.ReserveCoeff.ToString("0.#####")
                    );
                }
            }
        }

        private void UpdatePlotForSelectedChannel()
        {
            _formsPlot.Plot.Clear();

            // 1. Plot temperature curve (from cache or generate sample baseline)
            double[] distances;
            double[] temperatures;

            if (_channelTempCurves.TryGetValue(_selectedChannel, out var data) && data.Distances.Length > 0)
            {
                distances = data.Distances;
                temperatures = data.Temperatures;
            }
            else
            {
                // Generate default baseline profile
                int pointsCount = 700;
                distances = new double[pointsCount];
                temperatures = new double[pointsCount];
                for (int i = 0; i < pointsCount; i++)
                {
                    distances[i] = i * 10.0;
                    temperatures[i] = 25.0 + Math.Sin(i * 0.05) * 1.5;
                }
            }

            var curve = _formsPlot.Plot.Add.Scatter(distances, temperatures);
            curve.Color = ScottPlot.Colors.Blue;
            curve.LineWidth = 1.5F;
            curve.MarkerSize = 0;

            // 2. Mark correction points
            if (_channelPoints.TryGetValue(_selectedChannel, out var points) && points.Count > 0)
            {
                var ptX = points.Select(p => p.Position).ToArray();
                var ptY = points.Select(p => p.TargetTemp).ToArray();

                var scatterPoints = _formsPlot.Plot.Add.Scatter(ptX, ptY);
                scatterPoints.Color = ScottPlot.Colors.Red;
                scatterPoints.LineWidth = 0;
                scatterPoints.MarkerSize = 7;

                foreach (var p in points)
                {
                    var vLine = _formsPlot.Plot.Add.VerticalLine(p.Position);
                    vLine.Color = ScottPlot.Colors.OrangeRed.WithAlpha(0.6);
                    vLine.LineWidth = 1;
                    vLine.LinePattern = ScottPlot.LinePattern.Dotted;
                }
            }

            _formsPlot.Plot.Axes.SetLimitsX(0, distances.Length > 0 ? Math.Max(7000, distances.Max()) : 7000);
            _formsPlot.Plot.Axes.SetLimitsY(0, 100);

            _formsPlot.Plot.XLabel("Distance (m)");
            _formsPlot.Plot.YLabel("Temperature (°C)");
            _formsPlot.Plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#D0DDF0");

            _formsPlot.Refresh();
        }

        #endregion

        #region Actions (Add & Correct)

        private void BtnAdd_Click()
        {
            double pos = (double)_numPosition.Value;
            double temp = (double)_numTemp.Value;

            if (!_channelPoints.ContainsKey(_selectedChannel))
            {
                _channelPoints[_selectedChannel] = new List<CorrectionPoint>();
            }

            var list = _channelPoints[_selectedChannel];
            var existing = list.FirstOrDefault(p => Math.Abs(p.Position - pos) < 0.1);
            if (existing != null)
            {
                existing.TargetTemp = temp;
            }
            else
            {
                list.Add(new CorrectionPoint
                {
                    Position = pos,
                    CoeffA = 0,
                    CoeffB = 0,
                    TargetTemp = temp,
                    ReserveCoeff = 0.001700
                });
            }

            // Sort by position
            _channelPoints[_selectedChannel] = list.OrderBy(p => p.Position).ToList();

            UpdateGridForSelectedChannel();
            UpdatePlotForSelectedChannel();
        }

        private async Task BtnCorrect_Click()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_rawIniPayload))
                {
                    MessageBox.Show(this, "No DTS configuration loaded. Please read configuration first.", "Configuration Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Collect any edits from DataGridView back into _channelPoints
                var currentList = new List<CorrectionPoint>();
                for (int i = 0; i < _dgvCoefficients.Rows.Count; i++)
                {
                    var row = _dgvCoefficients.Rows[i];
                    double.TryParse(row.Cells["ColPosition"].Value?.ToString(), out double pos);
                    double.TryParse(row.Cells["ColCoeffA"].Value?.ToString(), out double coeffA);
                    double.TryParse(row.Cells["ColCoeffB"].Value?.ToString(), out double coeffB);
                    double.TryParse(row.Cells["ColTemp"].Value?.ToString(), out double temp);
                    double.TryParse(row.Cells["ColReserve"].Value?.ToString(), out double reserve);

                    if (reserve <= 0) reserve = 0.001700;

                    currentList.Add(new CorrectionPoint
                    {
                        Position = pos,
                        CoeffA = coeffA,
                        CoeffB = coeffB,
                        TargetTemp = temp,
                        ReserveCoeff = reserve
                    });
                }
                _channelPoints[_selectedChannel] = currentList;

                _btnCorrect.Enabled = false;
                this.Cursor = Cursors.WaitCursor;
                OperationStatus?.Invoke("Applying temperature correction coefficients...");

                string modifiedIni = BuildModifiedIni();

                var response = await _wsClient.SendCommandAsync("SetDtsCalibration", new
                {
                    IniPayload = modifiedIni
                });

                if (response.HasValue && response.Value.TryGetProperty("Payload", out var payload))
                {
                    bool isSuccess = payload.TryGetProperty("Success", out var successProp) && successProp.GetBoolean();
                    if (isSuccess)
                    {
                        _rawIniPayload = modifiedIni;
                        OperationStatus?.Invoke("Temperature correction applied successfully.");
                        MessageBox.Show(this, "Correction applied and saved to DTS hardware successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        string errorMsg = payload.TryGetProperty("Error", out var errProp) ? errProp.GetString() ?? "Unknown error" : "Failed to write DTS configuration";
                        MessageBox.Show(this, $"Failed to apply temperature correction: {errorMsg}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    MessageBox.Show(this, "No response received from FRMC service.", "Communication Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error applying correction: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.Cursor = Cursors.Default;
                _btnCorrect.Enabled = true;
                UpdatePlotForSelectedChannel();
            }
        }

        private string BuildModifiedIni()
        {
            string ini = _rawIniPayload;

            for (int ch = 1; ch <= TotalChannels; ch++)
            {
                if (!_channelPoints.TryGetValue(ch, out var points))
                    continue;

                string sectionHeader = $"[ch{ch}_revisecoef]";
                var sbSection = new StringBuilder();
                sbSection.AppendLine(sectionHeader);

                for (int i = 0; i < points.Count; i++)
                {
                    var p = points[i];
                    sbSection.AppendLine($"n{i + 1} = {p.Position:0},{p.CoeffA:0.0000000000},{p.CoeffB:0.0000000000},{p.TargetTemp:0.0000000000},{p.ReserveCoeff:0.0000000000}");
                }

                int sectionStart = ini.IndexOf(sectionHeader, StringComparison.OrdinalIgnoreCase);
                if (sectionStart >= 0)
                {
                    int nextSection = ini.IndexOf("[", sectionStart + sectionHeader.Length);
                    if (nextSection > sectionStart)
                    {
                        ini = ini.Substring(0, sectionStart) + sbSection.ToString().TrimEnd() + "\r\n\r\n" + ini.Substring(nextSection);
                    }
                    else
                    {
                        ini = ini.Substring(0, sectionStart) + sbSection.ToString().TrimEnd() + "\r\n";
                    }
                }
                else
                {
                    ini += $"\r\n{sbSection}";
                }
            }

            return ini;
        }

        #endregion

        /// <summary>
        /// Updates live channel temperature curve for plotting in the correction window.
        /// </summary>
        public void UpdateLiveChannelData(int channelId, double[] distances, double[] temperatures)
        {
            if (channelId <= 0 || distances == null || temperatures == null) return;
            _channelTempCurves[channelId] = (distances, temperatures);

            if (_selectedChannel == channelId && !this.IsDisposed && this.IsHandleCreated)
            {
                this.BeginInvoke((MethodInvoker)delegate
                {
                    UpdatePlotForSelectedChannel();
                });
            }
        }
    }
}
