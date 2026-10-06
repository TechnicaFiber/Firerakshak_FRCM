using FRCM.Services;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;

namespace FRCM
{
    internal static class Program
    {
        // Class to hold initialization result data
        private class InitializationResult
        {
            public ITemperatureDataService TemperatureDataService { get; set; } = null!;
            public ILogger Logger { get; set; } = null!;
            public string Username { get; set; } = null!;
            public string Password { get; set; } = null!;
            public CustomWebSocketClient WebSocketClient { get; set; } = null!;
            public IConfigurationService ConfigurationService { get; set; } = null!;
            public string Role { get; set; } = null!;
            public string? ErrorMessage { get; set; }
            public bool IsConnectionError { get; set; }
            public bool IsAuthError { get; set; }
        }

        // Global data
        public static CustomWebSocketClient? WebSocketClient;

        /// <summary>
        /// FRMC server host address. Change this for production deployment.
        /// Default: localhost (for testing when FRMC and DTSCM run on same machine)
        /// Production: Set to FRMC server IP (e.g., "192.168.1.100")
        /// </summary>
        public static string FrmcHost { get; set; } = "";

        /// <summary>
        /// FRMC Control WebSocket port (commands, config, health, alarms).
        /// Default: 6164
        /// </summary>
        public static int FrmcControlPort { get; set; } = 6164;

        /// <summary>
        /// FRMC Data Stream WebSocket port (live temperature streaming).
        /// Default: 6165
        /// </summary>
        public static int FrmcDataStreamPort { get; set; } = 6165;

        /// <summary>
        /// Flag indicating whether secure WebSocket (WSS) and HTTPS should be used.
        /// Controlled via USE_SECURE preprocessor macro defined in .csproj.
        /// </summary>
#if USE_SECURE
        public static bool FrmcUseSecure { get; set; } = true;
#else
        public static bool FrmcUseSecure { get; set; } = false;
#endif

        /// <summary>
        /// Centralized helper to get the active WebSocket URI scheme based on configuration.
        /// </summary>
        public static string GetWebSocketScheme() => FrmcUseSecure ? "wss" : "ws";

        /// <summary>
        /// Centralized helper to get the active HTTP/HTTPS scheme based on configuration.
        /// </summary>
        public static string GetHttpScheme() => FrmcUseSecure ? "https" : "http";

        /// <summary>
        /// Centralized helper to construct the FRMC Control WebSocket URI.
        /// </summary>
        public static Uri GetControlWebSocketUri() =>
            new Uri($"{GetWebSocketScheme()}://{FrmcHost}:{FrmcControlPort}/general_ws/");

        /// <summary>
        /// Centralized helper to construct the Token Authentication API URI.
        /// </summary>
        public static Uri GetTokenUri() =>
            new Uri($"{GetHttpScheme()}://{FrmcHost}:{FrmcControlPort}/get_token");

        /// <summary>
        /// Centralized helper to construct the Data Stream WebSocket URI.
        /// </summary>
        public static Uri GetDataStreamUri(string host, int port) =>
            new Uri($"{GetWebSocketScheme()}://{host}:{port}/data_stream/");

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_HIDE = 0;

        private static void SetupConsoleLogging(IConfiguration config)
        {
            bool logToFile = Convert.ToBoolean(config["ConsoleSettings:LogToFile"]);
            string logFileName = config["ConsoleSettings:LogFileName"] ?? "console.log";

            if (!logToFile)
                return;

            //var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, logFileName);

            string logDir;

            if (Debugger.IsAttached)
            {
                // ✅ Project (Debug/Release)
                logDir = AppDomain.CurrentDomain.BaseDirectory;
            }
            else
            {
                // ✅ Installer (visible location)
                logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "FRCMLogs"
                );
            }

            Directory.CreateDirectory(logDir);

            var logPath = Path.Combine(logDir, logFileName);

            //Stores all the logs
            //var logWriter = new StreamWriter(logPath, true);

            //Only latest run logs stored
            var logWriter = new StreamWriter(logPath, false);

            logWriter.AutoFlush = true;

            var multiWriter = new MultiTextWriter(Console.Out, logWriter);

            Console.SetOut(multiWriter);
            Console.SetError(multiWriter);

            Console.WriteLine($"===== Application Started at {DateTime.Now} =====");
        }

        private static void HandleConsoleVisibility(IConfiguration config)
        {
            bool enableConsole = Convert.ToBoolean(config["ConsoleSettings:EnableConsole"] ?? "false");

            if (enableConsole)
            {
                AllocConsole(); // ✅ create console
                Console.Title = "FRCM Console Logs";
            }
            else
            {
                var handle = GetConsoleWindow();
                if (handle != IntPtr.Zero)
                {
                    ShowWindow(handle, SW_HIDE);
                }
            }
        }


        [STAThread]
        static void Main()
        {
            var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

            HandleConsoleVisibility(config); // ✅ FIRST → create console
            SetupConsoleLogging(config);     // ✅ THEN → logging

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();

            Application.Idle += (s, e) =>
            {
                foreach (Form form in Application.OpenForms)
                {
                    if (form.Tag?.ToString() != "FontApplied")
                    {
                        FRCM.Services.FontSizeHelper.UpdateControlRecursive(
                            form,
                            FRCM.Services.FontSizeHelper.CurrentMultiplier
                        );

                        form.Tag = "FontApplied";
                    }
                }
            };

            Application.SetCompatibleTextRenderingDefault(false);

            // Global exception handlers: prevent silent crashes on background threads
            Application.ThreadException += (s, ex) =>
            {
                Console.WriteLine($"[UNHANDLED UI THREAD EXCEPTION] {ex.Exception.GetType().Name}: {ex.Exception.Message}");
                Console.WriteLine(ex.Exception.StackTrace);
                // Don't crash - just log and continue
            };

            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                var exception = ex.ExceptionObject as Exception;
                Console.WriteLine($"[UNHANDLED DOMAIN EXCEPTION] {exception?.GetType().Name}: {exception?.Message}");
                Console.WriteLine(exception?.StackTrace);
                // Log but don't terminate - let the app recover if possible
            };

            TaskScheduler.UnobservedTaskException += (s, ex) =>
            {
                Console.WriteLine($"[UNOBSERVED TASK EXCEPTION] {ex.Exception.GetType().Name}: {ex.Exception.Message}");
                foreach (var inner in ex.Exception.InnerExceptions)
                    Console.WriteLine($"  Inner: {inner.GetType().Name}: {inner.Message}");
                ex.SetObserved(); // Prevent process termination
            };

            // 1. Show IP Entry Form first to allow user to configure server address
            using (var ipForm = new FRCM.View.ServerIpEntryForm())
            {
                if (ipForm.ShowDialog() != DialogResult.OK)
                {
                    Application.Exit();
                    return;
                }
            }

            // 2. Load FRMC host from appsettings.json (if available)
            LoadFrmcHostFromConfig();

            while (true)
            {
                using (var login = new LoginPopup())
                {
                    // User pressed Cancel or closed login
                    if (login.ShowDialog() != DialogResult.OK)
                    {
                        Application.Exit();
                        return;
                    }

                    string username = login.Username;
                    string password = login.Password;
                    string role = login.Role;

                    var loadingForm = new LoadingForm();
                    loadingForm.Show();
                    loadingForm.UpdateStatus("Initializing...");
                    loadingForm.Refresh();

                    var initTask = Task.Run(async () =>
                    {
                        return await RunApplicationAsync(username, password, role, loadingForm);
                    });

                    while (!initTask.IsCompleted)
                    {
                        Application.DoEvents();
                        System.Threading.Thread.Sleep(10);
                    }

                    loadingForm.Close();

                    var initResult = initTask.Result;

                    // ✅ SUCCESS → start app
                    if (initResult != null && initResult.TemperatureDataService != null)
                    {
                        var mainForm = new Form1(
                            initResult.TemperatureDataService,
                            initResult.Logger,
                            initResult.Username,
                            initResult.Password,
                            initResult.WebSocketClient,
                            initResult.ConfigurationService,
                            initResult.Role);

                        Application.Run(mainForm);
                        return; // EXIT Main after app closes
                    }

                    // ❌ CONNECTION ERROR → show retry dialog
                    if (initResult?.IsConnectionError == true)
                    {
                        var retry = MessageBox.Show(
                            "Unable to connect to FRMC server.\n\n" +
                            "Please ensure FRMC is running and try again.\n\n" +
                            $"Details: {initResult.ErrorMessage}",
                            "Connection Failed",
                            MessageBoxButtons.RetryCancel,
                            MessageBoxIcon.Error
                        );

                        if (retry == DialogResult.Cancel)
                        {
                            Application.Exit();
                            return;
                        }
                        // Retry → loop continues
                        continue;
                    }

                    // ❌ AUTH ERROR → show error and retry
                    if (initResult?.IsAuthError == true)
                    {
                        MessageBox.Show(
                            $"Authentication failed.\n\n{initResult.ErrorMessage ?? "Invalid username or password."}\n\nPlease try again.",
                            "Login Failed",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error
                        );
                        // Loop continues → LoginPopup opens again
                        continue;
                    }

                    // ❌ OTHER ERROR → show error and retry
                    MessageBox.Show(
                        initResult?.ErrorMessage ?? "An unexpected error occurred.\n\nPlease try again.",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                    // Loop continues → LoginPopup opens again
                }
            }
        }

        private static async Task<InitializationResult?> RunApplicationAsync(string username, string password, string role, LoadingForm loadingForm)
        {
            try
            {
                loadingForm.UpdateStatus("Initializing...");

                ILogger logger = new FileLogger("client_debug.log");
                IAuthService authService = new AuthService(logger);

                WebSocketClient = new CustomWebSocketClient();

                // Subscribe to session expiration event
                // FIX: Avoid async void by using fire-and-forget with proper error handling
                WebSocketClient.SessionExpired += (sender, e) =>
                {
                    Console.WriteLine("[SESSION] Session expired event fired");
                    // Fire and forget with error handling - don't use async void
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await HandleSessionExpiredAsync(username);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[SESSION ERROR] Failed to handle session expiration: {ex.Message}");
                        }
                    });
                };

                // NOTE: ConnectionLost is handled inside Form1.OnMainWebSocketConnectionLost
                // which retries for 10 seconds then shows a popup - do NOT also call Application.Exit() here.

                try
                {
                    // Step 1: Connect to WebSocket (no token needed initially)
                    loadingForm.UpdateStatus($"Connecting to FRMC server ({FrmcHost})...");

                    await WebSocketClient.ConnectAsync(
                        GetControlWebSocketUri(),
                        "connecting").ConfigureAwait(false); // Use placeholder token for initial connection and leverage centralized URI generation

                    Console.WriteLine($"✅ WebSocket connected to FRMC at {FrmcHost}.");

                    // Step 2: Authenticate with FRMC using the new login system
                    loadingForm.UpdateStatus($"Authenticating as {username}...");

                    Console.WriteLine($"🔐 Authenticating as {username}...");
                    var loginResponse = await WebSocketClient.LoginAsync(username, password).ConfigureAwait(false);

                    if (loginResponse == null || !loginResponse.Success)
                    {
                        string errorMessage = loginResponse?.Message ?? "Invalid username or password.";
                        Console.WriteLine($"[LOGIN FAILED] {errorMessage}");
                        await WebSocketClient.DisconnectAsync().ConfigureAwait(false);
                        return new InitializationResult
                        {
                            ErrorMessage = errorMessage,
                            IsAuthError = true
                        };
                    }


                    Console.WriteLine($"✅ Authenticated successfully!");
                    Console.WriteLine($"   Username: {loginResponse.Username}");
                    Console.WriteLine($"   Role: {loginResponse.Role}");
                    Console.WriteLine($"   Session ID: {loginResponse.SessionId}");

                    // Use the actual role from FRMC, not the one selected in UI
                    string actualRole = loginResponse.Role ?? role;

                    loadingForm.SetProgress(25);

                    ChannelClient channelClient = new ChannelClient(WebSocketClient);

                    // Instantiate and load ConfigurationService
                    IConfigurationService configurationService = new ConfigurationService(channelClient);

                    loadingForm.UpdateStatus("Loading DTS Configuration...");

                    Console.WriteLine("📥 Loading DTS Configuration...");
                    try
                    {
                        await configurationService.LoadConfigurationAsync().ConfigureAwait(false); // This loads sequentially now
                        Console.WriteLine("✅ DTS Configuration loaded successfully!");
                        loadingForm.SetProgress(60);
                    }
                    catch (Exception configEx)
                    {
                        Console.WriteLine($"❌ Failed to load configuration: {configEx.Message}");
                        await WebSocketClient.DisconnectAsync().ConfigureAwait(false);
                        return new InitializationResult
                        {
                            ErrorMessage = $"Failed to load configuration from FRMC:\n\n{configEx.Message}",
                            IsConnectionError = false,
                            IsAuthError = false
                        };
                    }

                    loadingForm.UpdateStatus("Starting temperature data service...");

                    Console.WriteLine("🌡️ Starting temperature data service...");
                    ITemperatureDataService temperatureDataService =
                       new TemperatureDataService(WebSocketClient, authService, logger);

                    try
                    {
                        await temperatureDataService.StartReceivingData(username, password).ConfigureAwait(false);
                        Console.WriteLine("✅ Temperature data service started!");
                        loadingForm.SetProgress(85);
                    }
                    catch (Exception tempEx)
                    {
                        Console.WriteLine($"⚠️ Temperature service warning: {tempEx.Message}");
                        // Don't fail startup if temperature service has issues
                    }

                    loadingForm.UpdateStatus("Launching main application...");
                    loadingForm.SetProgress(100);
                    await Task.Delay(500).ConfigureAwait(false); // Brief pause to show completion

                    Console.WriteLine("🖥️ Initialization complete, ready to launch main application...");

                    // Return initialization data (Form1 will be created on UI thread)
                    return new InitializationResult
                    {
                        TemperatureDataService = temperatureDataService,
                        Logger = logger,
                        Username = loginResponse.Username ?? username,
                        Password = password,
                        WebSocketClient = WebSocketClient,
                        ConfigurationService = configurationService,
                        Role = actualRole
                    };
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[STARTUP ERROR] {ex.Message}");

                    // Check if this is a connection error
                    bool isConnectionError = IsConnectionException(ex);

                    return new InitializationResult
                    {
                        ErrorMessage = isConnectionError
                            ? ex.Message
                            : $"Startup error: {ex.Message}",
                        IsConnectionError = isConnectionError,
                        IsAuthError = false
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FATAL ERROR] {ex.Message}");

                bool isConnectionError = IsConnectionException(ex);

                return new InitializationResult
                {
                    ErrorMessage = isConnectionError
                        ? ex.Message
                        : $"Fatal error: {ex.Message}",
                    IsConnectionError = isConnectionError,
                    IsAuthError = false
                };
            }
        }

        /// <summary>
        /// Determines if an exception is related to connection failure (FRMC not running, network issues, etc.)
        /// </summary>
        private static bool IsConnectionException(Exception ex)
        {
            // Check the exception type
            string typeName = ex.GetType().Name;

            if (typeName.Contains("WebSocket") ||
                typeName.Contains("Socket") ||
                typeName.Contains("Http") ||
                typeName.Contains("Network") ||
                typeName.Contains("Connection"))
            {
                return true;
            }

            // Check common connection error messages
            string message = ex.Message.ToLowerInvariant();
            if (message.Contains("connection") ||
                message.Contains("refused") ||
                message.Contains("unreachable") ||
                message.Contains("timeout") ||
                message.Contains("websocket") ||
                message.Contains("socket") ||
                message.Contains("network") ||
                message.Contains("no connection") ||
                message.Contains("host"))
            {
                return true;
            }

            // Check inner exceptions
            if (ex.InnerException != null)
            {
                return IsConnectionException(ex.InnerException);
            }

            return false;
        }

        /// <summary>
        /// Loads FRMC host address from appsettings.json.
        /// Configuration key: "FrmcConfiguration:DefaultIpAddress" or "Frmc:Host"
        /// Falls back to "localhost" if not configured.
        ///
        /// Example appsettings.json:
        /// {
        ///   "FrmcConfiguration": {
        ///     "DefaultIpAddress": "192.168.0.124"
        ///   }
        /// }
        /// </summary>
        private static void LoadFrmcHostFromConfig()
        {
            try
            {
                var configPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (System.IO.File.Exists(configPath))
                {
                    var config = new ConfigurationBuilder()
                        .AddJsonFile(configPath, optional: true)
                        .Build();

                    // Load FRMC host address
                    var host = config["FrmcConfiguration:DefaultIpAddress"];
                    if (string.IsNullOrWhiteSpace(host))
                    {
                        host = config["Frmc:Host"];
                    }

                    if (!string.IsNullOrWhiteSpace(host))
                    {
                        FrmcHost = host;
                        Console.WriteLine($"[CONFIG] FRMC host: {FrmcHost}");
                    }
                    else
                    {
                        Console.WriteLine($"[CONFIG] FRMC host not configured, using default: {FrmcHost}");
                    }

                    // Load Control WebSocket port (port 6164)
                    var controlPortStr = config["FrmcConfiguration:ControlPort"] ?? config["WebSocketServers:ControlPort"];
                    if (!string.IsNullOrWhiteSpace(controlPortStr) && int.TryParse(controlPortStr, out int controlPort))
                    {
                        FrmcControlPort = controlPort;
                    }
                    Console.WriteLine($"[CONFIG] FRMC Control Port: {FrmcControlPort}");

                    // Load Data Stream WebSocket port (port 6165)
                    var dataPortStr = config["FrmcConfiguration:DataStreamPort"] ?? config["WebSocketServers:DataStreamPort"];
                    if (!string.IsNullOrWhiteSpace(dataPortStr) && int.TryParse(dataPortStr, out int dataPort))
                    {
                        FrmcDataStreamPort = dataPort;
                    }
                    Console.WriteLine($"[CONFIG] FRMC Data Stream Port: {FrmcDataStreamPort}");

                    // Load UseSecure setting (WSS/HTTPS option)
                    var useSecureStr = config["FrmcConfiguration:UseSecure"] ?? config["WebSocketServers:UseSecure"];
                    if (!string.IsNullOrWhiteSpace(useSecureStr) && bool.TryParse(useSecureStr, out bool useSecure))
                    {
                        FrmcUseSecure = useSecure;
                    }
                    Console.WriteLine($"[CONFIG] FRMC Use Secure: {FrmcUseSecure}");
                }
                else
                {
                    Console.WriteLine($"[CONFIG] appsettings.json not found, using defaults:");
                    Console.WriteLine($"[CONFIG]   Host: {FrmcHost}");
                    Console.WriteLine($"[CONFIG]   Control Port: {FrmcControlPort}");
                    Console.WriteLine($"[CONFIG]   Data Stream Port: {FrmcDataStreamPort}");
                    Console.WriteLine($"[CONFIG]   Use Secure: {FrmcUseSecure}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CONFIG] Error loading config: {ex.Message}, using defaults");
            }
        }

        private static void HandleConnectionLost()
        {
            // Use Invoke to run on UI thread
            if (Application.OpenForms.Count > 0)
            {
                var mainForm = Application.OpenForms[0];
                try
                {
                    mainForm.Invoke(new Action(() =>
                    {
                        Console.WriteLine("[CONNECTION] Showing connection lost dialog");

                        MessageBox.Show(
                            "Connection to FRMC has been lost.\n\n" +
                            "The FRMC server may have been stopped or the network connection was interrupted.\n\n" +
                            "The application will now close. Please ensure FRMC is running and try again.",
                            "Connection Lost",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);

                        Console.WriteLine("[CONNECTION] Exiting application due to connection loss");
                        Application.Exit();
                    }));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CONNECTION ERROR] Failed to show dialog: {ex.Message}");
                    Application.Exit();
                }
            }
        }

        private static async Task HandleSessionExpiredAsync(string lastUsername)
        {
            // FIX: Avoid async void by separating UI operations from async operations
            // UI dialogs run synchronously on UI thread, async operations run on background thread
            if (Application.OpenForms.Count == 0)
                return;

            var mainForm = Application.OpenForms[0];

            // Step 1: Show session expired dialog on UI thread (synchronous)
            DialogResult initialResult = DialogResult.Cancel;
            mainForm.Invoke(new Action(() =>
            {
                Console.WriteLine("[REAUTH] Session expired, showing re-login dialog");
                initialResult = MessageBox.Show(
                    $"Your session has expired.\n\nPlease log in again to continue.",
                    "Session Expired",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Warning);
            }));

            if (initialResult != DialogResult.OK)
            {
                // User cancelled - exit application
                mainForm.Invoke(new Action(() =>
                {
                    MessageBox.Show(
                        "The application will now close due to session expiration.",
                        "Session Expired",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    Application.Exit();
                }));
                return;
            }

            // Step 2: Show login dialog on UI thread (synchronous)
            string? username = null;
            string? password = null;
            bool loginDialogOk = false;

            mainForm.Invoke(new Action(() =>
            {
                using (var reloginDialog = new LoginPopup())
                {
                    if (reloginDialog.ShowDialog() == DialogResult.OK)
                    {
                        username = reloginDialog.Username;
                        password = reloginDialog.Password;
                        loginDialogOk = true;
                    }
                }
            }));

            if (!loginDialogOk || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                // User cancelled re-login
                mainForm.Invoke(new Action(() =>
                {
                    MessageBox.Show(
                        "You have chosen not to re-authenticate.\n\nThe application will now close.",
                        "Session Expired",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    Application.Exit();
                }));
                return;
            }

            // Step 3: Perform async re-authentication on background thread
            Console.WriteLine($"[REAUTH] Attempting to re-authenticate as {username}");

            try
            {
                // Reconnect WebSocket if needed
                if (WebSocketClient != null && !WebSocketClient.IsConnected)
                {
                    await WebSocketClient.ConnectAsync(
                        GetControlWebSocketUri(),
                        "reconnecting").ConfigureAwait(false);
                    Console.WriteLine($"[REAUTH] WebSocket reconnected to {FrmcHost}");
                }

                // Re-authenticate
                var loginResponse = await WebSocketClient!.LoginAsync(username, password).ConfigureAwait(false);

                // Step 4: Show result on UI thread (synchronous)
                if (loginResponse != null && loginResponse.Success)
                {
                    Console.WriteLine($"[REAUTH] Successfully re-authenticated as {loginResponse.Username}");
                    mainForm.Invoke(new Action(() =>
                    {
                        MessageBox.Show(
                            $"Welcome back, {loginResponse.Username}!\n\nYou have been successfully re-authenticated.",
                            "Re-authentication Successful",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }));
                }
                else
                {
                    string errorMessage = loginResponse?.Message ?? "Re-authentication failed";
                    mainForm.Invoke(new Action(() =>
                    {
                        MessageBox.Show(
                            $"Re-authentication Failed:\n\n{errorMessage}\n\nThe application will now close.",
                            "Re-authentication Failed",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        Application.Exit();
                    }));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[REAUTH ERROR] {ex.Message}");
                mainForm.Invoke(new Action(() =>
                {
                    MessageBox.Show(
                        $"Re-authentication Error:\n\n{ex.Message}\n\nThe application will now close.",
                        "Re-authentication Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    Application.Exit();
                }));
            }
        }
    }

    class MultiTextWriter : TextWriter
    {
        private readonly TextWriter _console;
        private readonly TextWriter _file;

        public MultiTextWriter(TextWriter console, TextWriter file)
        {
            _console = console;
            _file = file;
        }

        public override Encoding Encoding => _console.Encoding;

        public override void WriteLine(string? value)
        {
            _console.WriteLine(value);
            _file.WriteLine(value);
        }

        public override void Write(char value)
        {
            _console.Write(value);
            _file.Write(value);
        }
    }

}