// Build command
// dotnet publish -c Release -r win10-x64 --self-contained false
using System;
using System.IO;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Diagnostics;

static class Program
{
    // Add this line to make trayIcon accessible anywhere in the Program class
    public static NotifyIcon trayIcon;
    public static bool destinationReachable;
    public static bool IsRunningWithArguments { get; private set; }
    public static bool IsBackupInProgress = false;
    public static string TickFolderSource = "folders.txt";
    public static Process RobocopyProcess;


    [STAThread]
    static void Main(string[] args)
    {
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
            File.WriteAllText("config.ini", "// This is the configuration file for the backup program.\n// Destination is the location of the backup.\ndestination=\\192.168.11.110\backup\n\n// autobackup enables or disables the automatic backup.\nautobackup=1\n\n//hoursbetweenbackups is the number of hours between backups.\nhoursbetweenbackups=24\n\n//robocopymt is the number of threads to use for robocopy.\nrobocopymt=16\n\n//twobackups enables or disables the two backup system. two first weeks of the moth will have suffix _1 and the last two weeks will have suffix _2\ntwobackups=false\n\n//includeZipInBackup enables or disables the zipping of the backup.\nincludeZipInBackup=false\n\n//onlyMakeZipBackup=true sets the automatic backup to only make zip files of the selected folders and skip the incremental robocopy backup\nonlyMakeZipBackup=false\n\n//skipZipfileComparison=true skips the comparison of the zip files and just makes a new zip file every time\nskipZipfileComparison=false\n\n//defaultMaxZipRetention is the max number of zip files to keep for each folder. 0 means keep all.\ndefaultMaxZipRetention=0\n\n//systemimagedestination is the location of the system image backup.\nsystemimagedestination=\n");
        }

        //Variable to tell the backupmanager if program is started with arguments. then no trayicon actions
        IsRunningWithArguments = args.Length > 0;

        // Check for "-delay" argument
        if (args.Length > 0 && args[0] == "-delay")
        {
            System.Threading.Thread.Sleep(40000); // Delay for 40 seconds
        }

        // Check for arguments
        string customDestination = GetCustomDestination(args);
        string FolderSource = GetCustomFolderSource(args);
        TickFolderSource = FolderSource;
        string ExcludeListSource = GetCustomExcludeListSource(args);

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
        if (args.Contains("-now"))
        {
            // Perform backup with the custom destination if provided
            BackupManager.PerformBackup(customDestination, FolderSource, ExcludeListSource, true);
        }
        else
        {
            // Ensure Windows Forms is properly initialized
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

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
                // Check if the right mouse button was clicked
                if (e.Button == MouseButtons.Right)
                {
                    // Display the context menu at the location of the mouse click
                    contextMenu.Show(Cursor.Position);
                }
            };

            // Update the tooltip
            UpdateTrayIconTooltip();


#if DEBUG
            ShowSettingsForm();
#endif

            // Check for "-s" argument to open settings form
            if (args.Length > 0 && args[0] == "-s")
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

    private static void ShowSettingsForm()
    {
        var settingsForm = new SettingsForm();
        settingsForm.ShowDialog();
    }

    private static void Application_ApplicationExit(object sender, EventArgs e)
    {
        //Call on exit to remove the eventhandler
        OnExit(sender, e);
    }

    private static void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            // User has logged on or unlocked the session
            case SessionSwitchReason.SessionLogon:
            case SessionSwitchReason.SessionUnlock:
                // Resume the backup timer
                if (CustomApplicationContext.backupTimer != null && !CustomApplicationContext.backupTimer.Enabled)
                {
                    CustomApplicationContext.backupTimer.Start();
                }
                break;

            // User has logged off or locked the session
            case SessionSwitchReason.SessionLogoff:
            case SessionSwitchReason.SessionLock:
                // Pause the backup timer
                if (CustomApplicationContext.backupTimer != null && CustomApplicationContext.backupTimer.Enabled)
                {
                    CustomApplicationContext.backupTimer.Stop();
                }
                break;
        }
    }

    private static void OnBackupNow(object sender, EventArgs e)
    {
        // Check if "-destination" argument is provided
        string customDestination = null;
        if (Environment.GetCommandLineArgs().Length > 2 && Environment.GetCommandLineArgs()[1] == "-destination")
        {
            customDestination = Environment.GetCommandLineArgs()[2];
        }

        // Start the backup process on a separate thread
        IsBackupInProgress = true;
        Task.Run(() =>
        {
            BackupManager.PerformBackup(customDestination);
            IsBackupInProgress = false; // Mark backup as complete

            // Update UI from the UI thread
            trayIcon.ContextMenuStrip.Invoke(new MethodInvoker(() =>
            {
                UpdateTrayIconTooltip();
                UpdateTrayMenuItem(); // Ensure this updates the UI correctly after backup
            }));
        });

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

    private static void OnCancelBackup(object sender, EventArgs e)
    {
        if (Program.RobocopyProcess != null && !Program.RobocopyProcess.HasExited)
        {
            Program.RobocopyProcess.Kill();
            Program.RobocopyProcess.Dispose();
            Program.RobocopyProcess = null;
        }

        // Reset backup status and update tray menu as necessary
        StopBlinking();
        UpdateTrayMenuItem();
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

        if (BackupManager.IsBackupLocationReachable())
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

    // Function to stop blinking and reset the icon to green
    public static void StopBlinking()
    {
        // Stop the blinking
        IsBackupInProgress = false;
        BackupManager.BlinkTrayIcon(false);
        // Update tray icon tooltip after backup completion
        UpdateTrayIconTooltip();
        // Update the tray icon to green
        trayIcon.Icon = new Icon("Resources/green.ico");
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

    private static string GetCustomDestination(string[] args)
    {
        // Look for the -destination argument in the command line arguments
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-destination" || args[i] == "-d")
            {
                return args[i + 1];
            }
        }

        // If the -destination argument is not found, check the config.ini file
        string configPath = "config.ini";
        string destinationKey = "destination=";
        string defaultValue = @"\\127.0.0.1\backup\";

        if (File.Exists(configPath))
        {
            string[] configLines = File.ReadAllLines(configPath);
            string destinationLine = configLines.FirstOrDefault(line => line.StartsWith(destinationKey, StringComparison.OrdinalIgnoreCase));

            if (destinationLine != null)
            {
                string customDestination = destinationLine.Substring(destinationKey.Length).Trim();
                return customDestination;
            }
        }

        // Return the default value if no custom destination is found
        return defaultValue;
    }

    private static string GetCustomFolderSource(string[] args)
    {
        // Look for the -foldersource argument in the command line arguments
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-foldersource" || args[i] == "-f")
            {
                return args[i + 1];
            }
        }

        // Return the default value if no custom folder list source is found
        return "folders.txt";
    }

    private static string GetCustomExcludeListSource(string[] args)
    {
        // Look for the -foldersource argument in the command line arguments
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-excludesource" || args[i] == "-e")
            {
                return args[i + 1];
            }
        }

        // Return the default value if no custom exclude item list is found
        return "excludedItems.txt";
    }
}

class CustomApplicationContext : ApplicationContext
{
    private NotifyIcon trayIcon;
    public static System.Windows.Forms.Timer backupTimer; // Make the timer accessible

    public CustomApplicationContext(NotifyIcon trayIcon)
    {
        this.trayIcon = trayIcon;

        // Check the value of autobackup from the config.ini file
        if (IsAutoBackupEnabled())
        {
            // Create the backup timer
            backupTimer = new System.Windows.Forms.Timer();
            backupTimer.Interval = 60000; // 1 minutes
            backupTimer.Tick += BackupTimer_Tick;
            backupTimer.Start();
        }
    }

    private bool IsAutoBackupEnabled()
    {
        // Read the config.ini file and check the value of autobackup
        string configFilePath = "config.ini";
        if (File.Exists(configFilePath))
        {
            string[] lines = File.ReadAllLines(configFilePath);
            foreach (string line in lines)
            {
                if (line.Trim().Equals("autobackup=1", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void BackupTimer_Tick(object sender, EventArgs e)
    {
        string filePath = Program.TickFolderSource;

        // Check if a list of folders to be backed up exist
        if (File.Exists(filePath))
        {
            string[] lines = File.ReadAllLines(filePath);
            if (lines.Length >= 1)
            {
                // Check if the last backup was over 24 hours ago and no backup is currently in progress
                if (BackupManager.IsLastBackupOlderThanConfigHours() && !BackupManager.isBlinking)
                {
                    // Check if a connection to the backup location exists
                    if (BackupManager.IsBackupLocationReachable())
                    {
                        Program.destinationReachable = true;

                        string configPath = "config.ini";
                        string destinationKey = "destination=";
                        string tickDestination = @"\\127.0.0.1\backup\";

                        if (File.Exists(configPath))
                        {
                            string[] configLines = File.ReadAllLines(configPath);
                            string destinationLine = configLines.FirstOrDefault(line => line.StartsWith(destinationKey, StringComparison.OrdinalIgnoreCase));

                            if (destinationLine != null)
                            {
                                tickDestination = destinationLine.Substring(destinationKey.Length).Trim();
                            }
                        }

                        // Start the backup process on a separate thread
                        Program.IsBackupInProgress = true;
                        Task.Run(() =>
                        {
                            BackupManager.PerformBackup(tickDestination);
                        });
                        Program.UpdateTrayIconTooltip();
                        Program.UpdateTrayMenuItem();
                    }
                    else
                    {
                        // Change the tray icon to red
                        trayIcon.Icon = new Icon("Resources/red.ico");
                        Program.destinationReachable = false;
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
        }
        base.Dispose(disposing);
    }
}
