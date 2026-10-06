using FireRakshak.FRMC.Core.Models;
using FRCM;
using FRCM.Models;
using FRCM.Services;
using FRCM.View;
using OfficeOpenXml;
using ScottPlot;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Timers;
using System.Windows.Forms;
using DrawingColor = System.Drawing.Color;
using WinFormsLabel = System.Windows.Forms.Label;

namespace FRCM
{
    public partial class Form1 : Form
    {
        private bool isMonitoring = false;
        private TreeView? treeViewZones;

        private readonly CustomWebSocketClient _wsClient; // Made readonly
        private readonly string _loginRole;

        // DUAL WEBSOCKET: Separate client for temperature data streaming (port 6165)
        private DataStreamClient? _dataStreamClient;

        // FM-05: Dual-Port Health Watchdog against Asymmetric Port Desynchronization
        private CancellationTokenSource? _dualPortWatchdogCts;
        private readonly object _dualPortWatchdogLock = new();
        private DateTime _lastWatchdogResyncUtc = DateTime.MinValue;

        // MULTI-THREADED PROCESSING: Dedicated background threads for data processing
        private TemperatureDataProcessor? _temperatureProcessor;
        private CommandMessageProcessor? _commandProcessor;

        private ChannelClient _ChannelClient;

        // FM-07: Double-buffered trace cache for thread-safe lock-free graph rendering and cursor tracking
        private readonly DoubleBufferedTraceCache _traceCache = new();

        private List<double> distances = new();
        private List<double> temperatures = new();
        //private ScottPlot.Plottables.Scatter? scatter;
        private const double PointDistance = 0.4; // meters
        private bool isPlottingEnabled = false;
        private bool hasLiveData = false; // indicates data exists and can be frozen
        private bool initialStatusShown = false;
        private List<AlarmIndex> alarmIndices = new();
        private List<AlarmMapping> alarmMappings = new();
        private List<ChannelBatch> _configBatches = new();
        private int _currentChannelIndex = 0;
        private System.Windows.Forms.Label lblCursorValue;
        private System.Windows.Forms.Label lblGraphTitle;
        private ScottPlot.Plottables.VerticalLine? markerLine;
        private double _markerLineX = 200;
        private HashSet<int> _channelsWithAlarm = new();          // Zone alarms only for enabled zones (not fiber break)
        private HashSet<int> _channelsWithFiberBreak = new();     // Fiber break alarms only
        private HashSet<int> _channelsWithAnyAlarm = new();       // All alarms from FRMC (for system LED sync with hardware)
        private HashSet<(int channelId, int zoneId)> _zonesWithAlarm = new();
        private HashSet<int> _loggedChannelAlarms = new();
        private HashSet<(int channelId, int zoneId)> _loggedZoneAlarms = new();
        public static bool ActiveAlarmClosedByUser = false;
        private int _previousAlarmCount = -1; // Initialize to -1 so that the first check (even with count 0) is treated correctly
        // Cache for zone and channel states (for fast switching and real-time updates)
        private readonly Dictionary<(int channelId, int zoneId), ZoneStateInfo> _zoneStateCache = new();
        private readonly Dictionary<int, object> _channelStateCache = new();
        private Dictionary<int, (double[] x, double[] y)> _graphCache = new();

        // Backing store for DataGridView1 VirtualMode (zone points grid)
        private List<PointData> _gridPointsCache = new();

        // Graph data cache for persistent display across channel/zone switches
        private readonly Dictionary<int, (List<double> distances, List<double> temperatures)> _channelGraphCache = new();
        private readonly Dictionary<(int ch, int zone), (List<double> distances, List<double> temperatures)> _zoneGraphCache = new();

        //private Dictionary<int, int> _currentChannelZoneCounts = new();
        //private List<ChannelBatch> _loadedChannelBatches = new();
        //private bool _channelLoadSuccessful = false;
        //private object _validatedZoneMessage;

        private Dictionary<int, int> _currentChannelZoneCounts = new();
        private List<ChannelBatch> _loadedChannelBatches = new();

        private bool _channelLoadSuccessful = false;

        private string _validatedChannelMessage = "";
        private string _validationFailureMessage = "";

        private object? _validatedZoneMessage;
        private bool _zoneLoadSuccessful = false;

        private int _selectedChannelId = -1;
        private int _selectedZoneId = -1;
        private double? _selectedZoneStartPosition = null;
        private double? _selectedZoneEndPosition = null;
        private int _subscribedChannelId = -1; // Tracks which channel we're currently subscribed to for live data
        private string _currentGraphTitle = "Live Temperature Data";




        private readonly ITemperatureDataService _temperatureDataService;
        private readonly ILogger _logger;
        private readonly string _username;
        private readonly string _password;
        private readonly IConfigurationService _configurationService;
        private int _currentChannelToDisplayId = 1; // Current channel ID (1-based)
        private System.Windows.Forms.Label? _loadingLabel = null; // Loading indicator
        private System.Windows.Forms.Timer? _ledUpdateTimer = null; // Timer for periodic LED updates
        private System.Windows.Forms.Timer? _alarmPollingTimer = null; // Timer for periodic active alarm polling

        // LED state tracking - only repaint when state actually changes
        private bool _lastChannelAlarmState = false;
        private bool _lastFiberBreakState = false;
        private bool _lastSystemAlarmState = false;
        private bool _lastSystemHealthFault = false;
        private bool _lastDtsCommState = false;
        private bool _lastChannelEnabledState = false;
        private bool _lastZoneEnabledState = false;
        private bool _lastZoneAlarmState = false;

        // Graph performance optimization - throttling
        private DateTime _lastGraphUpdate = DateTime.MinValue;
        private const int GRAPH_UPDATE_THROTTLE_MS = 100;

        // Grid performance optimization - throttling (heavy WinForms op)
        private DateTime _lastGridUpdate = DateTime.MinValue;
        private const int GRID_UPDATE_THROTTLE_MS = 1000;

        // Health LED - consecutive failure threshold
        private int _healthCheckFailures = 0;
        private const int HEALTH_GRAY_THRESHOLD = 2;

        // FIX: Lock object for thread-safe ActiveAlarm singleton initialization
        private static readonly object _activeAlarmLock = new object();
        private ScottPlot.Plottables.Scatter? _liveScatter;

        // Axis fixing state (managed automatically, no visible checkboxes)
        private bool _isXAxisFixed = true;  // Default: fixed to 0-ChannelLength
        private bool _isYAxisFixed = true;  // Default: fixed to 0-80°C
        private const double DEFAULT_Y_MIN = 0;
        private const double DEFAULT_Y_MAX = 80;



        // Channel-specific axis limits (preserved per channel)
        private readonly Dictionary<int, (double xMin, double xMax, double yMin, double yMax)> _channelAxisLimits = new();
        private bool _userAxisApplied = false;
        private double _userXMin, _userXMax, _userYMin, _userYMax;
        private ContextMenuStrip? _graphContextMenu;
        private Point _lastMousePosition;
        private bool _isDragging = false;

        // Track previous health state for logging changes
        private bool? _previousDtsResponsive = null;
        private bool? _previousLampStatus = null;
        private bool? _previousTempWarning = null;

        // System Health LED tooltip and active faults cache
        private ToolTip? _systemHealthToolTip = null;
        private List<SystemHealthFaultInfo> _activeHealthFaults = new List<SystemHealthFaultInfo>();

        // Reconnect state: prevents multiple simultaneous reconnect loops
        private bool _isReconnecting = false;
        private ConnectingPopup? _connectingPopup = null; // shown immediately while retrying

        public Form1(ITemperatureDataService temperatureDataService, ILogger logger,
              string username, string password,
              CustomWebSocketClient wsClient,
              IConfigurationService configurationService, // New parameter
              string loginRole)
        {
            InitializeComponent();

            // Font Size Settings
            normalFontSizeToolStripMenuItem.Click += (s, e) => FRCM.Services.FontSizeHelper.SetGlobalFontSize(1.0f);
            largeFontSizeToolStripMenuItem.Click += (s, e) => FRCM.Services.FontSizeHelper.SetGlobalFontSize(1.15f);
            FRCM.Services.FontSizeHelper.UpdateControlRecursive(this, FRCM.Services.FontSizeHelper.CurrentMultiplier);

            // ✅ FIX: Format all numeric columns to show max 2 decimal places
            ConfigureDataGridViewFormatting();

            this.AutoScaleMode = AutoScaleMode.Dpi;

            // Enable automatic row height scaling and text wrapping for 55-inch screens
            float scalingFactor = this.DeviceDpi / 96f;
            float fontScaling = FontSizeHelper.GetAutoScaling(this);
            int scaledHeaderHeight = (int)(60 * scalingFactor);
            int scaledPanelHeight = (int)(85 * scalingFactor);

            // FIX: Use TableLayoutPanel to prevent ANY overlapping between LEDs and Grid

            // 1. Setup Channel Information Tab
            tabChannelInformation.Controls.Clear();
            var tlpChannel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            tlpChannel.RowStyles.Add(new RowStyle(SizeType.Absolute, scaledPanelHeight));
            tlpChannel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            panelChannelHeader.Dock = DockStyle.Fill; // Fill the first row
            dataGridViewZone.Dock = DockStyle.Fill;   // Fill the second row

            tlpChannel.Controls.Add(panelChannelHeader, 0, 0);
            tlpChannel.Controls.Add(dataGridViewZone, 0, 1);
            tabChannelInformation.Controls.Add(tlpChannel);

            // 2. Setup Zone Information Tab
            tabZoneInformation.Controls.Add(dataGridView1); // Ensure it's in the collection
            tabZoneInformation.Controls.Clear();
            var tlpZone = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            tlpZone.RowStyles.Add(new RowStyle(SizeType.Absolute, scaledPanelHeight));
            tlpZone.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            panel8.Dock = DockStyle.Fill; // Fill the first row
            dataGridView1.Dock = DockStyle.Fill;   // Fill the second row

            tlpZone.Controls.Add(panel8, 0, 0);
            tlpZone.Controls.Add(dataGridView1, 0, 1);
            tabZoneInformation.Controls.Add(tlpZone);

            // Scale the Channel/Zone name labels and prevent clipping
            lblChannelName.AutoSize = true;
            lblZoneName.AutoSize = true;
            // lblChannelName.Font = new System.Drawing.Font("Segoe UI", 12f * scalingFactor, System.Drawing.FontStyle.Bold);
            // lblZoneName.Font = new System.Drawing.Font("Segoe UI", 12f * scalingFactor, System.Drawing.FontStyle.Bold);
            lblChannelName.Font = new System.Drawing.Font("Segoe UI", 10f * fontScaling, System.Drawing.FontStyle.Bold);
            lblZoneName.Font = new System.Drawing.Font("Segoe UI", 10f * fontScaling, System.Drawing.FontStyle.Bold);

            // Re-position them so they stay in the right corner but have room to grow
            lblChannelName.Location = new Point(tabChannelInformation.Width - (int)(180 * scalingFactor), (int)(25 * scalingFactor));
            lblZoneName.Location = new Point(tabZoneInformation.Width - (int)(180 * scalingFactor), (int)(25 * scalingFactor));

            // Prevent top panels from being squashed by WinForms layout engine
            panel5.MinimumSize = new Size(0, panel5.Height);
            groupSafety.MinimumSize = new Size(0, groupSafety.Height);
            groupBoxChannelSelection.MinimumSize = new Size(0, groupBoxChannelSelection.Height);
            groupOperation.MinimumSize = new Size(0, groupOperation.Height);

            // Intelligent Resize for Status Panel: Only scroll main panel if Status would disappear
            panelRight.Resize += (s, e) => {
                int fixedControlsHeight = panel5.Height + groupSafety.Height + groupBoxChannelSelection.Height + groupOperation.Height;
                int availableHeight = panelRight.ClientSize.Height - fixedControlsHeight;

                // Only show main scrollbar if the Status box is nearly invisible (< 20px)
                if (availableHeight > 20)
                {
                    // Status box has some room, let it occupy it and hide main scrollbar
                    // We use -2 as a safety margin to avoid layout rounding triggering the scrollbar
                    panel9.Height = availableHeight - 2;
                }
                else
                {
                    // Status box is disappearing, force a usable height (150px) to trigger main scrollbar
                    panel9.Height = 150;
                }
            };
            // Define scalable font sizes for grids
            float headerFontSize = 8f * fontScaling;
            float cellFontSize = 8f * fontScaling;
            System.Drawing.Font headerFont = new System.Drawing.Font("Segoe UI", headerFontSize, System.Drawing.FontStyle.Bold);
            System.Drawing.Font cellFont = new System.Drawing.Font("Segoe UI", cellFontSize, System.Drawing.FontStyle.Regular);

            // dataGridViewZone (Main Zone Status Grid)
            dataGridViewZone.EnableHeadersVisualStyles = false; // Required for custom styles
            dataGridViewZone.ColumnHeadersDefaultCellStyle.Font = headerFont;
            dataGridViewZone.ColumnHeadersDefaultCellStyle.BackColor = System.Drawing.Color.LightGray;
            dataGridViewZone.ColumnHeadersDefaultCellStyle.SelectionBackColor = System.Drawing.Color.LightGray;
            dataGridViewZone.ColumnHeadersDefaultCellStyle.SelectionForeColor = System.Drawing.Color.Black;
            dataGridViewZone.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True; // Enable wrapping for multi-word headers
            dataGridViewZone.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter; // Center text for better wrapping look

            // Fix: Force header height to scale for 55-inch screens
            dataGridViewZone.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dataGridViewZone.ColumnHeadersHeight = (int)(56 * scalingFactor);

            dataGridViewZone.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            dataGridViewZone.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewZone.DefaultCellStyle.Font = cellFont;
            dataGridViewZone.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dataGridViewZone.RowTemplate.MinimumHeight = (int)(30 * scalingFactor);
            dataGridViewZone.DefaultCellStyle.Padding = new Padding((int)(3 * scalingFactor));

            // dataGridView1 (Zone Points Grid)
            dataGridView1.EnableHeadersVisualStyles = false;
            dataGridView1.ColumnHeadersDefaultCellStyle.Font = headerFont;
            dataGridView1.ColumnHeadersDefaultCellStyle.BackColor = System.Drawing.Color.LightGray;
            dataGridView1.ColumnHeadersDefaultCellStyle.SelectionBackColor = System.Drawing.Color.LightGray;
            dataGridView1.ColumnHeadersDefaultCellStyle.SelectionForeColor = System.Drawing.Color.Black;

            // Fix: Force header height to scale for 55-inch screens
            dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dataGridView1.ColumnHeadersHeight = scaledHeaderHeight;

            dataGridView1.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridView1.DefaultCellStyle.Font = cellFont;
            dataGridView1.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dataGridView1.RowTemplate.MinimumHeight = (int)(30 * scalingFactor);
            dataGridView1.DefaultCellStyle.Padding = new Padding((int)(3 * scalingFactor));

            this.StartPosition = FormStartPosition.Manual;
            //this.MinimumSize = new Size(1280, 800);

            // 🔹 attach Shown ONCE
            this.Shown += Form1_Shown;

            Console.WriteLine("FRCM started FORM1_LOAD");

            _temperatureDataService = temperatureDataService;
            _logger = logger;
            _username = username;
            _password = password;
            _wsClient = wsClient;
            _configurationService = configurationService; // Assign new service
            _loginRole = loginRole;

            _ChannelClient = new ChannelClient(_wsClient); // Initialize ChannelClient
        }

        private void OpenGraphTab()
        {
            tabControlDisplay.SelectedTab = openGraphToolStripMenuItem;
        }

        /// <summary>
        /// Configures number formatting for all numeric columns in the zone information DataGridView.
        /// Sets all temperature, position, RoR, and deviation values to display max 2 decimal places.
        /// </summary>
        private void ConfigureDataGridViewFormatting()
        {
            // Format: "0.##" shows up to 2 decimal places, removing trailing zeros
            // Examples: 25.5 → "25.5", 25.00 → "25", 25.123 → "25.12"

            // Position columns (Zone Start, Zone Stop)
            if (dataGridViewZone.Columns["colStart"] != null)
                dataGridViewZone.Columns["colStart"].DefaultCellStyle.Format = "0.##";

            if (dataGridViewZone.Columns["colStop"] != null)
                dataGridViewZone.Columns["colStop"].DefaultCellStyle.Format = "0.##";

            // Temperature columns (Max, Min, Avg)
            if (dataGridViewZone.Columns["colMaxA1"] != null)
                dataGridViewZone.Columns["colMaxA1"].DefaultCellStyle.Format = "0.##";

            if (dataGridViewZone.Columns["colMaxA2"] != null)
                dataGridViewZone.Columns["colMaxA2"].DefaultCellStyle.Format = "0.##";

            if (dataGridViewZone.Columns["Column4"] != null)
                dataGridViewZone.Columns["Column4"].DefaultCellStyle.Format = "0.##";

            // Rate of Rise column
            if (dataGridViewZone.Columns["colMin"] != null)
                dataGridViewZone.Columns["colMin"].DefaultCellStyle.Format = "0.##";

            // Deviation column
            if (dataGridViewZone.Columns["Column1"] != null)
                dataGridViewZone.Columns["Column1"].DefaultCellStyle.Format = "0.##";
        }

        private void UpdateStatus(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateStatus(message)));
                return;
            }

            string time = DateTime.Now.ToString("HH:mm:ss");
            richTextBoxStatus.AppendText($"[{time}] {message}\n");
            richTextBoxStatus.ScrollToCaret();
        }

        private async void Form1_Load(object? sender, EventArgs e)
        {
            //this.WindowState = FormWindowState.Normal;

            //pictureLogo.Image = Properties.Resources.FireRakshakLogo;
            //pictureLogo.Image = Properties.Resources.logoo;
            pictureLogo.Image = Properties.Resources.Final_Logo;
            pictureLogo.SizeMode = PictureBoxSizeMode.Zoom;
            pictureLogo.BackColor = System.Drawing.Color.White;

            if (!initialStatusShown)
            {
                UpdateStatus("FRMC application started");
                initialStatusShown = true;
            }
            _currentChannelToDisplayId = 1;
            dataGridViewZone.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            _systemHealthToolTip = new ToolTip
            {
                AutoPopDelay = 10000,  // Show for 10 seconds
                InitialDelay = 200,    // Show after 200ms hover
                ReshowDelay = 100,
                ShowAlways = true
            };
            if (ledSystem != null)
                _systemHealthToolTip.SetToolTip(ledSystem, "System Health: OK");

            // Channel/Zone LEDs now use custom LedIndicator controls - no setup needed

            // Initialize loading label
            InitializeLoadingLabel();

            //this.Shown += Form1_Shown;

            UpdateSafetyActiveAlarmLed();

            if (!tabControlDisplay.TabPages.Contains(tabChannelInformation))
                tabControlDisplay.TabPages.Add(tabChannelInformation);

            if (tabControlDisplay.TabPages.Contains(tabZoneInformation))
                tabControlDisplay.TabPages.Remove(tabZoneInformation);

            InitializeChannelTree();

            SelectDefaultChannel();

            InitializeGraph();
            float scalingFactor = this.DeviceDpi / 96f;
            float fontScaling = FontSizeHelper.GetAutoScaling(this);

            // ---------------- GRAPH INFO LABELS ----------------
            // Set panel height to 45 (slightly more breathing room)
            panelGraphControls.Height = (int)(45 * scalingFactor);
            int sharedHeight = (int)(24 * scalingFactor);
            int commonY = (int)(10 * scalingFactor);
            int labelYOffset = (int)(3 * scalingFactor); // Offset for labels to align text baselines

            // 🚀 PERFECT HORIZONTAL SYNC & UNIFORM HEIGHT
            // Squeezing axis controls (X-Axis)
            lblXAxis.Location = new Point((int)(5 * scalingFactor), commonY + labelYOffset);
            txtXMin.Location = new Point((int)(20 * scalingFactor), commonY);
            txtXMin.Size = new Size((int)(30 * scalingFactor), sharedHeight);
            lblXTo.Location = new Point((int)(55 * scalingFactor), commonY + labelYOffset);
            txtXMax.Location = new Point((int)(72 * scalingFactor), commonY);
            txtXMax.Size = new Size((int)(30 * scalingFactor), sharedHeight);

            // Squeezing Y-Axis
            lblYAxis.Location = new Point((int)(110 * scalingFactor), commonY + labelYOffset);
            txtYMin.Location = new Point((int)(125 * scalingFactor), commonY);
            txtYMin.Size = new Size((int)(30 * scalingFactor), sharedHeight);
            lblYTo.Location = new Point((int)(160 * scalingFactor), commonY + labelYOffset);
            txtYMax.Location = new Point((int)(177 * scalingFactor), commonY);
            txtYMax.Size = new Size((int)(30 * scalingFactor), sharedHeight);

            // Squeezing Buttons - Increased width to 52 to prevent text clipping
            btnApplyAxis.Location = new Point((int)(215 * scalingFactor), commonY);
            btnApplyAxis.Size = new Size((int)(52 * scalingFactor), sharedHeight);
            btnResetAxis.Location = new Point((int)(272 * scalingFactor), commonY);
            btnResetAxis.Size = new Size((int)(52 * scalingFactor), sharedHeight);

            // Container for Labels - Now starts at X=335 (shifted right to accommodate wider buttons)
            var pnlGraphLabels = new TableLayoutPanel
            {
                Location = new Point((int)(335 * scalingFactor), 0),
                Size = new Size(panelGraphControls.Width - (int)(340 * scalingFactor), (int)(45 * scalingFactor)),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = System.Drawing.Color.Transparent,
                Padding = new Padding(0, 0, (int)(15 * scalingFactor), 0)
            };
            pnlGraphLabels.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pnlGraphLabels.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            pnlGraphLabels.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pnlGraphLabels.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            panelGraphControls.Controls.Add(pnlGraphLabels);

            // 1. Graph Title Label (Reduced to 8.5pt)
            lblGraphTitle = new System.Windows.Forms.Label
            {
                AutoSize = true,
                BackColor = System.Drawing.Color.Transparent,
                Font = new System.Drawing.Font("Segoe UI", 8.5f * fontScaling, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.Black,
                Text = _currentGraphTitle,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                MaximumSize = new Size((int)(350 * scalingFactor), 0),
                AutoEllipsis = true
            };

            lblCursorValue = new System.Windows.Forms.Label
            {
                AutoSize = true,
                BackColor = System.Drawing.Color.FromArgb(230, System.Drawing.Color.White),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new System.Drawing.Font("Segoe UI", 6.5f * fontScaling, System.Drawing.FontStyle.Bold),
                Visible = false,
                Anchor = AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding((int)(2 * scalingFactor), (int)(1 * scalingFactor), (int)(2 * scalingFactor), (int)(1 * scalingFactor)),
                Margin = new Padding(0, 0, (int)(10 * scalingFactor), 0),
                UseCompatibleTextRendering = true
            };

            pnlGraphLabels.Controls.Add(lblGraphTitle, 0, 0);
            pnlGraphLabels.Controls.Add(lblCursorValue, 2, 0);

            lblGraphTitle.BringToFront();
            lblCursorValue.BringToFront();

            // Attach mouse events
            formsPlot1.MouseMove += FormsPlot1_MouseMove;
            formsPlot1.MouseLeave += FormsPlot1_MouseLeave;

            // Add axis fixing checkboxes
            InitializeAxisControls();


            tabControlDisplay.SelectedTab = openGraphToolStripMenuItem;

            // Refresh info grids when user switches tabs so data is always current
            tabControlDisplay.SelectedIndexChanged += TabControlDisplay_SelectedIndexChanged;

            // MULTI-THREADED PROCESSING: Initialize background processors FIRST
            InitializeProcessors();

            // Legacy event-based handler removed - now using direct WebSocket message handler with channel filtering
            // _temperatureDataService.TemperatureDataUpdated += OnTemperatureDataUpdated;
            _wsClient.MessageReceived += OnWebSocketMessageReceived;
            _wsClient.BinaryMessageReceived += OnWebSocketBinaryMessageReceived;
            _wsClient.ConnectionLost += OnMainWebSocketConnectionLost;
            if (_wsClient.IsConnected)
            {
                UpdateStatus("WebSocket connected successfully");

                // DUAL WEBSOCKET: Initialize and connect the data stream client for temperature data
                await InitializeDataStreamClientAsync();

                // FM-05: Start Dual-Port Health Watchdog to detect and recover asymmetric port desync
                StartDualPortHealthWatchdog();

                // Ensure FRMC is not streaming when DTSCM starts
                await StopMonitoringAsync();

                RequestActiveAlarms();
            }

            // FIX: Thread-safe singleton initialization with double-checked locking
            if (ActiveAlarm.Instance == null)
            {
                lock (_activeAlarmLock)
                {
                    if (ActiveAlarm.Instance == null)
                    {
                        ActiveAlarm.Instance = new ActiveAlarm(_wsClient, _configurationService);
                        ActiveAlarm.Instance.ActiveChannelsChanged += OnActiveChannelsChanged;
                        ActiveAlarm.Instance.ActiveFiberBreakChannelsChanged += OnActiveFiberBreakChannelsChanged;
                        ActiveAlarm.Instance.AllActiveChannelsChanged += OnAllActiveChannelsChanged;
                        ActiveAlarm.Instance.ActiveZonesChanged += OnActiveZonesChanged;
                    }
                }
            }

            // Initialize LED update timer (30-second interval)
            _ledUpdateTimer = new System.Windows.Forms.Timer();
            _ledUpdateTimer.Interval = 30000; // 30 seconds
            _ledUpdateTimer.Tick += LedUpdateTimer_Tick;
            _ledUpdateTimer.Start();

            // Initialize alarm polling timer (5-second interval) - starts when monitoring begins
            _alarmPollingTimer = new System.Windows.Forms.Timer();
            _alarmPollingTimer.Interval = 5000; // 5 seconds
            _alarmPollingTimer.Tick += AlarmPollingTimer_Tick;
            // Timer starts when measurement starts (see btnStartMeasurement_Click)

            // Perform initial LED update
            _ = UpdateMainLEDsAsync();
        }

        /// <summary>
        /// MULTI-THREADED PROCESSING: Initialize background processors for temperature data and commands.
        /// This separates network I/O from data processing from UI updates.
        /// </summary>
        private void InitializeProcessors()
        {
            // Temperature data processor - handles data from DataStreamClient
            // Throttle: 100ms minimum between UI updates
            // Queue size: 10 (drops oldest if backed up)
            _temperatureProcessor = new TemperatureDataProcessor(
                onDataReady: OnProcessedTemperatureData,
                minUpdateIntervalMs: 100,
                maxQueueSize: 10);

            // Command message processor - handles messages from control WebSocket
            _commandProcessor = new CommandMessageProcessor(maxQueueSize: 100);

            // Subscribe to processed command events
            _commandProcessor.OnHealthStatus += OnProcessedHealthStatus;
            _commandProcessor.OnZoneStateUpdate += OnProcessedZoneStateUpdate;
            _commandProcessor.OnAlarmTriggered += OnProcessedAlarmTriggered;
            _commandProcessor.OnAlarmCleared += OnProcessedAlarmCleared;
            _commandProcessor.OnAlarmAutoCleared += OnProcessedAlarmAutoCleared;
            _commandProcessor.OnActiveAlarmSnapshot += OnProcessedActiveAlarmSnapshot;
            _commandProcessor.OnAllAlarmsCleared += OnProcessedAllAlarmsCleared;

            _commandProcessor.OnHealthFaultDetected += OnProcessedHealthFaultDetected;
            _commandProcessor.OnHealthFaultCleared += OnProcessedHealthFaultCleared;
            _commandProcessor.OnChannelConfigUpdated += OnProcessedChannelConfigUpdated;
            _commandProcessor.OnRelayStateUpdate += OnProcessedRelayStateUpdate;

            _logger.Log("[PROCESSORS] Temperature and command processors initialized");
            Console.WriteLine("[PROCESSORS] Background processors initialized");
        }

        /// <summary>
        /// Callback from TemperatureDataProcessor when processed data is ready.
        /// Called on background thread - must marshal to UI.
        /// Updates graph and info grids with live data.
        /// </summary>
        //
        private void OnProcessedTemperatureData(
            int channelId,
            List<FireRakshak.FRMC.Core.Models.TemperatureDataPoint> dataPoints)
        {
            // ✅ Only update if correct channel + plotting enabled
            if (channelId != _selectedChannelId || !isPlottingEnabled)
                return;

            // ✅ Ensure UI thread execution
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                    OnProcessedTemperatureData(channelId, dataPoints)));
                return;
            }

            hasLiveData = true;

            bool isZoneSelected = _selectedZoneId > 0
                && _selectedZoneStartPosition.HasValue
                && _selectedZoneEndPosition.HasValue;

            // 🔥 ZONE MODE → DO NOT UPDATE GRAPH CONTINUOUSLY
            if (isZoneSelected)
            {
                double zoneStart = _selectedZoneStartPosition.Value;
                double zoneEnd = _selectedZoneEndPosition.Value;

                var filtered = dataPoints
                    .Where(p => p.Position >= zoneStart && p.Position <= zoneEnd)
                    .ToList();

                // ✅ Only update cache
                _zoneGraphCache[(channelId, _selectedZoneId)] = (
                    filtered.Select(p => p.Position).ToList(),
                    filtered.Select(p => p.Temperature).ToList()
                );

                // ❌ IMPORTANT: DO NOT call UpdateGraph here
            }
            else
            {
                // ✅ CHANNEL MODE → live graph allowed
                UpdateGraph(dataPoints);

                _channelGraphCache[channelId] = (
                    dataPoints.Select(p => p.Position).ToList(),
                    dataPoints.Select(p => p.Temperature).ToList()
                );
            }

            // 🔥 UPDATE GRID (unchanged)
            UpdateInfoGridsWithLiveData(dataPoints);
        }


        /// <summary>
        /// Updates the info grids with live temperature data.
        /// Called when new temperature data is received during measurement.
        /// Zone points are fetched from FRMC API at full resolution (not from the decimated stream).
        /// Channel Information grid is updated via zone_state_broadcast (binary batches).
        /// </summary>
        private void UpdateInfoGridsWithLiveData(List<FireRakshak.FRMC.Core.Models.TemperatureDataPoint> dataPoints)
        {
            try
            {
                var now = DateTime.Now;
                if ((now - _lastGridUpdate).TotalMilliseconds < GRID_UPDATE_THROTTLE_MS)
                    return;
                _lastGridUpdate = now;

                var currentTab = tabControlDisplay.SelectedTab;

                // Zone Information tab: fetch full-resolution zone points from FRMC API
                if (currentTab == tabZoneInformation && _selectedChannelId > 0 && _selectedZoneId > 0)
                {
                    _ = RefreshZonePointsAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[INFO GRID UPDATE] Error: {ex.Message}");
            }
        }

        private DateTime _lastZonePointsRefresh = DateTime.MinValue;
        private const int ZONE_POINTS_REFRESH_MS = 5000; // 5 seconds between API calls
        private bool _zonePointsRefreshInProgress = false;

        /// <summary>
        /// Fetches zone points from FRMC at full resolution and updates the grid.
        /// Throttled to avoid flooding the WebSocket with requests.
        /// </summary>
        private async Task RefreshZonePointsAsync()
        {
            if (!isMonitoring) return;

            var now = DateTime.Now;
            if (_zonePointsRefreshInProgress || (now - _lastZonePointsRefresh).TotalMilliseconds < ZONE_POINTS_REFRESH_MS)
                return;

            _zonePointsRefreshInProgress = true;
            try
            {
                _lastZonePointsRefresh = now;
                var points = await _ChannelClient.GetZonePointsAsync(_selectedChannelId, _selectedZoneId);
                if (points != null && points.Count > 0)
                {
                    PopulateZonePointsGrid(points);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ZonePointsRefresh] Error: {ex.Message}");
            }
            finally
            {
                _zonePointsRefreshInProgress = false;
            }
        }

        /// <summary>
        /// DUAL WEBSOCKET: Initialize and connect the data stream client for temperature data (port 6165).
        /// This separates temperature streaming from control messages to prevent delays.
        /// Uses the same FRMC host as the control WebSocket (configured in Program.FrmcHost).
        /// </summary>
        private async Task InitializeDataStreamClientAsync()
        {
            try
            {
                // Use the same host as the control WebSocket (from Program.FrmcHost)
                string host = Program.FrmcHost;

                _dataStreamClient = new DataStreamClient();
                _dataStreamClient.TemperatureDataReceived += OnDataStreamTemperatureReceived;
                _dataStreamClient.ConnectionLost += OnDataStreamConnectionLost;

                int dataStreamPort = Program.FrmcDataStreamPort;
                await _dataStreamClient.ConnectAsync(host, dataStreamPort);
                _logger.Log($"[DUAL WS] Data stream client connected to {host}:{dataStreamPort}");
                Console.WriteLine($"[DUAL WS] Data stream client connected to {host}:{dataStreamPort}");
            }
            catch (Exception ex)
            {
                _logger.Log($"[DUAL WS] Failed to connect data stream client: {ex.Message}");
                Console.WriteLine($"[DUAL WS] Failed to connect data stream client: {ex.Message}");
                // Fall back to using control WebSocket for temperature data
                _dataStreamClient = null;
            }
        }

        /// <summary>
        /// DUAL WEBSOCKET: Handle temperature data received from the data stream server.
        /// MULTI-THREADED: Enqueues data to background processor instead of blocking network thread.
        /// </summary>
        private void OnDataStreamTemperatureReceived(object? sender, TemperatureDataEventArgs e)
        {
            // Quick filter on network thread - don't even enqueue if not needed
            if (!isPlottingEnabled || e.ChannelId != _selectedChannelId)
                return;

            // Enqueue to background processor (non-blocking)
            _temperatureProcessor?.Enqueue(e.ChannelId, e.DataPoints);
        }

        /// <summary>
        /// DUAL WEBSOCKET: Handle data stream connection lost.
        /// </summary>
        private void OnDataStreamConnectionLost(object? sender, EventArgs e)
        {
            _logger.Log("[DUAL WS] Data stream connection lost - will fall back to control WebSocket");
            Console.WriteLine("[DUAL WS] Data stream connection lost");

            // Try to reconnect in background
            _ = Task.Run(async () =>
            {
                await Task.Delay(5000); // Wait 5 seconds before reconnecting
                try
                {
                    if (_dataStreamClient != null)
                    {
                        await InitializeDataStreamClientAsync();

                        // Re-subscribe to current channel if we were subscribed
                        if (_subscribedChannelId > 0 && _dataStreamClient != null)
                        {
                            await _dataStreamClient.SubscribeToChannelAsync(_subscribedChannelId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DUAL WS] Reconnect failed: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// FM-05: Dual-Port Health Watchdog.
        /// Monitors asymmetric port desynchronization between Control WebSocket (Port 6164)
        /// and Data Stream WebSocket (Port 6165).
        /// If streaming is active and Control port is healthy but Data port stops receiving frames
        /// for > 15 seconds, the watchdog triggers an automatic stream resynchronization.
        /// </summary>
        private void StartDualPortHealthWatchdog()
        {
            lock (_dualPortWatchdogLock)
            {
                if (_dualPortWatchdogCts != null && !_dualPortWatchdogCts.IsCancellationRequested)
                    return;

                _dualPortWatchdogCts = new CancellationTokenSource();
            }

            var token = _dualPortWatchdogCts.Token;
            _ = Task.Run(async () =>
            {
                Console.WriteLine("[DUAL PORT WATCHDOG] Started monitoring Ports 6164/6165 health (FM-05)");

                // Initial warm-up delay before active checks
                try
                {
                    await Task.Delay(10000, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(5000, token).ConfigureAwait(false);

                        // Check only when Control Port (6164) is connected and monitoring/plotting is active
                        if (_wsClient != null && _wsClient.IsConnected && (isMonitoring || isPlottingEnabled) && _subscribedChannelId > 0)
                        {
                            // Avoid flapping: enforce minimum 15s cooldown between resync attempts
                            if ((DateTime.UtcNow - _lastWatchdogResyncUtc).TotalSeconds < 15)
                                continue;

                            bool streamStalled = false;
                            string stallReason = "";

                            if (_dataStreamClient == null || !_dataStreamClient.IsConnected)
                            {
                                streamStalled = true;
                                stallReason = "Data Stream client is disconnected on Port 6165";
                            }
                            else if (_dataStreamClient.LastDataReceivedUtc != DateTime.MinValue)
                            {
                                var timeSinceLastData = DateTime.UtcNow - _dataStreamClient.LastDataReceivedUtc;
                                if (timeSinceLastData > TimeSpan.FromSeconds(15))
                                {
                                    streamStalled = true;
                                    stallReason = $"No data received on Port 6165 for {timeSinceLastData.TotalSeconds:F0}s";
                                }
                            }

                            if (streamStalled)
                            {
                                _lastWatchdogResyncUtc = DateTime.UtcNow;
                                Console.WriteLine($"[DUAL PORT WATCHDOG] ⚠️ Asymmetric port desync detected: {stallReason} while Control Port 6164 is healthy (FM-05). Resynchronizing stream...");
                                _logger.Log($"[DUAL PORT WATCHDOG] Asymmetric port desync detected: {stallReason}. Resynchronizing...");

                                try
                                {
                                    if (_dataStreamClient == null || !_dataStreamClient.IsConnected)
                                    {
                                        await InitializeDataStreamClientAsync();
                                    }
                                    else
                                    {
                                        await _dataStreamClient.ReconnectAsync();
                                    }

                                    if (_subscribedChannelId > 0 && _dataStreamClient != null && _dataStreamClient.IsConnected)
                                    {
                                        await _dataStreamClient.SubscribeToChannelAsync(_subscribedChannelId);
                                        Console.WriteLine($"[DUAL PORT WATCHDOG] ✅ Resubscribed to Channel {_subscribedChannelId} after stream resync.");
                                    }
                                }
                                catch (Exception resyncEx)
                                {
                                    Console.WriteLine($"[DUAL PORT WATCHDOG] ❌ Stream resynchronization attempt failed: {resyncEx.Message}");
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[DUAL PORT WATCHDOG] Watchdog error: {ex.Message}");
                    }
                }
                Console.WriteLine("[DUAL PORT WATCHDOG] Watchdog stopped.");
            }, token);
        }

        private void StopDualPortHealthWatchdog()
        {
            lock (_dualPortWatchdogLock)
            {
                try
                {
                    _dualPortWatchdogCts?.Cancel();
                    _dualPortWatchdogCts?.Dispose();
                    _dualPortWatchdogCts = null;
                }
                catch { }
            }
        }

        /// <summary>
        /// Fired by CustomWebSocketClient when the main control WebSocket drops unexpectedly.
        /// Shows a popup immediately with Retry and Close options.
        /// Retry  → attempts to reconnect and re-authenticate with FRMC.
        /// Close  → exits the DTSCM application.
        /// </summary>
        private void OnMainWebSocketConnectionLost(object? sender, EventArgs e)
        {
            if (_isReconnecting)
                return;

            Console.WriteLine("[WS] Main control WebSocket connection lost.");
            _logger.Log("[WS] Connection to FRMC lost.");

            if (!this.IsHandleCreated || this.IsDisposed)
                return;

            this.BeginInvoke(() =>
            {
                UpdateStatus("⚠ Connection to FRMC lost.");

                // Close any stale popup first
                _connectingPopup?.SafeClose();
                _connectingPopup = new ConnectingPopup();

                // Retry button → keep retrying in a loop until connected.
                // User does not need to press Retry again; popup stays on "Connecting..."
                _connectingPopup.RetryRequested += async (_, _) =>
                {
                    if (_isReconnecting) return;
                    _isReconnecting = true;

                    Console.WriteLine("[WS] User requested reconnect — retrying until connected.");
                    _logger.Log("[WS] Retrying connection to FRMC...");

                    bool reconnected = false;

                    while (!reconnected && _connectingPopup != null && !_connectingPopup.IsDisposed)
                    {
                        try
                        {
                            await _wsClient.ConnectAsync(Program.FrmcHost, timeoutSeconds: 5);

                            if (_wsClient.IsConnected)
                            {
                                var loginResp = await _wsClient.LoginAsync(_username, _password);
                                if (loginResp != null && loginResp.Success)
                                {
                                    reconnected = true;
                                    Console.WriteLine($"[WS] Re-authenticated as {loginResp.Username}.");
                                    _logger.Log("[WS] Reconnected and re-authenticated with FRMC.");

                                    if (_dataStreamClient == null)
                                        await InitializeDataStreamClientAsync();

                                    StartDualPortHealthWatchdog();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[WS] Reconnect attempt failed: {ex.Message}");
                        }

                        if (!reconnected)
                            await Task.Delay(3000); // wait 3s before next attempt
                    }

                    _isReconnecting = false;

                    if (reconnected && this.IsHandleCreated && !this.IsDisposed)
                    {
                        this.BeginInvoke(() =>
                        {
                            _connectingPopup?.SafeClose();
                            _connectingPopup = null;
                            UpdateStatus("✅ Reconnected to FRMC.");
                        });
                    }
                };

                _connectingPopup.Show(this);
            });
        }

        #region Command Processor Event Handlers

        /// <summary>
        /// Handle processed health status from command processor.
        /// Called on background thread - must marshal to UI.
        /// </summary>
        private void OnProcessedHealthStatus(JsonElement payload)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => ProcessorHandleHealthStatus(payload));
            }
            else
            {
                ProcessorHandleHealthStatus(payload);
            }
        }

        /// <summary>
        /// Handle processed zone state update from command processor.
        /// Called on background thread - must marshal to UI.
        /// </summary>
        private void OnProcessedZoneStateUpdate(JsonElement payload)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => ProcessorHandleZoneStateUpdate(payload));
            }
            else
            {
                ProcessorHandleZoneStateUpdate(payload);
            }
        }

        /// <summary>
        /// Handle processed alarm triggered from command processor.
        /// </summary>
        private void OnProcessedAlarmTriggered(JsonElement payload)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => ProcessorHandleAlarmTriggered(payload));
            }
            else
            {
                ProcessorHandleAlarmTriggered(payload);
            }
        }

        /// <summary>
        /// Handle processed alarm cleared from command processor.
        /// </summary>
        private void OnProcessedAlarmCleared(JsonElement payload)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => ProcessorHandleAlarmCleared(payload));
            }
            else
            {
                ProcessorHandleAlarmCleared(payload);
            }
        }

        /// <summary>
        /// Handle processed alarm auto-cleared from command processor.
        /// </summary>
        private void OnProcessedAlarmAutoCleared(JsonElement payload)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => ProcessorHandleAlarmAutoCleared(payload));
            }
            else
            {
                ProcessorHandleAlarmAutoCleared(payload);
            }
        }

        /// <summary>
        /// Handle processed active alarm snapshot from command processor.
        /// </summary>
        private void OnProcessedActiveAlarmSnapshot(JsonElement payload)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => ProcessorHandleActiveAlarmSnapshot(payload));
            }
            else
            {
                ProcessorHandleActiveAlarmSnapshot(payload);
            }
        }

        /// <summary>
        /// Handle all alarms cleared message from FRMC.
        /// Called when alarm reset button is pressed and FRMC clears all alarms.
        /// </summary>
        private void OnProcessedAllAlarmsCleared(JsonElement payload)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => ProcessorHandleAllAlarmsCleared(payload));
            }
            else
            {
                ProcessorHandleAllAlarmsCleared(payload);
            }
        }

        /// <summary>
        /// Handle processed channel config updated from command processor.
        /// </summary>
        private void OnProcessedChannelConfigUpdated(JsonElement payload)
        {
            if (!isMonitoring) return;

            if (InvokeRequired)
            {
                BeginInvoke(() => ProcessorHandleChannelConfigUpdated(payload));
            }
            else
            {
                ProcessorHandleChannelConfigUpdated(payload);
            }
        }

        /// <summary>
        /// Handle processed relay state update from command processor.
        /// Called on background thread - must marshal to UI.
        /// </summary>
        private void OnProcessedRelayStateUpdate(JsonElement payload)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => ProcessorHandleRelayStateUpdate(payload));
            }
            else
            {
                ProcessorHandleRelayStateUpdate(payload);
            }
        }

        // Processor-specific handlers that delegate to existing methods
        private void ProcessorHandleHealthStatus(JsonElement payload)
        {
            // Health status is processed and UI is updated via existing mechanisms
            // The processor offloads JSON parsing from the network thread
            // Actual LED updates happen through the existing health status handling
        }

        private void ProcessorHandleZoneStateUpdate(JsonElement payload)
        {
            // Process zone state updates - update cache
            try
            {
                if (payload.TryGetProperty("Zones", out var zones) && zones.ValueKind == JsonValueKind.Array)
                {
                    foreach (var zone in zones.EnumerateArray())
                    {
                        int channelId = zone.TryGetProperty("ChannelId", out var ch) ? ch.GetInt32() : -1;
                        int zoneId = zone.TryGetProperty("ZoneId", out var z) ? z.GetInt32() : -1;

                        if (channelId > 0 && zoneId > 0)
                        {
                            var state = System.Text.Json.JsonSerializer.Deserialize<ZoneStateInfo>(zone.GetRawText());
                            if (state != null)
                            {
                                _zoneStateCache[(channelId, zoneId)] = state;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PROCESSOR] Error handling zone state: {ex.Message}");
            }
        }

        private void ProcessorHandleAlarmTriggered(JsonElement payload)
        {
            try
            {
                int channelId = payload.TryGetProperty("ChannelId", out var ch) ? ch.GetInt32() : -1;
                int zoneId = payload.TryGetProperty("ZoneId", out var z) ? z.GetInt32() : -1;

                if (channelId > 0)
                {
                    // Convert from 1-based (FRMC) to 0-based (DTSCM internal tracking)
                    int frmcChannelId = channelId - 1;
                    int frmcZoneId = zoneId > 0 ? zoneId - 1 : -1;

                    // Update all tracking sets for proper LED sync
                    _channelsWithAlarm.Add(frmcChannelId);
                    _channelsWithAnyAlarm.Add(frmcChannelId);

                    if (frmcZoneId >= 0)
                    {
                        _zonesWithAlarm.Add((frmcChannelId, frmcZoneId));
                    }

                    // Update LEDs
                    UpdateAlarmLed();
                    UpdateSafetyActiveAlarmLed();

                    // ✅ NEW LOGIC STARTS HERE
                    int currentAlarmCount = _channelsWithAnyAlarm.Count;

                    // Open form only if:
                    // 1. It was NOT closed manually
                    // OR
                    // 2. A NEW alarm has come
                    if (!ActiveAlarmClosedByUser || currentAlarmCount > _previousAlarmCount)
                    {
                        ShowActiveAlarmForm();
                        ActiveAlarmClosedByUser = false; // reset flag when opening
                    }

                    // Always refresh alarms
                    RefreshActiveAlarms();

                    // Update previous count
                    _previousAlarmCount = currentAlarmCount;
                    // ✅ NEW LOGIC ENDS HERE

                    Console.WriteLine($"[PROCESSOR] Alarm triggered: CH{channelId} (frmc:{frmcChannelId}), Zone{zoneId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PROCESSOR] Error handling alarm triggered: {ex.Message}");
            }
        }
        private void ProcessorHandleAlarmCleared(JsonElement payload)
        {
            try
            {
                int channelId = payload.TryGetProperty("ChannelId", out var ch) ? ch.GetInt32() : -1;
                int zoneId = payload.TryGetProperty("ZoneId", out var z) ? z.GetInt32() : -1;

                if (channelId > 0 && zoneId > 0)
                {
                    // Convert from 1-based (FRMC) to 0-based (DTSCM internal tracking)
                    int frmcChannelId = channelId - 1;
                    int frmcZoneId = zoneId - 1;

                    _zonesWithAlarm.Remove((frmcChannelId, frmcZoneId));

                    // Check if any other zones in this channel have alarms
                    bool channelHasAlarms = false;
                    foreach (var alarm in _zonesWithAlarm)
                    {
                        if (alarm.channelId == frmcChannelId)
                        {
                            channelHasAlarms = true;
                            break;
                        }
                    }

                    if (!channelHasAlarms)
                    {
                        _channelsWithAlarm.Remove(frmcChannelId);
                        _channelsWithAnyAlarm.Remove(frmcChannelId);
                    }

                    // Update both LEDs - channel-specific and system-wide
                    UpdateAlarmLed();
                    UpdateSafetyActiveAlarmLed();

                    Console.WriteLine($"[PROCESSOR] Alarm cleared: CH{channelId} (frmc:{frmcChannelId}), Zone{zoneId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PROCESSOR] Error handling alarm cleared: {ex.Message}");
            }
        }

        private void ProcessorHandleAlarmAutoCleared(JsonElement payload)
        {
            // Same as alarm cleared
            ProcessorHandleAlarmCleared(payload);
        }

        private void ProcessorHandleActiveAlarmSnapshot(JsonElement payload)
        {
            try
            {
                // Extract alarms array from snapshot
                if (payload.TryGetProperty("Alarms", out var alarmsArray) && alarmsArray.ValueKind == JsonValueKind.Array)
                {
                    int currentAlarmCount = alarmsArray.GetArrayLength();
                    bool hasAlarms = currentAlarmCount > 0;

                    // Open form ONLY if:
                    // 1. It was NOT closed manually by the user
                    // OR
                    // 2. A NEW alarm has arrived (count increased since last update)
                    if (hasAlarms)
                    {
                        if (!ActiveAlarmClosedByUser || currentAlarmCount > _previousAlarmCount)
                        {
                            ShowActiveAlarmForm();
                        }
                    }

                    // Update previous count for future comparisons
                    _previousAlarmCount = currentAlarmCount;

                    if (ActiveAlarm.Instance != null)
                    {
                        ActiveAlarm.Instance.UpdateAlarmList(payload);
                    }
                    else
                    {
                        ProcessActiveAlarmsForLed(alarmsArray);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PROCESSOR] Error handling alarm snapshot: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle all alarms cleared - clear the ActiveAlarm form grid and update LED.
        /// Does NOT close the form - just clears and refreshes.
        /// </summary>
        private void ProcessorHandleAllAlarmsCleared(JsonElement payload)
        {
            try
            {
                Console.WriteLine("[PROCESSOR] All alarms cleared by FRMC");

                // Clear the ActiveAlarm form grid if it's open (don't close the form)
                if (ActiveAlarm.Instance != null)
                {
                    ActiveAlarm.Instance.ClearAlarms();
                    // Request fresh alarm data to repopulate (in case some alarms still exist)
                    RequestActiveAlarms();
                }

                // Update the alarm LED to green (no alarms)
                OnActiveChannelsChanged(new HashSet<int>());
                OnActiveFiberBreakChannelsChanged(new HashSet<int>());
                OnAllActiveChannelsChanged(new HashSet<int>());
                OnActiveZonesChanged(new HashSet<(int, int)>());

                // Update status
                int clearedCount = payload.TryGetProperty("ClearedCount", out var cc) ? cc.GetInt32() : 0;
                UpdateStatus($"All alarms cleared ({clearedCount} alarms)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PROCESSOR] Error handling all alarms cleared: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle health fault detected event from command processor.
        /// </summary>
        private void OnProcessedHealthFaultDetected(JsonElement payload)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => ProcessorHandleHealthFaultDetected(payload));
            }
            else
            {
                ProcessorHandleHealthFaultDetected(payload);
            }
        }

        /// <summary>
        /// Handle health fault cleared event from command processor.
        /// </summary>
        private void OnProcessedHealthFaultCleared(JsonElement payload)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => ProcessorHandleHealthFaultCleared(payload));
            }
            else
            {
                ProcessorHandleHealthFaultCleared(payload);
            }
        }

        /// <summary>
        /// Handle health fault detected - add fault to local cache and update LED.
        /// </summary>
        private void ProcessorHandleHealthFaultDetected(JsonElement payload)
        {
            try
            {
                string faultTypeStr = payload.TryGetProperty("FaultType", out var ft) ? ft.GetString() ?? "" : "";
                string description = payload.TryGetProperty("Description", out var desc) ? desc.GetString() ?? "" : "";
                int faultId = payload.TryGetProperty("FaultId", out var fid) ? fid.GetInt32() : 0;
                double currentValue = payload.TryGetProperty("CurrentValue", out var cv) ? cv.GetDouble() : 0;
                double thresholdValue = payload.TryGetProperty("ThresholdValue", out var tv) ? tv.GetDouble() : 0;

                // Convert string fault type to numeric (matches SystemHealthFaultType enum)
                int faultType = ConvertFaultTypeStringToInt(faultTypeStr);

                Console.WriteLine($"[PROCESSOR] Health fault DETECTED: {faultTypeStr} (type={faultType}) - {description}");

                // Check if fault already exists in cache (by type)
                var existingFault = _activeHealthFaults.FirstOrDefault(f => f.FaultType == faultType);
                if (existingFault != null)
                {
                    // Update existing fault
                    existingFault.CurrentValue = currentValue;
                    existingFault.Description = description;
                }
                else
                {
                    // Add new fault to cache
                    _activeHealthFaults.Add(new SystemHealthFaultInfo
                    {
                        FaultId = faultId,
                        FaultType = faultType,
                        Description = description,
                        CurrentValue = currentValue,
                        ThresholdValue = thresholdValue,
                        FirstDetectedAt = DateTime.UtcNow,
                        IsActive = true
                    });
                }

                // Update System Health LED to red (only if state changed)
                if (!_lastSystemHealthFault)
                {
                    ledSystem.BackColor = System.Drawing.Color.Red;
                    ledSystem.Invalidate();
                    _lastSystemHealthFault = true;
                }
                UpdateSystemHealthTooltip();

                // Update status
                UpdateStatus($"Health Fault: {faultTypeStr} - {description}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PROCESSOR] Error handling health fault detected: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle health fault cleared - remove fault from local cache and update LED.
        /// </summary>
        private void ProcessorHandleHealthFaultCleared(JsonElement payload)
        {
            try
            {
                string faultTypeStr = payload.TryGetProperty("FaultType", out var ft) ? ft.GetString() ?? "" : "";
                int faultId = payload.TryGetProperty("FaultId", out var fid) ? fid.GetInt32() : 0;

                // Convert string fault type to numeric
                int faultType = ConvertFaultTypeStringToInt(faultTypeStr);

                Console.WriteLine($"[PROCESSOR] Health fault CLEARED: {faultTypeStr} (type={faultType})");

                // Remove fault from cache (by type or id)
                _activeHealthFaults.RemoveAll(f => f.FaultType == faultType || f.FaultId == faultId);

                // Update System Health LED based on remaining faults (only if state changed)
                bool hasActiveFaults = _activeHealthFaults.Count > 0;
                if (hasActiveFaults != _lastSystemHealthFault)
                {
                    ledSystem.BackColor = hasActiveFaults
                        ? System.Drawing.Color.Red
                        : System.Drawing.Color.LimeGreen;
                    ledSystem.Invalidate();
                    _lastSystemHealthFault = hasActiveFaults;
                }
                UpdateSystemHealthTooltip();

                // Update status
                UpdateStatus($"Health Fault Cleared: {faultTypeStr}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PROCESSOR] Error handling health fault cleared: {ex.Message}");
            }
        }

        /// <summary>
        /// Converts a fault type string to its numeric equivalent.
        /// Matches SystemHealthFaultType enum values.
        /// </summary>
        private static int ConvertFaultTypeStringToInt(string faultTypeStr)
        {
            return faultTypeStr switch
            {
                "DtsCommunicationLost" => 1,
                "DtsTemperatureHigh" => 2,
                "DtsTemperatureLow" => 3,
                "CpuLoadHigh" => 4,
                "MemoryLoadHigh" => 5,
                "DiskSpaceLow" => 6,
                "LaserOff" => 7,
                "SystemFault" => 99,
                _ => 0
            };
        }

        private void ProcessorHandleChannelConfigUpdated(JsonElement payload)
        {
            try
            {
                int channelId = payload.TryGetProperty("ChannelId", out var ch) ? ch.GetInt32() : -1;
                if (channelId > 0)
                {
                    Console.WriteLine($"[PROCESSOR] Channel {channelId} config updated");
                    // Configuration reload happens through existing mechanisms
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PROCESSOR] Error handling channel config update: {ex.Message}");
            }
        }

        private void ProcessorHandleRelayStateUpdate(JsonElement payload)
        {
            try
            {
                // Log relay state change for debugging
                int relayId = payload.TryGetProperty("RelayId", out var rid) ? rid.GetInt32() : -1;
                bool isOn = payload.TryGetProperty("IsOn", out var onProp) && onProp.GetBoolean();
                string source = payload.TryGetProperty("ControlSource", out var srcProp) ? srcProp.GetString() ?? "Unknown" : "Unknown";

                Console.WriteLine($"[PROCESSOR] Relay {relayId} state changed: {(isOn ? "ON" : "OFF")} (Source: {source})");

                // Update status bar with relay change notification
                UpdateStatus($"Relay {relayId} turned {(isOn ? "ON" : "OFF")} ({source})");

                // Update FormRelayMapping if it's open (real-time update)
                FormRelayMapping.Instance?.UpdateRelayState(relayId, isOn, source);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PROCESSOR] Error handling relay state update: {ex.Message}");
            }
        }

        #endregion

        /// <summary>
        /// MULTI-THREADED: Attempts to route high-frequency messages to the command processor.
        /// Returns true if the message was handled (enqueued to processor).
        /// This keeps the network thread fast by avoiding JSON parsing for known message types.
        /// </summary>
        private bool TryRouteToProcessor(string message)
        {
            // Performance: only parse if we really need to, or use string checks for speed
            bool isRealTimeUpdate = message.Contains("\"health_status\"") ||
                                    message.Contains("\"zone_state_update\"") ||
                                    message.Contains("\"zone_state_broadcast\"") ||
                                    message.Contains("\"alarm_triggered\"") ||
                                    message.Contains("\"alarm_cleared\"") ||
                                    message.Contains("\"alarm_auto_cleared\"") ||
                                    message.Contains("\"active_alarm_snapshot\"") ||
                                    message.Contains("\"relay_state_update\"") ||
                                    message.Contains("\"relay_state_changed\"") ||
                                    message.Contains("\"channel_zones_batch_update\"") ||
                                    message.Contains("\"channel_state_update\"") ||
                                    message.Contains("\"channel_config_updated\"");
            //message.Contains("\"fiber_break_detected\"") ||
            //message.Contains("\"fiber_break_cleared\"");


            if (isRealTimeUpdate)
            {
                // Enqueue to processor (non-blocking)
                _commandProcessor?.Enqueue(message);
                return true;
            }

            // Let other messages be handled by the existing synchronous handler
            return false;
        }

        /// <summary>
        /// Subscribe to receive live temperature data for a specific channel from FRMC.
        /// Only sends subscription if not already subscribed to this channel.
        /// DUAL WEBSOCKET: Subscribes on both control and data stream servers.
        /// </summary>
        private async Task SubscribeToChannelAsync(int channelId)
        {
            Console.WriteLine($"[SUBSCRIBE] SubscribeToChannelAsync called with channelId={channelId}, _subscribedChannelId={_subscribedChannelId}");

            if (_subscribedChannelId == channelId)
            {
                _logger.Log($"Already subscribed to channel {channelId}, skipping subscription");
                Console.WriteLine($"[SUBSCRIBE] Already subscribed to channel {channelId}, skipping");
                return;
            }

            try
            {
                // DUAL WEBSOCKET: Subscribe on data stream server (preferred for temperature data)
                if (_dataStreamClient != null && _dataStreamClient.IsConnected)
                {
                    Console.WriteLine($"[SUBSCRIBE] Sending subscription to data stream for channel {channelId}");
                    await _dataStreamClient.SubscribeToChannelAsync(channelId);
                    _logger.Log($"[DATA STREAM] Subscribed to channel {channelId}");
                    Console.WriteLine($"[SUBSCRIBE] Data stream subscription sent for channel {channelId}");
                }
                else
                {
                    Console.WriteLine($"[SUBSCRIBE] Data stream client not connected, skipping data stream subscription");
                    // Don't set _subscribedChannelId - we'll retry when data stream connects
                    return;
                }

                // Also subscribe on control WebSocket for backward compatibility
                var subscribeMessage = new
                {
                    MessageType = "subscribe_channel",
                    Payload = new { channelId }
                };

                string json = System.Text.Json.JsonSerializer.Serialize(subscribeMessage);
                await _wsClient.SendAsync(json);

                _subscribedChannelId = channelId;
                _logger.Log($"Subscribed to live data for channel {channelId}");
                Console.WriteLine($"[SUBSCRIBE] _subscribedChannelId set to {channelId}");
            }
            catch (Exception ex)
            {
                _logger.Log($"Error subscribing to channel {channelId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Unsubscribe from receiving live temperature data for a specific channel.
        /// DUAL WEBSOCKET: Unsubscribes from both control and data stream servers.
        /// </summary>
        private async Task UnsubscribeFromChannelAsync(int channelId)
        {
            if (channelId < 0 || _subscribedChannelId != channelId)
                return;

            try
            {
                // DUAL WEBSOCKET: Unsubscribe from data stream server
                if (_dataStreamClient != null && _dataStreamClient.IsConnected)
                {
                    await _dataStreamClient.UnsubscribeFromChannelAsync(channelId);
                    _logger.Log($"[DATA STREAM] Unsubscribed from channel {channelId}");
                }

                // Also unsubscribe from control WebSocket
                var unsubscribeMessage = new
                {
                    MessageType = "unsubscribe_channel",
                    Payload = new { channelId }
                };

                string json = System.Text.Json.JsonSerializer.Serialize(unsubscribeMessage);
                await _wsClient.SendAsync(json);

                _subscribedChannelId = -1;
                _logger.Log($"Unsubscribed from live data for channel {channelId}");
            }
            catch (Exception ex)
            {
                _logger.Log($"Error unsubscribing from channel {channelId}: {ex.Message}");
            }
        }

        private void FormsPlot1_MouseMove(object? sender, MouseEventArgs e)
        {
            if (lblCursorValue == null)
                return;

            // FM-07: Read from thread-safe double-buffered immutable snapshot
            var snapshot = _traceCache.FrontBuffer;
            if (snapshot.Count == 0)
                return;

            // Convert mouse pixel -> plot coordinate
            Coordinates mouseCoords =
                formsPlot1.Plot.GetCoordinates(
                    new Pixel(e.X, e.Y));

            double mouseX = mouseCoords.X;
            double mouseY = mouseCoords.Y;

            // Keep inside graph range
            if (mouseX < snapshot.MinDistance)
                mouseX = snapshot.MinDistance;

            if (mouseX > snapshot.MaxDistance)
                mouseX = snapshot.MaxDistance;

            // Fast, race-condition-free nearest point lookup
            var (actualX, actualTemp, _) = snapshot.FindNearest(mouseX);

            // ✅ Code 1 style label text
            lblCursorValue.Text =
                $"X (Distance): {actualX:F2} m\n" +
                $"Y (Temperature): {actualTemp:F2} °C";

            lblCursorValue.Visible = true;

            // Move persistent vertical line
            if (markerLine != null)
            {
                markerLine.IsVisible = true;

                markerLine.X = actualX;

                // Store latest cursor position
                _markerLineX = actualX;

                // Force redraw
                formsPlot1.Refresh();
            }
        }
        private void FormsPlot1_MouseLeave(object? sender, EventArgs e)
        {
            if (lblCursorValue != null)
                lblCursorValue.Visible = false;
        }

        /// <summary>
        /// Initializes the axis fixing checkboxes on the graph panel.
        /// Default: X-axis fixed to 0-ChannelLength, Y-axis fixed to 0-80°C.
        /// Auto-unfixes when user pans/scrolls. Right-click to reset to default.
        /// </summary>
        private void InitializeAxisControls()
        {
            // Axis fixing is handled automatically in the background
            // _isXAxisFixed and _isYAxisFixed track the state
            // No visible checkboxes - axes auto-unfix on pan/scroll and reset via context menu

            // Initialize axis text boxes with default values
            txtXMin.Text = "0";
            txtXMax.Text = "1000";
            txtYMin.Text = DEFAULT_Y_MIN.ToString("F0");
            txtYMax.Text = DEFAULT_Y_MAX.ToString("F0");

            // Create right-click context menu for resetting view
            _graphContextMenu = new ContextMenuStrip();
            var resetMenuItem = new ToolStripMenuItem("Reset to Default View");
            resetMenuItem.Click += ResetGraphView_Click;
            _graphContextMenu.Items.Add(resetMenuItem);

            var resetXMenuItem = new ToolStripMenuItem("Reset X-Axis Only");
            resetXMenuItem.Click += ResetXAxis_Click;
            _graphContextMenu.Items.Add(resetXMenuItem);

            var resetYMenuItem = new ToolStripMenuItem("Reset Y-Axis Only");
            resetYMenuItem.Click += ResetYAxis_Click;
            _graphContextMenu.Items.Add(resetYMenuItem);

            formsPlot1.ContextMenuStrip = _graphContextMenu;

            // Hook mouse events for auto-unfixing when user pans/scrolls
            formsPlot1.MouseDown += FormsPlot1_MouseDown_AxisUnfix;
            formsPlot1.MouseUp += FormsPlot1_MouseUp_AxisUnfix;
            formsPlot1.MouseWheel += FormsPlot1_MouseWheel_AxisUnfix;
        }

        /// <summary>
        /// Detects when user starts dragging (panning) the graph.
        /// </summary>
        private void FormsPlot1_MouseDown_AxisUnfix(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _isDragging = true;
                _lastMousePosition = e.Location;
            }
        }

        /// <summary>
        /// Detects when user finishes dragging. If mouse moved significantly, unfix axes.
        /// </summary>
        private void FormsPlot1_MouseUp_AxisUnfix(object? sender, MouseEventArgs e)
        {
            if (_isDragging && e.Button == MouseButtons.Left)
            {
                _isDragging = false;

                // Calculate how much the mouse moved
                int deltaX = Math.Abs(e.Location.X - _lastMousePosition.X);
                int deltaY = Math.Abs(e.Location.Y - _lastMousePosition.Y);

                // Threshold for considering it a pan (not just a click)
                const int panThreshold = 10;

                // Auto-unfix X-axis if user panned horizontally
                if (deltaX > panThreshold && _isXAxisFixed)
                {
                    _isXAxisFixed = false;
                }

                // Auto-unfix Y-axis if user panned vertically
                if (deltaY > panThreshold && _isYAxisFixed)
                {
                    _isYAxisFixed = false;
                }
            }
        }

        /// <summary>
        /// Detects when user scrolls (zooms) the graph. Auto-unfixes both axes.
        /// </summary>
        private void FormsPlot1_MouseWheel_AxisUnfix(object? sender, MouseEventArgs e)
        {
            // Mouse wheel typically zooms both axes, so unfix both
            if (_isXAxisFixed)
            {
                _isXAxisFixed = false;
            }

            if (_isYAxisFixed)
            {
                _isYAxisFixed = false;
            }
        }

        /// <summary>
        /// Right-click menu: Reset both axes to default view.
        /// </summary>
        private void ResetGraphView_Click(object? sender, EventArgs e)
        {
            ResetXAxisToDefault();
            ResetYAxisToDefault();
            formsPlot1.Refresh();
        }

        /// <summary>
        /// Right-click menu: Reset X-axis only to default view.
        /// </summary>
        private void ResetXAxis_Click(object? sender, EventArgs e)
        {
            ResetXAxisToDefault();
            formsPlot1.Refresh();
        }

        /// <summary>
        /// Right-click menu: Reset Y-axis only to default view.
        /// </summary>
        private void ResetYAxis_Click(object? sender, EventArgs e)
        {
            ResetYAxisToDefault();
            formsPlot1.Refresh();
        }

        /// <summary>
        /// Resets X-axis to default (0 to channel length) and re-enables fixed mode.
        /// </summary>

        private void ResetXAxisToDefault()
        {
            double channelLength = GetChannelLengthForSelectedChannel();
            double correctionLength = GetCorrectionLengthForSelectedChannel();

            bool isZoneSelected = _selectedZoneId > 0
                                  && _selectedZoneStartPosition.HasValue
                                  && _selectedZoneEndPosition.HasValue;

            if (isZoneSelected)
            {
                // ✅ ZONE GRAPH → APPLY CORRECTION TO BOUNDARIES
                double zoneStart = _selectedZoneStartPosition.Value;
                double zoneEnd = _selectedZoneEndPosition.Value;

                formsPlot1.Plot.Axes.SetLimitsX(zoneStart, zoneEnd);

                txtXMin.Text = zoneStart.ToString("F0");
                txtXMax.Text = zoneEnd.ToString("F0");
            }
            else
            {
                // ✅ CHANNEL GRAPH → APPLY CORRECTION
                if (channelLength > 0)
                {
                    // formsPlot1.Plot.Axes.SetLimitsX(-correctionLength, channelLength);
                    formsPlot1.Plot.Axes.SetLimitsX(0, channelLength);
                }
                else if (distances.Count > 0)
                {
                    formsPlot1.Plot.Axes.SetLimitsX(distances.Min(), distances.Max());
                }

                txtXMin.Text = (-correctionLength).ToString("F0");
                txtXMax.Text = channelLength.ToString("F0");
            }

            _isXAxisFixed = true;
        }

        /// <summary>
        /// Resets Y-axis to default (0 to 80°C) and re-enables fixed mode.
        /// </summary>
        private void ResetYAxisToDefault()
        {
            formsPlot1.Plot.Axes.SetLimitsY(DEFAULT_Y_MIN, DEFAULT_Y_MAX);

            // Update text boxes
            txtYMin.Text = DEFAULT_Y_MIN.ToString("F0");
            txtYMax.Text = DEFAULT_Y_MAX.ToString("F0");

            // Re-enable fixed Y-axis
            _isYAxisFixed = true;
        }

        /// <summary>
        /// Apply button click - sets axis limits from text boxes.
        /// User-applied limits are preserved per channel across data updates and channel switches.
        /// </summary>
        private void btnApplyAxis_Click(object? sender, EventArgs e)
        {
            try
            {
                if (double.TryParse(txtXMin.Text, out double xMin) &&
                    double.TryParse(txtXMax.Text, out double xMax) &&
                    double.TryParse(txtYMin.Text, out double yMin) &&
                    double.TryParse(txtYMax.Text, out double yMax))
                {
                    if (xMin >= xMax || yMin >= yMax)
                    {
                        UpdateStatus("Invalid axis range: min must be less than max");
                        return;
                    }

                    // Store user-applied limits for current channel
                    _userAxisApplied = true;
                    _userXMin = xMin;
                    _userXMax = xMax;
                    _userYMin = yMin;
                    _userYMax = yMax;

                    // Store per-channel axis limits
                    if (_selectedChannelId > 0)
                    {
                        _channelAxisLimits[_selectedChannelId] = (xMin, xMax, yMin, yMax);
                    }

                    // Apply limits
                    formsPlot1.Plot.Axes.SetLimitsX(xMin, xMax);
                    formsPlot1.Plot.Axes.SetLimitsY(yMin, yMax);
                    formsPlot1.Refresh();

                    // Disable auto-axis so user values are preserved
                    _isXAxisFixed = false;
                    _isYAxisFixed = false;

                    UpdateStatus($"Axis limits applied: X[{xMin}-{xMax}], Y[{yMin}-{yMax}]");
                }
                else
                {
                    UpdateStatus("Invalid axis values. Please enter valid numbers.");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error applying axis limits: {ex.Message}");
            }
        }

        /// <summary>
        /// Reset button click - resets axis to default values and clears user-applied limits for current channel.
        /// </summary>
        private void btnResetAxis_Click(object? sender, EventArgs e)
        {
            // Clear user-applied axis limits
            _userAxisApplied = false;

            // Remove saved limits for current channel
            if (_selectedChannelId > 0)
            {
                _channelAxisLimits.Remove(_selectedChannelId);
            }

            ResetXAxisToDefault();
            ResetYAxisToDefault();
            UpdateAxisTextBoxes();
            formsPlot1.Refresh();
            UpdateStatus("Axis limits reset to default");
        }

        /// <summary>
        /// Updates the axis text boxes with current axis limits.
        /// </summary>
        private void UpdateAxisTextBoxes()
        {
            var limits = formsPlot1.Plot.Axes.GetLimits();
            txtXMin.Text = limits.Left.ToString("F0");
            txtXMax.Text = limits.Right.ToString("F0");
            txtYMin.Text = limits.Bottom.ToString("F0");
            txtYMax.Text = limits.Top.ToString("F0");
        }

        /// <summary>
        /// Updates axis text boxes with channel-specific values.
        /// Shows saved user limits if available, otherwise shows defaults.
        /// </summary>
        private void UpdateAxisTextBoxesForChannel()
        {
            // Check if this channel has saved user limits
            if (_selectedChannelId > 0 && _channelAxisLimits.TryGetValue(_selectedChannelId, out var savedLimits))
            {
                txtXMin.Text = savedLimits.xMin.ToString("F0");
                txtXMax.Text = savedLimits.xMax.ToString("F0");
                txtYMin.Text = savedLimits.yMin.ToString("F0");
                txtYMax.Text = savedLimits.yMax.ToString("F0");
            }
            else
            {
                // Use defaults: channel length for X-axis, 0-80 for Y-axis
                double channelLength = GetChannelLengthForSelectedChannel();
                txtXMin.Text = "0";
                txtXMax.Text = channelLength > 0 ? channelLength.ToString("F0") : "1000";
                txtYMin.Text = DEFAULT_Y_MIN.ToString("F0");
                txtYMax.Text = DEFAULT_Y_MAX.ToString("F0");
            }
        }

        /// <summary>
        /// Gets the channel length for the currently selected channel from configuration.
        /// </summary>

        private double GetChannelLengthForSelectedChannel()
        {
            if (_selectedChannelId <= 0) return 0;

            string key = $"Channel {_selectedChannelId}";
            var cfg = _configurationService.GetChannelConfig(key);
            if (cfg == null) return 0;

            return cfg.Length;
        }
        /// <summary>
        /// Timer tick handler for periodic LED updates (30-second interval)
        /// </summary>
        private async void LedUpdateTimer_Tick(object? sender, EventArgs e)
        {
            await UpdateMainLEDsAsync();
        }

        /// <summary>
        /// Timer tick handler for periodic active alarm polling (5-second interval).
        /// Ensures alarm state stays in sync even if real-time alarm_triggered messages are missed.
        /// </summary>
        private void AlarmPollingTimer_Tick(object? sender, EventArgs e)
        {
            Console.WriteLine("[ALARM POLL] Requesting active alarms...");
            RequestActiveAlarms();
        }

        /// <summary>
        /// Updates the main form LEDs (System Health, Alarm, DTS Communication) based on FRMC data
        /// System Health LED is now synced with Relay 2 (uses active health faults)
        /// </summary>
        private async Task UpdateMainLEDsAsync()
        {
            try
            {
                // Get health parameters and active faults from FRMC in parallel
                var healthTask = _ChannelClient.GetHealthParametersAsync();
                var faultsTask = _ChannelClient.GetActiveHealthFaultsAsync();

                await Task.WhenAll(healthTask, faultsTask);

                var health = healthTask.Result;
                var activeFaults = faultsTask.Result;

                // Cache the active faults for tooltip display
                _activeHealthFaults = activeFaults ?? new List<SystemHealthFaultInfo>();

                if (health == null)
                {
                    _healthCheckFailures++;
                    if (_healthCheckFailures < HEALTH_GRAY_THRESHOLD)
                    {
                        Console.WriteLine($"[FRCM] Health check timeout ({_healthCheckFailures}/{HEALTH_GRAY_THRESHOLD}) - keeping last known state");
                        return; // Keep last known LED state
                    }
                    // Only go gray after consecutive failures
                    if (InvokeRequired)
                    {
                        Invoke(new Action(() =>
                        {
                            if (ledSystem != null) { ledSystem.BackColor = System.Drawing.Color.Gray; ledSystem.Invalidate(); }
                            if (ledComm != null) { ledComm.BackColor = System.Drawing.Color.Gray; ledComm.Invalidate(); }
                            UpdateSystemHealthTooltip();
                        }));
                    }
                    else
                    {
                        if (ledSystem != null) { ledSystem.BackColor = System.Drawing.Color.Gray; ledSystem.Invalidate(); }
                        if (ledComm != null) { ledComm.BackColor = System.Drawing.Color.Gray; ledComm.Invalidate(); }
                        UpdateSystemHealthTooltip();
                    }
                    return;
                }

                // Success — reset counter
                _healthCheckFailures = 0;

                // Update LEDs on UI thread
                if (InvokeRequired)
                {
                    Invoke(new Action(() => UpdateLEDsFromHealth(health)));
                }
                else
                {
                    UpdateLEDsFromHealth(health);
                }
            }
            catch (Exception ex)
            {
                _healthCheckFailures++;
                Console.WriteLine($"[FRCM] Health check error ({_healthCheckFailures}/{HEALTH_GRAY_THRESHOLD}): {ex.Message}");
                if (_healthCheckFailures < HEALTH_GRAY_THRESHOLD)
                    return; // Keep last known LED state
                // On error after consecutive failures, set LEDs to gray (unknown state)
                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        if (ledSystem != null) { ledSystem.BackColor = System.Drawing.Color.Gray; ledSystem.Invalidate(); }
                        if (ledComm != null) { ledComm.BackColor = System.Drawing.Color.Gray; ledComm.Invalidate(); }
                        UpdateSystemHealthTooltip();
                    }));
                }
                else
                {
                    if (ledSystem != null) { ledSystem.BackColor = System.Drawing.Color.Gray; ledSystem.Invalidate(); }
                    if (ledComm != null) { ledComm.BackColor = System.Drawing.Color.Gray; ledComm.Invalidate(); }
                    UpdateSystemHealthTooltip();
                }
            }
        }

        /// <summary>
        /// Updates LED colors based on health parameters
        /// MUST be called on UI thread
        /// </summary>
        private void UpdateLEDsFromHealth(HealthParametersInfo health)
        {
            // Detect and log DTS communication status changes
            if (_previousDtsResponsive.HasValue && _previousDtsResponsive.Value != health.DtsResponsive)
            {
                if (health.DtsResponsive)
                {
                    UpdateStatus("🟢 DTS Communication restored");
                }
                else
                {
                    UpdateStatus("🔴 DTS Communication lost - Check DTS connection");
                }
            }
            _previousDtsResponsive = health.DtsResponsive;

            // Detect and log lamp status changes
            if (_previousLampStatus.HasValue && _previousLampStatus.Value != health.LampStatus)
            {
                if (health.LampStatus)
                {
                    UpdateStatus("🟢 Lamp powered ON");
                }
                else
                {
                    UpdateStatus("🔴 Lamp powered OFF - System not operational");
                }
            }
            _previousLampStatus = health.LampStatus;

            // Detect and log temperature warnings
            bool tempWarning = health.ModuleTemperature >= 60;
            if (_previousTempWarning.HasValue && _previousTempWarning.Value != tempWarning)
            {
                if (tempWarning)
                {
                    UpdateStatus($"⚠ Module temperature HIGH: {health.ModuleTemperature:F1}°C - Check cooling system");
                }
                else
                {
                    UpdateStatus($"🟢 Module temperature normal: {health.ModuleTemperature:F1}°C");
                }
            }
            else if (tempWarning && !_previousTempWarning.HasValue)
            {
                // Log warning on first check if temperature is already high
                UpdateStatus($"⚠ Module temperature HIGH: {health.ModuleTemperature:F1}°C - Check cooling system");
            }
            _previousTempWarning = tempWarning;

            // System Health LED - Synced with Relay 2 (based on active health faults)
            // Green = no active faults (Relay 2 OFF), Red = has active faults (Relay 2 ON)
            bool hasActiveFaults = _activeHealthFaults != null && _activeHealthFaults.Count > 0;
            if (hasActiveFaults != _lastSystemHealthFault)
            {
                if (ledSystem != null)
                {
                    ledSystem.IsOn = true;
                    ledSystem.LedColor = hasActiveFaults ? System.Drawing.Color.Red : System.Drawing.Color.LimeGreen;
                }
                _lastSystemHealthFault = hasActiveFaults;
            }

            // DTS Communication LED - Green if responsive, Red if not
            bool dtsResponsive = health.DtsResponsive;
            if (dtsResponsive != _lastDtsCommState)
            {
                if (ledComm != null)
                {
                    ledComm.IsOn = true;
                    ledComm.LedColor = dtsResponsive ? System.Drawing.Color.LimeGreen : System.Drawing.Color.Red;
                }
                _lastDtsCommState = dtsResponsive;
            }

            // Alarm LED is handled separately by UpdateSafetyActiveAlarmLed (no change here)

            // Update tooltip to show current fault details
            UpdateSystemHealthTooltip();
        }

        /// <summary>
        /// Updates the System Health LED tooltip to show active fault details
        /// </summary>
        private void UpdateSystemHealthTooltip()
        {
            if (_systemHealthToolTip == null) return;

            string tooltipText;

            if (_activeHealthFaults == null || _activeHealthFaults.Count == 0)
            {
                tooltipText = "System Health: OK\nNo active faults";
            }
            else
            {
                var faultLines = new List<string>
                {
                    $"System Health: FAULT ({_activeHealthFaults.Count} active)",
                    ""
                };

                foreach (var fault in _activeHealthFaults)
                {
                    string faultInfo = $"• {fault.FaultTypeName}";
                    if (!string.IsNullOrEmpty(fault.Description))
                    {
                        faultInfo += $": {fault.Description}";
                    }
                    if (fault.CurrentValue != 0 || fault.ThresholdValue != 0)
                    {
                        faultInfo += $" (Current: {fault.CurrentValue:F1}, Threshold: {fault.ThresholdValue:F1})";
                    }
                    faultLines.Add(faultInfo);
                }

                tooltipText = string.Join("\n", faultLines);
            }

            _systemHealthToolTip.SetToolTip(ledSystem, tooltipText);
        }

        private void MakeLedRound(Control led)
        {
            if (led.Width == 0 || led.Height == 0)
                return;

            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(0, 0, led.Width, led.Height);
                led.Region = new Region(path);
            }
        }

        private void InitializeLoadingLabel()
        {
            _loadingLabel = new System.Windows.Forms.Label
            {
                Text = "Loading...",
                Font = new System.Drawing.Font("Segoe UI", 16, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.Black,
                BackColor = System.Drawing.Color.FromArgb(240, 240, 240),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(200, 80),
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false
            };

            // Add to Form's Controls (not TabControl) and bring to front
            this.Controls.Add(_loadingLabel);
            _loadingLabel.BringToFront();

            // Center it
            CenterLoadingLabel();
        }

        private void CenterLoadingLabel()
        {
            if (_loadingLabel != null && tabControlDisplay != null)
            {
                // Position relative to the tabControlDisplay's location on the form
                int tabLeft = tabControlDisplay.Left;
                int tabTop = tabControlDisplay.Top;
                int tabWidth = tabControlDisplay.Width;
                int tabHeight = tabControlDisplay.Height;

                _loadingLabel.Location = new Point(
                    tabLeft + (tabWidth - _loadingLabel.Width) / 2,
                    tabTop + (tabHeight - _loadingLabel.Height) / 2
                );
            }
        }

        private void ShowLoading()
        {
            if (_loadingLabel != null)
            {
                CenterLoadingLabel();
                _loadingLabel.Visible = true;
                _loadingLabel.BringToFront();
                Application.DoEvents(); // Force UI update
            }
        }

        private void HideLoading()
        {
            if (_loadingLabel != null)
            {
                _loadingLabel.Visible = false;
            }
        }
        private void Form1_Shown(object? sender, EventArgs e)
        {
            // 🔹 1. Read system resolution
            Rectangle workArea = Screen.FromControl(this).WorkingArea;

            // 🔹 2. Fit form to screen
            this.Location = workArea.Location;
            this.Size = workArea.Size;

            // 🔹 3. Setup role-based menu visibility
            SetupRoleBasedUI();
        }

        private void SetupRoleBasedUI()
        {
            // Admin menu is hidden for all users
            adminToolStripMenuItem.Visible = true;
        }

        private void userManagementToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            // Only allow admins to access user management
            if (!_loginRole.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Access Denied.\n\nOnly administrators can access user management.",
                    "Access Denied",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            try
            {
                using (var userMgmtForm = new UserManagementForm(_wsClient))
                {
                    userMgmtForm.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening User Management:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void systemConfigurationToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            try
            {
                UpdateStatus("🔧 Opening System Configuration...");
                using (var systemConfigForm = new FormSystemConfig(_ChannelClient, _loginRole))
                {
                    var result = systemConfigForm.ShowDialog(this);
                    if (result == DialogResult.OK)
                    {
                        UpdateStatus("✅ System configuration saved successfully");
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"❌ Error opening System Configuration: {ex.Message}");
                MessageBox.Show($"Error opening System Configuration:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void logConfigurationToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            try
            {
                UpdateStatus("📝 Opening Log Configuration...");
                using (var logConfigForm = new FormLogConfiguration(_ChannelClient, _loginRole))
                {
                    var result = logConfigForm.ShowDialog(this);
                    if (result == DialogResult.OK)
                    {
                        UpdateStatus("✅ Log configuration saved successfully");
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"❌ Error opening Log Configuration: {ex.Message}");
                MessageBox.Show($"Error opening Log Configuration:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void systemHealthToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            try
            {
                UpdateStatus("📊 Opening System Health Monitor...");
                using (var systemHealthForm = new FormSystemHealth(_ChannelClient, _commandProcessor, _loginRole))
                {
                    systemHealthForm.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"❌ Error opening System Health: {ex.Message}");
                MessageBox.Show($"Error opening System Health:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void relayMappingToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            try
            {
                UpdateStatus("🔌 Opening Relay Mapping...");
                using (var relayMappingForm = new FormRelayMapping(_ChannelClient, _configurationService))
                {
                    var result = relayMappingForm.ShowDialog(this);
                    if (result == DialogResult.OK)
                    {
                        UpdateStatus("✅ Relay mappings updated successfully");
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"❌ Error opening Relay Mapping: {ex.Message}");
                MessageBox.Show($"Error opening Relay Mapping:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void atpToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            try
            {
                UpdateStatus("📋 Opening ATP Form...");
                using (var atpForm = new AtpForm(_configurationService, _temperatureDataService, _ChannelClient))
                {
                    atpForm.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"❌ Error opening ATP Form: {ex.Message}");
                MessageBox.Show($"Error opening ATP Form:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private async void fRMCCurrentVersionToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            try
            {
                UpdateStatus("📦 Requesting FRMC version...");

                // Send GetFRMCVersion command to FRMC
                var response = await _wsClient.SendCommandAsync("GetFRMCVersion", new { });

                if (response.HasValue && response.Value.TryGetProperty("Payload", out var payload))
                {
                    if (payload.TryGetProperty("Success", out var success) && success.GetBoolean())
                    {
                        string version = payload.GetProperty("Version").GetString() ?? "Unknown";

                        UpdateStatus($"✅ FRMC Version: {version}");
                        MessageBox.Show($"FRMC Software Version: {version}",
                            "FRMC Current Version",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    else
                    {
                        UpdateStatus("❌ Failed to get FRMC version");
                        MessageBox.Show("Failed to retrieve FRMC version.",
                            "Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
                else
                {
                    UpdateStatus("❌ No response from FRMC");
                    MessageBox.Show("No response from FRMC server.",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"❌ Error retrieving FRMC version: {ex.Message}");
                MessageBox.Show($"Error retrieving FRMC version:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void eventListToolStripMenuItem1_Click(object? sender, EventArgs e)
        {
            try
            {
                using (var eventHistoryForm = new EventHistoryForm(_wsClient))
                {
                    eventHistoryForm.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening Event History:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void auditTrailToolStripMenuItem1_Click(object? sender, EventArgs e)
        {
            // Only allow admins to access audit trail
            if (!_loginRole.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Access Denied.\n\nOnly administrators can view the audit trail.",
                    "Admin Access Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            try
            {
                using (var auditTrailForm = new AuditTrailForm(_wsClient))
                {
                    auditTrailForm.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening Audit Trail:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        private void OnWebSocketMessageReceived(object? sender, string message)
        {
            try
            {
                // MULTI-THREADED: Quick peek at message type to route high-frequency messages to processor
                // This keeps the network thread fast and offloads processing to background threads
                if (_commandProcessor != null && TryRouteToProcessor(message))
                {
                    return; // Message was handled by processor
                }

                var json = JsonDocument.Parse(message).RootElement;
                // 🔒 BLOCK real-time updates when not monitoring
                // Only block channel and zone state updates; alarms and health continue
                if (!isMonitoring &&
                    json.TryGetProperty("MessageType", out var blockMt))
                {
                    string type = blockMt.GetString() ?? "";

                    // Block only channel/zone information updates
                    if (type == "zone_state_update" ||
                        type == "zone_state_broadcast" ||
                        type == "channel_zones_batch_update" ||
                        type == "channel_state_update" ||
                        type == "channel_config_updated")
                    {
                        return; // ❌ Ignore channel/zone info updates when not monitoring
                    }
                }

                if (json.TryGetProperty("MessageType", out var mt2) &&
                    mt2.GetString() == "active_alarm_response")
                {
                    var payload = json.GetProperty("Payload");
                    int currentAlarmCount = payload.ValueKind == JsonValueKind.Array ? payload.GetArrayLength() : 0;
                    bool hasAlarms = currentAlarmCount > 0;

                    // Marshal to UI thread: open form if needed, then populate
                    void HandleAlarmResponse()
                    {
                        // Open form ONLY if:
                        // 1. It was NOT closed manually by the user
                        // OR
                        // 2. A NEW alarm has arrived (count increased since last update)
                        if (hasAlarms)
                        {
                            if (!ActiveAlarmClosedByUser || currentAlarmCount > _previousAlarmCount)
                            {
                                ShowActiveAlarmForm();
                            }
                        }

                        // Update previous count for future comparisons
                        _previousAlarmCount = currentAlarmCount;

                        if (ActiveAlarm.Instance != null)
                        {
                            ActiveAlarm.Instance.UpdateAlarmList(payload);
                        }
                        else
                        {
                            ProcessActiveAlarmsForLed(payload);
                        }
                    }

                    if (InvokeRequired)
                        BeginInvoke(HandleAlarmResponse);
                    else
                        HandleAlarmResponse();

                    return;
                }
                if (json.TryGetProperty("MessageType", out var mt) &&
                    mt.GetString() == "zone_state")
                {
                    // Legacy handler - kept for compatibility
                    return;
                }

                if (json.TryGetProperty("MessageType", out var ipMsgType))
                {
                    string msgTypeStr = ipMsgType.GetString();

                    // -------- GET NETRA1 IP RESPONSE --------
                    if (msgTypeStr == "GetNetra1IP_response")
                    {
                        if (json.TryGetProperty("Payload", out var payload) &&
                            payload.TryGetProperty("Success", out var success) &&
                            success.GetBoolean())
                        {
                            //string ip = payload.TryGetProperty("CurrentIP", out var ipProp)
                            //    ? ipProp.GetString() ?? ""
                            //    : "";
                            //FormIpConfiguration.Instance?.SetCurrentIp(ip);
                            string ip = payload.TryGetProperty("CurrentIP", out var ipProp)
                            ? ipProp.GetString() ?? ""
                            : "";

                            string subnet = payload.TryGetProperty("SubnetMask", out var subProp)
                                ? subProp.GetString() ?? ""
                                : "";

                            string gateway = payload.TryGetProperty("Gateway", out var gwProp)
                                ? gwProp.GetString() ?? ""
                                : "";

                            FormIpConfiguration.Instance?.SetCurrentIp(ip, subnet, gateway);
                        }
                        return;
                    }

                    // -------- SET NETRA1 IP RESPONSE --------
                    if (msgTypeStr == "SetNetra1IP_response")
                    {
                        if (json.TryGetProperty("Payload", out var payload) &&
                            payload.TryGetProperty("Success", out var success))
                        {
                            if (success.GetBoolean())
                            {
                                FormIpConfiguration.Instance?.OnSetIpSuccess();
                            }
                            else
                            {
                                string error = payload.TryGetProperty("Error", out var errProp)
                                    ? errProp.GetString() ?? "Failed to set Netra1 IP"
                                    : payload.TryGetProperty("Message", out var m)
                                        ? m.GetString() ?? "Failed to set Netra1 IP"
                                        : "Failed to set Netra1 IP";

                                FormIpConfiguration.Instance?.OnSetIpFailure(error);
                            }
                        }
                        return;
                    }

                    // -------- GET NETRA2 IP RESPONSE --------
                    if (msgTypeStr == "GetNetra2IP_response")
                    {
                        if (json.TryGetProperty("Payload", out var payload) &&
                            payload.TryGetProperty("Success", out var success) &&
                            success.GetBoolean())
                        {
                            //string ip = payload.TryGetProperty("CurrentIP", out var ipProp)
                            //    ? ipProp.GetString() ?? ""
                            //    : "";
                            //FormIpConfiguration.Instance?.SetCurrentNetra2Ip(ip);
                            string ip = payload.TryGetProperty("CurrentIP", out var ipProp)
                                ? ipProp.GetString() ?? ""
                                : "";

                            string subnet = payload.TryGetProperty("SubnetMask", out var subProp)
                                ? subProp.GetString() ?? ""
                                : "";

                            string gateway = payload.TryGetProperty("Gateway", out var gwProp)
                                ? gwProp.GetString() ?? ""
                                : "";

                            FormIpConfiguration.Instance?.SetCurrentNetra2Ip(ip, subnet, gateway);
                        }
                        return;
                    }

                    // -------- SET NETRA2 IP RESPONSE --------
                    if (msgTypeStr == "SetNetra2IP_response")
                    {
                        if (json.TryGetProperty("Payload", out var payload) &&
                            payload.TryGetProperty("Success", out var success))
                        {
                            if (success.GetBoolean())
                            {
                                FormIpConfiguration.Instance?.OnSetNetra2IpSuccess();
                            }
                            else
                            {
                                string error = payload.TryGetProperty("Error", out var errProp)
                                    ? errProp.GetString() ?? "Failed to set Netra2 IP"
                                    : payload.TryGetProperty("Message", out var m)
                                        ? m.GetString() ?? "Failed to set Netra2 IP"
                                        : "Failed to set Netra2 IP";

                                FormIpConfiguration.Instance?.OnSetNetra2IpFailure(error);
                            }
                        }
                        return;
                    }

                    // -------- GET AVAILABLE INTERFACES RESPONSE --------
                    if (msgTypeStr == "GetAvailableInterfaces_response")
                    {
                        if (json.TryGetProperty("Payload", out var payload) &&
                            payload.TryGetProperty("Success", out var success) &&
                            success.GetBoolean())
                        {
                            FormIpConfiguration.Instance?.OnAvailableInterfacesReceived(payload);
                        }
                        return;
                    }

                    // -------- CONFIGURE ETHERNET ROLES RESPONSE --------
                    if (msgTypeStr == "ConfigureEthernetRoles_response")
                    {
                        if (json.TryGetProperty("Payload", out var payload) &&
                            payload.TryGetProperty("Success", out var success))
                        {
                            if (success.GetBoolean())
                            {
                                string msg = payload.TryGetProperty("Message", out var msgProp)
                                    ? msgProp.GetString() ?? "Configuration saved"
                                    : "Configuration saved";
                                FormIpConfiguration.Instance?.OnPortConfigurationSuccess(msg);
                            }
                            else
                            {
                                string error = payload.TryGetProperty("Error", out var errProp)
                                    ? errProp.GetString() ?? "Configuration failed"
                                    : "Configuration failed";
                                FormIpConfiguration.Instance?.OnPortConfigurationFailure(error);
                            }
                        }
                        return;
                    }

                    // -------- SET PERSISTENT IP RESPONSE --------
                    if (msgTypeStr == "SetPersistentIp_response")
                    {
                        if (json.TryGetProperty("Payload", out var payload) &&
                            payload.TryGetProperty("Success", out var success))
                        {
                            if (success.GetBoolean())
                            {
                                string msg = payload.TryGetProperty("Message", out var msgProp)
                                    ? msgProp.GetString() ?? "Persistent IP configuration initiated."
                                    : "Persistent IP configuration initiated.";
                                FormIpConfiguration.Instance?.OnSetPersistentIpSuccess(msg);
                            }
                            else
                            {
                                string error = payload.TryGetProperty("Error", out var errProp)
                                    ? errProp.GetString() ?? "Failed to set persistent IP"
                                    : "Failed to set persistent IP";
                                FormIpConfiguration.Instance?.OnSetPersistentIpFailure(error);
                            }
                        }
                        return;
                    }

                    // -------- GET DTS CONNECTION RESPONSE --------
                    if (msgTypeStr == "GetDtsConnection_response")
                    {
                        if (json.TryGetProperty("Payload", out var payload) &&
                            payload.TryGetProperty("Success", out var success) &&
                            success.GetBoolean())
                        {
                            string ip = payload.TryGetProperty("IpAddress", out var ipProp) ? ipProp.GetString() ?? "" : "";
                            int port = payload.TryGetProperty("Port", out var portProp) ? portProp.GetInt32() : 3001;
                            
                            var ipConfigInstance = FormIpConfiguration.Instance;
                            if (ipConfigInstance != null && !ipConfigInstance.IsDisposed)
                            {
                                ipConfigInstance.SetDtsConnection(ip, port);
                            }
                        }
                        return;
                    }

                    // -------- UPDATE DTS CONNECTION RESPONSE --------
                    if (msgTypeStr == "UpdateDtsConnection_response")
                    {
                        if (json.TryGetProperty("Payload", out var payload) &&
                            payload.TryGetProperty("Success", out var success))
                        {
                            var ipConfigInstance = FormIpConfiguration.Instance;
                            if (ipConfigInstance != null && !ipConfigInstance.IsDisposed)
                            {
                                if (success.GetBoolean())
                                {
                                    ipConfigInstance.OnUpdateDtsConnectionSuccess();
                                }
                                else
                                {
                                    string error = payload.TryGetProperty("Error", out var errProp)
                                        ? errProp.GetString() ?? "Failed to update DTS connection settings"
                                        : "Failed to update DTS connection settings";
                                    ipConfigInstance.OnUpdateDtsConnectionFailure(error);
                                }
                            }
                        }
                        return;
                    }
                }

                // Handle batched zone state updates (more efficient - all zones in one message)
                if (json.TryGetProperty("MessageType", out var batchZoneMsg) &&
                    batchZoneMsg.GetString() == "channel_zones_batch_update")
                {
                    HandleZoneStateBatchUpdate(json);
                    return;
                }

                // Handle individual zone state updates (legacy/fallback)
                if (json.TryGetProperty("MessageType", out var zoneStateMsg) &&
                    zoneStateMsg.GetString() == "zone_state_update")
                {
                    HandleZoneStateUpdate(json);
                    return;
                }

                // Handle channel state updates - update cache and grid in real-time
                if (json.TryGetProperty("MessageType", out var channelStateMsg) &&
                    channelStateMsg.GetString() == "channel_state_update")
                {
                    HandleChannelStateUpdate(json);
                    return;
                }

                // Handle alarm triggered events - refresh active alarms display
                if (json.TryGetProperty("MessageType", out var alarmTriggeredMsg) &&
                    alarmTriggeredMsg.GetString() == "alarm_triggered")
                {
                    HandleAlarmTriggered(json);
                    return;
                }

                // Handle alarm cleared events - refresh active alarms display
                if (json.TryGetProperty("MessageType", out var alarmClearedMsg) &&
                    alarmClearedMsg.GetString() == "alarm_cleared")
                {
                    HandleAlarmCleared(json);
                    return;
                }

                // Handle alarm updated events - refresh active alarms display
                if (json.TryGetProperty("MessageType", out var alarmUpdatedMsg) &&
                    alarmUpdatedMsg.GetString() == "alarm_updated")
                {
                    HandleAlarmUpdated(json);
                    return;
                }

                // Handle fiber break detected events - refresh active alarms and update LED
                if (json.TryGetProperty("MessageType", out var fiberBreakMsg) &&
                    fiberBreakMsg.GetString() == "fiber_break_detected")
                {
                    HandleFiberBreakDetected(json);
                    return;
                }

                // Handle fiber break cleared events - refresh active alarms and update LED
                if (json.TryGetProperty("MessageType", out var fiberClearedMsg) &&
                    fiberClearedMsg.GetString() == "fiber_break_cleared")
                {
                    HandleFiberBreakCleared(json);
                    return;
                }

                // Handle channel configuration updated - refresh graph axes if channel length changed
                if (json.TryGetProperty("MessageType", out var channelConfigMsg) &&
                    channelConfigMsg.GetString() == "channel_config_updated")
                {
                    HandleChannelConfigUpdated(json);
                    return;
                }

                // Handle live temperature data broadcast from FRMC
                if (json.TryGetProperty("MessageType", out var liveDataMsg) &&
                    liveDataMsg.GetString() == "live_temperature_data")
                {
                    // Skip JSON graph updates when binary data stream is active (prevents dual-path conflicts)
                    if (_dataStreamClient != null && _dataStreamClient.IsConnected)
                        return;

                    // Fallback: only used when binary stream is down

                    if (json.TryGetProperty("Payload", out var livePayload))
                    {
                        if (livePayload.TryGetProperty("type", out var typeProp) &&
                            typeProp.GetString() == "temperatureData" &&
                            livePayload.TryGetProperty("data", out var dataArray))
                        {
                            hasLiveData = true;
                            int channel = livePayload.TryGetProperty("channel", out var chProp) ? chProp.GetInt32() : -1;

                            // Debug: Log channel comparison
                            Console.WriteLine($"[FRCM] Live data received: channel={channel}, _selectedChannelId={_selectedChannelId}, isPlottingEnabled={isPlottingEnabled}");

                            // Filter: Only update graph if this channel is selected
                            if (!isPlottingEnabled || channel != _selectedChannelId)
                            {
                                Console.WriteLine($"[FRCM] Filtered out: isPlottingEnabled={isPlottingEnabled}, channel match={channel == _selectedChannelId}");
                                return;
                            }

                            var xList = new List<double>();
                            var yList = new List<double>();

                            // Extract all points first
                            foreach (var point in dataArray.EnumerateArray())
                            {
                                if (point.TryGetProperty("Position", out var posProp) &&
                                    point.TryGetProperty("Temperature", out var tempProp))
                                {
                                    double position = posProp.GetDouble();
                                    double temperature = tempProp.GetDouble();

                                    // If a zone is selected, filter by zone position range
                                    if (_selectedZoneId > 0)
                                    {
                                        // Filter points within zone boundaries
                                        if (_selectedZoneStartPosition.HasValue && _selectedZoneEndPosition.HasValue)
                                        {
                                            if (position >= _selectedZoneStartPosition.Value && position <= _selectedZoneEndPosition.Value)
                                            {
                                                xList.Add(position);
                                                yList.Add(temperature);
                                            }
                                            // else: point outside zone range, skip it
                                        }
                                        else
                                        {
                                            Console.WriteLine($"[FRCM] WARNING: Zone {_selectedZoneId} selected but position range is null!");
                                        }
                                    }
                                    else
                                    {
                                        // No zone selected - show all channel data
                                        xList.Add(position);
                                        yList.Add(temperature);
                                    }
                                }
                            }

                            BeginInvoke(new Action(() =>
                            {
                                UpdateGraphFromWebSocket(xList.ToArray(), yList.ToArray());
                            }));
                        }
                    }
                    return;
                }

                if (json.TryGetProperty("MessageType", out var msgType) &&
                    msgType.GetString() == "start_measurement_response")
                {
                    string status = json.GetProperty("Payload").GetProperty("Status").GetString();

                    BeginInvoke(new Action(() =>
                    {
                        if (status == "OK")
                        {
                            isPlottingEnabled = true;
                            UpdateStatus("Measurement started");
                        }
                        else
                        {
                            isMonitoring = false;
                            btnStartMeasurement.Text = "Start Measurement";
                            btnStartMeasurement.BackColor = DrawingColor.Green;
                            UpdateStatus("Failed to start measurement");
                        }
                    }));

                    return;
                }
                if (json.TryGetProperty("MessageType", out msgType) &&
                    msgType.GetString() == "stop_measurement_response")
                {
                    BeginInvoke(new Action(() =>
                    {
                        isPlottingEnabled = false;
                        isMonitoring = false;
                        UpdateStatus("Measurement stopped");
                    }));

                    return;
                }
                if (json.TryGetProperty("MessageType", out msgType) &&
                    msgType.GetString() == "measurement_started")
                {
                    BeginInvoke(new Action(() =>
                    {
                        isPlottingEnabled = true;

                        // Update graph title for live streaming
                        if (_selectedZoneId > 0)
                        {
                            string zoneName = GetZoneName(_selectedChannelId, _selectedZoneId);
                            _currentGraphTitle = $"Live Data - {zoneName}";
                        }
                        else
                        {
                            _currentGraphTitle = $"Live Data - Channel {_selectedChannelId}";
                        }
                        SetGraphTitle(_currentGraphTitle);
                        formsPlot1.Refresh();
                        Console.WriteLine($"[FRCM] Live streaming enabled with title: {_currentGraphTitle}");
                    }));

                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error parsing WebSocket message: {ex.Message}");
            }
        }
        private double GetCorrectionLengthForSelectedChannel()
        {
            if (_selectedChannelId <= 0)
                return 0;

            string key = $"Channel {_selectedChannelId}";
            var cfg = _configurationService.GetChannelConfig(key);

            return cfg?.CorrectionLength ?? 0;
        }

        private void UpdateGraphFromWebSocket(double[] positions, double[] temperaturesArray)
        {
            try
            {
                if (positions == null || temperaturesArray == null)
                {
                    Console.WriteLine("Graph update skipped: Null data received");
                    return;
                }

                if (positions.Length == 0 || temperaturesArray.Length == 0)
                {
                    Console.WriteLine("Graph update skipped: Empty data received");
                    return;
                }

                if (positions.Length != temperaturesArray.Length)
                {
                    Console.WriteLine($"Graph update error: Position count ({positions.Length}) != Temperature count ({temperaturesArray.Length})");
                    UpdateStatus("Error: Temperature data mismatch");
                    return;
                }

                // Validate data ranges
                if (positions.Any(p => double.IsNaN(p) || double.IsInfinity(p)) ||
                    temperaturesArray.Any(t => double.IsNaN(t) || double.IsInfinity(t)))
                {
                    Console.WriteLine("Graph update error: Invalid numeric values detected (NaN or Infinity)");
                    UpdateStatus("Error: Invalid temperature data");
                    return;
                }

                // Throttle updates to prevent UI thread blocking (max 1 refresh per 100ms)
                var now = DateTime.Now;
                if ((now - _lastGraphUpdate).TotalMilliseconds < GRAPH_UPDATE_THROTTLE_MS)
                {
                    return; // Skip this update, too soon since last refresh
                }
                _lastGraphUpdate = now;

                // Save current axis limits to preserve zoom level
                var xAxis = formsPlot1.Plot.Axes.GetLimits();
                //bool hasData = distances.Count > 0;

                distances.Clear();
                temperatures.Clear();

                double channelLength = GetChannelLengthForSelectedChannel();
                double correctionLength = GetCorrectionLengthForSelectedChannel();
                bool isZoneSelected = _selectedZoneId > 0 && _selectedZoneStartPosition.HasValue && _selectedZoneEndPosition.HasValue;

                if (isZoneSelected)
                {
                    double zoneStart = _selectedZoneStartPosition!.Value;
                    double zoneEnd = _selectedZoneEndPosition!.Value;

                    for (int i = 0; i < positions.Length; i++)
                    {
                        // ✅ Filter by absolute boundaries, but plot using CORRECTED positions
                        if (positions[i] >= zoneStart && positions[i] <= zoneEnd)
                        {
                            distances.Add(positions[i]);
                            temperatures.Add(temperaturesArray[i]);
                        }
                    }

                    // 🚨 MATCH UpdateGraph() logic: Ignore updates with too few points
                    if (distances.Count < 5)
                    {
                        return;
                    }
                }
                else
                {
                    for (int i = 0; i < positions.Length; i++)
                    {
                        double correctedX = positions[i] - correctionLength;
                        correctedX = Math.Min(correctedX, channelLength);

                        distances.Add(correctedX);
                        temperatures.Add(temperaturesArray[i]);
                    }
                }

                // FM-07: Double-buffered snapshot promote
                var snapshot = _traceCache.Update(distances, temperatures);

                // Cache latest graph data
                if (isZoneSelected)
                {
                    _zoneGraphCache[
                        (_selectedChannelId, _selectedZoneId)
                    ] = (
                        new List<double>(distances),
                        new List<double>(temperatures)
                    );
                }
                else if (_selectedChannelId > 0)
                {
                    _channelGraphCache[_selectedChannelId] = (
                        new List<double>(distances),
                        new List<double>(temperatures)
                    );
                }
                // =========================================
                // CLEAR OLD GRAPH DATA ONLY
                // =========================================
                if (_liveScatter != null)
                {
                    formsPlot1.Plot.Remove(_liveScatter);
                }

                // =========================================
                // ADD LIVE SCATTER
                // =========================================
                _liveScatter = formsPlot1.Plot.Add.Scatter(
                    snapshot.Distances,
                    snapshot.Temperatures);

                _liveScatter.Color = ScottPlot.Colors.Blue;
                _liveScatter.LineWidth = 2;
                _liveScatter.MarkerSize = 0;

                // =========================================
                // RESTORE RED VERTICAL MARKER LINE
                // =========================================
                if (markerLine == null)
                {
                    markerLine = formsPlot1.Plot.Add.VerticalLine(_markerLineX);
                    markerLine.Color = ScottPlot.Colors.Red;
                    markerLine.LineWidth = 2;
                }


                // Preserve marker position

                markerLine.X = _markerLineX;
                markerLine.IsVisible = true;

                // =========================================
                // GRAPH STYLING
                // =========================================
                SetGraphTitle(_currentGraphTitle);
                formsPlot1.Plot.XLabel("Distance (m)");
                formsPlot1.Plot.YLabel("Temperature (°C)");

                // Apply axis limits based on fixed axis settings
                if (_userAxisApplied)
                {
                    formsPlot1.Plot.Axes.SetLimitsX(_userXMin, _userXMax);
                    formsPlot1.Plot.Axes.SetLimitsY(_userYMin, _userYMax);
                }
                else
                {
                    if (isZoneSelected)
                    {
                        formsPlot1.Plot.Axes.SetLimitsX(
                            _selectedZoneStartPosition!.Value,
                            _selectedZoneEndPosition!.Value);
                    }
                    else if (_isXAxisFixed)
                    {
                        if (channelLength > 0)
                        {
                            formsPlot1.Plot.Axes.SetLimitsX(
                                -correctionLength,
                                channelLength);
                        }
                        else
                        {
                            double dataMinX =
                                distances.Count > 0 ? distances.Min() : 0;

                            double dataMaxX =
                                distances.Count > 0 ? distances.Max() : 1000;

                            formsPlot1.Plot.Axes.SetLimitsX(
                                dataMinX,
                                dataMaxX);
                        }
                    }
                    else
                    {
                        // Preserve zoom/pan
                        formsPlot1.Plot.Axes.SetLimitsX(
                            xAxis.Left,
                            xAxis.Right);
                    }
                    if (_isYAxisFixed)
                    {
                        formsPlot1.Plot.Axes.SetLimitsY(
                            DEFAULT_Y_MIN,
                            DEFAULT_Y_MAX);
                    }
                    else if (
                        distances.Count > 0 &&
                        xAxis.Bottom == 0 &&
                        xAxis.Top == 1)
                    {
                        formsPlot1.Plot.Axes.AutoScale();
                    }
                    else
                    {
                        formsPlot1.Plot.Axes.SetLimitsY(
                            xAxis.Bottom,
                            xAxis.Top);
                    }
                }

                // Force a redraw with the new limits applied
                formsPlot1.Refresh();
            }
            catch (Exception ex)
            {
                UpdateStatus($"Graph update error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles batched zone state updates from FRMC broadcasts.
        /// More efficient than per-zone updates - processes all zones for a channel in one message.
        /// </summary>
        private void HandleZoneStateBatchUpdate(JsonElement json)
        {
            try
            {
                if (!json.TryGetProperty("Payload", out var payload))
                    return;

                int channelId = payload.TryGetProperty("ChannelId", out var chProp) ? chProp.GetInt32() : -1;
                if (channelId < 0)
                    return;

                if (!payload.TryGetProperty("Zones", out var zonesArray) ||
                    zonesArray.ValueKind != System.Text.Json.JsonValueKind.Array)
                    return;

                // Check if this is a chunked batch (has BatchIndex and TotalBatches)
                int batchIndex = payload.TryGetProperty("BatchIndex", out var biProp) ? biProp.GetInt32() : 0;
                int totalBatches = payload.TryGetProperty("TotalBatches", out var tbProp) ? tbProp.GetInt32() : 1;
                bool isLastBatch = (batchIndex >= totalBatches - 1);

                // Process all zones in the batch
                foreach (var zoneData in zonesArray.EnumerateArray())
                {
                    int zoneId = zoneData.TryGetProperty("ZoneId", out var zProp) ? zProp.GetInt32() : -1;
                    if (zoneId < 0)
                        continue;

                    var zoneState = new ZoneStateInfo
                    {
                        ZoneId = zoneId,
                        Name = zoneData.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() ?? $"Zone {zoneId}" : $"Zone {zoneId}",
                        StartPoint = zoneData.TryGetProperty("StartPoint", out var sp) ? sp.GetDouble() : 0,
                        EndPoint = zoneData.TryGetProperty("EndPoint", out var ep) ? ep.GetDouble() : 0,
                        Enabled = zoneData.TryGetProperty("Enabled", out var en) ? en.GetBoolean() : false,
                        ActiveAlarm = zoneData.TryGetProperty("ActiveAlarm", out var aa) ? aa.GetBoolean() : false,
                        AverageTemperature = zoneData.TryGetProperty("Avg", out var avg) ? avg.GetDouble() : 0,
                        MaxTemperature = zoneData.TryGetProperty("Max", out var max) ? max.GetDouble() : 0,
                        MinTemperature = zoneData.TryGetProperty("Min", out var min) ? min.GetDouble() : 0,
                        RateOfRise = zoneData.TryGetProperty("RateOfRise", out var ror) ? ror.GetDouble() : 0,
                        Deviation = zoneData.TryGetProperty("Deviation", out var dev) ? dev.GetDouble() : 0,
                        MaxAlarm = zoneData.TryGetProperty("MaxAlarm", out var maxAlm) ? maxAlm.GetBoolean() : false,
                        MinAlarm = zoneData.TryGetProperty("MinAlarm", out var minAlm) ? minAlm.GetBoolean() : false,
                        PreAlarm = zoneData.TryGetProperty("PreAlarm", out var preAlm) ? preAlm.GetBoolean() : false,
                        RorAlarm = zoneData.TryGetProperty("RorAlarm", out var rorAlm) ? rorAlm.GetBoolean() : false,
                        DeviationAlarm = zoneData.TryGetProperty("DeviationAlarm", out var devAlm) ? devAlm.GetBoolean() : false,
                        LastUpdate = DateTime.Now
                    };

                    // Update cache
                    _zoneStateCache[(channelId, zoneId)] = zoneState;

                    // UPDATE LED TRACKING: Sync LED state from zone state broadcasts
                    // This ensures LEDs update even when alarm_triggered messages are missed
                    int frmcChannelId = channelId - 1; // Convert to 0-based
                    int frmcZoneId = zoneId - 1;

                    if (zoneState.ActiveAlarm)
                    {
                        // Zone has active alarm - add to tracking sets
                        _channelsWithAlarm.Add(frmcChannelId);
                        _channelsWithAnyAlarm.Add(frmcChannelId);
                        _zonesWithAlarm.Add((frmcChannelId, frmcZoneId));
                    }
                    else
                    {
                        // Zone alarm cleared - remove from zone tracking
                        _zonesWithAlarm.Remove((frmcChannelId, frmcZoneId));

                        // Check if channel still has any alarms from other zones
                        bool channelHasOtherAlarms = _zonesWithAlarm.Any(z => z.channelId == frmcChannelId);
                        if (!channelHasOtherAlarms)
                        {
                            _channelsWithAlarm.Remove(frmcChannelId);
                            _channelsWithAnyAlarm.Remove(frmcChannelId);
                        }
                    }
                }

                // Only invoke UI update on the LAST batch (or single batch) for efficiency
                if (isLastBatch)
                {
                    // Update LEDs based on zone state alarm flags
                    if (this.InvokeRequired)
                    {
                        this.Invoke(() =>
                        {
                            UpdateAlarmLed();
                            UpdateSafetyActiveAlarmLed();
                            if (channelId == _selectedChannelId)
                            {
                                UpdateChannelInformationGrid(channelId);
                            }
                        });
                    }
                    else
                    {
                        UpdateAlarmLed();
                        UpdateSafetyActiveAlarmLed();
                        if (channelId == _selectedChannelId)
                        {
                            UpdateChannelInformationGrid(channelId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling zone state batch update: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles binary messages received on the control WebSocket (port 6164).
        /// Currently handles binary zone batch updates (MSG_ZONE_BATCH = 0x03).
        /// </summary>
        private void OnWebSocketBinaryMessageReceived(object? sender, byte[] data)
        {
            if (data == null || data.Length < 1)
                return;

            // 🔒 BLOCK real-time binary updates when not monitoring
            if (!isMonitoring)
                return;

            byte msgType = data[0];
            if (msgType == BinaryProtocol.MSG_ZONE_BATCH)
            {
                HandleBinaryZoneBatchUpdate(data);
            }
        }

        /// <summary>
        /// Handles binary zone batch updates from FRMC.
        /// Binary equivalent of HandleZoneStateBatchUpdate — same logic, ~93% less bandwidth.
        /// </summary>
        private void HandleBinaryZoneBatchUpdate(byte[] data)
        {
            try
            {
                var batch = BinaryProtocol.ParseZoneBatch(data);
                int channelId = batch.ChannelId;
                bool isLastBatch = batch.BatchIndex >= batch.TotalBatches - 1;

                foreach (var zone in batch.Zones)
                {
                    var zoneState = new ZoneStateInfo
                    {
                        ZoneId = zone.ZoneId,
                        Name = zone.Name,
                        StartPoint = zone.StartPoint,
                        EndPoint = zone.EndPoint,
                        Enabled = zone.Enabled,
                        ActiveAlarm = zone.ActiveAlarm,
                        AverageTemperature = zone.Avg,
                        MaxTemperature = zone.Max,
                        MinTemperature = zone.Min,
                        RateOfRise = zone.RateOfRise,
                        Deviation = zone.Deviation,
                        MaxAlarm = zone.MaxAlarm,
                        MinAlarm = zone.MinAlarm,
                        PreAlarm = zone.PreAlarm,
                        RorAlarm = zone.RorAlarm,
                        DeviationAlarm = zone.DeviationAlarm,
                        LastUpdate = zone.LastUpdate.ToLocalTime()
                    };

                    _zoneStateCache[(channelId, zone.ZoneId)] = zoneState;

                    // LED tracking (same logic as JSON handler)
                    int frmcChannelId = channelId - 1;
                    int frmcZoneId = zone.ZoneId - 1;

                    if (zoneState.ActiveAlarm)
                    {
                        _channelsWithAlarm.Add(frmcChannelId);
                        _channelsWithAnyAlarm.Add(frmcChannelId);
                        _zonesWithAlarm.Add((frmcChannelId, frmcZoneId));
                    }
                    else
                    {
                        _zonesWithAlarm.Remove((frmcChannelId, frmcZoneId));
                        bool channelHasOtherAlarms = _zonesWithAlarm.Any(z => z.channelId == frmcChannelId);
                        if (!channelHasOtherAlarms)
                        {
                            _channelsWithAlarm.Remove(frmcChannelId);
                            _channelsWithAnyAlarm.Remove(frmcChannelId);
                        }
                    }
                }

                if (isLastBatch)
                {
                    if (this.InvokeRequired)
                    {
                        this.Invoke(() =>
                        {
                            UpdateAlarmLed();
                            UpdateSafetyActiveAlarmLed();
                            if (channelId == _selectedChannelId)
                            {
                                UpdateChannelInformationGrid(channelId);
                            }
                        });
                    }
                    else
                    {
                        UpdateAlarmLed();
                        UpdateSafetyActiveAlarmLed();
                        if (channelId == _selectedChannelId)
                        {
                            UpdateChannelInformationGrid(channelId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling binary zone batch update: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles real-time zone state updates from FRMC broadcasts (legacy/fallback).
        /// Updates cache and refreshes Channel Information grid if viewing the updated channel.
        /// </summary>
        private void HandleZoneStateUpdate(JsonElement json)
        {
            try
            {
                if (!json.TryGetProperty("Payload", out var payload))
                    return;

                // Extract zone information from payload
                int channelId = payload.TryGetProperty("ChannelId", out var chProp) ? chProp.GetInt32() : -1;
                int zoneId = payload.TryGetProperty("ZoneId", out var zProp) ? zProp.GetInt32() : -1;

                if (channelId < 0 || zoneId < 0)
                    return;

                // Create ZoneStateInfo from payload
                var zoneState = new ZoneStateInfo
                {
                    ZoneId = zoneId,
                    Name = payload.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() ?? $"Zone {zoneId}" : $"Zone {zoneId}",
                    StartPoint = payload.TryGetProperty("StartPoint", out var sp) ? sp.GetDouble() : 0,
                    EndPoint = payload.TryGetProperty("EndPoint", out var ep) ? ep.GetDouble() : 0,
                    Enabled = payload.TryGetProperty("Enabled", out var en) ? en.GetBoolean() : false,
                    ActiveAlarm = payload.TryGetProperty("ActiveAlarm", out var aa) ? aa.GetBoolean() : false,
                    AverageTemperature = payload.TryGetProperty("Avg", out var avg) ? avg.GetDouble() : 0,
                    MaxTemperature = payload.TryGetProperty("Max", out var max) ? max.GetDouble() : 0,
                    MinTemperature = payload.TryGetProperty("Min", out var min) ? min.GetDouble() : 0,
                    RateOfRise = payload.TryGetProperty("RateOfRise", out var ror) ? ror.GetDouble() : 0,
                    Deviation = payload.TryGetProperty("Deviation", out var dev) ? dev.GetDouble() : 0,
                    // Specific alarm flags for cell-level highlighting
                    MaxAlarm = payload.TryGetProperty("MaxAlarm", out var maxAlm) ? maxAlm.GetBoolean() : false,
                    MinAlarm = payload.TryGetProperty("MinAlarm", out var minAlm) ? minAlm.GetBoolean() : false,
                    PreAlarm = payload.TryGetProperty("PreAlarm", out var preAlm) ? preAlm.GetBoolean() : false,
                    RorAlarm = payload.TryGetProperty("RorAlarm", out var rorAlm) ? rorAlm.GetBoolean() : false,
                    DeviationAlarm = payload.TryGetProperty("DeviationAlarm", out var devAlm) ? devAlm.GetBoolean() : false,
                    LastUpdate = DateTime.Now
                };

                // Update cache (always - needed for instant channel switching)
                _zoneStateCache[(channelId, zoneId)] = zoneState;

                // Only invoke UI update if this is the selected channel
                // This avoids unnecessary cross-thread marshaling for non-visible channels
                if (channelId == _selectedChannelId)
                {
                    if (this.InvokeRequired)
                    {
                        this.Invoke(() => UpdateChannelInformationGrid(channelId));
                    }
                    else
                    {
                        UpdateChannelInformationGrid(channelId);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling zone state update: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles real-time channel state updates from FRMC broadcasts.
        /// Updates cache and refreshes fiber break LED and channel header if viewing the updated channel.
        /// </summary>
        private void HandleChannelStateUpdate(JsonElement json)
        {
            if (!isMonitoring) return;

            try
            {
                if (!json.TryGetProperty("Payload", out var payload)) return;

                int channelId = payload.TryGetProperty("ChannelId", out var chProp) ? chProp.GetInt32() : -1;
                if (channelId < 0)
                    return;

                // Store channel state in cache (as dynamic object for flexibility)
                var channelState = new
                {
                    ChannelId = channelId,
                    ConfiguredLength = payload.TryGetProperty("ConfiguredLength", out var cl) ? cl.GetDouble() : 0,
                    ActualLength = payload.TryGetProperty("ActualLength", out var al) ? al.GetDouble() : 0,
                    FiberBroken = payload.TryGetProperty("FiberBroken", out var fb) ? fb.GetBoolean() : false,
                    LastUpdate = payload.TryGetProperty("LastUpdate", out var lu) ? lu.GetString() : "",
                    NumberOfZones = payload.TryGetProperty("NumberOfZones", out var nz) ? nz.GetInt32() : 0,
                    IsActive = payload.TryGetProperty("IsActive", out var ia) ? ia.GetBoolean() : false
                };

                _channelStateCache[channelId] = channelState;

                // If this is the currently selected channel, update UI elements
                if (_selectedChannelId == channelId)
                {
                    if (this.InvokeRequired)
                    {
                        this.Invoke(() => UpdateChannelStateUI(channelId));
                    }
                    else
                    {
                        UpdateChannelStateUI(channelId);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling channel state update: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles channel_config_updated broadcasts from FRMC.
        /// Updates local configuration cache and refreshes graph axes if channel length changed.
        /// </summary>
        private void HandleChannelConfigUpdated(JsonElement json)
        {
            try
            {
                if (!json.TryGetProperty("Payload", out var payload))
                    return;

                int channelId = payload.TryGetProperty("ChannelId", out var chProp) ? chProp.GetInt32() : -1;
                if (channelId < 0)
                    return;

                double channelLengthFromFrmc = payload.TryGetProperty("ChannelLength", out var lenProp) ? lenProp.GetDouble() : 0;
                double correctionLength = payload.TryGetProperty("CorrectionLength", out var corrProp) ? corrProp.GetDouble() : 0;
                string name = payload.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                bool isEnabled = payload.TryGetProperty("IsEnabled", out var enProp) ? enProp.GetBoolean() : false;
                int scanPeriod = payload.TryGetProperty("ScanPeriod", out var spProp) ? spProp.GetInt32() : 0;
                int numberOfZones = payload.TryGetProperty("NumberOfZones", out var nzProp) ? nzProp.GetInt32() : 0;

                Console.WriteLine($"[FRCM] Channel config updated: Ch{channelId}, MonitoredLength={channelLengthFromFrmc}m, Correction={correctionLength}m, Enabled={isEnabled}");

                // Update local configuration cache
                string key = $"Channel {channelId}";
                var existingCfg = _configurationService.GetChannelConfig(key);
                if (existingCfg != null)
                {
                    // Derive base Length from monitored range minus correction
                    //existingCfg.Length = channelLengthFromFrmc - correctionLength;
                    existingCfg.ChannelLength = channelLengthFromFrmc;
                    existingCfg.Length = channelLengthFromFrmc - correctionLength;
                    existingCfg.CorrectionLength = correctionLength;
                    existingCfg.Name = name;
                    existingCfg.IsEnabled = isEnabled;
                    existingCfg.ScanPeriod = scanPeriod;
                    existingCfg.NumberOfZones = numberOfZones;
                }

                // If this is the currently selected channel, refresh the graph axes
                if (channelId == _selectedChannelId)
                {
                    bool isZoneSelected = _selectedZoneId > 0 && _selectedZoneStartPosition.HasValue && _selectedZoneEndPosition.HasValue;

                    if (!isZoneSelected)
                    {
                        if (this.InvokeRequired)
                        {
                            this.Invoke(() =>
                            {
                                // Reset X-axis to new channel range
                                ResetXAxisToDefault();
                                formsPlot1.Refresh();
                            });
                        }
                        else
                        {
                            ResetXAxisToDefault();
                            formsPlot1.Refresh();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling channel config update: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates UI elements for channel state (fiber break LED, channel header, etc.)
        /// </summary>
        private void UpdateChannelStateUI(int channelId)
        {
            // Update fiber break LED
            if (ledFiberBreak != null && _channelStateCache.TryGetValue(channelId, out var state))
            {
                dynamic ch = state;
                ledFiberBreak.IsOn = ch.FiberBroken;
                ledFiberBreak.Invalidate();
            }

            // Update channel header if on Channel Information tab
            if (tabControlDisplay.SelectedTab == tabChannelInformation)
            {
                UpdateChannelHeader(channelId);
            }
        }

        /// <summary>
        /// Updates the channel header information (name, enabled status, etc.)
        /// </summary>
        public void OnChannelSelected(int channelId)
        {
            _selectedChannelId = channelId;

            Console.WriteLine($"[MAIN] Channel changed to {channelId}");

            // 🔥 Update graph from cache if available
            bool isZoneSelected = _selectedZoneId > 0 && _selectedZoneStartPosition.HasValue && _selectedZoneEndPosition.HasValue;

            if (isZoneSelected && _zoneGraphCache.ContainsKey((channelId, _selectedZoneId)))
            {
                var data = _zoneGraphCache[(channelId, _selectedZoneId)];
                formsPlot1.Plot.Clear();
                _liveScatter = formsPlot1.Plot.Add.Scatter(data.distances.ToArray(), data.temperatures.ToArray());
                _liveScatter.Color = ScottPlot.Colors.Blue;
                _liveScatter.LineWidth = 2;
                _liveScatter.MarkerSize = 0;

                // Restore marker line
                if (markerLine == null)
                {
                    markerLine = formsPlot1.Plot.Add.VerticalLine(_markerLineX);
                    markerLine.Color = ScottPlot.Colors.Red;
                    markerLine.LineWidth = 2;
                }
                markerLine.X = _markerLineX;
                markerLine.IsVisible = true;

                double correction = GetCorrectionLengthForSelectedChannel();
                formsPlot1.Plot.Axes.SetLimitsX(
                    _selectedZoneStartPosition!.Value - correction,
                    _selectedZoneEndPosition!.Value - correction);

                if (_isYAxisFixed)
                    formsPlot1.Plot.Axes.SetLimitsY(DEFAULT_Y_MIN, DEFAULT_Y_MAX);
                else
                    formsPlot1.Plot.Axes.AutoScale();

                formsPlot1.Refresh();
            }
            else if (_channelGraphCache.ContainsKey(channelId))
            {
                var data = _channelGraphCache[channelId];

                formsPlot1.Plot.Clear();
                _liveScatter = formsPlot1.Plot.Add.Scatter(data.distances.ToArray(), data.temperatures.ToArray());
                _liveScatter.Color = ScottPlot.Colors.Blue;
                _liveScatter.LineWidth = 2;
                _liveScatter.MarkerSize = 0;

                // Restore marker line
                if (markerLine == null)
                {
                    markerLine = formsPlot1.Plot.Add.VerticalLine(_markerLineX);
                    markerLine.Color = ScottPlot.Colors.Red;
                    markerLine.LineWidth = 2;
                }
                markerLine.X = _markerLineX;
                markerLine.IsVisible = true;

                double correction = GetCorrectionLengthForSelectedChannel();
                double length = GetChannelLengthForSelectedChannel();
                if (_isXAxisFixed && length > 0)
                    formsPlot1.Plot.Axes.SetLimitsX(-correction, length);
                else
                    formsPlot1.Plot.Axes.AutoScale();

                formsPlot1.Refresh();
            }
            else
            {
                // No graph data yet → clear graph
                formsPlot1.Plot.Clear();
                formsPlot1.Refresh();
            }

            // 🔥 Update channel state UI (grid, LEDs, etc.)
            if (_channelStateCache.ContainsKey(channelId))
            {
                UpdateChannelStateUI(channelId);
            }
        }
        private void UpdateChannelHeader(int channelId)
        {
            try
            {
                string key = $"Channel {channelId}";
                var cfg = _configurationService.GetChannelConfig(key);

                if (cfg != null)
                {
                    lblChannelName.Text = string.IsNullOrEmpty(cfg.Name) ? key : cfg.Name;

                    // Only update if state changed
                    bool channelEnabled = cfg.IsEnabled;
                    if (channelEnabled != _lastChannelEnabledState)
                    {
                        if (ledChannelEnabled != null) { ledChannelEnabled.IsOn = channelEnabled; ledChannelEnabled.Invalidate(); }
                        _lastChannelEnabledState = channelEnabled;
                    }
                }

                // Update alarm LED from cache (only if state changed)
                bool channelHasAlarm = _channelsWithAlarm.Contains(channelId - 1);
                if (channelHasAlarm != _lastChannelAlarmState)
                {
                    if (ledChannelAlarm != null) ledChannelAlarm.IsOn = channelHasAlarm;
                    _lastChannelAlarmState = channelHasAlarm;
                }

                // Update fiber break LED from cache (only if state changed)
                bool fiberBroken = false;
                if (_channelStateCache.TryGetValue(channelId, out var state))
                {
                    dynamic ch = state;
                    fiberBroken = ch.FiberBroken;
                }
                if (fiberBroken != _lastFiberBreakState)
                {
                    if (ledFiberBreak != null) { ledFiberBreak.IsOn = fiberBroken; ledFiberBreak.Invalidate(); }
                    _lastFiberBreakState = fiberBroken;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating channel header: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles tab selection changes — refreshes info grids with latest data when user
        /// switches to Channel Information or Zone Information tabs.
        /// </summary>
        private async void TabControlDisplay_SelectedIndexChanged(object? sender, EventArgs e)
        {
            try
            {
                var tab = tabControlDisplay.SelectedTab;
                if (tab == tabChannelInformation && _selectedChannelId > 0)
                {
                    // Rebuild zone state grid from cache (cache is always kept up-to-date by broadcasts)
                    UpdateChannelInformationGrid(_selectedChannelId);
                }
                else if (tab == tabZoneInformation && _selectedChannelId > 0 && _selectedZoneId > 0 && isMonitoring)
                {
                    // Fetch fresh zone points from FRMC
                    var points = await _ChannelClient.GetZonePointsAsync(_selectedChannelId, _selectedZoneId);
                    if (points != null && points.Count > 0)
                    {
                        PopulateZonePointsGrid(points);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TabChanged] Error refreshing tab data: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the Channel Information grid for the specified channel if it's currently selected.
        /// Uses cached data for fast updates.
        /// </summary>
        private void UpdateChannelInformationGrid(int channelId)
        {
            // Only update if viewing Channel Information tab and this is the selected channel
            if (tabControlDisplay.SelectedTab != tabChannelInformation || _selectedChannelId != channelId)
                return;

            // Get zone configurations to filter what we display
            var channelKey = $"Channel {channelId}";
            var zoneConfigs = _configurationService.GetZoneConfig(channelKey);
            var validZoneIds = zoneConfigs?.Select(zc => zc.ZoneId).ToHashSet() ?? new HashSet<int>();

            // Get all zones for this channel from cache
            var zonesForChannel = _zoneStateCache
                .Where(kvp => kvp.Key.channelId == channelId && validZoneIds.Contains(kvp.Key.zoneId))
                .OrderBy(kvp => kvp.Key.zoneId)
                .Select(kvp => kvp.Value)
                .ToList();

            if (zonesForChannel.Count == 0)
                return; // No cached data yet

            // Update grid
            PopulateZoneStateGrid(zonesForChannel);
        }

        /// <summary>
        /// Handles alarm_triggered broadcasts from FRMC.
        /// Refreshes the active alarms display and updates zone LED indicators.
        /// </summary>
        private void HandleAlarmTriggered(JsonElement json)
        {
            try
            {
                if (!json.TryGetProperty("Payload", out var payload))
                    return;

                int channelId = payload.TryGetProperty("ChannelId", out var chProp) ? chProp.GetInt32() : -1;
                int zoneId = payload.TryGetProperty("ZoneId", out var zProp) ? zProp.GetInt32() : -1;
                string alarmType = payload.TryGetProperty("AlarmType", out var atProp) ? atProp.GetString() ?? "" : "";

                Console.WriteLine($"[FRCM] Alarm triggered: Ch{channelId}, Zone{zoneId}, Type={alarmType}");

                // Automatically show the Active Alarm form when a new alarm is triggered
                if (this.InvokeRequired)
                {
                    this.Invoke(() => ShowActiveAlarmForm());
                }
                else
                {
                    ShowActiveAlarmForm();
                }

                // Refresh active alarms
                if (this.InvokeRequired)
                {
                    this.Invoke(() => RefreshActiveAlarms());
                }
                else
                {
                    RefreshActiveAlarms();
                }

                // Update LED indicators if this alarm is for the currently selected zone
                if (_selectedChannelId == channelId && _selectedZoneId == zoneId)
                {
                    if (this.InvokeRequired)
                    {
                        this.Invoke(() => UpdateAlarmLed());
                    }
                    else
                    {
                        UpdateAlarmLed();
                    }
                }

                // Refresh zone/channel information grids if alarm affects the currently displayed channel
                if (_selectedChannelId == channelId)
                {
                    if (this.InvokeRequired)
                    {
                        this.Invoke(async () => await RefreshCurrentInformationTab());
                    }
                    else
                    {
                        _ = RefreshCurrentInformationTab();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling alarm triggered: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles alarm_cleared broadcasts from FRMC.
        /// Refreshes the active alarms display and updates zone LED indicators.
        /// </summary>
        private void HandleAlarmCleared(JsonElement json)
        {
            try
            {
                if (!json.TryGetProperty("Payload", out var payload))
                    return;

                int alarmId = payload.TryGetProperty("AlarmId", out var aidProp) ? aidProp.GetInt32() : -1;

                Console.WriteLine($"[FRCM] Alarm cleared: AlarmId={alarmId}");

                // Refresh active alarms if the ActiveAlarm form is open
                if (this.InvokeRequired)
                {
                    this.Invoke(() => RefreshActiveAlarms());
                }
                else
                {
                    RefreshActiveAlarms();
                }

                // Update LED indicators for currently selected zone
                if (this.InvokeRequired)
                {
                    this.Invoke(() => UpdateAlarmLed());
                }
                else
                {
                    UpdateAlarmLed();
                }

                // Refresh zone/channel information grids
                if (this.InvokeRequired)
                {
                    this.Invoke(async () => await RefreshCurrentInformationTab());
                }
                else
                {
                    _ = RefreshCurrentInformationTab();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling alarm cleared: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles alarm_updated broadcasts from FRMC.
        /// Refreshes the active alarms display.
        /// </summary>
        private void HandleAlarmUpdated(JsonElement json)
        {
            try
            {
                if (!json.TryGetProperty("Payload", out var payload))
                    return;

                int alarmId = payload.TryGetProperty("AlarmId", out var aidProp) ? aidProp.GetInt32() : -1;

                Console.WriteLine($"[FRCM] Alarm updated: AlarmId={alarmId}");

                // Refresh active alarms if the ActiveAlarm form is open
                if (this.InvokeRequired)
                {
                    this.Invoke(() => RefreshActiveAlarms());
                }
                else
                {
                    RefreshActiveAlarms();
                }

                // Refresh zone/channel information grids
                if (this.InvokeRequired)
                {
                    this.Invoke(async () => await RefreshCurrentInformationTab());
                }
                else
                {
                    _ = RefreshCurrentInformationTab();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling alarm updated: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles fiber_break_detected broadcasts from FRMC.
        /// Updates fiber break LED and refreshes active alarms display.
        /// </summary>
        private void HandleFiberBreakDetected(JsonElement json)
        {
            Console.WriteLine("FIBER BREAK DETECTED CALLED");
            try
            {
                if (!json.TryGetProperty("Payload", out var payload))
                    return;

                int channelId = payload.TryGetProperty("ChannelId", out var chProp) ? chProp.GetInt32() : -1;
                double breakPosition = payload.TryGetProperty("FiberBreakPosition", out var posProp) ? posProp.GetDouble() : 0;

                Console.WriteLine($"[FRCM] Fiber break detected: Channel={channelId}, Position={breakPosition}m");

                // Update channel state cache
                if (_channelStateCache.TryGetValue(channelId, out var existingState))
                {
                    // Update the cached state with fiber break info
                    dynamic ch = existingState;
                    var updatedState = new
                    {
                        ChannelId = channelId,
                        ConfiguredLength = ch.ConfiguredLength,
                        ActualLength = breakPosition,
                        FiberBroken = true,
                        LastUpdate = DateTime.Now.ToString("o"),
                        NumberOfZones = ch.NumberOfZones,
                        IsActive = ch.IsActive
                    };
                    _channelStateCache[channelId] = updatedState;
                }

                // Immediately add channel to fiber break tracking (separate from zone alarms)
                int frmcChannelId = channelId - 1;
                _channelsWithFiberBreak.Add(frmcChannelId);

                // Update safety LED immediately (fiber break triggers system alarm LED)
                if (this.InvokeRequired)
                {
                    this.Invoke(() => UpdateSafetyActiveAlarmLed());
                }
                else
                {
                    UpdateSafetyActiveAlarmLed();
                }

                // Auto-open Active Alarm form when fiber break detected
                if (this.InvokeRequired)
                {
                    this.Invoke(() => ShowActiveAlarmForm());
                }
                else
                {
                    ShowActiveAlarmForm();
                }

                // Refresh active alarms - fiber break will now appear as an alarm
                if (this.InvokeRequired)
                {
                    this.Invoke(() => RefreshActiveAlarms());
                }
                else
                {
                    RefreshActiveAlarms();
                }

                // Update fiber break LED if this is the selected channel
                if (_selectedChannelId == channelId && ledFiberBreak != null)
                {
                    if (this.InvokeRequired)
                    {
                        this.Invoke(() =>
                        {
                            ledFiberBreak.IsOn = true;
                            ledFiberBreak.Invalidate();
                        });
                    }
                    else
                    {
                        ledFiberBreak.IsOn = true;
                        ledFiberBreak.Invalidate();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling fiber break detected: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles fiber_break_cleared broadcasts from FRMC.
        /// Updates fiber break LED and refreshes active alarms display.
        /// </summary>
        private void HandleFiberBreakCleared(JsonElement json)
        {
            try
            {
                if (!json.TryGetProperty("Payload", out var payload))
                    return;

                int channelId = payload.TryGetProperty("ChannelId", out var chProp) ? chProp.GetInt32() : -1;

                Console.WriteLine($"[FRCM] Fiber break cleared: Channel={channelId}");

                // Update channel state cache
                if (_channelStateCache.TryGetValue(channelId, out var existingState))
                {
                    dynamic ch = existingState;
                    var updatedState = new
                    {
                        ChannelId = channelId,
                        ConfiguredLength = ch.ConfiguredLength,
                        ActualLength = ch.ConfiguredLength, // Restored to full length
                        FiberBroken = false,
                        LastUpdate = DateTime.Now.ToString("o"),
                        NumberOfZones = ch.NumberOfZones,
                        IsActive = ch.IsActive
                    };
                    _channelStateCache[channelId] = updatedState;
                }

                // Remove from fiber break tracking
                int frmcChannelId = channelId - 1;
                _channelsWithFiberBreak.Remove(frmcChannelId);

                // Update safety LED (may turn off if no other alarms)
                if (this.InvokeRequired)
                {
                    this.Invoke(() => UpdateSafetyActiveAlarmLed());
                }
                else
                {
                    UpdateSafetyActiveAlarmLed();
                }

                // Refresh active alarms - fiber break alarm should be cleared
                if (this.InvokeRequired)
                {
                    this.Invoke(() => RefreshActiveAlarms());
                }
                else
                {
                    RefreshActiveAlarms();
                }

                // Update fiber break LED if this is the selected channel
                if (_selectedChannelId == channelId && ledFiberBreak != null)
                {
                    if (this.InvokeRequired)
                    {
                        this.Invoke(() =>
                        {
                            ledFiberBreak.IsOn = false;
                            ledFiberBreak.Invalidate();
                        });
                    }
                    else
                    {
                        ledFiberBreak.IsOn = false;
                        ledFiberBreak.Invalidate();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling fiber break cleared: {ex.Message}");
            }
        }

        /// <summary>
        /// Refreshes the active alarms display by querying FRMC for current active alarms.
        /// </summary>
        private void RefreshActiveAlarms()
        {
            // Always request active alarms to update _channelsWithAlarm for LED indicators
            // This ensures fiber break and other alarms properly trigger safety LED even if form is closed
            RequestActiveAlarms();

            // Also update alarm indicators for currently selected channel/zone
            UpdateAlarmLed();
        }

        /// <summary>
        /// Shows the Active Alarm form, creating it if necessary, and brings it to the front.
        /// If the form is already visible, does nothing to prevent flickering/reopening.
        /// </summary>
        private void ShowActiveAlarmForm()
        {
            // Reset the flag whenever we show the form (manually or automatically)
            ActiveAlarmClosedByUser = false;

            if (ActiveAlarm.Instance == null || ActiveAlarm.Instance.IsDisposed)
            {
                ActiveAlarm.Instance = new ActiveAlarm(_wsClient, _configurationService);
                ActiveAlarm.Instance.ActiveChannelsChanged += OnActiveChannelsChanged;
                ActiveAlarm.Instance.ActiveFiberBreakChannelsChanged += OnActiveFiberBreakChannelsChanged;
                ActiveAlarm.Instance.AllActiveChannelsChanged += OnAllActiveChannelsChanged;
                ActiveAlarm.Instance.ActiveZonesChanged += OnActiveZonesChanged;
                ActiveAlarm.Instance.Show();
                ActiveAlarm.Instance.BringToFront();
            }
            else if (!ActiveAlarm.Instance.Visible)
            {
                // Only show if not already visible
                ActiveAlarm.Instance.Show();
                ActiveAlarm.Instance.BringToFront();
            }
            // If already visible, do nothing - prevents flickering/reopening
        }

        /// <summary>
        /// Enables or disables configuration controls based on measurement state.
        /// Configurations should be disabled when measurement is running.
        /// </summary>
        private void SetConfigurationControlsEnabled(bool enabled)
        {
            // Disable/Enable Configuration menu and its items
            Configuration.Enabled = enabled;
            channelZoneConfigurationToolStripMenuItem.Enabled = enabled;
            usbConfigurationToolStripMenuItem.Enabled = enabled;
            dtsCalibrationToolStripMenuItem.Enabled = enabled;
            temperatureCorrectionToolStripMenuItem.Enabled = enabled;
            loadConfigurationFileToolStripMenuItem.Enabled = enabled;
            saveConfigurationFileToolStripMenuItem.Enabled = enabled;
            iPConfigurationToolStripMenuItem.Enabled = enabled;

            // Disable/Enable System Configuration in Admin menu
            systemConfigurationToolStripMenuItem.Enabled = enabled;
        }

        protected override async void OnFormClosing(FormClosingEventArgs e)
        {
            // Stop LED update timer
            if (_ledUpdateTimer != null)
            {
                _ledUpdateTimer.Stop();
                _ledUpdateTimer.Dispose();
                _ledUpdateTimer = null;
            }

            // Stop alarm polling timer
            if (_alarmPollingTimer != null)
            {
                _alarmPollingTimer.Stop();
                _alarmPollingTimer.Dispose();
                _alarmPollingTimer = null;
            }

            // FIX: Unsubscribe from WebSocket events to prevent memory leaks
            _wsClient.MessageReceived -= OnWebSocketMessageReceived;

            // DUAL WEBSOCKET: Dispose data stream client
            if (_dataStreamClient != null)
            {
                _dataStreamClient.TemperatureDataReceived -= OnDataStreamTemperatureReceived;
                _dataStreamClient.ConnectionLost -= OnDataStreamConnectionLost;
                await _dataStreamClient.DisconnectAsync();
                _dataStreamClient.Dispose();
                _dataStreamClient = null;
            }

            // MULTI-THREADED: Dispose processors
            if (_temperatureProcessor != null)
            {
                _temperatureProcessor.Dispose();
                _temperatureProcessor = null;
            }

            if (_commandProcessor != null)
            {
                _commandProcessor.OnHealthStatus -= OnProcessedHealthStatus;
                _commandProcessor.OnZoneStateUpdate -= OnProcessedZoneStateUpdate;
                _commandProcessor.OnAlarmTriggered -= OnProcessedAlarmTriggered;
                _commandProcessor.OnAlarmCleared -= OnProcessedAlarmCleared;
                _commandProcessor.OnAlarmAutoCleared -= OnProcessedAlarmAutoCleared;
                _commandProcessor.OnActiveAlarmSnapshot -= OnProcessedActiveAlarmSnapshot;
                _commandProcessor.OnAllAlarmsCleared -= OnProcessedAllAlarmsCleared;
                _commandProcessor.OnHealthFaultDetected -= OnProcessedHealthFaultDetected;
                _commandProcessor.OnHealthFaultCleared -= OnProcessedHealthFaultCleared;
                _commandProcessor.OnChannelConfigUpdated -= OnProcessedChannelConfigUpdated;
                _commandProcessor.Dispose();
                _commandProcessor = null;
            }

            // FIX: Unsubscribe from ActiveAlarm events if instance exists
            if (ActiveAlarm.Instance != null)
            {
                ActiveAlarm.Instance.ActiveChannelsChanged -= OnActiveChannelsChanged;
                ActiveAlarm.Instance.ActiveZonesChanged -= OnActiveZonesChanged;
            }

            // FM-05: Stop Dual-Port Health Watchdog
            StopDualPortHealthWatchdog();

            await _wsClient.DisconnectAsync();
            base.OnFormClosing(e);
        }

        private void InitializeChannelTree()
        {
            Panel scrollablePanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BorderStyle = BorderStyle.None
            };

            treeViewZones = new TreeView
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10),
                HideSelection = false,
                ShowLines = true,
                ShowRootLines = true,
                ShowPlusMinus = true
            };

            groupBoxChannelSelection.Controls.Clear();
            groupBoxChannelSelection.Controls.Add(scrollablePanel);
            scrollablePanel.Controls.Add(treeViewZones);

            treeViewZones.AfterSelect += TreeViewZones_AfterSelect;

            PopulateChannelNodes();
        }


        private void SelectDefaultChannel()
        {
            if (treeViewZones == null || treeViewZones.Nodes.Count == 0)
                return;

            // Select Channel Information tab BEFORE setting the node so that
            // TreeViewZones_AfterSelect sees wasOnAnyInfoTab=true and keeps the user
            // on the Channel Information tab (where the LEDs are visible).
            if (!tabControlDisplay.TabPages.Contains(tabChannelInformation))
                tabControlDisplay.TabPages.Add(tabChannelInformation);
            tabControlDisplay.SelectedTab = tabChannelInformation;

            TreeNode ch1 = treeViewZones.Nodes[0];
            treeViewZones.SelectedNode = ch1;
            ch1.EnsureVisible();
            ch1.Expand();
        }

        private void PopulateChannelNodes()
        {
            if (treeViewZones == null) return;

            treeViewZones.BeginUpdate();
            treeViewZones.Nodes.Clear();

            var allChannels = _configurationService.GetChannels();
            var allZones = _configurationService.GetZones();

            foreach (var kvp in allChannels
                .Where(c => c.Value.IsEnabled)
                .OrderBy(c => c.Value.ChannelId))
            {
                string channelName = kvp.Key;
                var channelNode = new TreeNode(channelName) { Tag = channelName };

                if (allZones.TryGetValue(channelName, out var zones) && zones.Count > 0)
                {
                    foreach (var zone in zones)
                    {
                        if (!zone.Enabled) continue;

                        string zoneText =
                            $"{zone.Name}  [Start: {zone.StartPoint}, End: {zone.EndPoint}]";

                        channelNode.Nodes.Add(new TreeNode(zoneText) { Tag = zone });
                    }
                }

                treeViewZones.Nodes.Add(channelNode);
            }

            treeViewZones.EndUpdate();
        }


        private void PopulateZoneStateGrid(List<ZoneStateInfo> zones)
        {
            Console.WriteLine($"[PopulateZoneStateGrid] Called with {zones.Count} zones, _selectedChannelId={_selectedChannelId}");

            if (dataGridViewZone.InvokeRequired)
            {
                Console.WriteLine($"[PopulateZoneStateGrid] Invoking on UI thread");
                dataGridViewZone.Invoke(new Action(() => PopulateZoneStateGrid(zones)));
                return;
            }

            Console.WriteLine($"[PopulateZoneStateGrid] Clearing grid, building {zones.Count} rows...");

            // Get zone configurations to display correct zone names
            var channelKey = $"Channel {_selectedChannelId}";
            var zoneConfigs = _configurationService.GetZoneConfig(channelKey);

            // Suspend painting to avoid per-row layout recalculation (critical for 100+ zones)
            dataGridViewZone.SuspendLayout();
            try
            {
                dataGridViewZone.Rows.Clear();

                // Pre-cache fonts to avoid creating new Font objects per row
                var boldFont = new System.Drawing.Font(dataGridViewZone.Font, System.Drawing.FontStyle.Bold);

                // Build all rows in memory first, then add in one batch
                var rows = new DataGridViewRow[zones.Count];
                for (int i = 0; i < zones.Count; i++)
                {
                    var z = zones[i];
                    int zoneId = z.ZoneId; // Use the actual ZoneId

                    string zoneName = $"Zone {zoneId}";
                    if (zoneConfigs != null)
                    {
                        var zoneConfig = zoneConfigs.FirstOrDefault(zc => zc.ZoneId == zoneId);
                        if (zoneConfig != null && !string.IsNullOrWhiteSpace(zoneConfig.Name))
                            zoneName = zoneConfig.Name;
                    }

                    var row = new DataGridViewRow();
                    row.CreateCells(dataGridViewZone,
                        zoneId, zoneName,
                        z.StartPoint, z.EndPoint,
                        z.ActiveAlarm, z.Enabled,
                        z.MaxTemperature, z.MinTemperature,
                        z.RateOfRise, z.AverageTemperature,
                        z.Deviation);

                    row.DefaultCellStyle.ForeColor = DrawingColor.Black;

                    // Alarm styling on specific cells
                    if (z.MaxAlarm || z.PreAlarm)
                    {
                        row.Cells[6].Style.ForeColor = DrawingColor.Red;
                        row.Cells[6].Style.Font = boldFont;
                    }
                    if (z.MinAlarm)
                    {
                        row.Cells[7].Style.ForeColor = DrawingColor.Red;
                        row.Cells[7].Style.Font = boldFont;
                    }
                    if (z.RorAlarm)
                    {
                        row.Cells[8].Style.ForeColor = DrawingColor.Red;
                        row.Cells[8].Style.Font = boldFont;
                    }
                    if (z.DeviationAlarm)
                    {
                        row.Cells[10].Style.ForeColor = DrawingColor.Red;
                        row.Cells[10].Style.Font = boldFont;
                    }

                    rows[i] = row;
                }

                // Single batch add — one layout pass instead of N
                dataGridViewZone.Rows.AddRange(rows);
            }
            finally
            {
                dataGridViewZone.ResumeLayout(true);
            }

            Console.WriteLine($"[PopulateZoneStateGrid] Added {dataGridViewZone.Rows.Count} rows to grid");
        }

        private async Task LoadZoneStateAndPopulateGrid(int uiChannelId)
        {
            if (!isMonitoring)
                return;
            Console.WriteLine($"[LoadZoneState] Loading zone state for channel {uiChannelId}, isMonitoring={isMonitoring}");

            try
            {
                // Get zone configurations to filter what we display
                var channelKey = $"Channel {uiChannelId}";
                var zoneConfigs = _configurationService.GetZoneConfig(channelKey);
                var validZoneIds = zoneConfigs?.Select(zc => zc.ZoneId).ToHashSet() ?? new HashSet<int>();

                // First show cached data for instant display (if available)
                var cachedZones = _zoneStateCache
                    .Where(kvp => kvp.Key.channelId == uiChannelId && validZoneIds.Contains(kvp.Key.zoneId))
                    .OrderBy(kvp => kvp.Key.zoneId)
                    .Select(kvp => kvp.Value)
                    .ToList();

                Console.WriteLine($"[LoadZoneState] Cache has {cachedZones.Count} zones for channel {uiChannelId}");

                if (cachedZones.Count > 0)
                {
                    PopulateZoneStateGrid(cachedZones);
                }

                // Skip fresh fetch if monitoring is not active
                if (!isMonitoring)
                {
                    Console.WriteLine("[LoadZoneState] Monitoring inactive - skipping fresh fetch from FRMC");
                    return;
                }

                // Always fetch fresh zone state from FRMC (even when not monitoring)
                // This ensures the grid shows zone configuration with latest values
                var zoneStates = await _ChannelClient.GetZoneStateAsync(uiChannelId);
                Console.WriteLine($"[LoadZoneState] API returned {zoneStates.Count} zones for channel {uiChannelId}");

                if (zoneStates.Count > 0)
                {
                    // Purge stale cache entries for this channel before repopulating
                    // This ensures deleted zones don't linger in the cache
                    var freshZoneIds = new HashSet<int>(zoneStates.Select(z => z.ZoneId));
                    var staleKeys = _zoneStateCache.Keys
                        .Where(k => k.channelId == uiChannelId && !freshZoneIds.Contains(k.zoneId))
                        .ToList();
                    foreach (var key in staleKeys)
                    {
                        _zoneStateCache.Remove(key);
                    }
                    if (staleKeys.Count > 0)
                    {
                        Console.WriteLine($"[LoadZoneState] Purged {staleKeys.Count} stale zone cache entries for channel {uiChannelId}");
                    }

                    // Update cache with fresh data
                    foreach (var zone in zoneStates)
                    {
                        _zoneStateCache[(uiChannelId, zone.ZoneId)] = zone;
                    }

                    PopulateZoneStateGrid(zoneStates);
                }
                else if (cachedZones.Count == 0)
                {
                    // No data from API and no cache - try to show zone config without temperature data
                    if (zoneConfigs != null && zoneConfigs.Count > 0)
                    {
                        // Create placeholder zone states from config
                        var placeholderZones = zoneConfigs.Select((cfg, idx) => new ZoneStateInfo
                        {
                            ZoneId = cfg.ZoneId,
                            Name = cfg.Name ?? $"Zone {cfg.ZoneId}",
                            StartPoint = cfg.StartPoint,
                            EndPoint = cfg.EndPoint,
                            Enabled = cfg.Enabled,
                            ActiveAlarm = false,
                            AverageTemperature = 0,
                            MaxTemperature = 0,
                            MinTemperature = 0,
                            RateOfRise = 0,
                            Deviation = 0,
                            LastUpdate = DateTime.Now
                        }).ToList();

                        PopulateZoneStateGrid(placeholderZones);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error loading zone state: {ex.Message}");

                // On error, still try to show zone configuration as fallback
                var zoneConfigs = _configurationService.GetZoneConfig($"Channel {uiChannelId}");
                if (zoneConfigs != null && zoneConfigs.Count > 0)
                {
                    var placeholderZones = zoneConfigs.Select((cfg, idx) => new ZoneStateInfo
                    {
                        ZoneId = cfg.ZoneId,
                        Name = cfg.Name ?? $"Zone {cfg.ZoneId}",
                        StartPoint = cfg.StartPoint,
                        EndPoint = cfg.EndPoint,
                        Enabled = cfg.Enabled,
                        ActiveAlarm = false,
                        AverageTemperature = 0,
                        MaxTemperature = 0,
                        MinTemperature = 0,
                        RateOfRise = 0,
                        Deviation = 0,
                        LastUpdate = DateTime.Now
                    }).ToList();

                    PopulateZoneStateGrid(placeholderZones);
                }
            }
        }
        /// <summary>
        /// Refreshes the currently displayed information tab (Channel Info or Zone Info)
        /// when alarm states change. This ensures the displayed data stays current.
        /// </summary>
        private async Task RefreshCurrentInformationTab()
        {
            try
            {
                // Check if Channel Information tab is currently selected and monitoring is active
                if (tabControlDisplay.SelectedTab == tabChannelInformation && _selectedChannelId != -1 && isMonitoring)
                {
                    await LoadZoneStateAndPopulateGrid(_selectedChannelId);
                }
                // Check if Zone Information tab is currently selected and monitoring is active
                else if (tabControlDisplay.SelectedTab == tabZoneInformation &&
                         _selectedChannelId != -1 && _selectedZoneId != -1 && isMonitoring)
                {
                    // Update zone indicators (alarm LEDs, enable status)
                    // Note: UpdateZoneInfoIndicators expects 0-based FRMC IDs
                    UpdateZoneInfoIndicators(_selectedChannelId - 1, _selectedZoneId - 1);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error refreshing information tab: {ex.Message}");
            }
        }


        private void PopulateZonePointsGrid(List<PointData> points)
        {
            if (dataGridView1.InvokeRequired)
            {
                dataGridView1.Invoke(new Action(() => PopulateZonePointsGrid(points)));
                return;
            }

            // Ensure columns exist (VirtualMode still needs column definitions)
            if (dataGridView1.Columns.Count != 2 ||
                dataGridView1.Columns[0].Name != "Position" ||
                dataGridView1.Columns[1].Name != "Temperature")
            {
                dataGridView1.Columns.Clear();

                dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "Position",
                    HeaderText = "Point number",
                    ValueType = typeof(double),
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    DefaultCellStyle = new DataGridViewCellStyle { Format = "0.###" }
                });

                dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "Temperature",
                    HeaderText = "Point Temperature",
                    ValueType = typeof(double),
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
                });
            }

            // ✅ Apply correction length so grid matches graph

            // VirtualMode: just swap the backing store and set RowCount — renders only visible rows
            _gridPointsCache = points;
            dataGridView1.RowCount = 0; // force reset
            dataGridView1.RowCount = points.Count;
            dataGridView1.Invalidate();
        }

        private void DataGridView1_CellValueNeeded(object? sender, DataGridViewCellValueEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _gridPointsCache.Count)
                return;

            var pt = _gridPointsCache[e.RowIndex];
            e.Value = e.ColumnIndex switch
            {
                0 => Math.Round(pt.Position, 3),
                1 => pt.Temperature,
                _ => null
            };
        }

        private async void TreeViewZones_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            if (e.Node == null)
                return;

            // Suspend layout to prevent flicker during tab changes
            tabControlDisplay.SuspendLayout();

            string selectedText = e.Node.Text.ToLower();

            // Remember which tab was selected before we change anything
            TabPage? previouslySelectedTab = tabControlDisplay.SelectedTab;
            bool wasOnAnyInfoTab = (previouslySelectedTab == tabChannelInformation ||
                                    previouslySelectedTab == tabZoneInformation);

            try
            {
                // Ensure graph tab exists
                if (!tabControlDisplay.TabPages.Contains(openGraphToolStripMenuItem))
                    tabControlDisplay.TabPages.Add(openGraphToolStripMenuItem);

                // =========================
                // NO ZONES CONFIGURED
                // =========================
                if (selectedText.Contains("no zone", StringComparison.OrdinalIgnoreCase))
                {
                    // Unsubscribe from previous channel (if any)
                    if (_subscribedChannelId > 0)
                    {
                        await UnsubscribeFromChannelAsync(_subscribedChannelId);
                    }

                    // Clear selection
                    _selectedChannelId = -1;
                    _selectedZoneId = -1;
                    _selectedZoneStartPosition = null;
                    _selectedZoneEndPosition = null;

                    // Remove information tabs
                    if (tabControlDisplay.TabPages.Contains(tabChannelInformation))
                        tabControlDisplay.TabPages.Remove(tabChannelInformation);
                    if (tabControlDisplay.TabPages.Contains(tabZoneInformation))
                        tabControlDisplay.TabPages.Remove(tabZoneInformation);

                    // Show graph tab and clear it
                    tabControlDisplay.SelectedTab = openGraphToolStripMenuItem;

                    // Clear graph data and initialize empty graph
                    InitializeGraph("No Zone Configured");

                    // Clear zone information grid
                    _gridPointsCache.Clear();
                    dataGridView1.RowCount = 0;

                    UpdateStatus("No zones configured for this channel");
                    return;
                }

                // =========================
                // CHANNEL SELECTED
                // =========================
                if (selectedText.StartsWith("Channel", StringComparison.OrdinalIgnoreCase))
                {
                    int channelId = int.Parse(selectedText.Substring("Channel".Length).Trim());

                    UpdateChannelEnableIndicator(channelId);

                    _selectedChannelId = channelId;
                    _selectedZoneId = -1;
                    _selectedZoneStartPosition = null;
                    _selectedZoneEndPosition = null;
                    _liveScatter = null; // Reset scatter so it gets recreated with new title

                    // Set graph title based on whether live streaming is active
                    string graphTitle = isPlottingEnabled
                        ? $"Live Data - Channel {channelId}"
                        : $"Channel {channelId}";

                    // Initialize graph with new title and restored cached data immediately (NO WAIT)
                    InitializeGraph(graphTitle);

                    // Subscribe to new channel for live data (unsubscribes from previous if different)
                    if (channelId != _subscribedChannelId)
                    {
                        if (_subscribedChannelId > 0)
                        {
                            await UnsubscribeFromChannelAsync(_subscribedChannelId);
                        }
                        await SubscribeToChannelAsync(channelId);
                    }

                    // Restore channel-specific axis limits if previously set
                    if (_channelAxisLimits.TryGetValue(channelId, out var savedLimits))
                    {
                        _userAxisApplied = true;
                        _userXMin = savedLimits.xMin;
                        _userXMax = savedLimits.xMax;
                        _userYMin = savedLimits.yMin;
                        _userYMax = savedLimits.yMax;
                        _isXAxisFixed = false;
                        _isYAxisFixed = false;
                    }
                    else
                    {
                        // No saved limits for this channel - use defaults
                        _userAxisApplied = false;
                        _isXAxisFixed = true;
                        _isYAxisFixed = true;
                    }

                    // Reset zone LEDs and their delta-tracking state
                    if (ledZoneEnabled != null) ledZoneEnabled.IsOn = false;
                    if (ledZoneAlarm != null) ledZoneAlarm.IsOn = false;
                    _lastZoneEnabledState = false;
                    _lastZoneAlarmState = false;

                    RequestActiveAlarms();

                    // ✅ Channel LED logic ONLY
                    UpdateAlarmLed();

                    _currentChannelToDisplayId = channelId;

                    // Update channel name label
                    lblChannelName.Text = $"Channel {channelId}";

                    // Add new tab BEFORE removing old one to prevent flicker
                    if (!tabControlDisplay.TabPages.Contains(tabChannelInformation))
                        tabControlDisplay.TabPages.Add(tabChannelInformation);

                    // Set selected tab to destination BEFORE removing others
                    if (wasOnAnyInfoTab)
                    {
                        tabControlDisplay.SelectedTab = tabChannelInformation;
                    }
                    else
                    {
                        tabControlDisplay.SelectedTab = openGraphToolStripMenuItem;
                    }

                    // Now remove the other info tab if present
                    if (tabControlDisplay.TabPages.Contains(tabZoneInformation))
                        tabControlDisplay.TabPages.Remove(tabZoneInformation);

                    if (isMonitoring)
                    {
                        await LoadZoneStateAndPopulateGrid(channelId);
                    }

                    if (isPlottingEnabled)
                    {
                        Console.WriteLine($"[FRCM] Live streaming active - switched to channel {channelId}, waiting for live data");
                    }
                    else
                    {
                        await LoadGraphDataForSelection();
                    }

                    // Update axis text boxes with channel-specific values
                    UpdateAxisTextBoxesForChannel();

                    return;
                }

                // =========================
                // ZONE SELECTED
                // =========================
                if (!selectedText.Contains("no zones configured"))
                {
                    string parentText = e.Node.Parent.Text;
                    int channelId = int.Parse(parentText.Substring("Channel".Length).Trim());

                    if (e.Node.Tag is ZoneInfo zoneInfo)
                    {
                        int zoneId = zoneInfo.ZoneId;

                        _selectedChannelId = channelId;
                        _selectedZoneId = zoneId;
                        _liveScatter = null;

                        // 🔥 ALWAYS RESET BOUNDARIES
                        _selectedZoneStartPosition = zoneInfo.StartPoint;
                        _selectedZoneEndPosition = zoneInfo.EndPoint;

                        // ✅ Reset user axis settings when switching to a new zone
                        // This prevents old zoom/limits from Zone 1 persisting for other zones
                        _userAxisApplied = false;
                        _isXAxisFixed = true;
                        _isYAxisFixed = true;

                        Console.WriteLine(
                            $"Zone Selected: CH={_selectedChannelId}, " +
                            $"Z={_selectedZoneId}, " +
                            $"Start={_selectedZoneStartPosition}, " +
                            $"End={_selectedZoneEndPosition}");

                        string zoneName = GetZoneName(channelId, zoneId);
                        string graphTitle = isMonitoring
                            ? $"Live Data - {zoneName}"
                            : $"{zoneName} (Channel {channelId})";

                        InitializeGraph(graphTitle);
                        UpdateAxisTextBoxes(); // Sync UI textboxes with new zone limits

                        // Subscribe for live data if needed
                        if (channelId != _subscribedChannelId)
                        {
                            if (_subscribedChannelId > 0)
                                await UnsubscribeFromChannelAsync(_subscribedChannelId);

                            await SubscribeToChannelAsync(channelId);
                        }

                        RequestActiveAlarms();
                        UpdateZoneInfoIndicators(channelId - 1, zoneId - 1);
                        lblZoneName.Text = $"{zoneName} (Ch{channelId})";

                        if (!tabControlDisplay.TabPages.Contains(tabZoneInformation))
                            tabControlDisplay.TabPages.Add(tabZoneInformation);

                        if (wasOnAnyInfoTab)
                            tabControlDisplay.SelectedTab = tabZoneInformation;
                        else
                            tabControlDisplay.SelectedTab = openGraphToolStripMenuItem;

                        if (tabControlDisplay.TabPages.Contains(tabChannelInformation))
                            tabControlDisplay.TabPages.Remove(tabChannelInformation);

                        bool isZeroLength = false;
                        if (_selectedZoneStartPosition.HasValue && _selectedZoneEndPosition.HasValue)
                        {
                            isZeroLength =
                                Math.Abs(_selectedZoneEndPosition.Value -
                                         _selectedZoneStartPosition.Value) < 0.01;
                        }

                        if (isZeroLength)
                        {
                            _gridPointsCache.Clear();
                            dataGridView1.RowCount = 0;

                            if (!isMonitoring)
                                UpdateStatus($"Zone {zoneId} has zero length");

                            return;
                        }


                        if (isMonitoring)
                        {
                            var points = await _ChannelClient
                                .GetZonePointsAsync(channelId, zoneId);

                            PopulateZonePointsGrid(points);

                            if (points != null && points.Count > 0)
                            {
                                var temperaturePoints = points
                                    .Select(p => new TemperatureDataPoint(
                                        p.Temperature,
                                        p.Position))
                                    .ToList();

                                UpdateGraph(temperaturePoints);
                            }
                        }

                        else
                        {
                            _gridPointsCache.Clear();
                            dataGridView1.RowCount = 0;

                            // 🔥 Instead of trusting zone cache,
                            // filter from channel cache using corrected boundaries
                            if (_channelGraphCache.TryGetValue(channelId, out var channelData))
                            {
                                var filtered = channelData.distances
                                    .Zip(channelData.temperatures, (d, t) => new { d, t })
                                    .Where(p =>
                                         p.d >= _selectedZoneStartPosition &&
                                         p.d <= _selectedZoneEndPosition);

                                var temperaturePoints = filtered
                                    .Select(p => new TemperatureDataPoint(p.t, p.d)) // Add back correction for UpdateGraph which subtracts it
                                    .ToList();

                                UpdateGraph(temperaturePoints);

                                var gridPoints = filtered
                                    .Select(p => new PointData
                                    {
                                        Position = p.d,
                                        Temperature = p.t
                                    })
                                    .ToList();

                                PopulateZonePointsGrid(gridPoints);
                            }
                            else
                            {
                                UpdateStatus($"Zone {zoneId}: No cached channel data available");
                            }
                        }
                    }

                    else
                    {
                        MessageBox.Show(
                            "Error: Zone data not found. Please refresh the tree view.",
                            "Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        return;
                    }
                }

                // Resume layout now that all tab changes are complete
                tabControlDisplay.ResumeLayout(performLayout: true);
            }
            finally
            {
                // Ensure layout is always resumed even if an error occurs
                if (tabControlDisplay.IsHandleCreated)
                {
                    try
                    {
                        tabControlDisplay.ResumeLayout(performLayout: true);
                    }
                    catch { }
                }
            }
        }


        private void SetGraphTitle(string title)
        {
            _currentGraphTitle = title;
            if (lblGraphTitle != null)
            {
                if (this.InvokeRequired)
                {
                    this.Invoke(() => lblGraphTitle.Text = title);
                }
                else
                {
                    lblGraphTitle.Text = title;
                }
            }

            // Always ensure plot title is empty to avoid overlap
            if (formsPlot1 != null && formsPlot1.Plot != null)
            {
                formsPlot1.Plot.Axes.Title.Label.Text = "";
            }
        }
        private void InitializeGraph(string title = "Live Temperature Data")
        {
            SetGraphTitle(title);

            // Clear plot immediately - data will be restored from cache if available
            formsPlot1.Plot.Clear();
            _liveScatter = null;

            // Reset throttle so first packet after switch is processed immediately
            _lastGraphUpdate = DateTime.MinValue;

            // PERSISTENCE: Restore cached data for this selection if it exists
            bool dataRestored = false;
            if (_selectedZoneId > 0)
            {
                if (_zoneGraphCache.TryGetValue((_selectedChannelId, _selectedZoneId), out var cached))
                {
                    distances = new List<double>(cached.distances);
                    temperatures = new List<double>(cached.temperatures);
                    dataRestored = true;
                }
            }
            else if (_selectedChannelId > 0)
            {
                if (_channelGraphCache.TryGetValue(_selectedChannelId, out var cached))
                {
                    distances = new List<double>(cached.distances);
                    temperatures = new List<double>(cached.temperatures);
                    dataRestored = true;
                }
            }

            if (!dataRestored)
            {
                // No cached data: Clear lists for a fresh start
                distances.Clear();
                temperatures.Clear();
                _traceCache.Clear();
            }
            else
            {
                _traceCache.Update(distances, temperatures);
            }

            // Add temperature graph
            _liveScatter = formsPlot1.Plot.Add.Scatter(_traceCache.FrontBuffer.Distances, _traceCache.FrontBuffer.Temperatures);
            _liveScatter.Color = ScottPlot.Colors.Blue;
            _liveScatter.LineWidth = 2;
            _liveScatter.MarkerSize = 0;


            // Calculate DPI scaling for the graph elements
            float scalingFactor = this.DeviceDpi / 96f;

            // Scale Plot Labels for 55-inch screens
            var titleStyle = formsPlot1.Plot.Axes.Title;
            titleStyle.Label.Text = "";
            titleStyle.Label.FontSize = 16 * scalingFactor;
            titleStyle.Label.Bold = true;

            var xLabelStyle = formsPlot1.Plot.Axes.Bottom.Label;
            xLabelStyle.Text = "Distance (m)";
            xLabelStyle.FontSize = 12 * scalingFactor;
            xLabelStyle.Bold = true;

            var yLabelStyle = formsPlot1.Plot.Axes.Left.Label;
            yLabelStyle.Text = "Temperature (°C)";
            yLabelStyle.FontSize = 12 * scalingFactor;
            yLabelStyle.Bold = true;

            // Scale Axis Ticks (the numbers on the axis)
            formsPlot1.Plot.Axes.Bottom.TickLabelStyle.FontSize = 10 * scalingFactor;
            formsPlot1.Plot.Axes.Left.TickLabelStyle.FontSize = 10 * scalingFactor;

            // Respect user-applied axis limits, otherwise auto-scale
            if (_userAxisApplied)
            {
                formsPlot1.Plot.Axes.SetLimitsX(_userXMin, _userXMax);
                formsPlot1.Plot.Axes.SetLimitsY(_userYMin, _userYMax);
            }
            else
            {
                bool hasZoneFilter =
                    _selectedZoneId > 0 &&
                    _selectedZoneStartPosition.HasValue &&
                    _selectedZoneEndPosition.HasValue;

                if (hasZoneFilter)
                {
                    formsPlot1.Plot.Axes.SetLimitsX(
                        _selectedZoneStartPosition.Value,
                        _selectedZoneEndPosition.Value);
                }
                else if (_isXAxisFixed)
                {
                    double channelLength = GetChannelLengthForSelectedChannel();
                    double correctionLength = GetCorrectionLengthForSelectedChannel();

                    if (channelLength > 0)
                    {
                        formsPlot1.Plot.Axes.SetLimitsX(
                            -correctionLength,
                            channelLength);
                    }
                }
                if(_isYAxisFixed)
                {
                    formsPlot1.Plot.Axes.SetLimitsY(
                        DEFAULT_Y_MIN,
                        DEFAULT_Y_MAX);
                }
                else
                {
                    formsPlot1.Plot.Axes.AutoScale();
                }
            }
            // IMPORTANT:
            // Recreate vertical line AFTER graph + axis setup
            markerLine = formsPlot1.Plot.Add.VerticalLine(_markerLineX);
            markerLine.Color = Colors.Red;
            markerLine.LineWidth = 2;
            markerLine.IsVisible = true;

            // Refresh graph
            formsPlot1.Refresh();
        }




        private void UpdateGraph(List<TemperatureDataPoint> dataPoints)
        {
            if (dataPoints == null || dataPoints.Count == 0)
                return;

            if (!isPlottingEnabled || _selectedChannelId <= 0)
                return;

            bool isZoneSelected =
         _selectedZoneId > 0
                        && _selectedZoneStartPosition.HasValue
                && _selectedZoneEndPosition.HasValue;

            List<double> x = new();
            List<double> y = new();

            if (isZoneSelected)
            {
                double zoneStart =
                    _selectedZoneStartPosition.Value;

                double zoneEnd =
                    _selectedZoneEndPosition.Value;

                var filtered = dataPoints
                    .Where(p =>
                        p.Position >= zoneStart &&
                        p.Position <= zoneEnd)
                    .OrderBy(p => p.Position)
                    .ToList();

                // Ignore bad updates
                if (filtered.Count < 5)
                    return;

                double min = filtered.First().Position;
                double max = filtered.Last().Position;

                // Ensure valid zone data
                if (min < zoneStart || max > zoneEnd)
                    return;

                x = filtered
                    .Select(p => p.Position)
                    .ToList();

                y = filtered
                    .Select(p => p.Temperature)
                    .ToList();

                // Update class fields for MouseMove to work
                distances = x;
                temperatures = y;

                _zoneGraphCache[
                    (_selectedChannelId, _selectedZoneId)
                ] = (x, y);
            }

            else
            {
                double correction =
                    GetCorrectionLengthForSelectedChannel();

                x = dataPoints
                    .Select(p => p.Position - correction)
                    .ToList();

                y = dataPoints
                    .Select(p => p.Temperature)
                    .ToList();

                // Update class fields for MouseMove to work
                distances = x;
                temperatures = y;

                _channelGraphCache[
                    _selectedChannelId
                ] = (x, y);
            }

            // FM-07: Double-buffered snapshot promote
            var snapshot = _traceCache.Update(x, y);

            // =========================================
            // REMOVE ONLY OLD SCATTER
            // DO NOT CLEAR THE PLOT
            // =========================================

            if (_liveScatter != null)
            {
                formsPlot1.Plot.Remove(_liveScatter);
            }

            // =========================================
            // ADD NEW SCATTER
            // =========================================

            _liveScatter = formsPlot1.Plot.Add.Scatter(
                snapshot.Distances,
                snapshot.Temperatures);

            _liveScatter.Color = ScottPlot.Colors.Blue;
            _liveScatter.LineWidth = 2;
            _liveScatter.MarkerSize = 0;

            // =========================================
            // CREATE MARKER ONLY ONCE
            // =========================================

            if (markerLine == null)
            {
                markerLine =
                    formsPlot1.Plot.Add.VerticalLine(
                        _markerLineX);

                markerLine.Color =
                    ScottPlot.Colors.Red;

                markerLine.LineWidth = 2;
            }

            // Keep marker alive
            markerLine.X = _markerLineX;

            // =========================================
            // AXIS SETTINGS
            // =========================================


            if (isZoneSelected)
            {
                formsPlot1.Plot.Axes.SetLimitsX(
                    _selectedZoneStartPosition!.Value,
                    _selectedZoneEndPosition!.Value);

                if (_isYAxisFixed)
                {
                    formsPlot1.Plot.Axes.SetLimitsY(
                        DEFAULT_Y_MIN,
                        DEFAULT_Y_MAX);
                }

            }
            else
            {
                double correction =
                    GetCorrectionLengthForSelectedChannel();

                double length =
                    GetChannelLengthForSelectedChannel();

                if (_isXAxisFixed && length > 0)
                {
                    formsPlot1.Plot.Axes.SetLimitsX(
                        -correction,
                        length);
                }

                if (_isYAxisFixed)
                {
                    formsPlot1.Plot.Axes.SetLimitsY(
                        DEFAULT_Y_MIN,
                        DEFAULT_Y_MAX);
                }
            }

            formsPlot1.Refresh();
        }

        private async Task LoadGraphDataForSelection()
        {
            try
            {

                if (!isMonitoring)
                {
                    if (_selectedZoneId > 0 && _selectedChannelId > 0)
                    {
                        // Try zone cache first
                        if (_zoneGraphCache.TryGetValue((_selectedChannelId, _selectedZoneId), out var zoneData))
                        {
                            UpdateGraphFromWebSocket(
                                zoneData.distances.ToArray(),
                                zoneData.temperatures.ToArray());
                        }
                        else
                        {
                            // 🔥 No cache → Load fresh snapshot from FRMC
                            await LoadZoneDataForGraph(_selectedChannelId, _selectedZoneId);
                        }
                    }
                    else if (_selectedChannelId > 0)
                    {
                        // Try channel cache first
                        if (_channelGraphCache.TryGetValue(_selectedChannelId, out var channelData))
                        {
                            UpdateGraphFromWebSocket(
                                channelData.distances.ToArray(),
                                channelData.temperatures.ToArray());
                        }
                        else
                        {
                            // 🔥 No cache → Load fresh snapshot from FRMC
                            await LoadChannelDataForGraph(_selectedChannelId);
                        }
                    }
                    else
                    {
                        formsPlot1.Plot.Clear();
                        formsPlot1.Refresh();
                    }

                    return;
                }

                // ================================
                // ▶ LIVE MEASUREMENT MODE
                // ================================
                if (_selectedZoneId > 0)
                {
                    await LoadZoneDataForGraph(_selectedChannelId, _selectedZoneId);
                }
                else if (_selectedChannelId > 0)
                {
                    await LoadChannelDataForGraph(_selectedChannelId);
                }
                else
                {
                    Console.WriteLine("No channel or zone selected for graph");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading graph data: {ex.Message}");
                UpdateStatus($"Error loading graph data: {ex.Message}");
            }
        }
        private async Task LoadChannelDataForGraph(int channelId)
        {

            if (!isMonitoring)
                return;

            try
            {
                Console.WriteLine($"Loading channel {channelId} data for graph (one-time query)");

                var allPoints = await _ChannelClient.GetChannelPointsAsync(channelId);

                if (allPoints == null || allPoints.Count == 0)
                {
                    Console.WriteLine($"No temperature data available for channel {channelId}");
                    UpdateStatus($"Channel {channelId}: No temperature data available");

                    if (InvokeRequired)
                    {
                        Invoke(new Action(() =>
                        {
                            formsPlot1.Plot.Clear();
                            formsPlot1.Plot.Title($"Channel {channelId}: No data available");
                            formsPlot1.Plot.XLabel("Distance (m)");
                            formsPlot1.Plot.YLabel("Temperature (°C)");
                            formsPlot1.Refresh();
                        }));
                    }
                    else
                    {
                        formsPlot1.Plot.Clear();
                        formsPlot1.Plot.Title($"Channel {channelId}: No data available");
                        formsPlot1.Plot.XLabel("Distance (m)");
                        formsPlot1.Plot.YLabel("Temperature (°C)");
                        formsPlot1.Refresh();
                    }
                    return;
                }

                var temperaturePoints = allPoints
                    .GroupBy(p => p.Position)
                    .Select(g => g.First())
                    .OrderBy(p => p.Position)
                    .Select(p => new TemperatureDataPoint(p.Temperature, p.Position))
                    .ToList();

                UpdateGraph(temperaturePoints);

                Console.WriteLine($"Loaded {temperaturePoints.Count} temperature points for channel {channelId}");
                UpdateStatus($"Channel {channelId}: Loaded {temperaturePoints.Count} points");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading channel data for graph: {ex.Message}");
                UpdateStatus($"Error loading channel data: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads temperature data for specific zone
        /// </summary>

        private async Task LoadZoneDataForGraph(int channelId, int zoneId)
        {

            //if (!isMonitoring)
            //    return;

            try
            {
                Console.WriteLine($"Loading zone {zoneId} on channel {channelId} data for graph (one-time query)");

                var points = await _ChannelClient.GetZonePointsAsync(channelId, zoneId);

                if (points != null && points.Count > 0)
                {
                    var temperaturePoints = points
                        .Select(p => new TemperatureDataPoint(p.Temperature, p.Position))
                        .ToList();

                    UpdateGraph(temperaturePoints);

                    Console.WriteLine($"Loaded {temperaturePoints.Count} temperature points for zone {zoneId}");
                    UpdateStatus($"Zone {zoneId}: Loaded {temperaturePoints.Count} points");
                }
                else
                {
                    Console.WriteLine($"No temperature data available for zone {zoneId}");
                    UpdateStatus($"Zone {zoneId}: No data available");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading zone data for graph: {ex.Message}");
                UpdateStatus($"Error loading zone data: {ex.Message}");
            }
        }


        private async void btnStartMeasurement_Click(object? sender, EventArgs e)
        {
            Console.WriteLine($"[BTN] btnStartMeasurement_Click: _selectedChannelId={_selectedChannelId}, _subscribedChannelId={_subscribedChannelId}, isMonitoring={isMonitoring}");

            try
            {
                isMonitoring = !isMonitoring;
                Console.WriteLine($"[BTN] After toggle: isMonitoring={isMonitoring}");

                if (isMonitoring)
                {
                    // Auto-select Channel 1 if no channel is selected
                    Console.WriteLine($"[BTN] Checking auto-select: _selectedChannelId={_selectedChannelId}");
                    if (_selectedChannelId <= 0)
                    {
                        Console.WriteLine("[BTN] Auto-selecting Channel 1");
                        _selectedChannelId = 1;
                        _selectedZoneId = -1;
                        _selectedZoneStartPosition = null;
                        _selectedZoneEndPosition = null;
                        _liveScatter = null; // Reset scatter for fresh graph creation

                        // Clear and prepare graph for Channel 1
                        InitializeGraph("Live Data - Channel 1");

                        Console.WriteLine("[FRCM] Auto-selected Channel 1 for measurement");
                    }

                    // Ensure we're subscribed to the selected channel for live data
                    if (_subscribedChannelId != _selectedChannelId)
                    {
                        if (_subscribedChannelId > 0)
                        {
                            await UnsubscribeFromChannelAsync(_subscribedChannelId);
                        }
                        await SubscribeToChannelAsync(_selectedChannelId);
                        Console.WriteLine($"[FRCM] Subscribed to Channel {_selectedChannelId} for measurement");
                    }

                    // Don't clear existing graph - just update title to show "Live Data"
                    string updatedTitle;
                    if (_selectedZoneId > 0)
                    {
                        updatedTitle = $"Live Data - Zone {_selectedZoneId} (Channel {_selectedChannelId})";
                        UpdateStatus($"▶ Started measurement for Channel {_selectedChannelId} Zone {_selectedZoneId}");
                    }
                    else if (_selectedChannelId > 0)
                    {
                        updatedTitle = $"Live Data - Channel {_selectedChannelId}";
                        UpdateStatus($"▶ Started measurement for Channel {_selectedChannelId}");
                    }
                    else
                    {
                        updatedTitle = "Live Temperature Data";
                        UpdateStatus("▶ Started measurement");
                    }

                    // Clear graph data and re-initialize for live measurement
                    InitializeGraph(updatedTitle);

                    await StartMonitoringAsync();

                    btnStartMeasurement.Text = "Stop Measurement";
                    btnStartMeasurement.BackColor = DrawingColor.Red;
                    btnStartMeasurement.ForeColor = DrawingColor.White;

                    // Disable configuration controls while measurement is running
                    SetConfigurationControlsEnabled(false);

                    // Start periodic alarm polling (5-second interval)
                    _alarmPollingTimer?.Start();
                    _ledUpdateTimer?.Start();
                    Console.WriteLine("[ALARM POLL] Timer started - polling every 5 seconds");

                    // Immediate first poll
                    RequestActiveAlarms();
                }
                else
                {
                    UpdateStatus("⏹ Stopped measurement");

                    await StopMonitoringAsync();

                    isPlottingEnabled = false;

                    btnStartMeasurement.Text = "Start Measurement";
                    btnStartMeasurement.BackColor = DrawingColor.Green;
                    btnStartMeasurement.ForeColor = DrawingColor.White;

                    SetConfigurationControlsEnabled(true);
                    if (refresh != null) refresh.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                isMonitoring = false;
                btnStartMeasurement.Text = "Start Measurement";
                btnStartMeasurement.BackColor = DrawingColor.Green;
                btnStartMeasurement.ForeColor = DrawingColor.White;

                // Re-enable configuration controls on error
                SetConfigurationControlsEnabled(true);

                MessageBox.Show(
                    $"Error controlling temperature monitoring:\n\n{ex.Message}",
                    "Monitoring Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                UpdateStatus($"Monitoring error: {ex.Message}");
            }
        }


        private async Task StartMonitoringAsync()
        {
            var msg = new
            {
                MessageType = "start_measurement",
                Payload = new { }
            };

            string json = System.Text.Json.JsonSerializer.Serialize(msg);
            await _wsClient.SendAsync(json);

            UpdateStatus("Start measurement command sent to FRMC");
        }

        private async Task StopMonitoringAsync()
        {
            var msg = new
            {
                MessageType = "stop_measurement",
                Payload = new { }
            };

            string json = System.Text.Json.JsonSerializer.Serialize(msg);
            await _wsClient.SendAsync(json);

            UpdateStatus("Stop measurement command sent to FRMC");
            isPlottingEnabled = false;
        }
        private void RequestActiveAlarms()
        {
            // Fire-and-forget with proper error handling
            _ = Task.Run(async () =>
            {
                try
                {
                    var msg = new
                    {
                        MessageType = "get_active_alarm",
                        Payload = new { }
                    };

                    await _wsClient.SendAsync(JsonSerializer.Serialize(msg));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"❌ Failed to request active alarms: {ex.Message}");
                    Console.WriteLine($"[ERROR] RequestActiveAlarms failed: {ex.Message}");
                }
            });
        }

        private async void channelZoneConfigurationToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            UpdateStatus("⚙ Opening Channel/Zone Configuration...");
            using (var f = new FormChannelZoneConfig(_configurationService, _loginRole, _ChannelClient))
            {
                var result = f.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    UpdateStatus("✅ Channel/Zone configuration saved successfully");
                }
            }

            // Clear zone state cache — zone configuration may have changed (zones added/deleted).
            // Cache will be repopulated by LoadZoneStateAndPopulateGrid and binary broadcasts.
            _zoneStateCache.Clear();

            PopulateChannelNodes();

            // Only refresh zone data if the Channel Information tab is actually visible
            if (_selectedChannelId != -1 && tabControlDisplay.SelectedTab == tabChannelInformation)
            {
                await LoadZoneStateAndPopulateGrid(_selectedChannelId);

                if (_selectedZoneId > 0)
                {
                    var zoneConfig = await _ChannelClient.GetZoneConfigAsync(_selectedChannelId, _selectedZoneId - 1);
                    if (zoneConfig != null)
                    {
                        _selectedZoneStartPosition = zoneConfig.StartPoint;
                        _selectedZoneEndPosition = zoneConfig.EndPoint;
                    }

                    UpdateZoneInfoIndicators(_selectedChannelId - 1, _selectedZoneId - 1);
                }
            }
        }

        private void UpdateAlarmLed()
        {
            // No channel selected
            if (_selectedChannelId == -1)
            {
                if (_lastChannelAlarmState || _lastFiberBreakState)
                {
                    if (ledChannelAlarm != null) ledChannelAlarm.IsOn = false;
                    if (ledFiberBreak != null) ledFiberBreak.IsOn = false;
                    _lastChannelAlarmState = false;
                    _lastFiberBreakState = false;
                }
                return;
            }

            // Channel alarm LED - only update if state changed
            bool channelHasAlarm = _channelsWithAlarm.Contains(_selectedChannelId - 1);
            if (channelHasAlarm != _lastChannelAlarmState)
            {
                if (ledChannelAlarm != null) ledChannelAlarm.IsOn = channelHasAlarm;
                _lastChannelAlarmState = channelHasAlarm;
            }

            // Fiber Break logic - only update if state changed
            bool fiberBroken = _channelsWithFiberBreak.Contains(_selectedChannelId - 1);

            if (fiberBroken != _lastFiberBreakState)
            {
                if (ledFiberBreak != null) { ledFiberBreak.IsOn = fiberBroken; ledFiberBreak.Invalidate(); }
                _lastFiberBreakState = fiberBroken;
            }
        }

        private void UpdateSafetyActiveAlarmLed()
        {
            if (ledAlarm == null) return;

            bool hasAlarm = _channelsWithAnyAlarm.Count > 0 || _channelsWithFiberBreak.Count > 0;
            bool anyChannelEnabled = _configurationService.GetChannels().Values.Any(c => c?.IsEnabled == true);

            // Determine desired state
            bool desiredOn;
            System.Drawing.Color desiredColor = System.Drawing.Color.LimeGreen;

            if (!anyChannelEnabled)
            {
                // No channels enabled: LED off regardless of any stale alarms in FRMC
                desiredOn = false;
            }
            else if (hasAlarm)
            {
                desiredOn = true;
                desiredColor = System.Drawing.Color.Red;
            }
            else
            {
                // Channels monitoring, no alarms: green = safe/ready
                desiredOn = true;
                desiredColor = System.Drawing.Color.LimeGreen;
            }

            // Update LED only when state changes to prevent blinking
            if (ledAlarm.IsOn != desiredOn || (desiredOn && ledAlarm.LedColor != desiredColor))
            {
                ledAlarm.IsOn = desiredOn;
                if (desiredOn)
                    ledAlarm.LedColor = desiredColor;
            }
            _lastSystemAlarmState = hasAlarm;
        }
        /// <summary>
        /// Processes active alarms payload directly (when ActiveAlarm form is closed)
        /// to update _channelsWithAlarm, _channelsWithFiberBreak, _channelsWithAnyAlarm and _zonesWithAlarm for LED indicators.
        /// </summary>
        private void ProcessActiveAlarmsForLed(JsonElement alarms)
        {
            HashSet<int> channelsWithAlarm = new();       // Zone alarms for enabled zones only
            HashSet<int> channelsWithFiberBreak = new();  // Fiber break alarms only
            HashSet<int> channelsWithAnyAlarm = new();    // ALL alarms from FRMC (for system LED sync)
            HashSet<(int channelId, int zoneId)> zonesWithAlarm = new();

            foreach (var alarm in alarms.EnumerateArray())
            {
                if (!alarm.TryGetProperty("ChannelId", out var chProp) ||
                    !alarm.TryGetProperty("ZoneId", out var zProp))
                    continue;

                int channelId = chProp.GetInt32(); // 1-based
                int zoneId = zProp.GetInt32();     // 1-based (0 for fiber break)

                // Get reason to check for fiber break
                string reason = alarm.TryGetProperty("Reason", out var reasonProp) ? reasonProp.GetString() ?? "" : "";

                // Check if this is a fiber break alarm (ZoneId == 0 indicates channel-level alarm)
                bool isFiberBreak = zoneId == 0 || reason.Equals("FiberBreak", StringComparison.OrdinalIgnoreCase);

                int frmcChannelId = channelId - 1;

                // Track ALL alarms for system LED sync with hardware
                channelsWithAnyAlarm.Add(frmcChannelId);

                if (isFiberBreak)
                {
                    // Fiber break goes to separate tracking set
                    channelsWithFiberBreak.Add(frmcChannelId);
                }
                else
                {
                    // For zone-level alarms, check if zone is enabled (skip disabled zones for display)
                    var zones = _configurationService.GetZoneConfig($"Channel {channelId}");
                    var zone = zones?.FirstOrDefault(z => z.ZoneId == zoneId);
                    if (zone == null || !zone.Enabled)
                        continue;

                    int frmcZoneId = zoneId - 1;
                    channelsWithAlarm.Add(frmcChannelId);
                    zonesWithAlarm.Add((frmcChannelId, frmcZoneId));
                }
            }

            // Update tracking sets and trigger LED updates
            _channelsWithFiberBreak = channelsWithFiberBreak;
            _channelsWithAnyAlarm = channelsWithAnyAlarm;
            OnActiveChannelsChanged(channelsWithAlarm);
            OnActiveZonesChanged(zonesWithAlarm);
        }

        private void OnActiveChannelsChanged(HashSet<int> channels)
        {
            _channelsWithAlarm = channels;

            foreach (int ch in channels)
            {
                if (_loggedChannelAlarms.Add(ch)) // logs only once
                {
                    int uiChannelId = ch + 1;
                    UpdateStatus($"⚠ Channel {uiChannelId} has ACTIVE ALARM");
                }
            }

            // Clear resolved alarms
            _loggedChannelAlarms.RemoveWhere(ch => !channels.Contains(ch));

            UpdateAlarmLed();
            UpdateSafetyActiveAlarmLed();
            UpdateActiveAlarmMenuState();

            // Refresh zone LED if selected
            if (_selectedChannelId != -1 && _selectedZoneId != -1)
            {
                // Convert UI IDs to FRMC IDs (0-based)
                UpdateZoneInfoIndicators(_selectedChannelId - 1, _selectedZoneId - 1);
            }
        }

        /// <summary>
        /// Updates the Active Alarm menu item state based on whether there are active alarms.
        /// </summary>
        private void UpdateActiveAlarmMenuState()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(UpdateActiveAlarmMenuState));
                return;
            }

            bool hasAlarms = (_channelsWithAlarm != null && _channelsWithAlarm.Count > 0) ||
                             (_channelsWithFiberBreak != null && _channelsWithFiberBreak.Count > 0);

            activeAlarmToolStripMenuItem.Enabled = hasAlarms;
            activeAlarmToolStripMenuItem.Text = hasAlarms ? "Active Alarm" : "Active Alarm (No Alarms)";
        }

        /// <summary>
        /// Handles changes to channels with fiber break alarms.
        /// Updates _channelsWithFiberBreak and triggers safety LED update.
        /// </summary>
        private void OnActiveFiberBreakChannelsChanged(HashSet<int> channels)
        {
            _channelsWithFiberBreak = channels;

            // Update safety LED (fiber break triggers system alarm LED)
            UpdateSafetyActiveAlarmLed();

            // Update fiber break LED if selected channel has fiber break
            UpdateAlarmLed();

            // Update menu state for Active Alarm option
            UpdateActiveAlarmMenuState();
        }

        /// <summary>
        /// Handles changes to ALL channels with any alarm from FRMC.
        /// Updates _channelsWithAnyAlarm for system LED sync with hardware.
        /// </summary>
        private void OnAllActiveChannelsChanged(HashSet<int> channels)
        {
            _channelsWithAnyAlarm = channels;

            // Update safety LED to sync with hardware
            UpdateSafetyActiveAlarmLed();
        }

        private void OnActiveZonesChanged(HashSet<(int channelId, int zoneId)> zones)
        {
            _zonesWithAlarm = zones;

            // Active zones changed: {zones.Count} alarms

            foreach (var (ch, zn) in zones)
            {
                if (_loggedZoneAlarms.Add((ch, zn))) // logs only once
                {
                    int uiChannelId = ch + 1;
                    int uiZoneId = zn + 1;

                    UpdateStatus($"⚠ Channel {uiChannelId} – Zone {uiZoneId} has ACTIVE ALARM");
                }
            }

            // Clear resolved alarms
            _loggedZoneAlarms.RemoveWhere(z => !zones.Contains(z));

            UpdateAlarmLed();
            UpdateSafetyActiveAlarmLed();

            if (_selectedChannelId != -1 && _selectedZoneId != -1)
            {
                // Convert UI IDs to FRMC IDs (0-based)
                UpdateZoneInfoIndicators(_selectedChannelId - 1, _selectedZoneId - 1);
            }
        }



        /// <summary>
        /// Gets the display name for a zone (e.g., "Fire Alarm Zone", "Zone A")
        /// </summary>
        /// <param name="uiChannelId">1-based UI channel ID</param>
        /// <param name="uiZoneId">1-based UI zone ID</param>
        /// <returns>Zone name or "Zone {id}" if not found</returns>
        private string GetZoneName(int uiChannelId, int uiZoneId)
        {
            var zones = _configurationService.GetZoneConfig($"Channel {uiChannelId}");
            var zone = zones?.FirstOrDefault(z => z.ZoneId == uiZoneId);
            return zone?.Name ?? $"Zone {uiZoneId}";
        }

        private void UpdateZoneInfoIndicators(int frmcChannelId, int frmcZoneId)
        {
            int uiChannelId = frmcChannelId + 1;
            int uiZoneId = frmcZoneId + 1;

            var zones = _configurationService.GetZoneConfig($"Channel {uiChannelId}");
            if (zones == null)
            {
                if (_lastZoneEnabledState || _lastZoneAlarmState)
                {
                    if (ledZoneEnabled != null) ledZoneEnabled.IsOn = false;
                    if (ledZoneAlarm != null) ledZoneAlarm.IsOn = false;
                    _lastZoneEnabledState = false;
                    _lastZoneAlarmState = false;
                }
                return;
            }

            var zone = zones.FirstOrDefault(z => z.ZoneId == uiZoneId);
            if (zone == null)
            {
                if (_lastZoneEnabledState || _lastZoneAlarmState)
                {
                    if (ledZoneEnabled != null) ledZoneEnabled.IsOn = false;
                    if (ledZoneAlarm != null) ledZoneAlarm.IsOn = false;
                    _lastZoneEnabledState = false;
                    _lastZoneAlarmState = false;
                }
                return;
            }

            // Zone Enabled LED - ON if zone is enabled (only update if changed)
            bool zoneEnabled = zone.Enabled;
            if (zoneEnabled != _lastZoneEnabledState)
            {
                if (ledZoneEnabled != null) ledZoneEnabled.IsOn = zoneEnabled;
                _lastZoneEnabledState = zoneEnabled;
            }

            // Zone Alarm LED - Only ON if zone is enabled AND has an alarm
            bool zoneHasAlarm = zoneEnabled && _zonesWithAlarm.Contains((frmcChannelId, frmcZoneId));
            if (zoneHasAlarm != _lastZoneAlarmState)
            {
                if (ledZoneAlarm != null) ledZoneAlarm.IsOn = zoneHasAlarm;
                _lastZoneAlarmState = zoneHasAlarm;
            }
        }

        //private void activeAlarmToolStripMenuItem_Click(object? sender, EventArgs e)
        //{
        //    // Check if there are any active alarms
        //    bool hasAlarms = (_channelsWithAlarm != null && _channelsWithAlarm.Count > 0) ||
        //                     (_channelsWithFiberBreak != null && _channelsWithFiberBreak.Count > 0);

        //    if (!hasAlarms)
        //    {
        //        MessageBox.Show("No active alarms.", "Active Alarms", MessageBoxButtons.OK, MessageBoxIcon.Information);
        //        return;
        //    }

        //    ShowActiveAlarmForm();
        //}

        private void activeAlarmToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            // Check if there are any active alarms
            bool hasAlarms = (_channelsWithAlarm != null && _channelsWithAlarm.Count > 0) ||
                             (_channelsWithFiberBreak != null && _channelsWithFiberBreak.Count > 0);

            if (!hasAlarms)
            {
                MessageBox.Show("No active alarms.", "Active Alarms",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // If form already exists
            if (ActiveAlarm.Instance != null && !ActiveAlarm.Instance.IsDisposed)
            {
                // Restore if minimized
                if (ActiveAlarm.Instance.WindowState == FormWindowState.Minimized)
                {
                    ActiveAlarm.Instance.WindowState = FormWindowState.Normal;
                }

                // Bring to front
                ActiveAlarm.Instance.Show();
                ActiveAlarm.Instance.BringToFront();
                ActiveAlarm.Instance.Activate();

                // Force focus
                ActiveAlarm.Instance.TopMost = true;
                ActiveAlarm.Instance.TopMost = false;
            }
            else
            {
                // Open new form
                ShowActiveAlarmForm();
            }
        }


        private async void refresh_Click(object? sender, EventArgs e)
        {
            if (refresh == null) return;
            refresh.Enabled = false;

            try
            {
                int channelId = _selectedChannelId;   // 🔥 Always use selected channel
                var currentTab = tabControlDisplay.SelectedTab;

                // =========================================
                // CHANNEL TAB REFRESH
                // =========================================
                if (currentTab == tabChannelInformation)
                {
                    UpdateStatus($"Refreshing Channel {channelId} information...");

                    // ================= STOP MODE =================
                    if (!isMonitoring)
                    {
                        if (_channelGraphCache.TryGetValue(channelId, out var channelData)
                            && channelData.distances.Count > 0)
                        {
                            var tempPoints = channelData.distances
                                .Zip(channelData.temperatures,
                                     (d, t) => new TemperatureDataPoint(t, d))
                                .ToList();

                            formsPlot1.Plot.Clear();
                            _liveScatter = null;

                            UpdateGraph(tempPoints);
                            formsPlot1.Refresh();

                            UpdateStatus($"Channel {channelId} refreshed from frozen data (Stopped)");
                        }
                        else
                        {
                            UpdateStatus($"Channel {channelId}: No frozen data available");
                        }

                        return;
                    }

                    // ================= LIVE MODE =================
                    dataGridViewZone.Rows.Clear();
                    dataGridViewZone.Refresh();

                    await Task.Delay(300);

                    await LoadZoneStateAndPopulateGrid(channelId);
                    await LoadGraphDataForSelection();

                    UpdateStatus($"Channel {channelId} information refreshed successfully");
                }

                // =========================================
                // ZONE TAB REFRESH
                // =========================================
                else if (currentTab == tabZoneInformation)
                {
                    if (_selectedZoneId <= 0 || channelId <= 0)
                    {
                        UpdateStatus("No zone selected to refresh");
                        return;
                    }

                    string zoneName = GetZoneName(channelId, _selectedZoneId);

                    // ================= STOP MODE =================
                    if (!isMonitoring)
                    {
                        if (_channelGraphCache.TryGetValue(channelId, out var channelData)
                            && _selectedZoneStartPosition.HasValue
                            && _selectedZoneEndPosition.HasValue)
                        {
                            var filtered = channelData.distances
                                .Zip(channelData.temperatures, (d, t) => new { d, t })
                                .Where(p =>
                                    p.d >= _selectedZoneStartPosition.Value &&
                                    p.d <= _selectedZoneEndPosition.Value)
                                .ToList();

                            var tempPoints = filtered
                                .Select(p => new TemperatureDataPoint(p.t, p.d))
                                .ToList();

                            formsPlot1.Plot.Clear();
                            _liveScatter = null;

                            UpdateGraph(tempPoints);
                            formsPlot1.Refresh();

                            var gridPoints = filtered
                                .Select(p => new PointData
                                {
                                    Position = p.d,
                                    Temperature = p.t
                                })
                                .ToList();

                            PopulateZonePointsGrid(gridPoints);

                            UpdateStatus($"{zoneName} refreshed from frozen data (Stopped)");
                        }
                        else
                        {
                            UpdateStatus($"{zoneName}: No frozen data available");
                        }

                        return;
                    }

                    // ================= LIVE MODE =================
                    _gridPointsCache.Clear();
                    dataGridView1.RowCount = 0;
                    dataGridView1.Refresh();

                    await Task.Delay(300);

                    var points = await _ChannelClient
                        .GetZonePointsAsync(channelId, _selectedZoneId);

                    PopulateZonePointsGrid(points);

                    UpdateStatus($"{zoneName} information refreshed successfully");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Refresh failed: {ex.Message}");
            }
            finally
            {
                if (refresh != null)
                    refresh.Enabled = true;
            }
        }

        private void HandleLoadingFailure(string message)
        {
            UpdateStatus("Loading failed");
            MessageBox.Show(
                message + "\n\nLoading did not happen properly. Existing configuration remains unchanged.",
                "Loading Failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private Dictionary<int, int> GetCurrentZoneCountsFromFRMC()
        {
            var result = new Dictionary<int, int>();
            var zones = _configurationService.GetZones();
            // zones: Dictionary<string, List<Zone>>

            foreach (var entry in zones)
            {
                // "Channel 1" → 1
                int channelId = int.Parse(entry.Key.Replace("Channel ", ""));
                result[channelId] = entry.Value.Count;
            }

            return result;
        }



        private bool ValidateZonesFileAgainstChannelCounts(
        string zonesFile,
        Dictionary<int, int> channelZoneCounts,
        out string errorMessage)
        {
            errorMessage = null;

            if (!ExcelHasWorksheet(zonesFile, "Zones"))
            {
                errorMessage = "Selected Excel file does not contain a 'Zones' worksheet.";
                return false;
            }

            var excelZoneCounts = GetZoneCountsFromExcel(zonesFile);

            foreach (var kvp in excelZoneCounts)
            {
                int channelId = kvp.Key;
                int excelZones = kvp.Value;

                if (!channelZoneCounts.TryGetValue(channelId, out int expectedZones))
                {
                    errorMessage =
                        $"Channel {channelId} does not exist in the loaded channel configuration.";
                    return false;
                }

                if (excelZones != expectedZones)
                {
                    errorMessage =
                        $"Zone count mismatch for Channel {channelId}.\n\n" +
                        $"Expected: {expectedZones}\n" +
                        $"Excel: {excelZones}";
                    return false;
                }
            }

            return true;
        }

        private Dictionary<int, int> GetZoneCountsFromChannelsExcel(string filePath)
        {
            var result = new Dictionary<int, int>();

            using var package = new ExcelPackage(new FileInfo(filePath));
            var ws = package.Workbook.Worksheets["Channels"];
            int lastRow = ws.Dimension.End.Row;

            for (int row = 2; row <= lastRow; row++)
            {
                int channelId = int.Parse(ws.Cells[row, 1].Text);
                int numberOfZones = int.Parse(ws.Cells[row, 7].Text); // Column G

                result[channelId] = numberOfZones;
            }

            return result;
        }

        private List<string> GetZoneMismatchMessages(
        Dictionary<int, int> excelZoneCounts,
        Dictionary<int, int> currentZoneCounts)
        {
            var mismatches = new List<string>();

            foreach (var kvp in excelZoneCounts)
            {
                int channelId = kvp.Key;
                int excelZones = kvp.Value;

                if (currentZoneCounts.TryGetValue(channelId, out int currentZones))
                {
                    if (excelZones != currentZones)
                    {
                        mismatches.Add(
                            $"Channel {channelId}: Existing = {currentZones}, Excel = {excelZones}");
                    }
                }
            }

            return mismatches;
        }



        private Dictionary<int, int> GetZoneCountsFromExcel(string filePath)
        {
            var result = new Dictionary<int, int>();

            using var package = new ExcelPackage(new FileInfo(filePath));
            var ws = package.Workbook.Worksheets["Zones"];
            int lastRow = ws.Dimension.End.Row;

            for (int row = 2; row <= lastRow; row++)
            {
                if (!int.TryParse(ws.Cells[row, 1].Text, out int channelId)) continue;

                if (!result.ContainsKey(channelId))
                    result[channelId] = 0;

                result[channelId]++;
            }

            return result;
        }

        private bool ExcelHasWorksheet(string filePath, string sheetName)
        {
            using var package = new ExcelPackage(new FileInfo(filePath));
            return package.Workbook.Worksheets.Any(ws =>
                ws.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase));
        }


        private bool ReadBool(ExcelRange cell)
        {
            if (cell?.Value == null)
                return false;

            if (cell.Value is bool b)
                return b;

            var v = cell.Value.ToString()?.Trim();

            if (bool.TryParse(v, out var parsed))
                return parsed;

            if (int.TryParse(v, out var i))
                return i != 0;

            return false;
        }

        private bool TryReadBool(
    ExcelRange cell,
    string fieldName,
    int row,
    List<string> errors,
    out bool value)
        {
            value = false;

            if (cell?.Value == null ||
                string.IsNullOrWhiteSpace(cell.Text))
            {
                errors.Add(
                    $"Row {row}: {fieldName} is blank");

                return false;
            }

            var v = cell.Text.Trim();

            if (bool.TryParse(v, out var parsed))
            {
                value = parsed;
                return true;
            }

            if (int.TryParse(v, out var i))
            {
                value = i != 0;
                return true;
            }

            errors.Add(
                $"Row {row}: Invalid {fieldName} value '{v}'. Allowed values are TRUE/FALSE");

            return false;
        }



        private bool TryReadDouble(ExcelRange cell, string fieldName, int row, List<string> errors,
        out double value)
        {
            value = 0;

            if (string.IsNullOrWhiteSpace(cell.Text))
            {
                errors.Add($"Row {row}: {fieldName} is blank");
                return false;
            }

            if (!double.TryParse(cell.Text, out value))
            {
                errors.Add(
                    $"Row {row}: Invalid {fieldName} value '{cell.Text}'");
                return false;
            }

            return true;
        }

        private bool TryReadInt(ExcelRange cell, string fieldName, int row, List<string> errors,
        out int value)
        {
            value = 0;

            if (string.IsNullOrWhiteSpace(cell.Text))
            {
                errors.Add($"Row {row}: {fieldName} is blank");
                return false;
            }

            if (!int.TryParse(cell.Text, out value))
            {
                errors.Add(
                    $"Row {row}: Invalid {fieldName} value '{cell.Text}'");
                return false;
            }

            return true;
        }

        private (bool loadChannels, bool loadZones)? ShowLoadOptionsDialog()
        {
            float scalingFactor = this.DeviceDpi / 96f;
            float fontScaling = FontSizeHelper.GetAutoScaling(this);
            var screen = Screen.FromControl(this);

            float sizeScaling = scalingFactor;
            if (screen.Bounds.Width <= 1920)
            {
                // Shrink the dialog footprint on laptops
                sizeScaling = 1.0f + (scalingFactor - 1.0f) * 0.60f;
            }
            // Set a standard, scaled font for the dialog
            var dialogFont = new System.Drawing.Font("Segoe UI", 10f * fontScaling, System.Drawing.FontStyle.Regular);

            Form dialog = new Form()
            {
                Text = "Load Configuration",
                Size = new Size((int)(320 * sizeScaling), (int)(180 * sizeScaling)),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                Font = dialogFont,
                BackColor = System.Drawing.Color.WhiteSmoke
            };

            TableLayoutPanel layout = new TableLayoutPanel()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                Padding = new Padding((int)(8 * sizeScaling))
            };

            // Set column styles: 50% each
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            // Set row styles: Row 1 & 2 for checkboxes, Row 3 for buttons
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(40 * sizeScaling)));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(40 * sizeScaling)));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            bool loadChannels = true;
            bool loadZones = true;

            var btnChannels = new Button()
            {
                Text = "☑ Load Channels",
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new System.Drawing.Font("Segoe UI", 10f * fontScaling, System.Drawing.FontStyle.Regular),
                Height = (int)(35 * sizeScaling),
                Dock = DockStyle.Fill,
                Margin = new Padding((int)(8 * sizeScaling), (int)(4 * sizeScaling), (int)(8 * sizeScaling), 0),
                BackColor = System.Drawing.Color.White,
                Cursor = Cursors.Hand
            };
            btnChannels.FlatAppearance.BorderSize = 1;
            btnChannels.FlatAppearance.BorderColor = System.Drawing.Color.DarkGray;
            btnChannels.Click += (s, e) => {
                loadChannels = !loadChannels;
                btnChannels.Text = (loadChannels ? "☑" : "☐") + " Load Channels";
                btnChannels.BackColor = loadChannels ? System.Drawing.Color.LightCyan : System.Drawing.Color.White;
            };
            layout.SetColumnSpan(btnChannels, 2);

            var btnZones = new Button()
            {
                Text = "☑ Load Zones",
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new System.Drawing.Font("Segoe UI", 10f * fontScaling, System.Drawing.FontStyle.Regular),
                Height = (int)(35 * sizeScaling),
                Dock = DockStyle.Fill,
                Margin = new Padding((int)(8 * sizeScaling), (int)(4 * sizeScaling), (int)(8 * sizeScaling), 0),
                BackColor = System.Drawing.Color.White,
                Cursor = Cursors.Hand
            };
            btnZones.FlatAppearance.BorderSize = 1;
            btnZones.FlatAppearance.BorderColor = System.Drawing.Color.DarkGray;
            btnZones.Click += (s, e) => {
                loadZones = !loadZones;
                btnZones.Text = (loadZones ? "☑" : "☐") + " Load Zones";
                btnZones.BackColor = loadZones ? System.Drawing.Color.LightCyan : System.Drawing.Color.White;
            };
            layout.SetColumnSpan(btnZones, 2);

            var btnOk = new Button()
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Size = new Size((int)(100 * scalingFactor), (int)(35 * scalingFactor)),
                Anchor = AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = System.Drawing.Color.LightGray,
                Font = new System.Drawing.Font("Segoe UI", 9f * scalingFactor, System.Drawing.FontStyle.Bold),
                Cursor = Cursors.Hand
            };

            var btnCancel = new Button()
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Size = new Size((int)(100 * scalingFactor), (int)(35 * scalingFactor)),
                Anchor = AnchorStyles.Left,
                FlatStyle = FlatStyle.Flat,
                BackColor = System.Drawing.Color.LightGray,
                Font = new System.Drawing.Font("Segoe UI", 9f * scalingFactor, System.Drawing.FontStyle.Bold),
                Cursor = Cursors.Hand
            };

            layout.Controls.Add(btnChannels, 0, 0);
            layout.Controls.Add(btnZones, 0, 1);
            layout.Controls.Add(btnOk, 0, 2);
            layout.Controls.Add(btnCancel, 1, 2);

            dialog.Controls.Add(layout);
            dialog.AcceptButton = btnOk;
            dialog.CancelButton = btnCancel;

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                return (loadChannels, loadZones);
            }

            return null;
        }


        private async void loadConfigurationFileToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            if (!_loginRole.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Access Denied.\n\nOnly administrators can load all configurations.",
                    "Access Denied",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
            bool loadSucceeded = false;
            bool allValidationPassed = false;
            _validationFailureMessage = null;
            _validatedZoneMessage = null;
            _validatedChannelMessage = null;

            try
            {
                var selection = ShowLoadOptionsDialog();
                if (selection == null)
                    return;

                var (loadChannels, loadZones) = selection.Value;

                var zoneCountsSnapshot = new Dictionary<int, int>();

                for (int ch = 1; ch <= 16; ch++) // or your max channel count
                {
                    var zones = _configurationService.GetZoneConfig($"Channel {ch}");
                    if (zones != null)
                    {
                        zoneCountsSnapshot[ch] = zones.Count; // ✅ CORRECT COUNT
                    }
                }

                if (!loadChannels && !loadZones)
                {
                    MessageBox.Show(
                        "Please select at least one option.",
                        "Nothing Selected",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                UpdateStatus("Configuration loading started...");

                string channelsFile = null;
                string zonesFile = null;
                bool forceLoadZones = false;

                if (loadChannels && loadZones)
                {
                    channelsFile = SelectExcelFile("Select Channels Excel File");
                    if (channelsFile == null) return;

                    if (!ExcelHasWorksheet(channelsFile, "Channels"))
                    {
                        MessageBox.Show(
                            "Selected Excel file does not contain a 'Channels' worksheet.",
                            "Invalid File",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        return;
                    }

                    zonesFile = SelectExcelFile("Select Zones Excel File");
                    if (zonesFile == null) return;
                }

                if (loadChannels && !loadZones)
                {
                    channelsFile = SelectExcelFile("Select Channels Excel File");
                    if (channelsFile == null) return;

                    if (!ExcelHasWorksheet(channelsFile, "Channels"))
                    {
                        MessageBox.Show(
                            "Selected Excel file does not contain a 'Channels' worksheet.",
                            "Invalid File",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        return;
                    }

                    var excelChannelZoneCounts =
                        GetZoneCountsFromChannelsExcel(channelsFile);

                    var mismatches =
                        GetZoneMismatchMessages(excelChannelZoneCounts, zoneCountsSnapshot);

                    if (mismatches.Any())
                    {
                        var decision = MessageBox.Show(
                            "Zone count mismatch detected:\n\n" +
                            string.Join("\n", mismatches) +
                            "\n\nTo proceed, you must load Channels as well as Zones.\n" +
                            "Do you want to continue?",
                            "Zone Count Mismatch",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning);

                        if (decision == DialogResult.No)
                            return;

                        forceLoadZones = true;
                    }
                    else
                    {
                        DialogResult decision = MessageBox.Show(
                            "No mismatch detected between Channels Excel and the current configuration.\n\n" +
                            "Do you want to load Zones now?\n\n" +
                            "Click NO to cancel loading and keep the current configuration.",
                            "Zone Validation Successful",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question);

                        if (decision == DialogResult.Yes)
                        {
                            forceLoadZones = true;
                        }
                        else
                        {
                            // 🛑 User explicitly chose NO → abort everything
                            return;
                        }
                    }
                }

                if (loadZones || forceLoadZones)
                {
                    zonesFile ??= SelectExcelFile("Select Zones Excel File");
                    if (zonesFile == null) return;
                }

                if (loadZones || forceLoadZones)
                {
                    if (!ValidateZonesFileAgainstChannelCounts(
                            zonesFile,
                            loadChannels
                                ? GetZoneCountsFromChannelsExcel(channelsFile)
                                : _currentChannelZoneCounts,
                            out string error))
                    {
                        MessageBox.Show(
                            error +
                            "\n\nLoading aborted. Existing configuration remains unchanged.",
                            "Zone Validation Failed",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);

                        return;
                    }
                }

                if (loadChannels)
                {
                    await LoadChannelsFromExcelAsync(channelsFile);

                    // STOP EVERYTHING if channels failed
                    if (!_channelLoadSuccessful)
                    {
                        HandleLoadingFailure(_validationFailureMessage);
                        return;
                    }
                }

                if (loadZones || forceLoadZones)
                {
                    await LoadZonesFromExcelAsync(zonesFile);

                    // STOP EVERYTHING if zones failed
                    if (!_zoneLoadSuccessful)
                    {
                        if (!string.IsNullOrWhiteSpace(_validationFailureMessage))
                        {
                            HandleLoadingFailure(_validationFailureMessage);
                        }

                        return;
                    }
                }

                _currentChannelZoneCounts.Clear();

                foreach (var batch in _loadedChannelBatches)
                {
                    _currentChannelZoneCounts[batch.ChannelId] =
                        batch.NumberOfZones;
                }

                allValidationPassed = true;
                await SendChannelsToFRMCAsync();

                await _wsClient.SendAsync(
                    JsonSerializer.Serialize(_validatedZoneMessage));

                UpdateStatus("Finalizing configuration in FRMC...");
                await _configurationService.LoadConfigurationAsync();

                PopulateChannelNodes();
                UpdateStatus("Configuration loaded successfully");

                // Refresh the Is-Enabled LED for the currently selected channel.
                // _lastChannelEnabledState may hold a stale value from before the load,
                // so force re-evaluation against the freshly loaded configuration.
                if (_selectedChannelId != -1)
                    UpdateChannelHeader(_selectedChannelId);

                MessageBox.Show(
                    "Configuration loaded successfully.",
                    "Loading Completed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                loadSucceeded = true;
            }

            catch (Exception ex)
            {
                HandleLoadingFailure(ex.Message);
            }
            finally
            {
                if (!loadSucceeded)
                {
                    UpdateStatus("Loading failed");
                }
            }
        }

        private async Task LoadZonesFromExcelAsync(string filePath)
        {
            _zoneLoadSuccessful = false;
            _validatedZoneMessage = null;

            var parsingErrors = new List<string>();

            if (string.IsNullOrWhiteSpace(filePath))
                return;

            if (!ExcelHasWorksheet(filePath, "Zones"))
            {
                MessageBox.Show(
                    "Selected Excel file does not contain a 'Zones' worksheet.",
                    "Invalid File",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            var excelZoneCounts = GetZoneCountsFromExcel(filePath);

            foreach (var kvp in excelZoneCounts)
            {
                int channelId = kvp.Key;
                int excelZoneCount = kvp.Value;

                if (!_currentChannelZoneCounts.TryGetValue(channelId, out int currentZoneCount))
                {
                    MessageBox.Show(
                        $"Channel {channelId} does not exist in the current configuration.\n\n" +
                        $"Please load Channels first.",
                        "Channel Missing",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                if (excelZoneCount != currentZoneCount)
                {
                    MessageBox.Show(
                        $"Zone count mismatch detected for Channel {channelId}.\n\n" +
                        $"Current configuration zones: {currentZoneCount}\n" +
                        $"Zones in Excel file: {excelZoneCount}",
                        "Zone Count Mismatch",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }
            }

            UpdateStatus("▶ Loading Zones...");
            var excelZonesByChannel = new Dictionary<int, List<ZoneInfo>>();

            using (var package = new ExcelPackage(new FileInfo(filePath)))
            {
                var ws = package.Workbook.Worksheets["Zones"];
                int lastRow = ws.Dimension.End.Row;

                // First pass: Detect if Excel uses 0-based or 1-based Zone IDs
                bool hasZeroId = false;
                for (int row = 2; row <= lastRow; row++)
                {
                    if (int.TryParse(ws.Cells[row, 2].Text, out int zid) && zid == 0)
                    {
                        hasZeroId = true;
                        break;
                    }
                }

                for (int row = 2; row <= lastRow; row++)
                {

                    if (!TryReadInt(ws.Cells[row, 1], "ChannelId", row, parsingErrors, out int channelId))
                        continue;

                    if (!TryReadInt(ws.Cells[row, 2], "ZoneId", row, parsingErrors, out int zoneId))
                        continue;

                    // Normalize to 1-based ID if Excel is 0-based
                    int normalizedZoneId = hasZeroId ? zoneId + 1 : zoneId;

                    if (!TryReadDouble(ws.Cells[row, 4], "StartPoint", row, parsingErrors, out double startPoint))
                        continue;

                    if (!TryReadDouble(ws.Cells[row, 5], "EndPoint", row, parsingErrors, out double endPoint))
                        continue;

                    if (!TryReadDouble(ws.Cells[row, 6], "MaxTemp", row, parsingErrors, out double maxTemp))
                        continue;

                    if (!TryReadDouble(ws.Cells[row, 7], "MinTemp", row, parsingErrors, out double minTemp))
                        continue;

                    if (!TryReadDouble(ws.Cells[row, 8], "PreAlarm", row, parsingErrors, out double preAlarm))
                        continue;

                    if (!TryReadDouble(ws.Cells[row, 9], "RoRThreshold", row, parsingErrors, out double rorThreshold))
                        continue;

                    if (!TryReadDouble(ws.Cells[row, 11], "DeviationThreshold", row, parsingErrors, out double deviationThreshold))
                        continue;

                    if (!TryReadBool(ws.Cells[row, 10], "Enabled", row, parsingErrors, out bool enabled))
                    {
                        continue;
                    }

                    if (!TryReadBool(ws.Cells[row, 12], "MaxEnable", row, parsingErrors, out bool maxEnable))
                    {
                        continue;
                    }

                    if (!TryReadBool(ws.Cells[row, 13], "MinEnable", row, parsingErrors, out bool minEnable))
                    {
                        continue;
                    }

                    if (!TryReadBool(ws.Cells[row, 14], "RoREnable", row, parsingErrors, out bool rorEnable))
                    {
                        continue;
                    }

                    if (!TryReadBool(ws.Cells[row, 15], "DeviationEnable", row, parsingErrors, out bool deviationEnable))
                    {
                        continue;
                    }

                    if (!TryReadBool(ws.Cells[row, 16], "PreAlarmEnable", row, parsingErrors, out bool preAlarmEnable))
                    {
                        continue;
                    }

                    // Normalize to 1-based ID if Excel is 0-based
                    //int normalizedZoneId = hasZeroId ? zoneId + 1 : zoneId;
                    int? assignedRelayId = null;

                    var relayText = ws.Cells[row, 17].Text?.Trim();

                    if (string.IsNullOrWhiteSpace(relayText) ||
                        relayText.Equals("None", StringComparison.OrdinalIgnoreCase))
                    {
                        assignedRelayId = null;
                    }
                    else
                    {
                        if (int.TryParse(relayText, out int relay))
                        {
                            assignedRelayId = relay;
                        }
                        else
                        {
                            parsingErrors.Add(
                                $"Row {row}: AssignedRelay must be a number between 12 and 48 or 'None'");

                            continue;
                        }
                    }

                    var zone = new ZoneInfo
                    {
                        ChannelId = channelId,
                        //ZoneId = zoneId,
                        ZoneId = normalizedZoneId,
                        Name = ws.Cells[row, 3].Text,

                        StartPoint = startPoint,
                        EndPoint = endPoint,

                        MaxTemp = maxTemp,
                        MinTemp = minTemp,

                        PreAlarm = preAlarm,
                        RoRThreshold = rorThreshold,

                        Enabled = enabled,

                        DeviationThreshold = deviationThreshold,

                        IsMaxTempEnabled = maxEnable,
                        IsMinTempEnabled = minEnable,
                        IsRateOfRiseEnabled = rorEnable,
                        IsDeviationEnabled = deviationEnable,
                        IsPreAlarmEnabled = preAlarmEnable,

                        AssignedRelayId = assignedRelayId
                    };

                    if (!excelZonesByChannel.ContainsKey(channelId))
                        excelZonesByChannel[channelId] = new List<ZoneInfo>();

                    excelZonesByChannel[channelId].Add(zone);
                }
            }

            //if (parsingErrors.Any())
            //{
            //    MessageBox.Show(
            //        string.Join("\n", parsingErrors),
            //        "Zone Excel Parsing Error",
            //        MessageBoxButtons.OK,
            //        MessageBoxIcon.Error);

            //    return;
            //}

            if (parsingErrors.Any())
            {
                _validationFailureMessage =
                    "Zone Excel Parsing Error\n\n" +
                    string.Join("\n", parsingErrors);

                return;
            }

            var allZones = excelZonesByChannel
            .SelectMany(x => x.Value)
            .ToList();

            var zoneErrors = new List<string>();

            zoneErrors.AddRange(
                ExcelConfigurationValidator.ValidateZones(
                    allZones,
                    _loadedChannelBatches));

            zoneErrors.AddRange(
                ExcelConfigurationValidator.ValidateZoneOverlaps(
                    allZones));

            zoneErrors.AddRange(
                ExcelConfigurationValidator.ValidateDuplicateZoneIds(
                    allZones));

            zoneErrors.AddRange(
                ExcelConfigurationValidator.ValidateZoneCountConsistency(
                    _loadedChannelBatches,
                    allZones));

            zoneErrors.AddRange(
                ExcelConfigurationValidator
                    .ValidateDuplicateZoneNames(allZones));

            if (zoneErrors.Any())
            {
                _validationFailureMessage =
                "Zone Validation Failed\n\n" +
                string.Join("\n", zoneErrors);

                return;
            }

            var finalZoneGroups = new Dictionary<string, List<object>>();

            foreach (var ch in _currentChannelZoneCounts)
            {
                int channelId = ch.Key;
                int expectedZoneCount = ch.Value;

                var finalZones = new List<object>();

                if (excelZonesByChannel.TryGetValue(channelId, out var channelZones))
                {
                    // 🔥 Load all zones found in Excel for this channel (up to expected count)
                    finalZones.AddRange(channelZones.Take(expectedZoneCount));
                }

                // If still need more to reach expected count, fill with defaults
                while (finalZones.Count < expectedZoneCount)
                {
                    int nextId = finalZones.Count + 1;
                    finalZones.Add(new
                    {
                        ChannelId = channelId,
                        ZoneId = nextId,
                        Name = $"Zone {nextId}",
                        StartPoint = 0,
                        EndPoint = 0,
                        MaxTemp = 0,
                        MinTemp = 0,
                        PreAlarm = 0,
                        RoRThreshold = 0,
                        Enabled = false,
                        DeviationThreshold = 0,
                        isMaxTempEnabled = false,
                        isMinTempEnabled = false,
                        isRateOfRiseEnabled = false,
                        isDeviationEnabled = false,
                        isPreAlarmEnabled = false,
                        assignedRelayId = (int?)null
                    });
                }

                finalZoneGroups[$"Channel {channelId}"] = finalZones;
            }

            var zoneMsg = new
            {
                MessageType = "SetZoneConfiguration",
                Payload = new
                {
                    Zones = finalZoneGroups
                }
            };

            //await _wsClient.SendAsync(JsonSerializer.Serialize(zoneMsg));

            //UpdateStatus("Zones loaded and fully synchronized with FRMC");

            _validatedZoneMessage = zoneMsg;
            _zoneLoadSuccessful = true;

            UpdateStatus("Zones validated successfully");
        }

        private async Task LoadChannelsFromExcelAsync(string filePath)
        {
            _channelLoadSuccessful = false;
            UpdateStatus("▶ Loading Channels...");

            var batches = new List<ChannelBatch>();
            var parsingErrors = new List<string>();

            using var package = new ExcelPackage(new FileInfo(filePath));
            var ws = package.Workbook.Worksheets["Channels"];
            int lastRow = ws.Dimension.End.Row;

            for (int row = 2; row <= lastRow; row++)
            {
                //int channelId = int.Parse(ws.Cells[row, 1].Text);

                if (!TryReadInt(ws.Cells[row, 1], "ChannelId", row, parsingErrors, out int channelId))
                    continue;

                if (!TryReadDouble(ws.Cells[row, 3], "Length", row, parsingErrors, out double length))
                    continue;

                if (!TryReadDouble(ws.Cells[row, 4], "CorrectionLength", row, parsingErrors, out double correctionLength))
                    continue;

                if (!TryReadInt(ws.Cells[row, 5], "ScanPeriod", row, parsingErrors, out int scanPeriod))
                    continue;

                if (!TryReadInt(ws.Cells[row, 7], "NumberOfZones", row, parsingErrors, out int numberOfZones))
                    continue;

                if (!TryReadBool(
                ws.Cells[row, 6],
                "Enabled",
                row,
                parsingErrors,
                out bool enabled))
                {
                    continue;
                }


                var batch = new ChannelBatch
                {
                    ChannelId = channelId,
                    Name = ws.Cells[row, 2].Text,
                    Length = length,
                    CorrectionLength = correctionLength,
                    ScanPeriod = scanPeriod,
                    Enabled = enabled,
                    NumberOfZones = numberOfZones
                };

                //_currentChannelZoneCounts[channelId] = batch.NumberOfZones;
                batches.Add(batch);
            }

            if (parsingErrors.Any())
            {
                _validationFailureMessage =
                    "Excel Parsing Error\n\n" +
                    string.Join("\n", parsingErrors);

                return;
            }

            var validationErrors = new List<string>();

            validationErrors.AddRange(
                ExcelConfigurationValidator.ValidateChannels(batches));

            validationErrors.AddRange(
                ExcelConfigurationValidator.ValidateDuplicateChannelIds(batches));

            if (validationErrors.Any())
            {
                _validationFailureMessage =
                    "Channel Validation Failed\n\n" +
                    string.Join("\n", validationErrors);

                return;
            }

            _currentChannelZoneCounts.Clear();

            foreach (var batch in batches)
            {
                _currentChannelZoneCounts[batch.ChannelId] =
                    batch.NumberOfZones;
            }

            _loadedChannelBatches = batches;
            _channelLoadSuccessful = true;
        }

        private async Task SendChannelsToFRMCAsync()
        {
            foreach (var batch in _loadedChannelBatches.OrderBy(b => b.ChannelId))
            {
                await _wsClient.SendAsync(JsonSerializer.Serialize(new
                {
                    MessageType = "SetChannelConfig",
                    Payload = new
                    {
                        Config = new
                        {
                            batch.ChannelId,
                            batch.Name,
                            ChannelLength = batch.Length + batch.CorrectionLength,
                            batch.CorrectionLength,
                            batch.ScanPeriod,
                            batch.NumberOfZones,
                            IsEnabled = batch.Enabled
                        }
                    }
                }));

                await Task.Delay(100);

                UpdateStatus($"✔ Channel {batch.ChannelId} sent");
            }
        }

        private string SelectExcelFile(string title)
        {
            using var ofd = new OpenFileDialog
            {
                Title = title,
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                Multiselect = false
            };

            if (ofd.ShowDialog(this) == DialogResult.OK)
                return ofd.FileName;

            return null;
        }

        public enum SaveResult
        {
            Success,
            Failed,
            Cancelled
        }

        private SaveResult SaveChannelsToExcel()
        {
            try
            {
                using var sfd = new SaveFileDialog
                {
                    Title = "Save Channel Configuration",
                    FileName = "FRCM_Channels.xlsx",
                    Filter = "Excel Workbook (*.xlsx)|*.xlsx"
                };

                //if (sfd.ShowDialog() != DialogResult.OK)

                if (sfd.ShowDialog(this) != DialogResult.OK)
                    return SaveResult.Cancelled;

                using var package = new ExcelPackage();
                var ws = package.Workbook.Worksheets.Add("Channels");

                // 🔹 YOUR EXISTING CODE (UNCHANGED)
                // headers, loop, autofit, save, Process.Start...

                // Header
                ws.Cells[1, 1].Value = "ChannelId";
                ws.Cells[1, 2].Value = "Name";
                ws.Cells[1, 3].Value = "Length";
                ws.Cells[1, 4].Value = "CorrectionLength";
                ws.Cells[1, 5].Value = "ScanPeriod";
                ws.Cells[1, 6].Value = "Enabled";
                ws.Cells[1, 7].Value = "NumberOfZones";

                ws.Row(1).Style.Font.Bold = true;

                int row = 2;
                var channels = _configurationService.GetChannels();

                foreach (var ch in channels.Values.OrderBy(c => c.ChannelId))
                {
                    ws.Cells[row, 1].Value = ch.ChannelId;
                    //ws.Cells[row, 2].Value = ch.Name;
                    ws.Cells[row, 2].Value = string.IsNullOrWhiteSpace(ch.Name) ? $"Channel {ch.ChannelId}" : ch.Name;
                    ws.Cells[row, 3].Value = ch.Length;
                    ws.Cells[row, 4].Value = ch.CorrectionLength;
                    ws.Cells[row, 5].Value = ch.ScanPeriod;
                    ws.Cells[row, 6].Value = ch.IsEnabled;
                    ws.Cells[row, 7].Value = ch.NumberOfZones;
                    row++;
                }

                ws.Column(2).Style.HorizontalAlignment =
                OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;

                ws.Cells.AutoFitColumns();
                package.SaveAs(new FileInfo(sfd.FileName));
                Process.Start(new ProcessStartInfo(sfd.FileName) { UseShellExecute = true });

                return SaveResult.Success;
            }
            catch
            {
                return SaveResult.Failed;
            }
        }

        private SaveResult SaveZonesToExcel()
        {
            try
            {
                using var sfd = new SaveFileDialog
                {
                    Title = "Save Zone Configuration",
                    FileName = "FRCM_Zones.xlsx",
                    Filter = "Excel Workbook (*.xlsx)|*.xlsx"
                };

                //if (sfd.ShowDialog() != DialogResult.OK)

                if (sfd.ShowDialog(this) != DialogResult.OK)
                    return SaveResult.Cancelled;

                using var package = new ExcelPackage();
                var ws = package.Workbook.Worksheets.Add("Zones");

                // 🔹 YOUR EXISTING CODE (UNCHANGED)
                // Header
                string[] headers =
                {
               "ChannelId","ZoneId","Name","Start","End",
               "MaxTemp","MinTemp","PreAlarm","RateOfRise",
               "Enabled","Deviation",
               "MaxEnable","MinEnable","RoREnable","DeviationEnable","PreAlarmEnable","AssignedRelay"
           };

                for (int i = 0; i < headers.Length; i++)
                    ws.Cells[1, i + 1].Value = headers[i];

                ws.Row(1).Style.Font.Bold = true;

                int row = 2;
                var zones = _configurationService.GetZones();

                foreach (var channelEntry in zones
                .OrderBy(z => int.Parse(z.Key.Replace("Channel ", ""))))
                {
                    int chId = int.Parse(channelEntry.Key.Replace("Channel ", ""));

                    foreach (var z in channelEntry.Value.OrderBy(x => x.ZoneId))
                    {
                        ws.Cells[row, 1].Value = chId;
                        ws.Cells[row, 2].Value = z.ZoneId;
                        ws.Cells[row, 3].Value = z.Name;
                        ws.Cells[row, 4].Value = z.StartPoint;
                        ws.Cells[row, 5].Value = z.EndPoint;
                        ws.Cells[row, 6].Value = z.MaxTemp;
                        ws.Cells[row, 7].Value = z.MinTemp;
                        ws.Cells[row, 8].Value = z.PreAlarm;
                        ws.Cells[row, 9].Value = z.RoRThreshold;
                        ws.Cells[row, 10].Value = z.Enabled;
                        ws.Cells[row, 11].Value = z.DeviationThreshold;
                        ws.Cells[row, 12].Value = z.IsMaxTempEnabled;
                        ws.Cells[row, 13].Value = z.IsMinTempEnabled;
                        ws.Cells[row, 14].Value = z.IsRateOfRiseEnabled;
                        ws.Cells[row, 15].Value = z.IsDeviationEnabled;
                        ws.Cells[row, 16].Value = z.IsPreAlarmEnabled;
                        ws.Cells[row, 17].Value = z.AssignedRelayId.HasValue ? z.AssignedRelayId.Value : "None";

                        row++;
                    }
                }
                ws.Column(3).Style.HorizontalAlignment =
                OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;

                ws.Column(17).Style.HorizontalAlignment =
                 OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;

                ws.Cells.AutoFitColumns();
                package.SaveAs(new FileInfo(sfd.FileName));

                Process.Start(new ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
                return SaveResult.Success;
            }
            catch
            {
                return SaveResult.Failed;
            }
        }

        private void saveConfigurationFileToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            if (!_loginRole.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Access Denied.\n\nOnly administrators can save all configurations.",
                    "Access Denied",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
            UpdateStatus("Saving channel and zone information...");

            // CHANNEL SAVE
            var channelResult = SaveChannelsToExcel();

            if (channelResult == SaveResult.Cancelled)
                return;

            MessageBox.Show(
                channelResult == SaveResult.Success
                    ? "Channel configuration saved successfully."
                    : "Failed to save channel configuration.",
                channelResult == SaveResult.Success ? "Success" : "Error",
                MessageBoxButtons.OK,
                channelResult == SaveResult.Success
                    ? MessageBoxIcon.Information
                    : MessageBoxIcon.Error
            );

            // ZONE SAVE
            var zoneResult = SaveZonesToExcel();

            if (zoneResult == SaveResult.Cancelled)
                return;

            MessageBox.Show(
                zoneResult == SaveResult.Success
                    ? "Zone configuration saved successfully."
                    : "Failed to save zone configuration.",
                zoneResult == SaveResult.Success ? "Success" : "Error",
                MessageBoxButtons.OK,
                zoneResult == SaveResult.Success
                    ? MessageBoxIcon.Information
                    : MessageBoxIcon.Error
            );

            UpdateStatus("Saving completed successfully");
        }

        //private Dictionary<int, int> _currentChannelZoneCounts = new();


        private void aboutToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            AboutForm about = new AboutForm();
            about.ShowDialog();
        }

        private void UpdateChannelEnableIndicator(int uiChannelId)
        {
            if (ledChannelEnabled == null) return;

            string key = $"Channel {uiChannelId}";

            var cfg = _configurationService.GetChannelConfig(key);
            if (cfg != null)
            {
                ledChannelEnabled.IsOn = cfg.IsEnabled;
            }
            else
            {
                ledChannelEnabled.IsOn = false;
            }
        }


        private void DrawBorder(PaintEventArgs e, Panel panel)
        {
            using (Pen pen = new Pen(System.Drawing.Color.Black, 1))
            {
                e.Graphics.DrawRectangle(
                    pen,
                    0, 0,
                    panel.Width - 1,
                    panel.Height - 1
                );
            }
        }



        private void Led_Paint(object? sender, PaintEventArgs e)
        {
            if (sender is not Panel panel)
                return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // 🔹 Fake transparency: paint parent background
            if (panel.Parent != null)
            {
                using (SolidBrush bg = new SolidBrush(panel.Parent.BackColor))
                {
                    e.Graphics.FillRectangle(bg, panel.ClientRectangle);
                }
            }

            // 🔹 Draw ONLY the LED (no square)
            DrawLed(e, panel.BackColor);
        }

        private void Led_PaintBackground(object? sender, PaintEventArgs e)
        {
            // DO NOTHING → prevents square background
        }

        private void DrawLed(PaintEventArgs e, System.Drawing.Color ledColor)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(2, 2,
                e.ClipRectangle.Width - 4,
                e.ClipRectangle.Height - 4);

            using (GraphicsPath glowPath = new GraphicsPath())
            {
                glowPath.AddEllipse(rect);
                using (PathGradientBrush glowBrush = new PathGradientBrush(glowPath))
                {
                    glowBrush.CenterColor = System.Drawing.Color.FromArgb(180, ledColor);
                    glowBrush.SurroundColors = new[] { System.Drawing.Color.Transparent };
                    e.Graphics.FillEllipse(glowBrush, rect);
                }
            }

            using (LinearGradientBrush bodyBrush = new LinearGradientBrush(
                       rect,
                       ControlPaint.Light(ledColor),
                       ControlPaint.Dark(ledColor),
                       LinearGradientMode.ForwardDiagonal))
            {
                e.Graphics.FillEllipse(bodyBrush, rect);
            }

            using (Pen pen = new Pen(System.Drawing.Color.Black, 1))
            {
                e.Graphics.DrawEllipse(pen, rect);
            }

            Rectangle highlight = new Rectangle(
                rect.X + rect.Width / 4,
                rect.Y + rect.Height / 6,
                rect.Width / 3,
                rect.Height / 3);

            using (SolidBrush shine = new SolidBrush(System.Drawing.Color.FromArgb(120, System.Drawing.Color.White)))
            {
                e.Graphics.FillEllipse(shine, highlight);
            }
        }

        private void iPConfigurationToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            UpdateStatus("Opening IP Configuration...");

            if (FormIpConfiguration.Instance == null || FormIpConfiguration.Instance.IsDisposed)
            {
                var form = new FormIpConfiguration(_wsClient, _loginRole);

                form.IpOperationStatus += msg =>
                {
                    UpdateStatus(msg);
                };

                form.IpConfigurationClosed += () =>
                {
                    UpdateStatus("IP configuration closed");
                };

                form.Show(this);
            }
            else
            {
                FormIpConfiguration.Instance.Show();
                FormIpConfiguration.Instance.BringToFront();
            }
        }

        private void usbConfigurationToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            UpdateStatus("Opening USB Configuration...");

            if (FormUsbConfiguration.Instance == null || FormUsbConfiguration.Instance.IsDisposed)
            {
                var form = new FormUsbConfiguration(_wsClient, _loginRole);

                form.UsbOperationStatus += msg =>
                {
                    UpdateStatus(msg);
                };

                form.UsbConfigurationClosed += () =>
                {
                    UpdateStatus("USB configuration closed");
                };

                form.Show(this);
            }
            else
            {
                FormUsbConfiguration.Instance.Show();
                FormUsbConfiguration.Instance.BringToFront();
            }
        }

        private void dtsCalibrationToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            UpdateStatus("Opening DTS Hardware Configuration...");

            if (FormDtsCalibration.Instance == null || FormDtsCalibration.Instance.IsDisposed)
            {
                var form = new FormDtsCalibration(_wsClient, _loginRole);

                form.DtsOperationStatus += msg =>
                {
                    UpdateStatus(msg);
                };

                form.DtsCalibrationClosed += () =>
                {
                    UpdateStatus("DTS Hardware Configuration closed");
                };

                form.Show(this);
            }
            else
            {
                FormDtsCalibration.Instance.Show();
                FormDtsCalibration.Instance.BringToFront();
            }
        }

        private void temperatureCorrectionToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            UpdateStatus("Opening Temperature Correction...");

            if (FormTemperatureCorrection.Instance == null || FormTemperatureCorrection.Instance.IsDisposed)
            {
                var form = new FormTemperatureCorrection(_wsClient, _loginRole);

                form.OperationStatus += msg =>
                {
                    UpdateStatus(msg);
                };

                form.TemperatureCorrectionClosed += () =>
                {
                    UpdateStatus("Temperature Correction closed");
                };

                form.Show(this);
            }
            else
            {
                FormTemperatureCorrection.Instance.Show();
                FormTemperatureCorrection.Instance.BringToFront();
            }
        }


        private async void resetAllConfigurationToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            // Only allow admins to reset configuration
            if (!_loginRole.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Access Denied.\n\nOnly administrators can reset all configurations.",
                    "Access Denied",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
            var result = MessageBox.Show(
                "WARNING: This will reset ALL channel configurations to default.\n\n" +
                "- All channels will be DISABLED\n" +
                "- All channel lengths will be set to 0\n" +
                "- All zones will be reset to ONE default zone per channel\n\n" +
                "This action cannot be undone.\n\n" +
                "Are you sure you want to continue?",
                "Reset All Configuration",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (result != DialogResult.Yes)
            {
                return;
            }

            try
            {
                UpdateStatus("Resetting all configurations...");
                this.Cursor = Cursors.WaitCursor;

                var (success, message) = await _ChannelClient.ResetAllConfigurationAsync();

                if (success)
                {
                    UpdateStatus("All configurations reset successfully");

                    // DO NOT manually recreate zones
                    await _configurationService.LoadConfigurationAsync();

                    InitializeChannelTree();

                    MessageBox.Show(
                        "All configurations have been reset to default.\n\n" +
                        "All channels are now disabled with 1 default zone each.\n" +
                        "New zones will start from Zone ID 1.",
                        "Reset Complete",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    await RefreshCurrentInformationTab();

                    // Force LED update: _lastChannelEnabledState may still reflect the
                    // pre-reset (enabled) state. After reset all channels are disabled,
                    // so re-evaluate the indicator for the currently selected channel.
                    if (_selectedChannelId != -1)
                        UpdateChannelHeader(_selectedChannelId);
                }
                else
                {
                    UpdateStatus($"Failed to reset configurations: {message}");
                    MessageBox.Show(
                        $"Failed to reset configurations:\n\n{message}",
                        "Reset Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error resetting configurations: {ex.Message}");
                MessageBox.Show(
                    $"Error resetting configurations:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        private void clockConfigurationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            UpdateStatus("Opening Clock Configuration...");
            using (var dlg = new ClockConfigurationPopup(_wsClient))
            {
                dlg.ShowDialog(this);
            }

            UpdateStatus("Clock configuration closed");
        }

        /// <summary>
        /// Public method to clear the live data cache for all zones.
        /// Called when hardware configuration changes to prevent stale data.
        /// </summary>
        public void ResetZoneStateCache()
        {
            _zoneStateCache.Clear();
        }

    }
}
