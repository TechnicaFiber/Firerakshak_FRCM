using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace FRCM
{
    // ===============================
    // MODELS — MATCH FRMC FORMAT
    // ===============================
    public class AlarmIndex
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public AlarmThreshold Threshold { get; set; } = new AlarmThreshold();
    }

    public class AlarmThreshold
    {
        public bool UseMaxAlert1 { get; set; }
        public double MaxAlert1 { get; set; }

        public bool UseMaxAlert2 { get; set; }
        public double MaxAlert2 { get; set; }

        public bool UseMax { get; set; }
        public double Max { get; set; }

        public bool UseMin { get; set; }
        public double Min { get; set; }

        public bool UseRateOfChange { get; set; }
        public double RateOfChange { get; set; }

        public bool UseDeviation { get; set; }
        public double Deviation { get; set; }
    }

    public class AlarmMapping
    {
        public string Channel { get; set; } = "";
        public string ZoneName { get; set; } = "";
        public int RelayIndex { get; set; }
        public double StartMeter { get; set; }
        public double StopMeter { get; set; }
        public int AlarmIndexId { get; set; }
        public bool Active { get; set; }
    }

    public class AlarmConfigurationPayload
    {
        public List<AlarmIndex> Indices { get; set; } = new();
        public List<AlarmMapping> Mappings { get; set; } = new();
    }

    // ===============================
    // MAIN FORM
    // ===============================
    public class AlarmConfigurationForm : Form
    {
        // Stored data
        private readonly Dictionary<string, List<ZoneInfo>>? channelZones;
        private readonly List<AlarmIndex>? alarmIndices;
        private readonly List<AlarmMapping>? alarmMappings;

        // Updated data returned to parent
        public List<AlarmIndex>? UpdatedAlarmIndices { get; private set; }
        public List<AlarmMapping>? UpdatedAlarmMappings { get; private set; }

        // Controls
        private ComboBox? cmbChannel;
        private ComboBox? cmbZone;
        private ListBox? lbAlarmIndex;

        private TextBox? txtAlarmIndexName;

        private CheckBox? chkMaxAlert1; private TextBox? txtMaxAlert1;
        private CheckBox? chkMaxAlert2; private TextBox? txtMaxAlert2;
        private CheckBox? chkMax; private TextBox? txtMax;
        private CheckBox? chkMin; private TextBox? txtMin;
        private CheckBox? chkRate; private TextBox? txtRate;
        private CheckBox? chkDeviation; private TextBox? txtDeviation;

        private DataGridView? dgvMappings;

        private Button? btnRead;
        private Button? btnApply;

        // ===============================
        // CONSTRUCTOR
        // ===============================
        public AlarmConfigurationForm(
            Dictionary<string, List<ZoneInfo>> channelZonesInput,
            IEnumerable<AlarmIndex>? existingIndices = null,
            IEnumerable<AlarmMapping>? existingMappings = null)
        {
            // Deep copy channel zones
            channelZones = channelZonesInput.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.Select(
                        z => new ZoneInfo
                        {
                            Name = z.Name,
                            StartPoint = z.StartPoint,
                            EndPoint = z.EndPoint,
                            MaxTemp = z.MaxTemp,
                            MinTemp = z.MinTemp,
                            RoRThreshold = z.RoRThreshold,
                            Enabled = z.Enabled
                        }).ToList()
            );

            // Deep copy alarm indices
            alarmIndices = existingIndices?.Select(i => new AlarmIndex
            {
                Id = i.Id,
                Name = i.Name,
                Threshold = JsonSerializer.Deserialize<AlarmThreshold>(
                    JsonSerializer.Serialize(i.Threshold)
                ) ?? new AlarmThreshold()
            }).ToList() ?? new List<AlarmIndex>();

            // Deep copy alarm mappings
            alarmMappings = existingMappings?.Select(m => new AlarmMapping
            {
                Channel = m.Channel,
                ZoneName = m.ZoneName,
                RelayIndex = m.RelayIndex,
                StartMeter = m.StartMeter,
                StopMeter = m.StopMeter,
                AlarmIndexId = m.AlarmIndexId,
                Active = m.Active
            }).ToList() ?? new List<AlarmMapping>();

            InitializeComponent();
            PopulateChannels();
            RefreshAlarmIndexList();
            RefreshMappingsGrid();
        }

        // ===============================
        // UI INITIALIZATION
        // ===============================
        private void InitializeComponent()
        {
            this.Text = "Alarm Configuration";
            this.Size = new Size(1000, 700);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            int margin = 12;

            // Channel
            var lblChannel = new Label { Text = "Select Channel:", Location = new Point(margin, 12), AutoSize = true };
            cmbChannel = new ComboBox { Location = new Point(130, 8), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbChannel.SelectedIndexChanged += (s, e) => RefreshZonesForChannel();

            var lblZone = new Label { Text = "Select Zone:", Location = new Point(350, 12), AutoSize = true };
            cmbZone = new ComboBox { Location = new Point(430, 8), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };

            this.Controls.Add(lblChannel);
            this.Controls.Add(cmbChannel);
            this.Controls.Add(lblZone);
            this.Controls.Add(cmbZone);

            // Alarm Index List
            lbAlarmIndex = new ListBox { Location = new Point(12, 40), Size = new Size(240, 300) };
            lbAlarmIndex.SelectedIndexChanged += (s, e) => LoadSelectedIndexIntoUI();
            this.Controls.Add(lbAlarmIndex);

            var btnAddIndex = new Button { Text = "Add Index", Location = new Point(12, 350), Size = new Size(110, 30) };
            btnAddIndex.Click += (s, e) => AddAlarmIndex();
            this.Controls.Add(btnAddIndex);

            var btnDeleteIndex = new Button { Text = "Delete", Location = new Point(132, 350), Size = new Size(110, 30) };
            btnDeleteIndex.Click += (s, e) => DeleteSelectedAlarmIndex();
            this.Controls.Add(btnDeleteIndex);

            // Thresholds Group
            var grpThreshold = new GroupBox
            {
                Text = "Alarm Thresholds",
                Location = new Point(260, 40),
                Size = new Size(700, 260)
            };
            this.Controls.Add(grpThreshold);

            int chkX = 12, lblX = 36, valX = 200;
            int rowY = 22, rowH = 32;

            grpThreshold.Controls.Add(new Label { Text = "Alarm Index Name:", Location = new Point(lblX, rowY), AutoSize = true });
            txtAlarmIndexName = new TextBox { Location = new Point(valX, rowY - 4), Width = 220 };
            grpThreshold.Controls.Add(txtAlarmIndexName);

            // Row 1
            rowY += rowH;
            chkMaxAlert1 = new CheckBox { Location = new Point(chkX, rowY + 3) };
            grpThreshold.Controls.Add(chkMaxAlert1);
            grpThreshold.Controls.Add(new Label { Text = "Max Alert 1 (°C):", Location = new Point(lblX, rowY), AutoSize = true });
            txtMaxAlert1 = new TextBox { Location = new Point(valX, rowY - 3), Width = 100 };
            grpThreshold.Controls.Add(txtMaxAlert1);

            // Row 2
            rowY += rowH;
            chkMaxAlert2 = new CheckBox { Location = new Point(chkX, rowY + 3) };
            grpThreshold.Controls.Add(chkMaxAlert2);
            grpThreshold.Controls.Add(new Label { Text = "Max Alert 2 (°C):", Location = new Point(lblX, rowY), AutoSize = true });
            txtMaxAlert2 = new TextBox { Location = new Point(valX, rowY - 3), Width = 100 };
            grpThreshold.Controls.Add(txtMaxAlert2);

            // Row 3
            rowY += rowH;
            chkMax = new CheckBox { Location = new Point(chkX, rowY + 3) };
            grpThreshold.Controls.Add(chkMax);
            grpThreshold.Controls.Add(new Label { Text = "Max (°C):", Location = new Point(lblX, rowY), AutoSize = true });
            txtMax = new TextBox { Location = new Point(valX, rowY - 3), Width = 100 };
            grpThreshold.Controls.Add(txtMax);

            // Row 4
            rowY += rowH;
            chkMin = new CheckBox { Location = new Point(chkX, rowY + 3) };
            grpThreshold.Controls.Add(chkMin);
            grpThreshold.Controls.Add(new Label { Text = "Min (°C):", Location = new Point(lblX, rowY), AutoSize = true });
            txtMin = new TextBox { Location = new Point(valX, rowY - 3), Width = 100 };
            grpThreshold.Controls.Add(txtMin);

            // Row 5
            rowY += rowH;
            chkRate = new CheckBox { Location = new Point(chkX, rowY + 3) };
            grpThreshold.Controls.Add(chkRate);
            grpThreshold.Controls.Add(new Label { Text = "Rate of Change:", Location = new Point(lblX, rowY), AutoSize = true });
            txtRate = new TextBox { Location = new Point(valX, rowY - 3), Width = 100 };
            grpThreshold.Controls.Add(txtRate);

            // Row 6
            rowY += rowH;
            chkDeviation = new CheckBox { Location = new Point(chkX, rowY + 3) };
            grpThreshold.Controls.Add(chkDeviation);
            grpThreshold.Controls.Add(new Label { Text = "Deviation (°C):", Location = new Point(lblX, rowY), AutoSize = true });
            txtDeviation = new TextBox { Location = new Point(valX, rowY - 3), Width = 100 };
            grpThreshold.Controls.Add(txtDeviation);

            // Save thresholds button
            var btnSaveThresholds = new Button { Text = "Save Thresholds", Location = new Point(440, 210), Size = new Size(130, 30) };
            btnSaveThresholds.Click += (s, e) => SaveSelectedThresholds();
            grpThreshold.Controls.Add(btnSaveThresholds);

            // Mapping grid
            dgvMappings = new DataGridView
            {
                Location = new Point(12, 390),
                Size = new Size(950, 200),
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowTemplate = { MinimumHeight = 32 }
            };

            dgvMappings.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dgvMappings.RowsDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dgvMappings.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
            dgvMappings.DefaultCellStyle.Padding = new Padding(3);

            dgvMappings.Columns.Add("Channel", "Channel");
            dgvMappings.Columns.Add("ZoneName", "Zone Name");
            dgvMappings.Columns.Add("RelayIndex", "Relay Index");
            dgvMappings.Columns.Add("StartMeter", "Start (m)");
            dgvMappings.Columns.Add("StopMeter", "Stop (m)");
            dgvMappings.Columns.Add("AlarmIndexId", "Alarm Index");
            dgvMappings.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Active", HeaderText = "Active" });

            this.Controls.Add(dgvMappings);

            var btnAddMapping = new Button { Text = "Add Mapping", Location = new Point(12, 600), Size = new Size(120, 30) };
            btnAddMapping.Click += (s, e) => AddMapping();
            this.Controls.Add(btnAddMapping);

            var btnDeleteMapping = new Button { Text = "Delete Mapping", Location = new Point(142, 600), Size = new Size(120, 30) };
            btnDeleteMapping.Click += (s, e) => DeleteMapping();
            this.Controls.Add(btnDeleteMapping);

            // Read + Apply
            btnRead = new Button { Text = "Read", Location = new Point(660, 600), Size = new Size(120, 30) };
            btnRead.Click += BtnRead_Click;
            this.Controls.Add(btnRead);

            btnApply = new Button { Text = "Apply", Location = new Point(800, 600), Size = new Size(120, 30) };
            btnApply.Click += BtnApply_Click;
            this.Controls.Add(btnApply);
        }

        // ============================================================
        // CHANNEL & ZONE POPULATION
        // ============================================================
        private void PopulateChannels()
        {
            cmbChannel.Items.Clear();
            foreach (var ch in channelZones.Keys.OrderBy(k => k))
                cmbChannel.Items.Add(ch);

            if (cmbChannel.Items.Count > 0)
                cmbChannel.SelectedIndex = 0;
        }

        private void RefreshZonesForChannel()
        {
            cmbZone.Items.Clear();
            if (cmbChannel.SelectedItem == null) return;

            var channel = cmbChannel.SelectedItem.ToString();
            if (channelZones.TryGetValue(channel, out var zones))
            {
                foreach (var z in zones)
                    cmbZone.Items.Add(z.Name);
            }

            if (cmbZone.Items.Count > 0)
                cmbZone.SelectedIndex = 0;
        }

       
        private void RefreshAlarmIndexList()
        {
            lbAlarmIndex.Items.Clear();
            foreach (var idx in alarmIndices.OrderBy(i => i.Id))
                lbAlarmIndex.Items.Add($"{idx.Id} - {idx.Name}");
        }

        private void LoadSelectedIndexIntoUI()
        {
            if (lbAlarmIndex == null || lbAlarmIndex.SelectedIndex < 0) return;
            if (alarmIndices == null) return;

            var selected = alarmIndices.OrderBy(i => i.Id).ToList()[lbAlarmIndex.SelectedIndex];
            if(txtAlarmIndexName != null) txtAlarmIndexName.Text = selected.Name;

            var t = selected.Threshold;

            if(chkMaxAlert1 != null) chkMaxAlert1.Checked = t.UseMaxAlert1;
            if(txtMaxAlert1 != null) txtMaxAlert1.Text = t.MaxAlert1.ToString();

            if(chkMaxAlert2 != null) chkMaxAlert2.Checked = t.UseMaxAlert2;
            if(txtMaxAlert2 != null) txtMaxAlert2.Text = t.MaxAlert2.ToString();

            if(chkMax != null) chkMax.Checked = t.UseMax;
            if(txtMax != null) txtMax.Text = t.Max.ToString();

            if(chkMin != null) chkMin.Checked = t.UseMin;
            if(txtMin != null) txtMin.Text = t.Min.ToString();

            if(chkRate != null) chkRate.Checked = t.UseRateOfChange;
            if(txtRate != null) txtRate.Text = t.RateOfChange.ToString();

            if(chkDeviation != null) chkDeviation.Checked = t.UseDeviation;
            if(txtDeviation != null) txtDeviation.Text = t.Deviation.ToString();
        }

        private void SaveSelectedThresholds()
        {
            if (lbAlarmIndex == null || lbAlarmIndex.SelectedIndex < 0 || alarmIndices == null) return;

            var selected = alarmIndices.OrderBy(i => i.Id).ToList()[lbAlarmIndex.SelectedIndex];

            if(txtAlarmIndexName != null) selected.Name = txtAlarmIndexName.Text;
            var t = new AlarmThreshold();

            double tmp;

            if (chkMaxAlert1 != null) t.UseMaxAlert1 = chkMaxAlert1.Checked;
            if (txtMaxAlert1 != null && double.TryParse(txtMaxAlert1.Text, out tmp)) t.MaxAlert1 = tmp;

            if (chkMaxAlert2 != null) t.UseMaxAlert2 = chkMaxAlert2.Checked;
            if (txtMaxAlert2 != null && double.TryParse(txtMaxAlert2.Text, out tmp)) t.MaxAlert2 = tmp;

            if (chkMax != null) t.UseMax = chkMax.Checked;
            if (txtMax != null && double.TryParse(txtMax.Text, out tmp)) t.Max = tmp;

            if (chkMin != null) t.UseMin = chkMin.Checked;
            if (txtMin != null && double.TryParse(txtMin.Text, out tmp)) t.Min = tmp;

            if (chkRate != null) t.UseRateOfChange = chkRate.Checked;
            if (txtRate != null && double.TryParse(txtRate.Text, out tmp)) t.RateOfChange = tmp;

            if (chkDeviation != null) t.UseDeviation = chkDeviation.Checked;
            if (txtDeviation != null && double.TryParse(txtDeviation.Text, out tmp)) t.Deviation = tmp;

            selected.Threshold = t;

            RefreshAlarmIndexList();
        }

        private void AddAlarmIndex()
        {
            int nextId = alarmIndices.Count == 0 ? 1 : alarmIndices.Max(i => i.Id) + 1;

            alarmIndices.Add(new AlarmIndex
            {
                Id = nextId,
                Name = $"Index {nextId}",
                Threshold = new AlarmThreshold()
            });

            RefreshAlarmIndexList();
            lbAlarmIndex.SelectedIndex = alarmIndices.OrderBy(i => i.Id).ToList().FindIndex(x => x.Id == nextId);
        }

        private void DeleteSelectedAlarmIndex()
        {
            if (lbAlarmIndex.SelectedIndex < 0) return;

            var ordered = alarmIndices.OrderBy(i => i.Id).ToList();
            var selected = ordered[lbAlarmIndex.SelectedIndex];

            alarmIndices.RemoveAll(x => x.Id == selected.Id);
            alarmMappings.RemoveAll(m => m.AlarmIndexId == selected.Id);

            RefreshAlarmIndexList();
            RefreshMappingsGrid();
        }

       
        private void RefreshMappingsGrid()
        {
            dgvMappings.Rows.Clear();
            foreach (var m in alarmMappings)
            {
                dgvMappings.Rows.Add(
                    m.Channel,
                    m.ZoneName,
                    m.RelayIndex,
                    m.StartMeter,
                    m.StopMeter,
                    m.AlarmIndexId,
                    m.Active
                );
            }
        }

        private void AddMapping()
        {
            if (cmbChannel.SelectedItem == null || cmbZone.SelectedItem == null)
            {
                MessageBox.Show("Select channel & zone first.");
                return;
            }

            if (alarmIndices.Count == 0)
            {
                MessageBox.Show("Create at least one AlarmIndex first.");
                return;
            }

            int firstId = alarmIndices.OrderBy(i => i.Id).First().Id;

            alarmMappings.Add(new AlarmMapping
            {
                Channel = cmbChannel.SelectedItem.ToString(),
                ZoneName = cmbZone.SelectedItem.ToString(),
                RelayIndex = 0,
                StartMeter = 0,
                StopMeter = 0,
                AlarmIndexId = firstId,
                Active = true
            });

            RefreshMappingsGrid();
        }

        private void DeleteMapping()
        {
            if (dgvMappings.SelectedRows.Count == 0) return;

            int index = dgvMappings.SelectedRows[0].Index;
            if (index < 0 || index >= alarmMappings.Count) return;

            alarmMappings.RemoveAt(index);
            RefreshMappingsGrid();
        }

        
        private async void BtnRead_Click(object? sender, EventArgs e)
        {
            var frmcData = await Program.WebSocketClient.ReadAlarmConfigAsync();

            if (frmcData == null || frmcData.Indices.Count == 0)
            {
                MessageBox.Show("FRMC has no stored alarm configuration.",
                    "No Data", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            alarmIndices.Clear();
            alarmIndices.AddRange(frmcData.Indices);

            alarmMappings.Clear();
            alarmMappings.AddRange(frmcData.Mappings);

            RefreshAlarmIndexList();
            RefreshMappingsGrid();

            MessageBox.Show("Alarm configuration loaded successfully.",
                "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

       
        private async void BtnApply_Click(object? sender, EventArgs e)
        {
            // Ensure last edited index is saved
            if (lbAlarmIndex.SelectedIndex >= 0)
                SaveSelectedThresholds();

            UpdatedAlarmIndices = alarmIndices.ToList();
            UpdatedAlarmMappings = alarmMappings.ToList();

            // Validate mappings
            foreach (var map in UpdatedAlarmMappings)
            {
                if (!alarmIndices.Any(i => i.Id == map.AlarmIndexId))
                {
                    MessageBox.Show($"Mapping refers to missing AlarmIndex ID {map.AlarmIndexId}. Fix before applying.",
                        "Invalid Mapping", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            bool ok = await Program.WebSocketClient.SendAlarmConfigAsync(
                UpdatedAlarmIndices,
                UpdatedAlarmMappings
            );

            if (ok)
            {
                MessageBox.Show("Alarm configuration applied successfully.",
                    "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Failed to send alarm configuration to FRMC.",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            this.DialogResult = DialogResult.OK;
        }
    }
}
