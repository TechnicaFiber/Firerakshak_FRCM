using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
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
        public Func<int, (double[] Distances, double[] Temperatures)?>? ChannelDataRequest { get; set; }

        public class CorrectionPoint
        {
            // Numeric values for graphing and validation
            public double Position { get; set; }
            public double TargetTemp { get; set; }
            public double? CoeffA { get; set; }
            public double? CoeffB { get; set; }
            public double? ReserveCoeff { get; set; }

            // Raw string representations to preserve exact INI precision and formatting
            public string RawPosition { get; set; } = string.Empty;
            public string RawTargetTemp { get; set; } = string.Empty;
            public string RawCoeffA { get; set; } = string.Empty;
            public string RawCoeffB { get; set; } = string.Empty;
            public string RawReserveCoeff { get; set; } = string.Empty;

            // Unambiguous state identification
            public bool IsCalculated => CoeffA.HasValue && CoeffB.HasValue && ReserveCoeff.HasValue;
            public bool IsPending => !IsCalculated;
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
                EnsureChannelCurveLoaded(_selectedChannel);
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
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                MultiSelect = false,
                EnableHeadersVisualStyles = true,
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
            _dgvCoefficients.DefaultCellStyle.BackColor = System.Drawing.Color.White;
            _dgvCoefficients.DefaultCellStyle.ForeColor = System.Drawing.Color.Black;
            _dgvCoefficients.DefaultCellStyle.SelectionBackColor = System.Drawing.Color.FromArgb(235, 242, 250);
            _dgvCoefficients.DefaultCellStyle.SelectionForeColor = System.Drawing.Color.Black;

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

            // Context menu for deleting positions via right click
            var dgvContextMenu = new ContextMenuStrip();
            var deleteMenuItem = new ToolStripMenuItem("Delete the selected position")
            {
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            int contextMenuTargetRow = -1;

            deleteMenuItem.Click += async (s, e) =>
            {
                if (contextMenuTargetRow >= 0)
                {
                    await DeletePositionAt(contextMenuTargetRow);
                }
            };
            dgvContextMenu.Items.Add(deleteMenuItem);

            _dgvCoefficients.CellMouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Right && e.RowIndex >= 0)
                {
                    contextMenuTargetRow = e.RowIndex;
                    _dgvCoefficients.ClearSelection();
                    if (e.ColumnIndex >= 0)
                    {
                        _dgvCoefficients.CurrentCell = _dgvCoefficients.Rows[e.RowIndex].Cells[e.ColumnIndex];
                    }
                    _dgvCoefficients.Rows[e.RowIndex].Selected = true;
                    dgvContextMenu.Show(System.Windows.Forms.Cursor.Position);
                }
            };

            _dgvCoefficients.KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.Delete && _dgvCoefficients.CurrentCell != null && _dgvCoefficients.CurrentCell.RowIndex >= 0)
                {
                    await DeletePositionAt(_dgvCoefficients.CurrentCell.RowIndex);
                }
            };

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
                Location = new Point(8, 8),
                AutoSize = true
            };
            _numPosition = new NumericUpDown
            {
                Location = new Point(60, 6),
                Width = 62,
                Minimum = 0,
                Maximum = 50000,
                DecimalPlaces = 2,
                Increment = 0.1M,
                Value = 0
            };

            var lblTemp = new Label
            {
                Text = "temp",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Location = new Point(128, 8),
                AutoSize = true
            };
            _numTemp = new NumericUpDown
            {
                Location = new Point(168, 6),
                Width = 58,
                Minimum = -50,
                Maximum = 300,
                DecimalPlaces = 2,
                Increment = 0.5M,
                Value = 0
            };

            _btnAdd = new Button
            {
                Text = "Add",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Location = new Point(232, 5),
                Size = new Size(54, 25),
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
                EnsureChannelCurveLoaded(_selectedChannel);
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
                            string rawPos = match.Groups[1].Value.Trim().Trim('"', '\'');
                            string rawCoeffA = match.Groups[2].Value.Trim().Trim('"', '\'');
                            string rawCoeffB = match.Groups[3].Value.Trim().Trim('"', '\'');
                            string rawTemp = match.Groups[4].Value.Trim().Trim('"', '\'');
                            string rawReserve = match.Groups[5].Value.Trim().Trim('"', '\'');

                            double.TryParse(rawPos, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double pos);
                            double.TryParse(rawCoeffA, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double coeffA);
                            double.TryParse(rawCoeffB, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double coeffB);
                            double.TryParse(rawTemp, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double temp);
                            double.TryParse(rawReserve, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double reserve);

                            pointsList.Add(new CorrectionPoint
                            {
                                Position = pos,
                                TargetTemp = temp,
                                CoeffA = coeffA,
                                CoeffB = coeffB,
                                ReserveCoeff = reserve,
                                RawPosition = rawPos,
                                RawTargetTemp = rawTemp,
                                RawCoeffA = rawCoeffA,
                                RawCoeffB = rawCoeffB,
                                RawReserveCoeff = rawReserve
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
                    string posStr = !string.IsNullOrEmpty(pt.RawPosition) ? pt.RawPosition : pt.Position.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    string tempStr = !string.IsNullOrEmpty(pt.RawTargetTemp) ? pt.RawTargetTemp : pt.TargetTemp.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    string coeffAStr = pt.IsCalculated ? (!string.IsNullOrEmpty(pt.RawCoeffA) ? pt.RawCoeffA : pt.CoeffA!.Value.ToString("G")) : "";
                    string coeffBStr = pt.IsCalculated ? (!string.IsNullOrEmpty(pt.RawCoeffB) ? pt.RawCoeffB : pt.CoeffB!.Value.ToString("G")) : "";
                    string reserveStr = pt.IsCalculated ? (!string.IsNullOrEmpty(pt.RawReserveCoeff) ? pt.RawReserveCoeff : pt.ReserveCoeff!.Value.ToString("G")) : "";

                    _dgvCoefficients.Rows.Add(
                        posStr,
                        coeffAStr,
                        coeffBStr,
                        tempStr,
                        reserveStr
                    );
                }
            }

            _dgvCoefficients.ClearSelection();
        }

        private void UpdatePlotForSelectedChannel()
        {
            _formsPlot.Plot.Clear();

            double maxX = 50;

            // Plot temperature curve when real DTS data is available
            if (_channelTempCurves.TryGetValue(_selectedChannel, out var data) && data.Distances != null && data.Distances.Length > 0 && data.Temperatures != null && data.Temperatures.Length > 0)
            {
                var curve = _formsPlot.Plot.Add.Scatter(data.Distances, data.Temperatures);
                curve.Color = ScottPlot.Colors.Blue;
                curve.LineWidth = 1.5F;
                curve.MarkerSize = 0;

                maxX = Math.Max(maxX, data.Distances.Max());
            }
            else if (_channelPoints.TryGetValue(_selectedChannel, out var points) && points.Count > 0)
            {
                maxX = Math.Max(maxX, points.Max(p => p.Position));
            }

            _formsPlot.Plot.Axes.SetLimitsX(0, Math.Max(50, maxX * 1.05));
            _formsPlot.Plot.Axes.SetLimitsY(0, 100);

            _formsPlot.Plot.XLabel("Distance (m)");
            _formsPlot.Plot.YLabel("Temperature (°C)");
            _formsPlot.Plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#D0DDF0");

            _formsPlot.Refresh();
        }

        #endregion

        #region Actions (Add & Correct & Delete)

        private async Task DeletePositionAt(int rowIndex)
        {
            if (!_channelPoints.TryGetValue(_selectedChannel, out var points) || rowIndex < 0 || rowIndex >= points.Count)
                return;

            try
            {
                var pt = points[rowIndex];
                string posStr = !string.IsNullOrEmpty(pt.RawPosition) ? pt.RawPosition : pt.Position.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

                this.Cursor = Cursors.WaitCursor;
                OperationStatus?.Invoke($"Deleting position {posStr}m from DTS hardware...");

                // Remove from local list
                points.RemoveAt(rowIndex);
                UpdateGridForSelectedChannel();

                // Build modified INI with this position removed
                string modifiedIni = BuildModifiedIni();

                // Send to DTS hardware to sync deletion immediately
                var response = await _wsClient.SendCommandAsync("SetDtsCalibration", new
                {
                    IniPayload = modifiedIni,
                    ChannelId = _selectedChannel
                });

                if (response.HasValue && response.Value.TryGetProperty("Payload", out var payload))
                {
                    bool isSuccess = payload.TryGetProperty("Success", out var successProp) && successProp.GetBoolean();
                    if (isSuccess)
                    {
                        string readBackIni = string.Empty;
                        if (payload.TryGetProperty("IniPayload", out var readBackIniProp))
                        {
                            readBackIni = readBackIniProp.GetString() ?? string.Empty;
                        }

                        if (!string.IsNullOrEmpty(readBackIni))
                        {
                            ParseIniPayload(readBackIni);
                        }
                        else
                        {
                            _rawIniPayload = modifiedIni;
                        }

                        OperationStatus?.Invoke($"Position {posStr}m deleted from DTS hardware successfully.");
                    }
                    else
                    {
                        string errorMsg = payload.TryGetProperty("Error", out var errProp) ? errProp.GetString() ?? "Unknown error" : "Failed to delete position from DTS";
                        OperationStatus?.Invoke($"Failed to delete position from DTS: {errorMsg}");
                    }
                }
            }
            catch (Exception ex)
            {
                OperationStatus?.Invoke($"Error deleting position: {ex.Message}");
            }
            finally
            {
                this.Cursor = Cursors.Default;
                UpdateGridForSelectedChannel();
                UpdatePlotForSelectedChannel();
            }
        }

        private void BtnAdd_Click()
        {
            double pos = (double)_numPosition.Value;
            double temp = (double)_numTemp.Value;

            if (!_channelPoints.ContainsKey(_selectedChannel))
            {
                _channelPoints[_selectedChannel] = new List<CorrectionPoint>();
            }

            var list = _channelPoints[_selectedChannel];
            var existing = list.FirstOrDefault(p => Math.Abs(p.Position - pos) < 0.005);
            if (existing != null)
            {
                existing.TargetTemp = temp;
                existing.RawPosition = pos.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                existing.RawTargetTemp = temp.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                // Mark pending since position/temp updated
                existing.CoeffA = null;
                existing.CoeffB = null;
                existing.ReserveCoeff = null;
                existing.RawCoeffA = string.Empty;
                existing.RawCoeffB = string.Empty;
                existing.RawReserveCoeff = string.Empty;
            }
            else
            {
                list.Add(new CorrectionPoint
                {
                    Position = pos,
                    TargetTemp = temp,
                    RawPosition = pos.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
                    RawTargetTemp = temp.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
                    CoeffA = null,
                    CoeffB = null,
                    ReserveCoeff = null
                });
            }

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

                // Sync any grid values back to _channelPoints
                if (_channelPoints.TryGetValue(_selectedChannel, out var currentPoints))
                {
                    for (int i = 0; i < Math.Min(_dgvCoefficients.Rows.Count, currentPoints.Count); i++)
                    {
                        var row = _dgvCoefficients.Rows[i];
                        if (double.TryParse(row.Cells["ColPosition"].Value?.ToString(), out double pos))
                            currentPoints[i].Position = pos;
                        if (double.TryParse(row.Cells["ColTemp"].Value?.ToString(), out double temp))
                            currentPoints[i].TargetTemp = temp;
                    }
                }

                _btnCorrect.Enabled = false;
                this.Cursor = Cursors.WaitCursor;
                OperationStatus?.Invoke("Applying temperature correction and querying DTS...");

                string modifiedIni = BuildModifiedIni();

                var response = await _wsClient.SendCommandAsync("SetDtsCalibration", new
                {
                    IniPayload = modifiedIni,
                    ChannelId = _selectedChannel
                });

                if (response.HasValue && response.Value.TryGetProperty("Payload", out var payload))
                {
                    bool isSuccess = payload.TryGetProperty("Success", out var successProp) && successProp.GetBoolean();
                    if (isSuccess)
                    {
                        string readBackIni = string.Empty;
                        if (payload.TryGetProperty("IniPayload", out var readBackIniProp))
                        {
                            readBackIni = readBackIniProp.GetString() ?? string.Empty;
                        }

                        // Fallback: If FRMC response did not include IniPayload, actively query GetDtsCalibration to read back from DTS hardware
                        if (string.IsNullOrEmpty(readBackIni))
                        {
                            var getResp = await _wsClient.SendCommandAsync("GetDtsCalibration", new { });
                            if (getResp.HasValue && getResp.Value.TryGetProperty("Payload", out var getPayload))
                            {
                                if (getPayload.TryGetProperty("IniPayload", out var iniProp))
                                {
                                    readBackIni = iniProp.GetString() ?? string.Empty;
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(readBackIni))
                        {
                            ParseIniPayload(readBackIni);
                        }
                        else
                        {
                            _rawIniPayload = modifiedIni;
                        }

                        // Update plot immediately from Points payload if returned
                        if (payload.TryGetProperty("Points", out var ptsElem) && ptsElem.ValueKind == JsonValueKind.Array)
                        {
                            var distList = new List<double>();
                            var tempList = new List<double>();
                            foreach (var p in ptsElem.EnumerateArray())
                            {
                                if (p.TryGetProperty("Position", out var posProp) && p.TryGetProperty("Temperature", out var tempProp))
                                {
                                    distList.Add(posProp.GetDouble());
                                    tempList.Add(tempProp.GetDouble());
                                }
                            }
                            if (distList.Count > 0)
                            {
                                UpdateLiveChannelData(_selectedChannel, distList.ToArray(), tempList.ToArray());
                            }
                        }

                        // Baseline graph shift fallback: if measurement is stopped or idle, immediately adjust display curve by calibration delta
                        if (_channelPoints.TryGetValue(_selectedChannel, out var chPoints) && chPoints.Count > 0 &&
                            _channelTempCurves.TryGetValue(_selectedChannel, out var currentCurve) &&
                            currentCurve.Distances != null && currentCurve.Temperatures != null && currentCurve.Temperatures.Length > 0)
                        {
                            var refPt = chPoints.FirstOrDefault(p => p.TargetTemp > -100);
                            if (refPt != null)
                            {
                                int bestIdx = 0;
                                double bestDistDiff = double.MaxValue;
                                for (int i = 0; i < currentCurve.Distances.Length; i++)
                                {
                                    double diff = Math.Abs(currentCurve.Distances[i] - refPt.Position);
                                    if (diff < bestDistDiff)
                                    {
                                        bestDistDiff = diff;
                                        bestIdx = i;
                                    }
                                }

                                double currentTempAtPos = currentCurve.Temperatures[bestIdx];
                                if (currentTempAtPos > -200)
                                {
                                    double deltaT = refPt.TargetTemp - currentTempAtPos;
                                    var newTemps = new double[currentCurve.Temperatures.Length];
                                    for (int i = 0; i < currentCurve.Temperatures.Length; i++)
                                    {
                                        if (currentCurve.Temperatures[i] > -200)
                                            newTemps[i] = Math.Round(currentCurve.Temperatures[i] + deltaT, 2);
                                        else
                                            newTemps[i] = currentCurve.Temperatures[i];
                                    }
                                    _channelTempCurves[_selectedChannel] = (currentCurve.Distances, newTemps);
                                    UpdatePlotForSelectedChannel();
                                }
                            }
                        }

                        // Also actively query fresh channel curve from FRMC / DTS
                        await RequestChannelCurveAsync(_selectedChannel);

                        OperationStatus?.Invoke("Temperature correction applied and read back from DTS successfully.");
                        MessageBox.Show(this, "Correction applied and updated from DTS hardware successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        string errorMsg = payload.TryGetProperty("Error", out var errProp) ? errProp.GetString() ?? "Unknown error" : "Failed to apply DTS configuration";
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
                UpdateGridForSelectedChannel();
                UpdatePlotForSelectedChannel();
            }
        }

        private string BuildModifiedIni()
        {
            string ini = _rawIniPayload;

            for (int ch = 1; ch <= TotalChannels; ch++)
            {
                string sectionHeader = $"[ch{ch}_revisecoef]";
                var sbSection = new StringBuilder();
                sbSection.AppendLine(sectionHeader);

                if (_channelPoints.TryGetValue(ch, out var points) && points != null && points.Count > 0)
                {
                    for (int i = 0; i < points.Count; i++)
                {
                    var p = points[i];
                    string pos = !string.IsNullOrEmpty(p.RawPosition) ? p.RawPosition : p.Position.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    string coeffA = !string.IsNullOrEmpty(p.RawCoeffA) ? p.RawCoeffA : (p.CoeffA.HasValue ? p.CoeffA.Value.ToString("0.0000000000", System.Globalization.CultureInfo.InvariantCulture) : "0");
                    string coeffB = !string.IsNullOrEmpty(p.RawCoeffB) ? p.RawCoeffB : (p.CoeffB.HasValue ? p.CoeffB.Value.ToString("0.0000000000", System.Globalization.CultureInfo.InvariantCulture) : "0");
                    string temp = !string.IsNullOrEmpty(p.RawTargetTemp) ? p.RawTargetTemp : p.TargetTemp.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    string reserve = !string.IsNullOrEmpty(p.RawReserveCoeff) ? p.RawReserveCoeff : (p.ReserveCoeff.HasValue ? p.ReserveCoeff.Value.ToString("0.0000000000", System.Globalization.CultureInfo.InvariantCulture) : "0.0017000000");

                    sbSection.AppendLine($"n{i + 1} = {pos},{coeffA},{coeffB},{temp},{reserve}");
                    }
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
            if (channelId <= 0 || distances == null || temperatures == null || distances.Length == 0) return;

            // Reject idle/standby packets where all points are -273.1°C (laser off)
            bool hasValidData = false;
            for (int i = 0; i < Math.Min(50, temperatures.Length); i++)
            {
                if (temperatures[i] > -200)
                {
                    hasValidData = true;
                    break;
                }
            }
            if (!hasValidData) return;

            _channelTempCurves[channelId] = (distances, temperatures);

            if (_selectedChannel == channelId && !this.IsDisposed && this.IsHandleCreated)
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        UpdatePlotForSelectedChannel();
                    });
                }
                else
                {
                    UpdatePlotForSelectedChannel();
                }
            }
        }

        private void EnsureChannelCurveLoaded(int channelId)
        {
            if (!_channelTempCurves.ContainsKey(channelId) && ChannelDataRequest != null)
            {
                var cached = ChannelDataRequest(channelId);
                if (cached != null && cached.Value.Distances != null && cached.Value.Distances.Length > 0)
                {
                    _channelTempCurves[channelId] = cached.Value;
                    UpdatePlotForSelectedChannel();
                    return;
                }
            }

            if (!_channelTempCurves.ContainsKey(channelId))
            {
                _ = RequestChannelCurveAsync(channelId);
            }
        }

        public async Task RequestChannelCurveAsync(int channelId)
        {
            try
            {
                var resp = await _wsClient.SendCommandAsync("GetChannelPoints", new { ChannelId = channelId });
                if (resp.HasValue && resp.Value.TryGetProperty("Payload", out var payload))
                {
                    if (payload.TryGetProperty("Points", out var ptsElem) && ptsElem.ValueKind == JsonValueKind.Array)
                    {
                        var distList = new List<double>();
                        var tempList = new List<double>();
                        foreach (var p in ptsElem.EnumerateArray())
                        {
                            if (p.TryGetProperty("Position", out var posProp) && p.TryGetProperty("Temperature", out var tempProp))
                            {
                                distList.Add(posProp.GetDouble());
                                tempList.Add(tempProp.GetDouble());
                            }
                        }
                        if (distList.Count > 0)
                        {
                            UpdateLiveChannelData(channelId, distList.ToArray(), tempList.ToArray());
                        }
                    }
                }
            }
            catch
            {
                // Non-critical fallback
            }
        }
    }
}
