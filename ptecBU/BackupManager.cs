using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;

public class BackupManager
{
    static string hostname = Environment.MachineName;
    static string Destination = GetBackupDestination();

    public static bool IsBlinking { get; private set; }
    private static CancellationTokenSource BlinkCancellationTokenSource;
    private static Task BlinkTask;
    private static Icon GreenIcon;
    private static Icon YellowIcon;

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

    public static async Task PerformBackup(string customDestination = null, string folderSource = "folders.txt", string excludeSource = "excludedItems.txt", bool exitAfter = false)
    {
        // Initialize the stopwatch at the start of the backup process
        Stopwatch backupTimer = new Stopwatch();
        backupTimer.Start();

        // Ensure the log file is clear or create it if not existing
        string logFilePath = "log/backupDuration.log";
        File.WriteAllText(logFilePath, "");  // Clears the existing log or creates a new one
        TimeSpan robocopyDuration = TimeSpan.Zero;

        // Start blinking tray icon to indicate backup is in progress
        Program.IsBackupInProgress = true;
        // Start blinking the tray icon to indicate the backup process has started
        Debug.WriteLine("Blinking tray icon to indicate backup process has started.");
        GreenIcon = new Icon("Resources/green.ico");
        YellowIcon = new Icon("Resources/yellow.ico");
        try
        {
            await BlinkTrayIconAsync(true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error blinking tray icon: {ex.Message}");
        }

        // Load global settings
        var globalConfig = AppConfigManager.ReadConfigIni("config.ini");

        // Load individual folder settings
        var folderConfigs = FolderConfigManager.LoadFolderConfigs();

        // If onlyMakeZipBackup is true, perform only the zip backup and skip robocopy
        if (IsOnlyMakeZipBackupEnabled())
        {
            var archiver = new FolderArchiver(); // Assuming FolderArchiver is accessible and implemented
            try
            {
                // Run the zip archive process in a separate thread and await its completion
                await Task.Run(() => archiver.ArchiveFolders(false));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error archiving folders: {ex.Message}");
            }
        }
        else
        {
            //CONTINUE WITH ROBOCOPY BACKUP

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
            string[] folders = File.Exists(folderSource)
                ? File.ReadAllLines(folderSource).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray()
                : new string[0];

            // Load excluded items list
            string[] excludedItems = File.Exists(excludeSource)
                ? File.ReadAllLines(excludeSource).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray()
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
                var onlyMakeZipBackup = false;
                var includeZipInBackup = false;
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

                // Determine if the source is a drive root
                bool isDriveRoot = Path.GetPathRoot(folder).Equals(folder, StringComparison.OrdinalIgnoreCase);

                string folderNameForDestination;
                if (isDriveRoot)
                {
                    folderNameForDestination = $"{folder.Replace(":", "")}_drive";
                }
                else
                {
                    // Use the last folder name and append a short hash
                    string shortHash = GenerateShortHash(folder);
                    folderNameForDestination = $"{Path.GetFileName(folder)}_{shortHash}";
                }

                // Build the destination path
                string folderDestination = Path.Combine(destination, folderNameForDestination + destinationSuffix);

                // Validate the folderDestination path
                if (PathValidator.IsValidPath(folderDestination))
                {
                    // Remove trailing backslash from source and destination
                    folder = folder.TrimEnd('\\');
                    folderDestination = folderDestination.TrimEnd('\\');

                    // Read MT value from config.ini
                    string mtValue = ReadMTValueFromConfig();

                    // Build robocopy command
                    string robocopyArgs = $"\"{folder}\" \"{folderDestination} \" /E /R:1 /W:1 /MT:{mtValue} /Z /LOG:log/backup_{i + 1}.log {excludeParams} /A-:SH";
                    File.WriteAllText("log/robocopyArgs.log", robocopyArgs.ToString());

                    // Before starting a new robocopy process, ensure any existing one is properly handled
                    if (Program.RobocopyProcess != null && !Program.RobocopyProcess.HasExited)
                    {
                        Program.RobocopyProcess.Kill();
                        Program.RobocopyProcess.Dispose();
                    }

                    // Run robocopy
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
                        Program.IsBackupInProgress = false;

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
                            // Command to remove hidden and system attributes
                            string attribArgs = $"-s -h \"{folderDestination}\"";
                            ProcessStartInfo psiAttrib = new ProcessStartInfo("attrib.exe", attribArgs)
                            {
                                CreateNoWindow = true,
                                UseShellExecute = false
                            };
                            Process pAttrib = Process.Start(psiAttrib);
                            pAttrib.WaitForExit();
                        }
                        catch (Exception ex)
                        {
                            // Write current timestamp and error to file
                            File.WriteAllText("log/attribFail.log", $"{DateTime.Now.ToString("o")}\nExit code: {ex}");
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

            // Add zip to backup if enabled in config.ini and not set to only make zip backups
            if (!IsOnlyMakeZipBackupEnabled())
            {
                // Run the archiver in a separate thread. The FolderArchiver class is in ZipHelper.cs
                var archiver = new FolderArchiver();                    // Create a new instance of the FolderArchiver class
                try
                {
                    await Task.Run(() => archiver.ArchiveFolders(false));   // Run the archiver in a separate thread
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
                    Program.IsBackupInProgress = false;

                    // Write the full backup duration to the log file
                    TimeSpan zipDuration = backupTimer.Elapsed - robocopyDuration;
                    File.AppendAllText(logFilePath, $"{DateTime.Now:O} ZIP archiving duration: {zipDuration}\n");

                    // Conclude the backup process
                    backupTimer.Stop();
                    TimeSpan totalBackupDuration = backupTimer.Elapsed;
                    File.AppendAllText(logFilePath, $"{DateTime.Now:O} Full backup duration: {totalBackupDuration}\n");
                }
            }
        }

        if (exitAfter)
        {
            Application.Exit();
        }
    }

    private static string ReadMTValueFromConfig() // Read the MT value from config.ini that is used in robocopy to set the number of threads
    {
        string mtValue = "16"; // Default value
        if (File.Exists("config.ini"))
        {
            foreach (var line in File.ReadLines("config.ini"))
            {
                if (line.StartsWith("robocopymt="))
                {
                    mtValue = line.Substring("robocopymt=".Length);
                    break;
                }
            }
        }
        return mtValue;
    }

    private static bool IsTwoBackupsEnabled() // Check if the twobackups setting is enabled in config.ini
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

    private static bool IsZipInBackupEnabled()  // Check if the includeZipInBackup setting is enabled in config.ini
    {
        string configPath = "config.ini";
        string zipKey = "includeZipInBackup="; // Key to look for in the config file

        if (File.Exists(configPath))
        {
            string[] configLines = File.ReadAllLines(configPath);
            // Find the first line that starts with the zipKey, ignoring case
            string zipLine = configLines.FirstOrDefault(line => line.StartsWith(zipKey, StringComparison.OrdinalIgnoreCase));

            if (zipLine != null)
            {
                // Extract the value part after the '=' and trim any whitespace, then compare it with "true"
                string zipValue = zipLine.Substring(zipKey.Length).Trim().ToLower();
                return zipValue == "true"; // Return true if the setting's value is "true"
            }
        }

        // Return false as default if the config file does not exist or the includeZipInBackup setting is not found or not set to true
        return false;
    }

    private static bool IsOnlyMakeZipBackupEnabled()    // Check if the onlyMakeZipBackup setting is enabled in config.ini
    {
        string configPath = "config.ini";
        string onlyMakeZipBackupKey = "onlyMakeZipBackup=";
        if (File.Exists(configPath))
        {
            var configLines = File.ReadAllLines(configPath);
            var onlyMakeZipBackupLine = configLines.FirstOrDefault(line => line.StartsWith(onlyMakeZipBackupKey, StringComparison.OrdinalIgnoreCase));
            if (onlyMakeZipBackupLine != null)
            {
                var onlyMakeZipBackupValue = onlyMakeZipBackupLine.Substring(onlyMakeZipBackupKey.Length).Trim().ToLower();
                return onlyMakeZipBackupValue == "true";
            }
        }
        return false; // Return false by default if the setting is not found or not true
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
                    Debug.WriteLine("Ignore above exception. (Exception thrown: 'System.Threading.Tasks.TaskCanceledException' in System.Private.CoreLib.dll) It is normal.");
                    if (BlinkCancellationTokenSource != null)
                    {
                        BlinkCancellationTokenSource.Dispose();
                        BlinkCancellationTokenSource = null;
                        Debug.WriteLine("Blinking cancellation token source disposed.");
                    }
                }
            }
            Program.trayIcon.Icon = GreenIcon; // Reset to the default icon when stopped
        }
    }


    public static void DisposeIcons()
    {
        GreenIcon?.Dispose();
        YellowIcon?.Dispose();
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
        string[] lines = File.ReadAllLines("config.ini");
        string betweenKey = "hoursbetweenbackups=";
        int hours = 24; // Default to 24 hours

        foreach (var line in lines)
        {
            if (line.StartsWith(betweenKey))
            {
                if (!int.TryParse(line.Substring(betweenKey.Length), out hours))
                {
                    // If the hours can't be parsed, assume the last backup was too old
                    return true;
                }
                break;
            }
        }

        if (!File.Exists("log/lastBackup.log"))
        {
            return true;
        }

        string timestampStr = File.ReadAllText("log/lastBackup.log");
        DateTime lastBackup;
        if (!DateTime.TryParseExact(timestampStr, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out lastBackup))
        {
            return true;
        }

        return DateTime.Now - lastBackup > TimeSpan.FromHours(hours);
    }

    // Check if backup location is reachable on network or in local drive
    public static bool IsBackupLocationReachable(string destination = null)
    {
        try
        {
            string path = destination ?? GetBackupDestination();

            if (string.IsNullOrEmpty(path))
                return false;

            return TryWriteAndDeleteFile(path);
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
