// Build command
// dotnet publish -c Release -r win-x64 --self-contained false
using Accessibility;
using Microsoft.Win32;
using System.Diagnostics;
using System.Globalization;

static class Program
{
    // Add this line to make trayIcon accessible anywhere in the Program class
    public static NotifyIcon trayIcon;
    public static bool destinationReachable;
    public static bool IsRunningWithArguments { get; private set; }
    public static bool IsBackupInProgress = false;
    public static string FolderSource = "folders.txt";
    public static string ExcludeListSource = "excludedItems.txt";
    public static bool FolderListNotEmpty = false;
    public static Process RobocopyProcess;
    public static int BackupTimerInterval = 60000; // 1 minute
    private static Mutex mutex = null;
    private static EventWaitHandle eventWaitHandle = null;

    [STAThread]
    static async Task Main(string[] args)
    {
        //Variable to tell the backupmanager if program is started with arguments. then no trayicon actions or mutex check is needed
        IsRunningWithArguments = args.Length > 0;

        if (!IsRunningWithArguments) // If the program is running normally create a mutex and eventwaithandle
        {
            // Check if the application is already running
            const string mutexName = "PtecBUSingleInstanceApp";
            const string eventWaitHandleName = "PtecBUSingleInstanceAppSignal";

            bool createdNew;
            mutex = new Mutex(true, mutexName, out createdNew);
            eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventWaitHandleName);

            if (!createdNew)
            {
                // Signal the existing instance to bring up the SettingsForm
                Debug.WriteLine("Signaling existing instance to bring up the SettingsForm");
                eventWaitHandle.Set();
                // The application is already running
                return;
            }

            // Task to wait for signals to show SettingsForm
            _ = Task.Run(() => WaitForSignal());
        }

        SystemEvents.SessionSwitch += new SessionSwitchEventHandler(SystemEvents_SessionSwitch);
        Application.ApplicationExit += new EventHandler(Application_ApplicationExit);

        string APP_PATH = Application.ExecutablePath.ToString();
        Environment.CurrentDirectory = Path.GetDirectoryName(APP_PATH);

        // Check if the log directory exists, if not create it
        if (!Directory.Exists("log"))
        {
            Directory.CreateDirectory("log");
        }

        // Check if config.ini exists, if not create it with default values
        if (!File.Exists("config.ini"))
        {
            File.WriteAllText("config.ini", "// This is the configuration file for the backup program.\n// Destination is the location of the backup.\ndestination=\\192.168.11.110\backup\n\n// autobackup enables or disables the automatic backup.\nautobackup=1\n\n//hoursbetweenbackups is the number of hours between backups.\nhoursbetweenbackups=24\n\n//robocopymt is the number of threads to use for robocopy.\nrobocopymt=16\n\n//twobackups enables or disables the two backup system. two first weeks of the moth will have suffix _1 and the last two weeks will have suffix _2\ntwobackups=false\n\n//includeZipInBackup enables or disables the zipping of the backup.\nincludeZipInBackup=false\n\n//onlyMakeZipBackup=true sets the automatic backup to only make zip files of the selected folders and skip the incremental robocopy backup\nonlyMakeZipBackup=false\n\n//skipZipfileComparison=true skips the comparison of the zip files and just makes a new zip file every time\nskipZipfileComparison=false\n\n//defaultMaxZipRetention is the max number of zip files to keep for each folder. 0 means keep all.\ndefaultMaxZipRetention=10\n\n//systemimagedestination is the location of the system image backup.\nsystemimagedestination=\n");
        }
        else
        {
            // Update the config.ini file with missing lines
            UpdateConfigIniFile("config.ini");
        }

        // Load the configuration file
        ConfigurationManager.LoadConfig("config.ini");
        // Update the configuration with command line arguments
        ProcessCommandLineArguments(args);

        string value; // Variable to store the value of the configuration key

        // Check for "-delay" argument in ConfigurationManager.Config
        if (ConfigurationManager.Config.TryGetValue("delay", out value) && int.TryParse(value, out int delay))
        {
            // Delay the program for the specified time
            System.Threading.Thread.Sleep(delay); // Delay for 40 seconds
        }

        // FonderSource and ExcludeListSource are the default sources for the folder list and exclude item list
        // Read the FolderSource from ConfigurationManager.Config and use the default if not found
        FolderSource = ConfigurationManager.Config.TryGetValue("foldersource", out value) ? value : "folders.txt";
        // Read the ExcludeListSource from ConfigurationManager.Config and use the default if not found
        string ExcludeListSource = ConfigurationManager.Config.TryGetValue("excludesource", out value) ? value : "excludedItems.txt";

        // Check that the folders list has content
        IsFolderListNotEmpty(FolderSource);

        // Check that custom foldersource and excludeitemlist exist
        if (!File.Exists(FolderSource))
        {
            MessageBox.Show($"PtecBU: Backup folder list {FolderSource} unreachable.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            //Application.Exit();
        }

        if (!File.Exists(ExcludeListSource))
        {
            MessageBox.Show($"PtecBU: Exclude items list {ExcludeListSource} unreachable.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            //Application.Exit();
        }

        // If -now argument is given then we dont open the tray app. Just do the backup in the background.
        if (ConfigurationManager.Config.TryGetValue("now", out value) && value == "1")
        {
            // Perform backup with the custom destination if provided
            try
            {
                await BackupManager.PerformBackup(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }
        else
        {
            // Ensure Windows Forms is properly initialized
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);


            // Check if folders.txt exists
            if (!File.Exists(FolderSource))
            {
                // If it doesn't exist, show the SettingsForm
                ShowSettingsForm();
            }

            // Create a context menu for the tray icon
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Backup now", null, OnBackupNow);
            contextMenu.Items.Add("Settings", null, OnSettings);
            contextMenu.Items.Add("Exit", null, OnExit);

            // Create a tray icon
            trayIcon = new NotifyIcon()
            {
                //Icon = Properties.Resources.green, // Set this to an appropriate icon
                Icon = new Icon("Resources/green.ico"),
                ContextMenuStrip = contextMenu,
                Visible = true
            };

            // Handle the MouseUp event for the tray icon
            trayIcon.MouseUp += (sender, e) =>
            {
                // Check if the right or left mouse button was clicked
                if (e.Button == MouseButtons.Right || e.Button == MouseButtons.Left)
                {
                    // Display the context menu at the location of the mouse click
                    contextMenu.Show(Cursor.Position);
                }
            };

            // Show a balloon tip for 3 seconds
            // trayIcon.ShowBalloonTip(3000, "Backup Program", "Backup program running", ToolTipIcon.Info);

            // Update the tooltip
            UpdateTrayIconTooltip();


#if DEBUG
            ShowSettingsForm();
#endif

            // Check for "-s" argument in config to open settings form
            if (ConfigurationManager.Config.TryGetValue("s", out value) && value == "1")
            {
                Debug.WriteLine("-s argument found");
                ShowSettingsForm();
            }

            // Run the application
            // Create the custom application context
            var context = new CustomApplicationContext(trayIcon);
            // Run the application with the custom context
            Application.Run(context);
        }
    }

    private static void WaitForSignal()
    {
        while (true)
        {
            eventWaitHandle.WaitOne();
            // Show the SettingsForm when signal is received
            ShowSettingsForm();
        }
    }

    private static void ShowSettingsForm()
    {
        Thread thread = new Thread(() =>
        {
            var settingsForm = new SettingsForm();
            settingsForm.ShowDialog();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    private static void Application_ApplicationExit(object sender, EventArgs e)
    {
        //Call on exit to remove the eventhandler
        OnExit(sender, e);
    }

    // Function to check if folders list has content
    public static bool IsFolderListNotEmpty(string source)
    {
        // Check that the folders list has content
        if (File.Exists(source))
        {
            string[] lines = File.ReadAllLines(source);
            if (lines.Length > 0)
            {
                FolderListNotEmpty = true;
            }
            else
            {
                FolderListNotEmpty = false;
            }
        }
        return FolderListNotEmpty;
    }

    private static void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            // User has logged on or unlocked the session
            case SessionSwitchReason.SessionLogon:
            case SessionSwitchReason.SessionUnlock:
                Application.Restart();
                break;

            // User has logged off or locked the session
            case SessionSwitchReason.SessionLogoff:
            case SessionSwitchReason.SessionLock:
                // Pause the backup timer
                if (CustomApplicationContext.backupTimer != null && CustomApplicationContext.backupTimer.Enabled)
                {
                    // Write to log file that logoff or lock was detected
                    using (StreamWriter sw = File.AppendText("log/logoffOrLockDetected.log"))
                    {
                        sw.WriteLine(DateTime.Now.ToString());
                    }
                    CustomApplicationContext.backupTimer.Stop();
                }
                break;
        }
    }

    private static void ProcessCommandLineArguments(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-destination":
                    if (i + 1 < args.Length) ConfigurationManager.UpdateConfig("destination", args[i + 1]);
                    break;
                // Handle other arguments similarly
                case "-autobackup":
                    if (i + 1 < args.Length) ConfigurationManager.UpdateConfig("autobackup", args[i + 1]);
                    break;
                case "-hoursbetweenbackups":
                    if (i + 1 < args.Length) ConfigurationManager.UpdateConfig("hoursbetweenbackups", args[i + 1]);
                    break;
                case "-robocopymt":
                    if (i + 1 < args.Length) ConfigurationManager.UpdateConfig("robocopymt", args[i + 1]);
                    break;
                case "-twobackups":
                    if (i + 1 < args.Length) ConfigurationManager.UpdateConfig("twobackups", args[i + 1]);
                    break;
                case "-includeZipInBackup":
                    if (i + 1 < args.Length) ConfigurationManager.UpdateConfig("includeZipInBackup", args[i + 1]);
                    break;
                case "-onlyMakeZipBackup":
                    if (i + 1 < args.Length) ConfigurationManager.UpdateConfig("onlyMakeZipBackup", args[i + 1]);
                    break;
                case "-skipZipfileComparison":
                    if (i + 1 < args.Length) ConfigurationManager.UpdateConfig("skipZipfileComparison", args[i + 1]);
                    break;
                case "-defaultMaxZipRetention":
                    if (i + 1 < args.Length) ConfigurationManager.UpdateConfig("defaultMaxZipRetention", args[i + 1]);
                    break;
                case "-systemimagedestination":
                    if (i + 1 < args.Length) ConfigurationManager.UpdateConfig("systemimagedestination", args[i + 1]);
                    break;
                case "-foldersource":   // Custom folder list source
                case "-f":
                    if (i + 1 < args.Length) FolderSource = args[i + 1];
                    break;
                case "-excludesource":  // Custom exclude item list source
                case "-e":
                    if (i + 1 < args.Length) FolderSource = args[i + 1];
                    break;
                case "-now":
                    // set a flag to config to perform the backup immediately
                    ConfigurationManager.UpdateConfig("now", "1");
                    break;
                case "-delay":
                    // Handle delay argument
                    ConfigurationManager.UpdateConfig("delay", args[i + 1]);
                    break;
                case "-s":
                    break;
                case "-debug":
                    // Enable debug mode by setting DEBUG constant?
                    break;
                case "-help":
                    // Display help information
                    break;
                case "-version":
                    // Display version information
                    break;
                case "-exit":
                    // Exit the application
                    break;
            }
        }
    }

    public static void OnBackupNow(object sender, EventArgs e)
    {
        OnBackupNow(sender, e, false); // Calls the modified version with a default for the new parameter
    }
    public static void OnBackupNow(object sender, EventArgs e, bool updateUI = false)
    {
        Debug.WriteLine("Backup now clicked");

        // Start the backup process on a separate thread
        IsBackupInProgress = true;
        try
        {
            Task.Run(async () =>
            {
                try
                {
                    // Perform backup using the ConfigurationManager destination
                    await BackupManager.PerformBackup(false, updateUI);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
                // Check if invoke is required to update the UI from the backup task
                if (trayIcon.ContextMenuStrip.InvokeRequired)
                {
                    IsBackupInProgress = false; // Mark backup as complete
                    trayIcon.ContextMenuStrip.Invoke(new MethodInvoker(UpdateTrayIconTooltip));
                    trayIcon.ContextMenuStrip.Invoke(new MethodInvoker(UpdateTrayMenuItem));
                }
                else
                {
                    IsBackupInProgress = false; // Mark backup as complete
                    UpdateTrayIconTooltip();
                    UpdateTrayMenuItem();
                }
                /*
                // Update UI from the UI thread
                trayIcon.ContextMenuStrip.Invoke(new MethodInvoker(() =>
                {
                    IsBackupInProgress = false; // Mark backup as complete
                    UpdateTrayIconTooltip();
                    UpdateTrayMenuItem(); // Ensure this updates the UI correctly after backup
                }));
                */
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error in backup task run: " + ex.Message);
        }

        // Check backup progress state and update UI accordingly
        // Use 'Invoke' if necessary to ensure thread safety when updating UI elements
        if (trayIcon.ContextMenuStrip.InvokeRequired)
        {
            trayIcon.ContextMenuStrip.Invoke(new MethodInvoker(UpdateTrayIconTooltip));
            trayIcon.ContextMenuStrip.Invoke(new MethodInvoker(UpdateTrayMenuItem));
        }
        else
        {
            UpdateTrayIconTooltip();
            UpdateTrayMenuItem();
        }
    }

    private static async void OnCancelBackup(object sender, EventArgs e)
    {
        if (Program.RobocopyProcess != null && !Program.RobocopyProcess.HasExited)
        {
            Program.RobocopyProcess.Kill();
            Program.RobocopyProcess.Dispose();
            Program.RobocopyProcess = null;
        }

        IsBackupInProgress = false;

        // Call function to run attrib.exe to remove the hidden attribute from all of the destination folders
        RemoveHiddenAttributeFromDestinationFolders();

        // Reset backup status and update tray menu as necessary
        try
        {
            //await StopBlinking();
            await BackupManager.BlinkTrayIconAsync(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }

        UpdateTrayMenuItem();
        // Restart the application to ensure proper cleanup
        Application.Restart();
    }

    // Define RemoveHiddenAttributeFromDestinationFolders() function
    private static void RemoveHiddenAttributeFromDestinationFolders()
    {
        // Get the destination path from the configuration
        string destination = Path.Combine(ConfigurationManager.Config["destination"], Environment.MachineName);

        // Check if the destination path is a valid directory
        if (Directory.Exists(destination))
        {
            // Get all directories in the destination path
            string[] directories = Directory.GetDirectories(destination);

            // Loop through each directory
            foreach (string directory in directories)
            {
                try
                {
                    // Get the attributes of the directory
                    FileAttributes attributes = File.GetAttributes(directory);

                    // Remove the hidden attribute from the directory
                    if ((attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                    {
                        File.SetAttributes(directory, attributes & ~FileAttributes.Hidden);
                    }
                }
                catch (Exception ex)
                {
                    // Log the exception or handle it as needed
                    Console.WriteLine($"Failed to remove hidden attribute from {directory}: {ex.Message}");
                }
            }
        }
    }


    public static void UpdateTrayMenuItem()
    {
        // Assuming 'trayIcon' is your NotifyIcon and it has a ContextMenuStrip assigned
        var backupMenuItem = trayIcon.ContextMenuStrip.Items.Cast<ToolStripMenuItem>().FirstOrDefault(item => item.Text == "Backup now" || item.Text == "Cancel backup");

        if (backupMenuItem != null)
        {
            if (IsBackupInProgress)
            {
                backupMenuItem.Text = "Cancel backup";
                backupMenuItem.Click -= OnBackupNow; // Make sure to remove the previous event handler
                backupMenuItem.Click += OnCancelBackup; // Add the new event handler
            }
            else
            {
                backupMenuItem.Text = "Backup now";
                backupMenuItem.Click -= OnCancelBackup; // Remove the cancel event handler
                backupMenuItem.Click += OnBackupNow; // Add the backup event handler
            }
        }
    }


    public static void UpdateTrayIconTooltip()
    {
        string filePath = "log/lastBackup.log"; // replace with your path

        if (BackupManager.IsBackupLocationReachable(ConfigurationManager.Config["destination"]))
        {
            Program.destinationReachable = true;
        }
        else
        {
            Program.destinationReachable = false;
        }

        if (File.Exists(filePath))
        {
            using (StreamReader sr = File.OpenText(filePath))
            {
                string s = "";
                while ((s = sr.ReadLine()) != null)
                {
                    // assuming that your date is in the first line of the file
                    if (DateTime.TryParse(s, out DateTime lastBackupDateTime))
                    {
                        // Check if the last successful backup was more than a month ago
                        if (BackupManager.IsLastBackupOlderThanOneMonth())
                        {
                            // Change the tray icon to red
                            trayIcon.Icon = new Icon("Resources/red.ico");
                        }
                        else
                        {
                            // Change the tray icon to green
                            trayIcon.Icon = new Icon("Resources/green.ico");
                        }
                    }
                    else
                    {
                        string errorText = "Last successful backup: Date format is incorrect";
                        trayIcon.Text = errorText;
                        // Change the tray icon to red
                        trayIcon.Icon = new Icon("Resources/red.ico");
                    }

                    string lastBackupText = "";

                    if (Program.destinationReachable)
                    {
                        lastBackupText = $"Last backup: {lastBackupDateTime:d.M.yyyy H:mm}";
                    }
                    else
                    {
                        lastBackupText = $"Backup destination unreachable.\nLast backup: {lastBackupDateTime:d.M.yyyy H:mm}";
                    }

                    trayIcon.Text = lastBackupText;
                    break;
                }
            }
        }
        else
        {
            string neverBackupText = "Last successful backup: Never";
            if (!Program.destinationReachable)
            {
                neverBackupText = "Backup destination unreachable.\nLast successful backup: Never";
                // Set the tray icon to red when the destination is not reachable
                trayIcon.Icon = new Icon("Resources/red.ico");
            }
            else
            {
                // Optionally set a different text when the destination is reachable
                neverBackupText = "Backup destination reachable.\nLast successful backup: Never";
                // Set the tray icon to white (or another icon) when the destination is reachable
                trayIcon.Icon = new Icon("Resources/white.ico");
            }
            trayIcon.Text = neverBackupText;
        }
    }

    private static void OnSettings(object sender, EventArgs e)
    {
        // Handle settings clicked
        ShowSettingsForm();
    }

    private static void OnExit(object sender, EventArgs e)
    {
        // Handle exit clicked
        // Check if the RobocopyProcess exists and is running
        if (Program.RobocopyProcess != null && !Program.RobocopyProcess.HasExited)
        {
            Program.RobocopyProcess.Kill();
            Program.RobocopyProcess.Dispose(); // Dispose of the process object
        }

        // Unsubscribe from the SessionSwitch event
        SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;
        // Unsubscribe from the ApplicationExit event
        Application.ApplicationExit -= Application_ApplicationExit;
        // Dispose of the backup timer
        CustomApplicationContext.backupTimer?.Dispose();
        // Exit the application
        Application.Exit();
    }

    // Function to update missing lines to the config.ini file
    private static void UpdateConfigIniFile(string configPath)
    {
        // Check if the config.ini file exists
        if (File.Exists(configPath))
        {
            // Read all lines from the config.ini file
            string[] lines = File.ReadAllLines(configPath);
            // All lines are: destination, autobackup, hoursbetweenbackups, robocopymt, twobackups, includeZipInBackup, onlyMakeZipBackup, skipZipfileComparison, defaultMaxZipRetention, systemimagedestination
            // Check if the lines are missing and add them
            if (!lines.Any(line => line.StartsWith("destination=", StringComparison.OrdinalIgnoreCase)))
            {
                File.AppendAllText(configPath, "\n// Destination is the location of the backup.\n");
                File.AppendAllText(configPath, "destination=\\192.168.11.110\backup\n");
            };
            if (!lines.Any(line => line.StartsWith("autobackup=", StringComparison.OrdinalIgnoreCase)))
            {
                File.AppendAllText(configPath, "\n// autobackup enables or disables the automatic backup.\n");
                File.AppendAllText(configPath, "autobackup=1\n");
            };
            if (!lines.Any(line => line.StartsWith("hoursbetweenbackups=", StringComparison.OrdinalIgnoreCase)))
            {
                File.AppendAllText(configPath, "\n//hoursbetweenbackups is the number of hours between backups.\n");
                File.AppendAllText(configPath, "hoursbetweenbackups=24\n");
            };
            if (!lines.Any(line => line.StartsWith("robocopymt=", StringComparison.OrdinalIgnoreCase)))
            {
                File.AppendAllText(configPath, "\n//robocopymt is the number of threads to use for robocopy.\n");
                File.AppendAllText(configPath, "robocopymt=16\n");
            };
            if (!lines.Any(line => line.StartsWith("twobackups=", StringComparison.OrdinalIgnoreCase)))
            {
                File.AppendAllText(configPath, "\n//twobackups enables or disables the two backup system. two first weeks of the moth will have suffix _1 and the last two weeks will have suffix _2.\n");
                File.AppendAllText(configPath, "twobackups=false\n");
            };
            if (!lines.Any(line => line.StartsWith("includeZipInBackup=", StringComparison.OrdinalIgnoreCase)))
            {
                File.AppendAllText(configPath, "\n//includeZipInBackup enables or disables the zipping of the backup.\n");
                File.AppendAllText(configPath, "includeZipInBackup=false\n");
            };
            if (!lines.Any(line => line.StartsWith("onlyMakeZipBackup=", StringComparison.OrdinalIgnoreCase)))
            {
                File.AppendAllText(configPath, "\n//onlyMakeZipBackup=true sets the automatic backup to only make zip files of the selected folders and skip the incremental robocopy backup.\n");
                File.AppendAllText(configPath, "onlyMakeZipBackup=false\n");
            };
            if (!lines.Any(line => line.StartsWith("skipZipfileComparison=", StringComparison.OrdinalIgnoreCase)))
            {
                File.AppendAllText(configPath, "\n//skipZipfileComparison=true skips the comparison of the zip files and just makes a new zip file every time.\n");
                File.AppendAllText(configPath, "skipZipfileComparison=false\n");
            };
            if (!lines.Any(line => line.StartsWith("defaultMaxZipRetention=", StringComparison.OrdinalIgnoreCase)))
            {
                File.AppendAllText(configPath, "\n//defaultMaxZipRetention is the max number of zip files to keep for each folder. 0 means keep all.\n");
                File.AppendAllText(configPath, "defaultMaxZipRetention=10\n");
            };
            if (!lines.Any(line => line.StartsWith("systemimagedestination=", StringComparison.OrdinalIgnoreCase)))
            {
                File.AppendAllText(configPath, "\n//systemimagedestination is the location of the system image backup.\n");
                File.AppendAllText(configPath, "systemimagedestination=\n");
            };

        };
    }
}

// Class to manage the configuration file
public static class ConfigurationManager
{
    public static Dictionary<string, string> Config { get; private set; } = new Dictionary<string, string>();   // Dictionary to store the configuration values

    public static void LoadConfig(string filePath)
    {
        Config.Clear();
        if (File.Exists(filePath))
        {
            var lines = File.ReadAllLines(filePath);
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith(";") && line.Contains('='))
                {
                    var parts = line.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim();
                        var value = parts[1].Trim();
                        Config[key] = value;
                    }
                }
            }
        }
        else
        {
            Console.WriteLine($"Configuration file not found: {filePath}");
        }
    }

    public static void SaveConfig(string filePath)
    {
        var lines = Config.Select(kv => $"{kv.Key}={kv.Value}");
        File.WriteAllLines(filePath, lines);
    }

    public static void UpdateConfig(string key, string value)
    {
        if (Config.ContainsKey(key))
        {
            Config[key] = value;
        }
        else
        {
            Config.Add(key, value);
        }
    }

    public static bool UpdateIniFile(string key, string value, string filePath = "config.ini")
    {
        try
        {
            // Read all lines from the configuration file
            var lines = File.ReadAllLines(filePath).ToList();
            bool found = false;
            for (int i = 0; i < lines.Count; i++)
            {
                // Split the line into key and value parts to handle cases with spaces and comments
                var lineParts = lines[i].Split('=', 2);
                if (lineParts.Length == 2 && lineParts[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    // Update the line with the new value
                    lines[i] = key + "=" + value;
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                // Append the new key-value pair to the list ensuring proper newline handling
                if (lines.Count > 0 && !lines.Last().EndsWith(Environment.NewLine))
                {
                    lines.Add(""); // Add a new line if the last line does not end with a newline
                }
                lines.Add(key + "=" + value);
            }
            // Write the updated lines back to the file
            File.WriteAllLines(filePath, lines);

            // Reload the configuration to reflect changes
            LoadConfig(filePath);

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error updating ini file: " + ex.Message);
            return false;
        }
    }

}


class CustomApplicationContext : ApplicationContext
{
    private NotifyIcon trayIcon;
    public static System.Windows.Forms.Timer backupTimer; // Make the timer accessible

    public CustomApplicationContext(NotifyIcon trayIcon)
    {
        this.trayIcon = trayIcon;

        // Check the value of autobackup from the configuration
        if (ConfigurationManager.Config.ContainsKey("autobackup") && ConfigurationManager.Config["autobackup"] == "1")
        {
            // Create the backup timer
            CreateBackupTimer();
        }
    }

    // public function to create new backup timer
    public static void CreateBackupTimer()
    {
        backupTimer = new System.Windows.Forms.Timer
        {
            Interval = Program.BackupTimerInterval
        };
        backupTimer.Tick += new EventHandler(BackupTimer_Tick);
        backupTimer.Start();
    }

    private static void BackupTimer_Tick(object sender, EventArgs e)
    {
        string filePath = Program.FolderSource;

        // Write to log file that the backup timer ticked. Overwrite the file if it already exists
        using (StreamWriter sw = File.CreateText("log/backupTimerTick.log"))
        {
            sw.WriteLine(DateTime.Now.ToString());
        }

        // Check if the folder list has content
        Program.IsFolderListNotEmpty(filePath);

        // If the folder list has content, proceed with the backup process
        if (Program.FolderListNotEmpty)
        {
            string[] lines = File.ReadAllLines(filePath);
            if (lines.Length >= 1)
            {
                // Check if the last backup was over 24 hours ago and no backup is currently in progress
                if (BackupManager.IsLastBackupOlderThanConfigHours() && !Program.IsBackupInProgress)
                {
                    // Check if a connection to the backup location exists
                    if (BackupManager.IsBackupLocationReachable(ConfigurationManager.Config["destination"]))
                    {
                        Program.destinationReachable = true;

                        // Set the backup timer interval to the configured value
                        if (Program.BackupTimerInterval != 60000)
                        {
                            Program.BackupTimerInterval = 60000; // 1 minute
                        }

                        // Start the backup process on a separate thread
                        Program.IsBackupInProgress = true;
                        Task.Run(async () =>
                        {
                            try
                            {
                                Debug.WriteLine("Backup timer ticked and starting backup process.");
                                await BackupManager.PerformBackup();
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("Backup timer ticked and backup process failed: " + ex.Message);
                            }
                        });
                        Program.UpdateTrayIconTooltip();
                        Program.UpdateTrayMenuItem();
                    }
                    else
                    {
                        // Change the tray icon to red
                        Program.trayIcon.Icon = new Icon("Resources/red.ico");
                        Program.destinationReachable = false;

                        // Change backup timer interval to 5 minutes if the destination is unreachable
                        Program.BackupTimerInterval = 300000; // 5 minutes
                    }
                }
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            backupTimer?.Stop();
            backupTimer?.Dispose();
            // Hide the tray icon, otherwise it will remain shown until the user mouses over it
            trayIcon.Visible = false;
            // Dispose of the tray icon
            trayIcon?.Dispose();
            BackupManager.DisposeIcons();
        }
        base.Dispose(disposing);
    }
}