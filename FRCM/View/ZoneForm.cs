using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FRCM
{
    public partial class ZoneForm : Form
    {
        public List<string> Zones { get; private set; } = new List<string>();

        private Label? lblChannel;
        private DataGridView? dgvZones;
        private string? channelName; 

        public ZoneForm(string channelName, List<string> existingZones)
        {
            InitializeComponent();
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.AutoScaleMode = AutoScaleMode.Dpi;

            this.Text = "Zone Selection";
            this.Width = 450;
            this.Height = 520;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.channelName = channelName;
            Zones = new List<string>(existingZones);
            InitializeControls(channelName);
            LoadExistingZones();
        }
        private void InitializeControls(string channelName)
        {
            lblChannel = new Label
            {
                Text = $"Channel Selected → {channelName}",
                Top = 10,
                Left = 10,
                Width = 400,
                Height = 25,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            this.Controls.Add(lblChannel);
            dgvZones = new DataGridView
            {
                Top = 40,
                Left = 10,
                Width = 410,
                Height = 350,
                AllowUserToAddRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                MultiSelect = false,
                ColumnHeadersDefaultCellStyle = { WrapMode = DataGridViewTriState.False }
            };
            var colNumber = new DataGridViewTextBoxColumn
            {
                Name = "ZoneNumber",
                HeaderText = "Zone No.",
                Width = 120,
                ReadOnly = true
            };
            var colName = new DataGridViewTextBoxColumn
            {
                Name = "ZoneName",
                HeaderText = "Zone Name",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                ReadOnly = true
            };

            dgvZones.Columns.Add(colNumber);
            dgvZones.Columns.Add(colName);
            this.Controls.Add(dgvZones);
        }
        private void LoadExistingZones()
        {
            dgvZones.Rows.Clear();
            int number = 1;
            foreach (var zone in Zones)
            {
                dgvZones.Rows.Add(number, zone);
                number++;
            }
            dgvZones.ClearSelection();
        }
    }
}