using System;
using System.IO;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;


static class Program
{
    // Add this line to make trayIcon accessible anywhere in the Program class
    public static NotifyIcon trayIcon;
    public static bool destinationReachable;
    public static bool IsRunningWithArguments { get; private set; }

    [STAThread]
    static void Main(string[] args)
    {
        string APP_PATH = Application.ExecutablePath.ToString();
        Environment.CurrentDirectory = Path.GetDirectoryName(APP_PATH);
        
        //Variable to tell the backupmanager if program is started with arguments. then no trayicon actions
        IsRunningWithArguments = args.Length > 0;

        // Check for "-delay" argument
        if (args.Length > 0 && args[0] == "-delay")
        {
            System.Threading.Thread.Sleep(40000); // Delay for 40 seconds
        }

        // Check for "-now" argument
        string customDestination = GetCustomDestination(args);
        if (args.Contains("-now"))
        {
            // Perform backup with the custom destination if provided
            BackupManager.PerformBackup(customDestination);
        }
        else
        {
            // Ensure Windows Forms is properly initialized
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Check if folders.txt exists
            if (!File.Exists("folders.txt"))
            {
                // If it doesn't exist, show the SettingsForm
                var settingsForm = new SettingsForm();
                settingsForm.ShowDialog();
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

            // Run the application
            // Create the custom application context
            var context = new CustomApplicationContext(trayIcon);
            // Run the application with the custom context
            Application.Run(context);
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

        // Handle backup now clicked
        BackupManager.PerformBackup(customDestination);

        UpdateTrayIconTooltip();
    }
    public static void UpdateTrayIconTooltip()
    {
        string filePath = "lastBackup.txt"; // replace with your path

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

                        if(Program.destinationReachable)
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
            if(!Program.destinationReachable)
            {
                neverBackupText = "Backup destination unreachable.\nLast successful backup: Never";
                trayIcon.Icon = new Icon("Resources/red.ico");
            }
            trayIcon.Text = neverBackupText;
            trayIcon.Icon = new Icon("Resources/white.ico");
        }
    }

    private static void OnSettings(object sender, EventArgs e)
    {
        // Handle settings clicked
        var settingsForm = new SettingsForm();
        settingsForm.ShowDialog();
    }

    private static void OnExit(object sender, EventArgs e)
    {
        // Handle exit clicked
        Application.ExitThread();
    }

    private static string GetCustomDestination(string[] args)
    {
        // Look for the -destination argument in the command line arguments
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-destination")
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
}

class CustomApplicationContext : ApplicationContext
{
    private NotifyIcon trayIcon;
    private System.Windows.Forms.Timer backupTimer;

    public CustomApplicationContext(NotifyIcon trayIcon)
    {
        this.trayIcon = trayIcon;

        // Check the value of autobackup from the config.ini file
        if (IsAutoBackupEnabled())
        {
            // Create the backup timer
            backupTimer = new System.Windows.Forms.Timer();
            backupTimer.Interval = 60000; // 1 minute
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
        // Check if the last backup was over 24 hours ago and no backup is currently in progress
        if (BackupManager.IsLastBackupOlderThanOneDay() && !BackupManager.isBlinking)
        {
            // Check if a connection to the backup location exists
            if (BackupManager.IsBackupLocationReachable())
            {
                Program.destinationReachable = true;

                string configPath = "config.ini";
                string destinationKey = "destination=";
                string tickDestination = @"\\192.168.11.99\backup\";

                if (File.Exists(configPath))
                {
                    string[] configLines = File.ReadAllLines(configPath);
                    string destinationLine = configLines.FirstOrDefault(line => line.StartsWith(destinationKey, StringComparison.OrdinalIgnoreCase));

                    if (destinationLine != null)
                    {
                        tickDestination = destinationLine.Substring(destinationKey.Length).Trim();
                    }
                }
                
                // Perform backup
                BackupManager.PerformBackup(tickDestination);

                // Update tray icon tooltip text after backup
                Program.UpdateTrayIconTooltip();
            }
            else
            {
                // Change the tray icon to red
                trayIcon.Icon = new Icon("Resources/red.ico");
                Program.destinationReachable = false;
            }
        }
    }


    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            backupTimer?.Stop();
            backupTimer?.Dispose();
            trayIcon?.Dispose();
        }
        base.Dispose(disposing);
    }
}
