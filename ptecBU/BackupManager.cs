using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;

public class BackupManager
{
    public static event Action<string> StatusUpdated;
    static string hostname = Environment.MachineName;
    public static string backupDestination = Path.Combine(ConfigurationManager.Config["destination"], hostname);
    public static bool IsBlinking { get; private set; }
    private static CancellationTokenSource BlinkCancellationTokenSource;
    private static TimeSpan robocopyDuration;
    private static TimeSpan zipDuration;
    private static Task BlinkTask;
    private static Icon GreenIcon;
    private static Icon YellowIcon;
    private static Icon RedIcon;

    private static void UpdateStatus(string message)
    {
        StatusUpdated?.Invoke(message);
    }

    public static async Task PerformBackup(bool exitAfter = false, bool updateUI = false)
    {
        // Initialize the stopwatch at the start of the backup process
        Stopwatch backupTimer = new Stopwatch();
        backupTimer.Start();
        Program.IsBackupInProgress = true;
        robocopyDuration = TimeSpan.Zero;
        zipDuration = TimeSpan.Zero;

        if (updateUI)
        {
            UpdateStatus("Backup in progress...");
        }

        // Set the destination path for the backup
        backupDestination = Path.Combine(ConfigurationManager.Config["destination"], hostname);

        // Ensure the log file is clear or create it if not existing
        string logFilePath = "log/backupDuration.log";
        File.WriteAllText(logFilePath, "");  // Clears the existing log or creates a new one

        // Start blinking the tray icon to indicate the backup process has started
        Debug.WriteLine("Blinking tray icon to indicate backup process has started.");
        GreenIcon = new Icon("Resources/green.ico");
        YellowIcon = new Icon("Resources/yellow.ico");
        RedIcon = new Icon("Resources/red.ico");
        //Set the tray icon to the yellow icon
        Program.trayIcon.Icon = YellowIcon;
        try
        {
            await BlinkTrayIconAsync(true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error blinking tray icon: {ex.Message}");
        }

        // Load global settings
        var globalConfig = ConfigurationManager.Config;

        // Load individual folder settings
        var folderConfigs = FolderConfigManager.LoadFolderConfigs();

        bool onlyMakeZipBackupGlobal = false;
        // If onlyMakeZipBackup is true, perform only the zip backup and skip robocopy
        if (ConfigurationManager.Config.TryGetValue("onlyMakeZipBackup", out var value) && bool.TryParse(value, out onlyMakeZipBackupGlobal) && onlyMakeZipBackupGlobal)
        {
            var archiver = new FolderArchiver(); // Assuming FolderArchiver is accessible and implemented
            try
            {
                // Run the zip archive process and await its completion
                await archiver.ArchiveFolders(updateUI);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error archiving folders: {ex.Message}");
            }
            finally
            {
                // Stop blinking the tray icon to indicate the backup process has completed
                try
                {
                    Debug.WriteLine("Stop blinking tray icon to indicate backup process has completed.");
                    await BlinkTrayIconAsync(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error blinking tray icon: {ex.Message}");
                }

                // Write current timestamp to file
                File.WriteAllText("log/lastBackup.log", DateTime.Now.ToString("o"));

                // Write the full backup duration to the log file
                zipDuration = backupTimer.Elapsed - robocopyDuration;
                File.AppendAllText(logFilePath, $"{DateTime.Now:O} ZIP archiving duration: {zipDuration}\n");

                // Conclude the backup process
                backupTimer.Stop();
                TimeSpan totalBackupDuration = backupTimer.Elapsed;
                File.AppendAllText(logFilePath, $"{DateTime.Now:O} Full backup duration: {totalBackupDuration}\n");
            }
        }
        else
        {
            //CONTINUE WITH ROBOCOPY BACKUP

            // Check if the backup location is reachable
            if (!IsBackupLocationReachable(backupDestination))
            {
                MessageBox.Show($"PtecBU: Destination {backupDestination} unreachable.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                // Stop blinking the tray icon and set it to red to indicate the backup process has completed with errors
                try
                {
                    Debug.WriteLine("Stop blinking tray icon to indicate backup process has completed with errors.");
                    await BlinkTrayIconAsync(false);
                    //set the tray icon to red
                    Program.trayIcon.Icon = RedIcon;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error blinking tray icon: {ex.Message}");
                    //set the tray icon to red
                    Program.trayIcon.Icon = RedIcon;
                }

                return;
            }

            // Load folders to backup
            string[] folders = File.Exists(Program.FolderSource)
                ? File.ReadAllLines(Program.FolderSource).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray()
                : new string[0];

            // Load excluded items list
            string[] excludedItems = File.Exists(Program.ExcludeListSource)
                ? File.ReadAllLines(Program.ExcludeListSource).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray()
                : new string[0];

            // Update tray icon tooltip to "Backup in progress"
            if (!Program.IsRunningWithArguments)
            {
                // Start blinking the tray icon to indicate the backup process has started
                try
                {
                    await BlinkTrayIconAsync(true);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error blinking tray icon: {ex.Message}");
                }
                Program.trayIcon.Text = "Backup in progress";
            }

            // Generate exclusion parameters for robocopy
            // Lists to hold excluded directories and files
            List<string> excludedDirs = new List<string>();
            List<string> excludedFiles = new List<string>();

            foreach (string item in excludedItems)
            {
                if (Directory.Exists(item)) // Folder
                {
                    excludedDirs.Add(item);
                }
                else if (File.Exists(item)) // File
                {
                    excludedFiles.Add(item);
                }
                else if (item.StartsWith(@"*") && (item.EndsWith(@"\") || item.EndsWith(@"/"))) // Folder with wildcard
                {
                    Match match = Regex.Match(item, @"[^*\\/]+");
                    if (match.Success)
                    {
                        excludedDirs.Add(match.Value);
                    }
                }
                else // Assume file extension
                {
                    excludedFiles.Add(item);
                }
            }

            // Generate exclusion parameters for robocopy
            string excludeDirsParams = excludedDirs.Count > 0 ? $"/XD {string.Join(" ", excludedDirs.Select(d => $"\"{d}\""))} " : "";
            string excludeFilesParams = excludedFiles.Count > 0 ? $"/XF {string.Join(" ", excludedFiles.Select(f => $"\"{f}\""))} " : "";

            string excludeParams = excludeDirsParams + excludeFilesParams;

            bool isTwoBackupsEnabled = bool.Parse(globalConfig.GetValueOrDefault("twobackups", "false"));
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

            // Method to generate a short hash for a given folder path
            static string GenerateShortHash(string folderPath)
            {
                byte[] hashBytes = SHA1.HashData(Encoding.UTF8.GetBytes(folderPath));
                // Convert the first 4 bytes of the hash to a hexadecimal string for a shorter identifier
                string shortHash = BitConverter.ToString(hashBytes, 0, 4).Replace("-", "").ToLower();
                return shortHash;
            }

            // Perform backup for each folder
            for (int i = 0; i < folders.Length; i++)
            {
                bool onlyMakeZipBackup = false;
                bool includeZipInBackup = false;
                folderConfigs.TryGetValue(folders[i], out var folderConfig);

                // Check folders individual settings for 'BackupOption' from folderConfigs. Skip robocopy if the folder is zip only (BackupOption=1)
                if (folderConfig != null)
                {
                    switch ((BackupOptions)folderConfig.BackupOption)
                    {
                        case BackupOptions.OnlyMakeZipBackup:
                            onlyMakeZipBackup = true;
                            break;
                        case BackupOptions.IncludeZipInNormalBackup:
                            includeZipInBackup = true;
                            break;
                        case BackupOptions.UseGlobalSetting:
                            // Fallback to global settings
                            onlyMakeZipBackup = bool.Parse(globalConfig.GetValueOrDefault("onlyMakeZipBackup", "false"));
                            includeZipInBackup = bool.Parse(globalConfig.GetValueOrDefault("includeZipInBackup", "false"));
                            break;
                    }
                }

                // Skip robocopy if the folder is set to only make zip backups
                if (onlyMakeZipBackup)
                {
                    continue;
                }

                // Get the source folder
                // Normalize the folder path
                string folder = folders[i].TrimEnd('\\');

                if (updateUI)
                {
                    UpdateStatus("Backing up " + folder);
                }

                // Determine if the source is a drive root
                bool isDriveRoot = Path.GetPathRoot(folder).Equals(folder, StringComparison.OrdinalIgnoreCase);

                string folderNameForDestination;
                if (isDriveRoot)
                {
                    Debug.WriteLine($"Drive root {folder} detected.");

                    // Use the drive letter and append a suffix
                    folderNameForDestination = $"{folder.Replace(":", "")}_drive";

                    // Add backslash to source folder if it's a drive root
                    folder += "\\";
                }
                else
                {
                    // Use the last folder name and append a short hash
                    string shortHash = GenerateShortHash(folder);
                    folderNameForDestination = $"{Path.GetFileName(folder)}_{shortHash}";
                }

                // Build the destination path
                string folderDestination = Path.Combine(backupDestination, folderNameForDestination + destinationSuffix);

                // Validate the folderDestination path
                if (PathValidator.IsValidPath(folderDestination))
                {
                    // Remove trailing backslash from source (if not drive root) and destination
                    if (!isDriveRoot)
                    {
                        folder = folder.TrimEnd('\\');
                    }
                    folderDestination = folderDestination.TrimEnd('\\');

                    // Read robocopymt value from config dictionary
                    string mtValue = globalConfig.GetValueOrDefault("robocopymt", "16");

                    // Build robocopy command
                    string sourcePath = isDriveRoot ? folder : $"\"{folder}\"";
                    string destinationPath = $"\"{folderDestination}\"";
                    string robocopyArgs = $"{sourcePath} {destinationPath} /E /R:1 /W:1 /MT:{mtValue} /Z /LOG:log/backup_{i + 1}.log {excludeParams} /A-:SH";

                    File.WriteAllText("log/robocopyArgs.log", robocopyArgs.ToString());

                    // Before starting a new robocopy process, ensure any existing one is properly handled
                    if (Program.RobocopyProcess != null && !Program.RobocopyProcess.HasExited)
                    {
                        Program.RobocopyProcess.Kill();
                        Program.RobocopyProcess.Dispose();
                    }

                    // Program.IsBackupInProgress is checked if the backup process is cancelled by the user
                    if (!Program.IsBackupInProgress)
                    {
                        // Stop blinking the tray icon to indicate the backup process has completed
                        try
                        {
                            Debug.WriteLine("Stop blinking tray icon to indicate backup process has completed.");
                            await BlinkTrayIconAsync(false);
                            // End the backup process early if it was cancelled
                            return;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error blinking tray icon: {ex.Message}");
                        }
                        return;
                    }

                    // Run robocopy
                    Debug.WriteLine($"Running robocopy for {folder} to {folderDestination}");
                    Debug.WriteLine($"Robocopy arguments: {robocopyArgs}");
                    ProcessStartInfo psiRobocopy = new ProcessStartInfo("robocopy.exe", robocopyArgs);
                    psiRobocopy.CreateNoWindow = true;
                    psiRobocopy.UseShellExecute = false;
                    psiRobocopy.RedirectStandardError = true;  // Add this line to redirect standard error

                    Program.RobocopyProcess = new Process { StartInfo = psiRobocopy };

                    Program.RobocopyProcess.ErrorDataReceived += new DataReceivedEventHandler((sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            // Handle the error output line e.Data
                        }
                    });
                    Program.RobocopyProcess.Start();

                    // Read standard error output asynchronously
                    Program.RobocopyProcess.BeginErrorReadLine();

                    Program.RobocopyProcess.WaitForExit();

                    // Check if Program.RobocopyProcess is null after waiting for exit. This can happen if the process was killed or exited unexpectedly or cancelled by the user.
                    if (Program.RobocopyProcess == null)
                    {
                        // Stop blinking the tray icon to indicate the backup process has completed
                        try
                        {
                            Debug.WriteLine("Stop blinking tray icon to indicate backup process has completed.");
                            await BlinkTrayIconAsync(false);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error blinking tray icon: {ex.Message}");
                        }

                        if (exitAfter)
                        {
                            Application.Exit();
                        }
                        // Exit the PerformBackup method early if the Robocopy process is null
                        return;
                    }

                    if (!File.Exists("log/backup.log"))
                    {
                        File.WriteAllText("log/backup.log", "Error:" + DateTime.Now.ToString("o"));
                    }

                    string logContents = File.ReadAllText("log/backup.log");

                    if (logContents.Contains("ERROR 112"))
                    {
                        // Write current timestamp to file
                        File.WriteAllText("log/lastFail.log", DateTime.Now.ToString("o"));

                        // Show a popup warning that the destination is full
                        MessageBox.Show("Destination drive is full. Backup operation could not be completed.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }

                    // Check if robocopy was successful
                    if (Program.RobocopyProcess.ExitCode <= 7) // robocopy exit codes 0-7 are considered successful
                    {
                        try
                        {
                            // Get the attributes of the directory
                            FileAttributes attributes = File.GetAttributes(folderDestination);

                            // Remove the hidden attribute from the directory
                            if ((attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                            {
                                File.SetAttributes(folderDestination, attributes & ~FileAttributes.Hidden);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log the exception or handle it as needed
                            Debug.WriteLine($"Failed to remove hidden attribute from {folderDestination}: {ex.Message}");
                        }

                        // Write current timestamp to file
                        File.WriteAllText("log/lastBackup.log", DateTime.Now.ToString("o"));
                    }
                    else
                    {
                        // Write current timestamp and exit code to file
                        File.WriteAllText("log/lastFail.log", $"{DateTime.Now.ToString("o")}\nExit code: {Program.RobocopyProcess.ExitCode}");

                        // Show a popup warning that the backup operation failed
                        MessageBox.Show($"Backup operation failed with exit code {Program.RobocopyProcess.ExitCode}.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                        if (Program.RobocopyProcess.ExitCode == 12) // robocopy exit code 12 indicates destination full
                        {
                            // Show a popup warning that the destination is full
                            MessageBox.Show("Destination drive is full. Backup operation could not be completed.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                }
                else
                {
                    // Write current timestamp to file
                    File.WriteAllText("log/lastFail.log", DateTime.Now.ToString("o"));
                }

                // Log the robocopy duration
                robocopyDuration = backupTimer.Elapsed;
                File.AppendAllText(logFilePath, $"{DateTime.Now:O} Robocopy duration ({folder}): {robocopyDuration}\n");
            }

            robocopyDuration = backupTimer.Elapsed;
            File.AppendAllText(logFilePath, $"{DateTime.Now:O} Robocopy total duration: {robocopyDuration}\n");

            // Check if onlyMakeZipBackupGlobal was set to true. Skip the zip archiving process if true since it was already done.
            if (!onlyMakeZipBackupGlobal)
            {
                // Run the archiver in a separate thread. The FolderArchiver class is in ZipHelper.cs
                var archiver = new FolderArchiver();                    // Create a new instance of the FolderArchiver class
                try
                {
                    await archiver.ArchiveFolders(updateUI);   // Run the zip archiver

                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error archiving folders: {ex.Message}");
                }
                finally
                {
                    // Stop blinking the tray icon to indicate the backup process has completed
                    try
                    {
                        Debug.WriteLine("Stop blinking tray icon to indicate backup process has completed.");
                        await BlinkTrayIconAsync(false);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error blinking tray icon: {ex.Message}");
                    }

                    // Write the full backup duration to the log file
                    zipDuration = backupTimer.Elapsed - robocopyDuration;
                    File.AppendAllText(logFilePath, $"{DateTime.Now:O} ZIP archiving duration: {zipDuration}\n");

                    // Conclude the backup process
                    backupTimer.Stop();
                    TimeSpan totalBackupDuration = backupTimer.Elapsed;
                    File.AppendAllText(logFilePath, $"{DateTime.Now:O} Full backup duration: {totalBackupDuration}\n");
                }
            }
            else
            {
                // Stop blinking the tray icon to indicate the backup process has completed
                try
                {
                    Debug.WriteLine("Stop blinking tray icon to indicate backup process has completed.");
                    await BlinkTrayIconAsync(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error blinking tray icon: {ex.Message}");
                }
            }
        }

        if (updateUI)
        {
            // Initialize a timeout or a maximum number of retries
            int maxRetries = 50; // equivalent to 5 seconds if each delay is 100 ms
            int retryCount = 0;

            // Wait for the blinking task to complete before updating the status
            while (IsBlinking && retryCount < maxRetries)
            {
                await Task.Delay(100);
                retryCount++;
            }

            if (retryCount >= maxRetries)
            {
                UpdateStatus("Timeout waiting for blinking to stop.");
            }
            else
            {
                UpdateStatus($"Backup completed in {backupTimer.Elapsed.ToString(@"hh\:mm\:ss")} (Robocopy: {robocopyDuration.ToString(@"hh\:mm\:ss")}, Zip: {zipDuration.ToString(@"hh\:mm\:ss")})");
            }
        }

        Program.IsBackupInProgress = false;

        // Wait for 200ms second before calling Program.UpdateTrayIconTooltip()
        await Task.Delay(200);
        Program.UpdateTrayIconTooltip();

        if (exitAfter)
        {
            Application.Exit();
        }
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


    public static async Task BlinkTrayIconAsync(bool start)
    {
        int blinkDuration = 500; // Duration in milliseconds to wait before switching icons

        if (start && !IsBlinking)
        {
            IsBlinking = true;
            BlinkCancellationTokenSource = new CancellationTokenSource();

            try
            {
                BlinkTask = Task.Run(async () =>
                {
                    try
                    {
                        while (!BlinkCancellationTokenSource.IsCancellationRequested)
                        {
                            // Swap between Green and Yellow icons
                            Program.trayIcon.Icon = Program.trayIcon.Icon == GreenIcon ? YellowIcon : GreenIcon;
                            // Wait for the blink duration or until cancellation is requested. handle task cancellation
                            await Task.Delay(blinkDuration, BlinkCancellationTokenSource.Token);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // This catch block handles task cancellation, ensuring a clean stop
                        Program.trayIcon.Icon = GreenIcon; // Reset to the default icon when blinking stops
                    }
                    finally
                    {
                        IsBlinking = false; // Ensure the blinking flag is reset after stopping
                    }
                }, BlinkCancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error starting blinking task: {ex.Message}");
            }
        }
        else if (!start && IsBlinking)
        {
            Debug.WriteLine("Stopping blinking task.");
            IsBlinking = false;
            if (BlinkCancellationTokenSource != null)
            {
                BlinkCancellationTokenSource.Cancel();
                try
                {
                    await BlinkTask; // Wait for the task to acknowledge cancellation
                }
                catch (OperationCanceledException)
                {
                    Debug.WriteLine("Blinking task was successfully cancelled.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error waiting for blinking task to stop: {ex.Message}");
                }
                finally
                {
                    Debug.WriteLine("Ignore above exception. (Exception thrown: 'System.Threading.Tasks.TaskCanceledException' in System.Private.CoreLib.dll) It is expected.");
                    if (BlinkCancellationTokenSource != null)
                    {
                        BlinkCancellationTokenSource.Dispose();
                        BlinkCancellationTokenSource = null;
                        Debug.WriteLine("Blinking cancellation token source disposed.");
                    }
                }
            }
            Program.trayIcon.Icon = GreenIcon; // Reset to the default icon when stopped
            Program.UpdateTrayIconTooltip(); // Update the tray icon tooltip to the default text
        }
    }


    public static void DisposeIcons()
    {
        GreenIcon?.Dispose();
        YellowIcon?.Dispose();
        RedIcon?.Dispose();
    }

    public static bool IsLastBackupOlderThanOneMonth()   // Check if the last backup was more than a month ago
    {
        if (!File.Exists("log/lastBackup.log"))
        {
            // If the file doesn't exist, assume the last backup was more than a month ago
            return true;
        }

        string timestampStr = File.ReadAllText("log/lastBackup.log");
        DateTime lastBackup;
        if (!DateTime.TryParseExact(timestampStr, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out lastBackup))
        {
            // If the timestamp can't be parsed, assume the last backup was more than a month ago
            return true;
        }

        // Check if the last backup was more than a month ago
        return DateTime.Now - lastBackup > TimeSpan.FromDays(30);
    }

    public static bool IsLastBackupOlderThanConfigHours() // Check if the last backup was older than the defined hours in config.ini
    {
        // Check if the log file exists
        if (!File.Exists("log/lastBackup.log"))
        {
            Debug.WriteLine("No lastBackup.log file found.");
            return true; // Assume backup is needed if no log file exists
        }

        // Read the timestamp from the log file
        string timestampStr = File.ReadAllText("log/lastBackup.log");
        DateTime lastBackup;

        // Try to parse the stored DateTime using the exact format
        if (!DateTime.TryParseExact(timestampStr, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out lastBackup))
        {
            Debug.WriteLine("Failed to parse the last backup timestamp.");
            return true; // Assume backup is needed if parsing fails
        }

        //Debug.WriteLine($"Last backup: {lastBackup}");
        //Debug.WriteLine($"Current time: {DateTime.Now}");
        //Debug.WriteLine($"Difference: {DateTime.Now - lastBackup}");

        // Safely retrieve and parse the backup interval hours from the configuration
        if (ConfigurationManager.Config.TryGetValue("hoursbetweenbackups", out string hoursStr) && int.TryParse(hoursStr, out int hours))
        {
            //Debug.WriteLine($"Config hours: {hours}");
            return DateTime.Now - lastBackup > TimeSpan.FromHours(hours);
        }
        else
        {
            Debug.WriteLine("Failed to retrieve or parse 'hoursbetweenbackups' from config.");
            return true; // Default to needing a backup if the interval is not properly defined or parsing failed
        }
    }


    // Check if backup location is reachable on network or in local drive
    public static bool IsBackupLocationReachable(string customDestination = null)
    {
        try
        {
            string path = customDestination ?? backupDestination;

            if (string.IsNullOrEmpty(path))
                return false;

            // Set up a cancellation token with a 2-second timeout
            using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
            {
                try
                {
                    // Attempt to write and delete a file in the backup location
                    Task<bool> task = Task.Run(() => TryWriteAndDeleteFile(path), cts.Token);
                    return task.Wait(TimeSpan.FromSeconds(2)) && task.Result;  // Wait for the task to complete or timeout
                }
                catch (OperationCanceledException ex)
                {
                    LogError(ex);
                    return false;  // Return false if the operation times out
                }
            }
        }
        catch (Exception ex)
        {
            LogError(ex);
            return false;
        }
    }

    private static bool TryWriteAndDeleteFile(string path)  // Try to write and delete a file to check if the directory is writable
    {
        string testFilePath = Path.Combine(path, "testfile.tmp");

        try
        {
            // Check if the directory exists, create if it doesn't
            var directory = Path.GetDirectoryName(testFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Try to write an empty file
            File.WriteAllText(testFilePath, string.Empty);

            // If successful, delete the file and return true
            File.Delete(testFilePath);
            return true;
        }
        catch
        {
            // If any error occurs, return false
            return false;
        }
    }

    private static void LogError(Exception ex) // Log error to file
    {
        string logFilePath = "log/network_check_error.log";
        string errorMessage = $"Error: {ex.Message}\nStack Trace: {ex.StackTrace}\nTimestamp: {DateTime.Now}\n";

        try
        {
            File.AppendAllText(logFilePath, errorMessage);
            //MessageBox.Show($"Error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception fileEx)
        {
            MessageBox.Show($"Failed to log to file: {fileEx.Message}", "Logging Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

}
