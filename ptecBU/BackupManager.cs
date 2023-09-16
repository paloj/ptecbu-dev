using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;

public class BackupManager
{
    static string hostname = Environment.MachineName;
    static string Destination = GetBackupDestination();

    public static string GetBackupDestination()
    {
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
                return Path.Combine(customDestination, hostname);
            }
        }

        return Path.Combine(defaultValue, hostname);
    }


    public static void Initialize(string customDestination)
    {
        // Append the hostname to the customDestination before setting the Destination
        Destination = customDestination != null ? Path.Combine(customDestination, hostname) : GetBackupDestination();
    }

    public static void PerformBackup(string customDestination = null)
    {
        // Use the customDestination if provided, otherwise use the default destination
        // Append the hostname to the customDestination before using it
        string destination = customDestination != null ? Path.Combine(customDestination, hostname) : Destination;

        // Check if the backup location is reachable
        if (!IsBackupLocationReachable(destination))
        {
            MessageBox.Show($"PtecBU: Destination {destination} unreachable.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Load folders to backup
        string[] folders = File.Exists("folders.txt") 
            ? File.ReadAllLines("folders.txt").Where(x => !string.IsNullOrWhiteSpace(x)).ToArray() 
            : new string[0];

        // Load excluded items
        string[] excludedItems = File.Exists("excludedItems.txt") 
            ? File.ReadAllLines("excludedItems.txt").Where(x => !string.IsNullOrWhiteSpace(x)).ToArray() 
            : new string[0];

        // Update tray icon tooltip to "Backup in progress"
        if (!Program.IsRunningWithArguments)
        {
            // Start blinking tray icon
            BlinkTrayIcon(true);
            Program.trayIcon.Text = "Backup in progress";
        }

        // Generate exclusion parameters for robocopy
        string excludeParams = "";
        foreach (string item in excludedItems)
        {
            // Check if item is a folder, file, or file extension
            if (Directory.Exists(item)) // Folder
            {
                excludeParams += $"/XD \"{item}\" ";
            }
            else if (File.Exists(item)) // File
            {
                excludeParams += $"/XF \"{item}\" ";
            }
            else // Assume file extension
            {
                excludeParams += $"/XF \"{item}\" ";
            }
        }

        bool isTwoBackupsEnabled = IsTwoBackupsEnabled();
        string destinationSuffix = "";

        if (isTwoBackupsEnabled)
        {
            // Decide the backup folder suffix (_1 or _2) based on the current day of the month
            int currentDayOfMonth = DateTime.Now.Day;
            destinationSuffix = currentDayOfMonth <= 15 ? "_1" : "_2";
        }
        else
        {
            destinationSuffix = "_1"; // Always use the "_1" suffix if twobackups is disabled
        }

        // Perform backup for each folder
        for (int i = 0; i < folders.Length; i++)
        {
            string folder = folders[i];
            
            // Build the destination path with numeration
            string folderDestination = Path.Combine(destination, Path.GetFileName(folder)+destinationSuffix, (i + 1).ToString());
            
            // Validate the folderDestination path
            if (PathValidator.IsValidPath(folderDestination))
            {
                // Build robocopy command
                string robocopyArgs = $"\"{folder}\" \"{folderDestination} \" /E /R:1 /W:1 /MT:16 /Z /LOG:backup.log {excludeParams}";
                File.WriteAllText("robocopyArgs.txt", robocopyArgs.ToString());
                
                // Run robocopy
                ProcessStartInfo psiRobocopy = new ProcessStartInfo("robocopy.exe", robocopyArgs);
                psiRobocopy.CreateNoWindow = true;
                psiRobocopy.UseShellExecute = false;
                psiRobocopy.RedirectStandardError = true;  // Add this line to redirect standard error
                Process pRobocopy = Process.Start(psiRobocopy);

                // Capture the standard error output
                string errorOutput = pRobocopy.StandardError.ReadToEnd();

                pRobocopy.WaitForExit();

                if (!File.Exists("backup.log"))
                {
                    File.WriteAllText("backup.log", "Error:" + DateTime.Now.ToString("o"));
                }

                string logContents = File.ReadAllText("backup.log");

                if (logContents.Contains("ERROR 112"))
                {
                    // Write current timestamp to file
                    File.WriteAllText("lastFail.txt", DateTime.Now.ToString("o"));

                    // Show a popup warning that the destination is full
                    MessageBox.Show("Destination drive is full. Backup operation could not be completed.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                // Check if robocopy was successful
                if (pRobocopy.ExitCode <= 7) // robocopy exit codes 0-7 are considered successful
                {
                    // Write current timestamp to file
                    File.WriteAllText("lastBackup.txt", DateTime.Now.ToString("o"));
                }
                else
                {
                    // Write current timestamp and exit code to file
                    File.WriteAllText("lastFail.txt", $"{DateTime.Now.ToString("o")}\nExit code: {pRobocopy.ExitCode}");

                    // Show a popup warning that the backup operation failed
                    MessageBox.Show($"Backup operation failed with exit code {pRobocopy.ExitCode}.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                    if (pRobocopy.ExitCode == 12) // robocopy exit code 12 indicates destination full
                    {
                        // Show a popup warning that the destination is full
                        MessageBox.Show("Destination drive is full. Backup operation could not be completed.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
            else
            {
                // Write current timestamp to file
                File.WriteAllText("lastFail.txt", DateTime.Now.ToString("o"));
            }

        }

        // Stop blinking tray icon
        BlinkTrayIcon(false);

        // Update tray icon tooltip after backup completion
        Program.UpdateTrayIconTooltip();
    }

    private static bool IsTwoBackupsEnabled()
    {
        string configPath = "config.ini";
        string backupKey = "twobackups=";
        
        if (File.Exists(configPath))
        {
            string[] configLines = File.ReadAllLines(configPath);
            string backupLine = configLines.FirstOrDefault(line => line.StartsWith(backupKey, StringComparison.OrdinalIgnoreCase));

            if (backupLine != null)
            {
                string backupValue = backupLine.Substring(backupKey.Length).Trim().ToLower();
                return backupValue == "true";
            }
        }
        
        // Return false as default if the config file or twobackups setting does not exist
        return false;
    }

    public static class PathValidator
    {
        public static bool IsValidPath(string path)
        {
            try
            {
                // Attempt to get the root directory of the path
                string root = Path.GetPathRoot(path);
                return !string.IsNullOrEmpty(root);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }


    public static bool isBlinking = false;
    private static CancellationTokenSource blinkingCancellationTokenSource;
    private static Task blinkingTask;

    private static async void BlinkTrayIcon(bool start)
    {
        // Define the path to the green and yellow icons
        string greenIconPath = "Resources/green.ico";
        string yellowIconPath = "Resources/yellow.ico";

        // Define the duration of each icon state in milliseconds
        int iconDuration = 500; // 0.5 seconds

        // Get the handle to the green and yellow icons
        IntPtr greenIconHandle = new Icon(greenIconPath).Handle;
        IntPtr yellowIconHandle = new Icon(yellowIconPath).Handle;

        if (start && !isBlinking)
        {
            isBlinking = true;

            // Create a cancellation token source for stopping the blinking animation
            blinkingCancellationTokenSource = new CancellationTokenSource();

            // Start the blinking animation task
            blinkingTask = Task.Run(async () =>
            {
                // Alternate between the green and yellow icons while backup is in progress
                bool isGreenIconVisible = true;
                while (isBlinking && !blinkingCancellationTokenSource.Token.IsCancellationRequested)
                {
                    // Set the tray icon to the green or yellow icon based on the current state
                    if (isGreenIconVisible)
                    {
                        Program.trayIcon.Icon = Icon.FromHandle(greenIconHandle);
                    }
                    else
                    {
                        Program.trayIcon.Icon = Icon.FromHandle(yellowIconHandle);
                    }

                    Application.DoEvents(); // Allow the icon to be updated
                    await Task.Delay(iconDuration); // Delay between icon changes

                    // Toggle the icon state
                    isGreenIconVisible = !isGreenIconVisible;
                }

                // Set the tray icon to the green icon when backup is completed or stopped
                Program.trayIcon.Icon = Icon.FromHandle(greenIconHandle);
            }, blinkingCancellationTokenSource.Token);
        }
        else if (!start && isBlinking)
        {
            isBlinking = false;

            // Stop the blinking animation
            blinkingCancellationTokenSource?.Cancel();
            await blinkingTask;
        }
    }

    public static bool IsLastBackupOlderThanOneMonth()
    {
        if (!File.Exists("lastBackup.txt"))
        {
            // If the file doesn't exist, assume the last backup was more than a month ago
            return true;
        }

        string timestampStr = File.ReadAllText("lastBackup.txt");
        DateTime lastBackup;
        if (!DateTime.TryParseExact(timestampStr, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out lastBackup))
        {
            // If the timestamp can't be parsed, assume the last backup was more than a month ago
            return true;
        }

        // Check if the last backup was more than a month ago
        return DateTime.Now - lastBackup > TimeSpan.FromDays(30);
    }

    public static bool IsLastBackupOlderThanOneDay()
    {
        if (!File.Exists("lastBackup.txt"))
        {
            // If the file doesn't exist, assume the last backup was more than a day ago
            return true;
        }

        string timestampStr = File.ReadAllText("lastBackup.txt");
        DateTime lastBackup;
        if (!DateTime.TryParseExact(timestampStr, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out lastBackup))
        {
            // If the timestamp can't be parsed, assume the last backup was more than a day ago
            return true;
        }

        // Check if the last backup was more than a day ago
        return DateTime.Now - lastBackup > TimeSpan.FromDays(1);
    }

    public static bool IsBackupLocationReachable(string destination = null)
    {
        // Extract the first part of the destination path
        string firstPart = Path.GetPathRoot(destination ?? GetBackupDestination());

        // Check if the first part of the path is reachable
        return Directory.Exists(firstPart);
    }

}
