namespace FRCM
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;


        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle2 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle3 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle4 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle5 = new DataGridViewCellStyle();
            panelRight = new Panel();
            panel9 = new Panel();
            tableLayoutPanel2 = new TableLayoutPanel();
            groupStatus = new GroupBox();
            richTextBoxStatus = new RichTextBox();
            groupOperation = new GroupBox();
            refresh = new Button();
            btnStartMeasurement = new Button();
            groupBoxChannelSelection = new GroupBox();
            groupSafety = new GroupBox();
            ledAlarm = new FRCM.View.LedIndicator();
            ledSystem = new FRCM.View.LedIndicator();
            ledComm = new FRCM.View.LedIndicator();
            panel5 = new Panel();
            tableLayoutPanel1 = new TableLayoutPanel();
            pictureLogo = new PictureBox();
            lblAlarm = new Label();
            lblSystem = new Label();
            lblComm = new Label();
            monitoringToolStripMenuItem = new ToolStripMenuItem();
            activeAlarmToolStripMenuItem = new ToolStripMenuItem();
            eventListToolStripMenuItem1 = new ToolStripMenuItem();
            auditTrailToolStripMenuItem1 = new ToolStripMenuItem();
            systemHealthToolStripMenuItem = new ToolStripMenuItem();
            relayMappingToolStripMenuItem = new ToolStripMenuItem();
            softwareUpdateToolStripMenuItem = new ToolStripMenuItem();
            fRMCCurrentVersionToolStripMenuItem = new ToolStripMenuItem();
            helpToolStripMenuItem = new ToolStripMenuItem();
            aboutToolStripMenuItem = new ToolStripMenuItem();
            adminToolStripMenuItem = new ToolStripMenuItem();
            userManagementToolStripMenuItem = new ToolStripMenuItem();
            systemConfigurationToolStripMenuItem = new ToolStripMenuItem();
            logConfigurationToolStripMenuItem = new ToolStripMenuItem();
            atpToolStripMenuItem = new ToolStripMenuItem();
            menuStrip1 = new MenuStrip();
            Configuration = new ToolStripMenuItem();
            channelZoneConfigurationToolStripMenuItem = new ToolStripMenuItem();
            usbConfigurationToolStripMenuItem = new ToolStripMenuItem();
            loadConfigurationFileToolStripMenuItem = new ToolStripMenuItem();
            saveConfigurationFileToolStripMenuItem = new ToolStripMenuItem();
            iPConfigurationToolStripMenuItem = new ToolStripMenuItem();
            clockConfigurationToolStripMenuItem = new ToolStripMenuItem();
            dtsCalibrationToolStripMenuItem = new ToolStripMenuItem();
            temperatureCorrectionToolStripMenuItem = new ToolStripMenuItem();
            resetAllConfigurationToolStripMenuItem = new ToolStripMenuItem();
            tabControlDisplay = new TabControl();
            tabChannelInformation = new TabPage();
            dataGridViewZone = new DataGridView();
            colZone = new DataGridViewTextBoxColumn();
            colZoneName = new DataGridViewTextBoxColumn();
            colStart = new DataGridViewTextBoxColumn();
            colStop = new DataGridViewTextBoxColumn();
            colActive = new DataGridViewTextBoxColumn();
            colRelay = new DataGridViewTextBoxColumn();
            colMaxA1 = new DataGridViewTextBoxColumn();
            colMaxA2 = new DataGridViewTextBoxColumn();
            colMin = new DataGridViewTextBoxColumn();
            Column4 = new DataGridViewTextBoxColumn();
            Column1 = new DataGridViewTextBoxColumn();
            panelChannelHeader = new Panel();
            lblChannelName = new Label();
            ledChannelEnabled = new FRCM.View.LedIndicator();
            ledChannelAlarm = new FRCM.View.LedIndicator();
            ledFiberBreak = new FRCM.View.LedIndicator();
            tabZoneInformation = new TabPage();
            dataGridView1 = new DataGridView();
            Column2 = new DataGridViewTextBoxColumn();
            Column3 = new DataGridViewTextBoxColumn();
            panel8 = new Panel();
            lblZoneName = new Label();
            ledZoneEnabled = new FRCM.View.LedIndicator();
            ledZoneAlarm = new FRCM.View.LedIndicator();
            openGraphToolStripMenuItem = new TabPage();
            panel7 = new Panel();
            formsPlot1 = new ScottPlot.WinForms.FormsPlot();
            panelGraphControls = new Panel();
            btnResetAxis = new Button();
            btnApplyAxis = new Button();
            txtYMax = new TextBox();
            lblYTo = new Label();
            txtYMin = new TextBox();
            lblYAxis = new Label();
            txtXMax = new TextBox();
            lblXTo = new Label();
            txtXMin = new TextBox();
            lblXAxis = new Label();
            groupDisplay = new Panel();
            settingsToolStripMenuItem = new ToolStripMenuItem();
            normalFontSizeToolStripMenuItem = new ToolStripMenuItem();
            largeFontSizeToolStripMenuItem = new ToolStripMenuItem();
            panelRight.SuspendLayout();
            panel9.SuspendLayout();
            tableLayoutPanel2.SuspendLayout();
            groupStatus.SuspendLayout();
            groupOperation.SuspendLayout();
            groupSafety.SuspendLayout();
            panel5.SuspendLayout();
            tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureLogo).BeginInit();
            menuStrip1.SuspendLayout();
            tabControlDisplay.SuspendLayout();
            tabChannelInformation.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridViewZone).BeginInit();
            panelChannelHeader.SuspendLayout();
            tabZoneInformation.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            panel8.SuspendLayout();
            openGraphToolStripMenuItem.SuspendLayout();
            panel7.SuspendLayout();
            panelGraphControls.SuspendLayout();
            groupDisplay.SuspendLayout();
            SuspendLayout();
            // 
            // panelRight
            // 
            panelRight.AutoScroll = true;
            panelRight.BackColor = Color.WhiteSmoke;
            panelRight.Controls.Add(panel9);
            panelRight.Controls.Add(groupOperation);
            panelRight.Controls.Add(groupBoxChannelSelection);
            panelRight.Controls.Add(groupSafety);
            panelRight.Controls.Add(panel5);
            panelRight.Dock = DockStyle.Right;
            panelRight.Location = new Point(834, 35);
            panelRight.Margin = new Padding(4, 5, 4, 5);
            panelRight.Name = "panelRight";
            panelRight.Size = new Size(536, 713);
            panelRight.TabIndex = 1;
            // 
            // panel9
            // 
            panel9.Controls.Add(tableLayoutPanel2);
            panel9.Dock = DockStyle.Top;
            panel9.Location = new Point(0, 759);
            panel9.Margin = new Padding(4, 5, 4, 5);
            panel9.Name = "panel9";
            panel9.Padding = new Padding(14, 8, 14, 17);
            panel9.Size = new Size(510, 150);
            panel9.TabIndex = 5;
            // 
            // tableLayoutPanel2
            // 
            tableLayoutPanel2.ColumnCount = 1;
            tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel2.Controls.Add(groupStatus, 0, 0);
            tableLayoutPanel2.Dock = DockStyle.Fill;
            tableLayoutPanel2.Location = new Point(14, 8);
            tableLayoutPanel2.Name = "tableLayoutPanel2";
            tableLayoutPanel2.RowCount = 1;
            tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel2.Size = new Size(482, 125);
            tableLayoutPanel2.TabIndex = 0;
            // 
            // groupStatus
            // 
            groupStatus.Controls.Add(richTextBoxStatus);
            groupStatus.Dock = DockStyle.Fill;
            groupStatus.Location = new Point(4, 5);
            groupStatus.Margin = new Padding(4, 5, 4, 5);
            groupStatus.Name = "groupStatus";
            groupStatus.Padding = new Padding(4, 5, 4, 5);
            groupStatus.Size = new Size(474, 115);
            groupStatus.TabIndex = 3;
            groupStatus.TabStop = false;
            groupStatus.Text = "Status";
            // 
            // richTextBoxStatus
            // 
            richTextBoxStatus.BackColor = Color.White;
            richTextBoxStatus.BorderStyle = BorderStyle.None;
            richTextBoxStatus.Dock = DockStyle.Fill;
            richTextBoxStatus.Location = new Point(4, 29);
            richTextBoxStatus.Margin = new Padding(4, 5, 4, 5);
            richTextBoxStatus.Name = "richTextBoxStatus";
            richTextBoxStatus.ReadOnly = true;
            richTextBoxStatus.Size = new Size(466, 81);
            richTextBoxStatus.TabIndex = 0;
            richTextBoxStatus.Text = "";
            // 
            // groupOperation
            // 
            groupOperation.Controls.Add(refresh);
            groupOperation.Controls.Add(btnStartMeasurement);
            groupOperation.Dock = DockStyle.Top;
            groupOperation.Location = new Point(0, 592);
            groupOperation.Margin = new Padding(14, 0, 14, 0);
            groupOperation.Name = "groupOperation";
            groupOperation.Padding = new Padding(14, 8, 14, 8);
            groupOperation.Size = new Size(510, 167);
            groupOperation.TabIndex = 2;
            groupOperation.TabStop = false;
            groupOperation.Text = "Operation";
            // 
            // refresh
            // 
            refresh.Location = new Point(137, 33);
            refresh.Margin = new Padding(4, 5, 4, 5);
            refresh.Name = "refresh";
            refresh.RightToLeft = RightToLeft.Yes;
            refresh.Size = new Size(227, 53);
            refresh.TabIndex = 1;
            refresh.Text = "Refresh";
            refresh.UseVisualStyleBackColor = true;
            refresh.Click += refresh_Click;
            // 
            // btnStartMeasurement
            // 
            btnStartMeasurement.Location = new Point(137, 97);
            btnStartMeasurement.Margin = new Padding(4, 5, 4, 5);
            btnStartMeasurement.Name = "btnStartMeasurement";
            btnStartMeasurement.Size = new Size(227, 58);
            btnStartMeasurement.TabIndex = 0;
            btnStartMeasurement.Text = "Start Measurement";
            btnStartMeasurement.UseVisualStyleBackColor = true;
            btnStartMeasurement.Click += btnStartMeasurement_Click;
            // 
            // groupBoxChannelSelection
            // 
            groupBoxChannelSelection.Dock = DockStyle.Top;
            groupBoxChannelSelection.Location = new Point(0, 325);
            groupBoxChannelSelection.Margin = new Padding(14, 0, 14, 0);
            groupBoxChannelSelection.Name = "groupBoxChannelSelection";
            groupBoxChannelSelection.Padding = new Padding(14, 8, 14, 8);
            groupBoxChannelSelection.Size = new Size(510, 267);
            groupBoxChannelSelection.TabIndex = 1;
            groupBoxChannelSelection.TabStop = false;
            groupBoxChannelSelection.Text = "Channel Selection / Zone Selection";
            // 
            // groupSafety
            // 
            groupSafety.Controls.Add(ledAlarm);
            groupSafety.Controls.Add(ledSystem);
            groupSafety.Controls.Add(ledComm);
            groupSafety.Dock = DockStyle.Top;
            groupSafety.Location = new Point(0, 167);
            groupSafety.Margin = new Padding(14, 0, 14, 0);
            groupSafety.Name = "groupSafety";
            groupSafety.Padding = new Padding(14, 8, 14, 8);
            groupSafety.Size = new Size(510, 158);
            groupSafety.TabIndex = 0;
            groupSafety.TabStop = false;
            groupSafety.Text = "Safety";
            // 
            // ledAlarm
            // 
            ledAlarm.BackColor = Color.Transparent;
            ledAlarm.IsOn = false;
            ledAlarm.Label = "Active Alarm";
            ledAlarm.LedColor = Color.Red;
            ledAlarm.Location = new Point(375, 43);
            ledAlarm.Margin = new Padding(4, 5, 4, 5);
            ledAlarm.Name = "ledAlarm";
            ledAlarm.Size = new Size(140, 85);
            ledAlarm.TabIndex = 2;
            // 
            // ledSystem
            // 
            ledSystem.BackColor = Color.Transparent;
            ledSystem.IsOn = true;
            ledSystem.Label = "System Health";
            ledSystem.LedColor = Color.LimeGreen;
            ledSystem.Location = new Point(215, 43);
            ledSystem.Margin = new Padding(4, 5, 4, 5);
            ledSystem.Name = "ledSystem";
            ledSystem.Size = new Size(140, 85);
            ledSystem.TabIndex = 1;
            // 
            // ledComm
            // 
            ledComm.BackColor = Color.Transparent;
            ledComm.IsOn = false;
            ledComm.Label = "DTS Communication";
            ledComm.LedColor = Color.LimeGreen;
            ledComm.Location = new Point(15, 43);
            ledComm.Margin = new Padding(4, 5, 4, 5);
            ledComm.Name = "ledComm";
            ledComm.Size = new Size(185, 85);
            ledComm.TabIndex = 0;
            // 
            // panel5
            // 
            panel5.Controls.Add(tableLayoutPanel1);
            panel5.Dock = DockStyle.Top;
            panel5.Location = new Point(0, 0);
            panel5.Margin = new Padding(4, 5, 4, 5);
            panel5.Name = "panel5";
            panel5.Padding = new Padding(14, 8, 14, 8);
            panel5.Size = new Size(510, 167);
            panel5.TabIndex = 4;
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.ColumnCount = 1;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.Controls.Add(pictureLogo, 0, 0);
            tableLayoutPanel1.Dock = DockStyle.Fill;
            tableLayoutPanel1.Location = new Point(14, 8);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 1;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.Size = new Size(482, 151);
            tableLayoutPanel1.TabIndex = 0;
            // 
            // pictureLogo
            // 
            pictureLogo.Dock = DockStyle.Fill;
            pictureLogo.Location = new Point(3, 3);
            pictureLogo.Name = "pictureLogo";
            pictureLogo.Size = new Size(476, 145);
            pictureLogo.SizeMode = PictureBoxSizeMode.Zoom;
            pictureLogo.TabIndex = 0;
            pictureLogo.TabStop = false;
            // 
            // lblAlarm
            // 
            lblAlarm.Location = new Point(0, 0);
            lblAlarm.Name = "lblAlarm";
            lblAlarm.Size = new Size(100, 23);
            lblAlarm.TabIndex = 0;
            // 
            // lblSystem
            // 
            lblSystem.Location = new Point(0, 0);
            lblSystem.Name = "lblSystem";
            lblSystem.Size = new Size(100, 23);
            lblSystem.TabIndex = 0;
            // 
            // lblComm
            // 
            lblComm.Location = new Point(0, 0);
            lblComm.Name = "lblComm";
            lblComm.Size = new Size(100, 23);
            lblComm.TabIndex = 0;
            // 
            // monitoringToolStripMenuItem
            // 
            monitoringToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { activeAlarmToolStripMenuItem, eventListToolStripMenuItem1, auditTrailToolStripMenuItem1, systemHealthToolStripMenuItem, relayMappingToolStripMenuItem });
            monitoringToolStripMenuItem.Name = "monitoringToolStripMenuItem";
            monitoringToolStripMenuItem.Size = new Size(117, 29);
            monitoringToolStripMenuItem.Text = "Monitoring";
            // 
            // activeAlarmToolStripMenuItem
            // 
            activeAlarmToolStripMenuItem.Name = "activeAlarmToolStripMenuItem";
            activeAlarmToolStripMenuItem.Size = new Size(227, 34);
            activeAlarmToolStripMenuItem.Text = "Active Alarm";
            activeAlarmToolStripMenuItem.Click += activeAlarmToolStripMenuItem_Click;
            // 
            // eventListToolStripMenuItem1
            // 
            eventListToolStripMenuItem1.Name = "eventListToolStripMenuItem1";
            eventListToolStripMenuItem1.Size = new Size(227, 34);
            eventListToolStripMenuItem1.Text = "Event List";
            eventListToolStripMenuItem1.Click += eventListToolStripMenuItem1_Click;
            // 
            // auditTrailToolStripMenuItem1
            // 
            auditTrailToolStripMenuItem1.Name = "auditTrailToolStripMenuItem1";
            auditTrailToolStripMenuItem1.Size = new Size(227, 34);
            auditTrailToolStripMenuItem1.Text = "Audit Trail";
            auditTrailToolStripMenuItem1.Click += auditTrailToolStripMenuItem1_Click;
            // 
            // systemHealthToolStripMenuItem
            // 
            systemHealthToolStripMenuItem.Name = "systemHealthToolStripMenuItem";
            systemHealthToolStripMenuItem.Size = new Size(227, 34);
            systemHealthToolStripMenuItem.Text = "System Health";
            systemHealthToolStripMenuItem.Click += systemHealthToolStripMenuItem_Click;
            // 
            // relayMappingToolStripMenuItem
            // 
            relayMappingToolStripMenuItem.Name = "relayMappingToolStripMenuItem";
            relayMappingToolStripMenuItem.Size = new Size(227, 34);
            relayMappingToolStripMenuItem.Text = "Relay Status";
            relayMappingToolStripMenuItem.Click += relayMappingToolStripMenuItem_Click;
            // 
            // softwareUpdateToolStripMenuItem
            // 
            softwareUpdateToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { fRMCCurrentVersionToolStripMenuItem });
            softwareUpdateToolStripMenuItem.Name = "softwareUpdateToolStripMenuItem";
            softwareUpdateToolStripMenuItem.Size = new Size(159, 29);
            softwareUpdateToolStripMenuItem.Text = "Software update";
            // 
            // fRMCCurrentVersionToolStripMenuItem
            // 
            fRMCCurrentVersionToolStripMenuItem.Name = "fRMCCurrentVersionToolStripMenuItem";
            fRMCCurrentVersionToolStripMenuItem.Size = new Size(288, 34);
            fRMCCurrentVersionToolStripMenuItem.Text = "FRMC  current version";
            fRMCCurrentVersionToolStripMenuItem.Click += fRMCCurrentVersionToolStripMenuItem_Click;
            // 
            // helpToolStripMenuItem
            // 
            helpToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { aboutToolStripMenuItem });
            helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            helpToolStripMenuItem.Size = new Size(65, 29);
            helpToolStripMenuItem.Text = "Help";
            // 
            // aboutToolStripMenuItem
            // 
            aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            aboutToolStripMenuItem.Size = new Size(270, 34);
            aboutToolStripMenuItem.Text = "About";
            aboutToolStripMenuItem.Click += aboutToolStripMenuItem_Click;
            // 
            // adminToolStripMenuItem
            // 
            adminToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { userManagementToolStripMenuItem, systemConfigurationToolStripMenuItem, logConfigurationToolStripMenuItem });
            adminToolStripMenuItem.Name = "adminToolStripMenuItem";
            adminToolStripMenuItem.Size = new Size(81, 29);
            adminToolStripMenuItem.Text = "Admin";
            // 
            // userManagementToolStripMenuItem
            // 
            userManagementToolStripMenuItem.Name = "userManagementToolStripMenuItem";
            userManagementToolStripMenuItem.Size = new Size(285, 34);
            userManagementToolStripMenuItem.Text = "User Management";
            userManagementToolStripMenuItem.Click += userManagementToolStripMenuItem_Click;
            // 
            // systemConfigurationToolStripMenuItem
            // 
            systemConfigurationToolStripMenuItem.Name = "systemConfigurationToolStripMenuItem";
            systemConfigurationToolStripMenuItem.Size = new Size(285, 34);
            systemConfigurationToolStripMenuItem.Text = "System Configuration";
            systemConfigurationToolStripMenuItem.Click += systemConfigurationToolStripMenuItem_Click;
            // 
            // logConfigurationToolStripMenuItem
            // 
            logConfigurationToolStripMenuItem.Name = "logConfigurationToolStripMenuItem";
            logConfigurationToolStripMenuItem.Size = new Size(285, 34);
            logConfigurationToolStripMenuItem.Text = "Log Configuration";
            logConfigurationToolStripMenuItem.Click += logConfigurationToolStripMenuItem_Click;
            // 
            // atpToolStripMenuItem
            // 
            atpToolStripMenuItem.Name = "atpToolStripMenuItem";
            atpToolStripMenuItem.Size = new Size(62, 29);
            atpToolStripMenuItem.Text = "ATP";
            atpToolStripMenuItem.Click += atpToolStripMenuItem_Click;
            // 
            // menuStrip1
            // 
            menuStrip1.ImageScalingSize = new Size(24, 24);
            menuStrip1.Items.AddRange(new ToolStripItem[] { Configuration, monitoringToolStripMenuItem, atpToolStripMenuItem, softwareUpdateToolStripMenuItem, adminToolStripMenuItem, helpToolStripMenuItem, settingsToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Padding = new Padding(9, 3, 0, 3);
            menuStrip1.Size = new Size(1370, 35);
            menuStrip1.TabIndex = 0;
            menuStrip1.Text = "menuStrip1";
            // 
            // Configuration
            // 
            Configuration.DropDownItems.AddRange(new ToolStripItem[] { channelZoneConfigurationToolStripMenuItem, usbConfigurationToolStripMenuItem, dtsCalibrationToolStripMenuItem, temperatureCorrectionToolStripMenuItem, loadConfigurationFileToolStripMenuItem, saveConfigurationFileToolStripMenuItem, iPConfigurationToolStripMenuItem, clockConfigurationToolStripMenuItem, resetAllConfigurationToolStripMenuItem });
            Configuration.Name = "Configuration";
            Configuration.Size = new Size(137, 29);
            Configuration.Text = "Configuration";
            // 
            // usbConfigurationToolStripMenuItem
            // 
            usbConfigurationToolStripMenuItem.Name = "usbConfigurationToolStripMenuItem";
            usbConfigurationToolStripMenuItem.Size = new Size(338, 34);
            usbConfigurationToolStripMenuItem.Text = "USB Configuration";
            usbConfigurationToolStripMenuItem.Click += usbConfigurationToolStripMenuItem_Click;
            // 
            // dtsCalibrationToolStripMenuItem
            // 
            dtsCalibrationToolStripMenuItem.Name = "dtsCalibrationToolStripMenuItem";
            dtsCalibrationToolStripMenuItem.Size = new Size(338, 34);
            dtsCalibrationToolStripMenuItem.Text = "DTS Hardware Configuration";
            dtsCalibrationToolStripMenuItem.Click += dtsCalibrationToolStripMenuItem_Click;
            // 
            // temperatureCorrectionToolStripMenuItem
            // 
            temperatureCorrectionToolStripMenuItem.Name = "temperatureCorrectionToolStripMenuItem";
            temperatureCorrectionToolStripMenuItem.Size = new Size(338, 34);
            temperatureCorrectionToolStripMenuItem.Text = "Temperature Correction";
            temperatureCorrectionToolStripMenuItem.Click += temperatureCorrectionToolStripMenuItem_Click;
            // 
            // channelZoneConfigurationToolStripMenuItem
            // 
            channelZoneConfigurationToolStripMenuItem.Name = "channelZoneConfigurationToolStripMenuItem";
            channelZoneConfigurationToolStripMenuItem.Size = new Size(338, 34);
            channelZoneConfigurationToolStripMenuItem.Text = "Channel/Zone Configuration";
            channelZoneConfigurationToolStripMenuItem.Click += channelZoneConfigurationToolStripMenuItem_Click;
            // 
            // loadConfigurationFileToolStripMenuItem
            // 
            loadConfigurationFileToolStripMenuItem.Name = "loadConfigurationFileToolStripMenuItem";
            loadConfigurationFileToolStripMenuItem.Size = new Size(338, 34);
            loadConfigurationFileToolStripMenuItem.Text = "Load ConfigurationFile";
            loadConfigurationFileToolStripMenuItem.Click += loadConfigurationFileToolStripMenuItem_Click;
            // 
            // saveConfigurationFileToolStripMenuItem
            // 
            saveConfigurationFileToolStripMenuItem.Name = "saveConfigurationFileToolStripMenuItem";
            saveConfigurationFileToolStripMenuItem.Size = new Size(338, 34);
            saveConfigurationFileToolStripMenuItem.Text = "Save Configuration File";
            saveConfigurationFileToolStripMenuItem.Click += saveConfigurationFileToolStripMenuItem_Click;
            // 
            // iPConfigurationToolStripMenuItem
            // 
            iPConfigurationToolStripMenuItem.Name = "iPConfigurationToolStripMenuItem";
            iPConfigurationToolStripMenuItem.Size = new Size(338, 34);
            iPConfigurationToolStripMenuItem.Text = "IP Configuration";
            iPConfigurationToolStripMenuItem.Click += iPConfigurationToolStripMenuItem_Click;
            // 
            // clockConfigurationToolStripMenuItem
            // 
            clockConfigurationToolStripMenuItem.Name = "clockConfigurationToolStripMenuItem";
            clockConfigurationToolStripMenuItem.Size = new Size(338, 34);
            clockConfigurationToolStripMenuItem.Text = "Clock Configuration";
            clockConfigurationToolStripMenuItem.Click += clockConfigurationToolStripMenuItem_Click;
            // 
            // resetAllConfigurationToolStripMenuItem
            // 
            resetAllConfigurationToolStripMenuItem.Name = "resetAllConfigurationToolStripMenuItem";
            resetAllConfigurationToolStripMenuItem.Size = new Size(338, 34);
            resetAllConfigurationToolStripMenuItem.Text = "Reset All Configuration";
            resetAllConfigurationToolStripMenuItem.Click += resetAllConfigurationToolStripMenuItem_Click;
            // 
            // tabControlDisplay
            // 
            tabControlDisplay.Controls.Add(tabChannelInformation);
            tabControlDisplay.Controls.Add(tabZoneInformation);
            tabControlDisplay.Controls.Add(openGraphToolStripMenuItem);
            tabControlDisplay.Dock = DockStyle.Fill;
            tabControlDisplay.Location = new Point(0, 0);
            tabControlDisplay.Margin = new Padding(0);
            tabControlDisplay.Name = "tabControlDisplay";
            tabControlDisplay.SelectedIndex = 0;
            tabControlDisplay.Size = new Size(834, 713);
            tabControlDisplay.TabIndex = 23;
            // 
            // tabChannelInformation
            // 
            tabChannelInformation.Controls.Add(dataGridViewZone);
            tabChannelInformation.Controls.Add(panelChannelHeader);
            tabChannelInformation.Location = new Point(4, 34);
            tabChannelInformation.Margin = new Padding(4, 5, 4, 5);
            tabChannelInformation.Name = "tabChannelInformation";
            tabChannelInformation.Size = new Size(826, 675);
            tabChannelInformation.TabIndex = 1;
            tabChannelInformation.Text = "Channel Information";
            tabChannelInformation.UseVisualStyleBackColor = true;
            // 
            // dataGridViewZone
            // 
            dataGridViewZone.AllowUserToAddRows = false;
            dataGridViewZone.AllowUserToDeleteRows = false;
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = SystemColors.Control;
            dataGridViewCellStyle1.Font = new Font("Segoe UI", 9F);
            dataGridViewCellStyle1.ForeColor = SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = DataGridViewTriState.True;
            dataGridViewZone.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            dataGridViewZone.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewZone.Columns.AddRange(new DataGridViewColumn[] { colZone, colZoneName, colStart, colStop, colActive, colRelay, colMaxA1, colMaxA2, colMin, Column4, Column1 });
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = SystemColors.Window;
            dataGridViewCellStyle2.Font = new Font("Segoe UI", 9F);
            dataGridViewCellStyle2.ForeColor = SystemColors.ControlText;
            dataGridViewCellStyle2.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = DataGridViewTriState.False;
            dataGridViewZone.DefaultCellStyle = dataGridViewCellStyle2;
            dataGridViewZone.Dock = DockStyle.Fill;
            dataGridViewZone.Location = new Point(0, 83);
            dataGridViewZone.Margin = new Padding(0);
            dataGridViewZone.Name = "dataGridViewZone";
            dataGridViewZone.ReadOnly = true;
            dataGridViewCellStyle3.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = SystemColors.Control;
            dataGridViewCellStyle3.Font = new Font("Segoe UI", 9F);
            dataGridViewCellStyle3.ForeColor = SystemColors.WindowText;
            dataGridViewCellStyle3.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = DataGridViewTriState.True;
            dataGridViewZone.RowHeadersDefaultCellStyle = dataGridViewCellStyle3;
            dataGridViewZone.RowHeadersWidth = 62;
            dataGridViewZone.Size = new Size(826, 592);
            dataGridViewZone.TabIndex = 0;
            // 
            // colZone
            // 
            colZone.HeaderText = "Zone";
            colZone.MinimumWidth = 8;
            colZone.Name = "colZone";
            colZone.ReadOnly = true;
            colZone.Width = 150;
            // 
            // colZoneName
            // 
            colZoneName.HeaderText = "Zone Name";
            colZoneName.MinimumWidth = 8;
            colZoneName.Name = "colZoneName";
            colZoneName.ReadOnly = true;
            colZoneName.Width = 150;
            // 
            // colStart
            // 
            colStart.HeaderText = "Zone Start (m)";
            colStart.MinimumWidth = 8;
            colStart.Name = "colStart";
            colStart.ReadOnly = true;
            colStart.Width = 150;
            // 
            // colStop
            // 
            colStop.HeaderText = "Zone Stop (m)";
            colStop.MinimumWidth = 8;
            colStop.Name = "colStop";
            colStop.ReadOnly = true;
            colStop.Width = 150;
            // 
            // colActive
            // 
            colActive.HeaderText = "Active Alarm";
            colActive.MinimumWidth = 8;
            colActive.Name = "colActive";
            colActive.ReadOnly = true;
            colActive.Width = 200;
            // 
            // colRelay
            // 
            colRelay.HeaderText = "Enable";
            colRelay.MinimumWidth = 8;
            colRelay.Name = "colRelay";
            colRelay.ReadOnly = true;
            colRelay.Width = 150;
            // 
            // colMaxA1
            // 
            colMaxA1.HeaderText = "Zone max(°C)";
            colMaxA1.MinimumWidth = 8;
            colMaxA1.Name = "colMaxA1";
            colMaxA1.ReadOnly = true;
            colMaxA1.Width = 150;
            // 
            // colMaxA2
            // 
            colMaxA2.HeaderText = "Zone min(°C)";
            colMaxA2.MinimumWidth = 8;
            colMaxA2.Name = "colMaxA2";
            colMaxA2.ReadOnly = true;
            colMaxA2.Width = 150;
            // 
            // colMin
            // 
            colMin.HeaderText = "ROR (°C/min)";
            colMin.MinimumWidth = 8;
            colMin.Name = "colMin";
            colMin.ReadOnly = true;
            colMin.Width = 150;
            // 
            // Column4
            // 
            Column4.HeaderText = "Avg (°C)";
            Column4.MinimumWidth = 8;
            Column4.Name = "Column4";
            Column4.ReadOnly = true;
            Column4.Width = 150;
            // 
            // Column1
            // 
            Column1.HeaderText = "Deviation (°C)";
            Column1.MinimumWidth = 8;
            Column1.Name = "Column1";
            Column1.ReadOnly = true;
            Column1.Width = 150;
            // 
            // panelChannelHeader
            // 
            panelChannelHeader.BackColor = Color.FromArgb(240, 240, 240);
            panelChannelHeader.Controls.Add(lblChannelName);
            panelChannelHeader.Controls.Add(ledChannelEnabled);
            panelChannelHeader.Controls.Add(ledChannelAlarm);
            panelChannelHeader.Controls.Add(ledFiberBreak);
            panelChannelHeader.Dock = DockStyle.Top;
            panelChannelHeader.Location = new Point(0, 0);
            panelChannelHeader.Name = "panelChannelHeader";
            panelChannelHeader.Size = new Size(826, 83);
            panelChannelHeader.TabIndex = 8;
            // 
            // lblChannelName
            // 
            lblChannelName.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblChannelName.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblChannelName.ForeColor = Color.FromArgb(64, 64, 64);
            lblChannelName.Location = new Point(503, 25);
            lblChannelName.Margin = new Padding(4, 0, 4, 0);
            lblChannelName.Name = "lblChannelName";
            lblChannelName.Size = new Size(314, 33);
            lblChannelName.TabIndex = 3;
            lblChannelName.TextAlign = ContentAlignment.MiddleRight;
            // 
            // ledChannelEnabled
            // 
            ledChannelEnabled.BackColor = Color.Transparent;
            ledChannelEnabled.IsOn = false;
            ledChannelEnabled.Label = "Is Enabled";
            ledChannelEnabled.LedColor = Color.LimeGreen;
            ledChannelEnabled.Location = new Point(7, 5);
            ledChannelEnabled.Margin = new Padding(4, 5, 4, 5);
            ledChannelEnabled.Name = "ledChannelEnabled";
            ledChannelEnabled.Size = new Size(129, 75);
            ledChannelEnabled.TabIndex = 0;
            // 
            // ledChannelAlarm
            // 
            ledChannelAlarm.BackColor = Color.Transparent;
            ledChannelAlarm.IsOn = false;
            ledChannelAlarm.Label = "Has Alarm";
            ledChannelAlarm.LedColor = Color.Red;
            ledChannelAlarm.Location = new Point(143, 5);
            ledChannelAlarm.Margin = new Padding(4, 5, 4, 5);
            ledChannelAlarm.Name = "ledChannelAlarm";
            ledChannelAlarm.Size = new Size(129, 75);
            ledChannelAlarm.TabIndex = 1;
            // 
            // ledFiberBreak
            // 
            ledFiberBreak.BackColor = Color.Transparent;
            ledFiberBreak.IsOn = false;
            ledFiberBreak.Label = "Fiber Break";
            ledFiberBreak.LedColor = Color.Orange;
            ledFiberBreak.Location = new Point(279, 5);
            ledFiberBreak.Margin = new Padding(4, 5, 4, 5);
            ledFiberBreak.Name = "ledFiberBreak";
            ledFiberBreak.Size = new Size(129, 75);
            ledFiberBreak.TabIndex = 2;
            // 
            // tabZoneInformation
            // 
            tabZoneInformation.Controls.Add(dataGridView1);
            tabZoneInformation.Controls.Add(panel8);
            tabZoneInformation.Location = new Point(4, 34);
            tabZoneInformation.Margin = new Padding(4, 5, 4, 5);
            tabZoneInformation.Name = "tabZoneInformation";
            tabZoneInformation.Size = new Size(826, 675);
            tabZoneInformation.TabIndex = 2;
            tabZoneInformation.Text = "Zone Information";
            tabZoneInformation.UseVisualStyleBackColor = true;
            // 
            // dataGridView1
            // 
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.AllowUserToDeleteRows = false;
            dataGridViewCellStyle4.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle4.BackColor = SystemColors.Control;
            dataGridViewCellStyle4.Font = new Font("Segoe UI", 9F);
            dataGridViewCellStyle4.ForeColor = SystemColors.WindowText;
            dataGridViewCellStyle4.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle4.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle4.WrapMode = DataGridViewTriState.True;
            dataGridView1.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle4;
            dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Columns.AddRange(new DataGridViewColumn[] { Column2, Column3 });
            dataGridViewCellStyle5.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle5.BackColor = SystemColors.Window;
            dataGridViewCellStyle5.Font = new Font("Segoe UI", 9F);
            dataGridViewCellStyle5.ForeColor = SystemColors.ControlText;
            dataGridViewCellStyle5.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle5.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle5.WrapMode = DataGridViewTriState.False;
            dataGridView1.DefaultCellStyle = dataGridViewCellStyle5;
            dataGridView1.Dock = DockStyle.Fill;
            dataGridView1.Location = new Point(0, 83);
            dataGridView1.Margin = new Padding(0);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.ReadOnly = true;
            dataGridView1.RowHeadersWidth = 62;
            dataGridView1.Size = new Size(826, 592);
            dataGridView1.TabIndex = 0;
            dataGridView1.VirtualMode = true;
            dataGridView1.CellValueNeeded += DataGridView1_CellValueNeeded;
            // 
            // Column2
            // 
            Column2.HeaderText = "Point number";
            Column2.MinimumWidth = 8;
            Column2.Name = "Column2";
            Column2.ReadOnly = true;
            Column2.Width = 502;
            // 
            // Column3
            // 
            Column3.HeaderText = "Point Temperature";
            Column3.MinimumWidth = 8;
            Column3.Name = "Column3";
            Column3.ReadOnly = true;
            Column3.Width = 532;
            // 
            // panel8
            // 
            panel8.BackColor = Color.FromArgb(240, 240, 240);
            panel8.Controls.Add(lblZoneName);
            panel8.Controls.Add(ledZoneEnabled);
            panel8.Controls.Add(ledZoneAlarm);
            panel8.Dock = DockStyle.Top;
            panel8.Location = new Point(0, 0);
            panel8.Name = "panel8";
            panel8.Size = new Size(826, 83);
            panel8.TabIndex = 8;
            // 
            // lblZoneName
            // 
            lblZoneName.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblZoneName.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblZoneName.ForeColor = Color.FromArgb(64, 64, 64);
            lblZoneName.Location = new Point(503, 25);
            lblZoneName.Margin = new Padding(4, 0, 4, 0);
            lblZoneName.Name = "lblZoneName";
            lblZoneName.Size = new Size(314, 33);
            lblZoneName.TabIndex = 2;
            lblZoneName.TextAlign = ContentAlignment.MiddleRight;
            // 
            // ledZoneEnabled
            // 
            ledZoneEnabled.BackColor = Color.Transparent;
            ledZoneEnabled.IsOn = false;
            ledZoneEnabled.Label = "Is Enabled";
            ledZoneEnabled.LedColor = Color.LimeGreen;
            ledZoneEnabled.Location = new Point(7, 5);
            ledZoneEnabled.Margin = new Padding(4, 5, 4, 5);
            ledZoneEnabled.Name = "ledZoneEnabled";
            ledZoneEnabled.Size = new Size(129, 75);
            ledZoneEnabled.TabIndex = 0;
            // 
            // ledZoneAlarm
            // 
            ledZoneAlarm.BackColor = Color.Transparent;
            ledZoneAlarm.IsOn = false;
            ledZoneAlarm.Label = "Has Alarm";
            ledZoneAlarm.LedColor = Color.Red;
            ledZoneAlarm.Location = new Point(143, 5);
            ledZoneAlarm.Margin = new Padding(4, 5, 4, 5);
            ledZoneAlarm.Name = "ledZoneAlarm";
            ledZoneAlarm.Size = new Size(129, 75);
            ledZoneAlarm.TabIndex = 1;
            // 
            // openGraphToolStripMenuItem
            // 
            openGraphToolStripMenuItem.Controls.Add(panel7);
            openGraphToolStripMenuItem.Location = new Point(4, 34);
            openGraphToolStripMenuItem.Margin = new Padding(4, 5, 4, 5);
            openGraphToolStripMenuItem.Name = "openGraphToolStripMenuItem";
            openGraphToolStripMenuItem.Size = new Size(826, 675);
            openGraphToolStripMenuItem.TabIndex = 3;
            openGraphToolStripMenuItem.Text = "Graph";
            // 
            // panel7
            // 
            panel7.Controls.Add(formsPlot1);
            panel7.Controls.Add(panelGraphControls);
            panel7.Dock = DockStyle.Fill;
            panel7.Location = new Point(0, 0);
            panel7.Margin = new Padding(0);
            panel7.Name = "panel7";
            panel7.Size = new Size(826, 675);
            panel7.TabIndex = 1;
            // 
            // formsPlot1
            // 
            formsPlot1.DisplayScale = 1F;
            formsPlot1.Dock = DockStyle.Fill;
            formsPlot1.Location = new Point(0, 50);
            formsPlot1.Margin = new Padding(4, 5, 4, 5);
            formsPlot1.Name = "formsPlot1";
            formsPlot1.Size = new Size(826, 625);
            formsPlot1.TabIndex = 0;
            // 
            // panelGraphControls
            // 
            panelGraphControls.Controls.Add(btnResetAxis);
            panelGraphControls.Controls.Add(btnApplyAxis);
            panelGraphControls.Controls.Add(txtYMax);
            panelGraphControls.Controls.Add(lblYTo);
            panelGraphControls.Controls.Add(txtYMin);
            panelGraphControls.Controls.Add(lblYAxis);
            panelGraphControls.Controls.Add(txtXMax);
            panelGraphControls.Controls.Add(lblXTo);
            panelGraphControls.Controls.Add(txtXMin);
            panelGraphControls.Controls.Add(lblXAxis);
            panelGraphControls.Dock = DockStyle.Top;
            panelGraphControls.Location = new Point(0, 0);
            panelGraphControls.Name = "panelGraphControls";
            panelGraphControls.Padding = new Padding(6, 5, 6, 5);
            panelGraphControls.Size = new Size(826, 50);
            panelGraphControls.TabIndex = 1;
            // 
            // btnResetAxis
            // 
            btnResetAxis.Location = new Point(496, 5);
            btnResetAxis.Name = "btnResetAxis";
            btnResetAxis.Size = new Size(93, 40);
            btnResetAxis.TabIndex = 9;
            btnResetAxis.Text = "Reset";
            btnResetAxis.UseVisualStyleBackColor = true;
            btnResetAxis.Click += btnResetAxis_Click;
            // 
            // btnApplyAxis
            // 
            btnApplyAxis.Location = new Point(394, 5);
            btnApplyAxis.Name = "btnApplyAxis";
            btnApplyAxis.Size = new Size(93, 40);
            btnApplyAxis.TabIndex = 8;
            btnApplyAxis.Text = "Apply";
            btnApplyAxis.UseVisualStyleBackColor = true;
            btnApplyAxis.Click += btnApplyAxis_Click;
            // 
            // txtYMax
            // 
            txtYMax.Location = new Point(317, 5);
            txtYMax.Name = "txtYMax";
            txtYMax.Size = new Size(60, 31);
            txtYMax.TabIndex = 7;
            txtYMax.Text = "80";
            // 
            // lblYTo
            // 
            lblYTo.AutoSize = true;
            lblYTo.Location = new Point(291, 8);
            lblYTo.Name = "lblYTo";
            lblYTo.Size = new Size(29, 25);
            lblYTo.TabIndex = 6;
            lblYTo.Text = "to";
            // 
            // txtYMin
            // 
            txtYMin.Location = new Point(227, 5);
            txtYMin.Name = "txtYMin";
            txtYMin.Size = new Size(60, 31);
            txtYMin.TabIndex = 5;
            txtYMin.Text = "0";
            // 
            // lblYAxis
            // 
            lblYAxis.AutoSize = true;
            lblYAxis.Location = new Point(200, 8);
            lblYAxis.Name = "lblYAxis";
            lblYAxis.Size = new Size(26, 25);
            lblYAxis.TabIndex = 4;
            lblYAxis.Text = "Y:";
            // 
            // txtXMax
            // 
            txtXMax.Location = new Point(126, 5);
            txtXMax.Name = "txtXMax";
            txtXMax.Size = new Size(60, 31);
            txtXMax.TabIndex = 3;
            txtXMax.Text = "1000";
            // 
            // lblXTo
            // 
            lblXTo.AutoSize = true;
            lblXTo.Location = new Point(100, 8);
            lblXTo.Name = "lblXTo";
            lblXTo.Size = new Size(29, 25);
            lblXTo.TabIndex = 2;
            lblXTo.Text = "to";
            // 
            // txtXMin
            // 
            txtXMin.Location = new Point(34, 5);
            txtXMin.Name = "txtXMin";
            txtXMin.Size = new Size(60, 31);
            txtXMin.TabIndex = 1;
            txtXMin.Text = "0";
            // 
            // lblXAxis
            // 
            lblXAxis.AutoSize = true;
            lblXAxis.Location = new Point(9, 8);
            lblXAxis.Name = "lblXAxis";
            lblXAxis.Size = new Size(27, 25);
            lblXAxis.TabIndex = 0;
            lblXAxis.Text = "X:";
            // 
            // groupDisplay
            // 
            groupDisplay.Controls.Add(tabControlDisplay);
            groupDisplay.Dock = DockStyle.Fill;
            groupDisplay.Location = new Point(0, 35);
            groupDisplay.Margin = new Padding(0);
            groupDisplay.Name = "groupDisplay";
            groupDisplay.Size = new Size(834, 713);
            groupDisplay.TabIndex = 24;
            // 
            // settingsToolStripMenuItem
            // 
            settingsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { normalFontSizeToolStripMenuItem, largeFontSizeToolStripMenuItem });
            settingsToolStripMenuItem.Name = "settingsToolStripMenuItem";
            settingsToolStripMenuItem.Size = new Size(92, 29);
            settingsToolStripMenuItem.Text = "Settings";
            // 
            // normalFontSizeToolStripMenuItem
            // 
            normalFontSizeToolStripMenuItem.Name = "normalFontSizeToolStripMenuItem";
            normalFontSizeToolStripMenuItem.Size = new Size(270, 34);
            normalFontSizeToolStripMenuItem.Text = "Normal Font Size";
            // 
            // largeFontSizeToolStripMenuItem
            // 
            largeFontSizeToolStripMenuItem.Name = "largeFontSizeToolStripMenuItem";
            largeFontSizeToolStripMenuItem.Size = new Size(270, 34);
            largeFontSizeToolStripMenuItem.Text = "Large Font Size";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1370, 748);
            Controls.Add(groupDisplay);
            Controls.Add(panelRight);
            Controls.Add(menuStrip1);
            ImeMode = ImeMode.On;
            MainMenuStrip = menuStrip1;
            Margin = new Padding(4, 5, 4, 5);
            Name = "Form1";
            Text = "Fire Rakshak Control Monitoring Configuration Tool";
            Load += Form1_Load;
            panelRight.ResumeLayout(false);
            panel9.ResumeLayout(false);
            tableLayoutPanel2.ResumeLayout(false);
            groupStatus.ResumeLayout(false);
            groupOperation.ResumeLayout(false);
            groupSafety.ResumeLayout(false);
            panel5.ResumeLayout(false);
            tableLayoutPanel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)pictureLogo).EndInit();
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            tabControlDisplay.ResumeLayout(false);
            tabChannelInformation.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridViewZone).EndInit();
            panelChannelHeader.ResumeLayout(false);
            tabZoneInformation.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            panel8.ResumeLayout(false);
            openGraphToolStripMenuItem.ResumeLayout(false);
            panel7.ResumeLayout(false);
            panelGraphControls.ResumeLayout(false);
            panelGraphControls.PerformLayout();
            groupDisplay.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private Panel panelRight;
        private GroupBox groupSafety;
        private View.LedIndicator ledAlarm;
        private View.LedIndicator ledSystem;
        private View.LedIndicator ledComm;
        private Label lblAlarm;
        private Label lblSystem;
        private Label lblComm;
        private GroupBox groupBoxChannelSelection;
        private GroupBox groupOperation;
        private Button btnStartMeasurement;
        private ToolStripMenuItem monitoringToolStripMenuItem;
        private ToolStripMenuItem softwareUpdateToolStripMenuItem;
        private ToolStripMenuItem helpToolStripMenuItem;
        private ToolStripMenuItem aboutToolStripMenuItem;
        private ToolStripMenuItem adminToolStripMenuItem;
        private ToolStripMenuItem userManagementToolStripMenuItem;
        private ToolStripMenuItem systemConfigurationToolStripMenuItem;
        private ToolStripMenuItem logConfigurationToolStripMenuItem;
        private ToolStripMenuItem systemHealthToolStripMenuItem;
        private ToolStripMenuItem relayMappingToolStripMenuItem;
        private ToolStripMenuItem atpToolStripMenuItem;
        private MenuStrip menuStrip1;
        private Button refresh;
        private ToolStripMenuItem fRMCCurrentVersionToolStripMenuItem;
        private ToolStripMenuItem Configuration;
        private ToolStripMenuItem channelZoneConfigurationToolStripMenuItem;
        private ToolStripMenuItem activeAlarmToolStripMenuItem;
        private ToolStripMenuItem loadConfigurationFileToolStripMenuItem;
        private ToolStripMenuItem saveConfigurationFileToolStripMenuItem;
        private ToolStripMenuItem eventListToolStripMenuItem1;
        private ToolStripMenuItem auditTrailToolStripMenuItem1;
        private Panel panel5;
        private TableLayoutPanel tableLayoutPanel1;
        private PictureBox pictureLogo;
        private TabControl tabControlDisplay;
        private TabPage tabChannelInformation;
        private Label lblChannelName;
        private View.LedIndicator ledChannelEnabled;
        private View.LedIndicator ledChannelAlarm;
        private View.LedIndicator ledFiberBreak;
        private DataGridView dataGridViewZone;
        private TabPage tabZoneInformation;
        private Label lblZoneName;
        private View.LedIndicator ledZoneEnabled;
        private View.LedIndicator ledZoneAlarm;
        private DataGridView dataGridView1;
        private DataGridViewTextBoxColumn Column2;
        private DataGridViewTextBoxColumn Column3;
        private TabPage openGraphToolStripMenuItem;
        private Panel panel7;
        private ScottPlot.WinForms.FormsPlot formsPlot1;
        //private GroupBox groupDisplay;
        private Panel groupDisplay;
        private Panel panelChannelHeader;
        private Panel panel8;
        private Panel panel9;
        private TableLayoutPanel tableLayoutPanel2;
        private GroupBox groupStatus;
        private RichTextBox richTextBoxStatus;
        private ToolStripMenuItem iPConfigurationToolStripMenuItem;
        private ToolStripMenuItem usbConfigurationToolStripMenuItem;
        private Panel panelGraphControls;
        private Label lblXAxis;
        private TextBox txtXMin;
        private Label lblXTo;
        private TextBox txtXMax;
        private Label lblYAxis;
        private TextBox txtYMin;
        private Label lblYTo;
        private TextBox txtYMax;
        private Button btnApplyAxis;
        private Button btnResetAxis;
        private ToolStripMenuItem resetAllConfigurationToolStripMenuItem;
        private ToolStripMenuItem clockConfigurationToolStripMenuItem;
        private ToolStripMenuItem dtsCalibrationToolStripMenuItem;
        private ToolStripMenuItem temperatureCorrectionToolStripMenuItem;
        private DataGridViewTextBoxColumn colZone;
        private DataGridViewTextBoxColumn colZoneName;
        private DataGridViewTextBoxColumn colStart;
        private DataGridViewTextBoxColumn colStop;
        private DataGridViewTextBoxColumn colActive;
        private DataGridViewTextBoxColumn colRelay;
        private DataGridViewTextBoxColumn colMaxA1;
        private DataGridViewTextBoxColumn colMaxA2;
        private DataGridViewTextBoxColumn colMin;
        private DataGridViewTextBoxColumn Column4;
        private DataGridViewTextBoxColumn Column1;
        private ToolStripMenuItem settingsToolStripMenuItem;
        private ToolStripMenuItem normalFontSizeToolStripMenuItem;
        private ToolStripMenuItem largeFontSizeToolStripMenuItem;
    }
}
