using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

public class FileInfoItem
{
    public string FilePath { get; set; }
    public DateTime LastModified { get; set; }
    public bool IsExcluded { get; set; }
}

public class DirectoryProcessingService
{
    private readonly FolderArchiver _folderArchiver;

    public DirectoryProcessingService(FolderArchiver folderArchiver)
    {
        _folderArchiver = folderArchiver;
    }

    public async Task ProcessDirectoriesAsync(List<string> directories, List<string> excludedPatterns)
    {
        var tasks = directories.Select(dir => ScanAndProcessDirectoryAsync(dir, excludedPatterns)).ToList();
        var results = await Task.WhenAll(tasks);

        // Debug output to verify the lists
        foreach (var result in results)
        {
            Console.WriteLine("Processed Directory:");
            foreach (var file in result)
            {
                Console.WriteLine(file);
            }
        }
    }

    private async Task<List<string>> ScanAndProcessDirectoryAsync(string directoryPath, List<string> excludedPatterns)
    {
        return await _folderArchiver.ScanDirectoryAsync(directoryPath, excludedPatterns);
    }
}



public class FolderArchiver
{
    private Label statusLabel;
    private string destinationPath;
    private const string ExcludedItemsPath = "excludedItems.txt";
    private const string LogFilePath = "log/archive_err.log";

    public FolderArchiver(Label statusLabel = null)
    {
        this.statusLabel = statusLabel;
        ReadConfig();
    }

    public List<string> LoadExcludedItems()
    {
        var excludedItems = new List<string>();
        if (File.Exists(ExcludedItemsPath))
        {
            excludedItems = new List<string>(File.ReadAllLines(ExcludedItemsPath));
        }
        else
        {
            Debug.WriteLine("Excluded items file not found.");
        }
        return excludedItems;
    }

    public async Task<List<string>> ScanDirectoryAsync(string directoryPath, List<string> excludedPatterns)
    {
        List<string> filesList = new List<string>();
        await Task.Run(() =>
        {
            var files = Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                if (!IsExcluded(file, excludedPatterns))
                {
                    filesList.Add(file);
                }
            }
        });
        return filesList;
    }

    public static bool IsExcluded(string filePath, List<string> excludedItems)
    {
        // Normalize path to use backslashes
        string normalizedPath = filePath.Replace('/', '\\');
        Debug.WriteLine($"Evaluating exclusion for: {normalizedPath}");

        foreach (var pattern in excludedItems)
        {
            // Properly escape the pattern and replace wildcard characters
            string regexPattern = Regex.Escape(pattern)
                                     .Replace("\\*", ".*") // Replace '*' with '.*' for regex
                                     .Replace("\\?", ".")  // Replace '?' with '.' for regex
                                     .Replace("\\\\", "\\\\"); // Ensure backslashes are escaped correctly

            // If the pattern is directory-specific, it should match any part of the path
            if (pattern.Trim().EndsWith("\\"))
            {
                regexPattern += ".*";  // Allow anything to follow after the directory name
            }

            regexPattern = "^" + regexPattern + "$"; // Match the whole path
            Debug.WriteLine($"Matching against pattern: {pattern} => Regex: {regexPattern} for path: {normalizedPath}");

            if (Regex.IsMatch(normalizedPath, regexPattern, RegexOptions.IgnoreCase))
            {
                Debug.WriteLine($"Excluded by regex match: {normalizedPath} against pattern: {pattern}");
                return true; // This file or directory matches an exclusion pattern
            }
        }

        Debug.WriteLine($"Included: {normalizedPath}");
        return false; // No exclusion patterns matched
    }

    // Read Global Config
    private void ReadConfig()
    {
        try
        {
            var lines = File.ReadAllLines("config.ini");
            foreach (var line in lines)
            {
                if (line.StartsWith("destination="))
                {
                    string basePath = line.Substring("destination=".Length).Trim();
                    string hostname = Dns.GetHostName();
                    destinationPath = Path.Combine(basePath, hostname, "ziparchives");

                    // Ensure the destination directory exists
                    Directory.CreateDirectory(destinationPath);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"Error reading config file: {ex.Message}");
            if (statusLabel != null)
            {
                UpdateStatusLabel($"Error reading config file: {ex.Message}");
            }
        }
    }

    // Read individual folder settings
    private FolderConfig LoadFolderSettings(string folderPath, Dictionary<string, string> globalConfig)
    {
        var folderConfigs = FolderConfigManager.LoadFolderConfigs();
        if (folderConfigs.TryGetValue(folderPath, out var config))
        {
            return config;
        }

        // If no individual settings are found, create a default FolderConfig using global settings
        int maxZipRetention = 0;
        bool skipCompare = false;

        if (globalConfig.TryGetValue("defaultMaxZipRetention", out string maxRetentionStr))
        {
            int.TryParse(maxRetentionStr, out maxZipRetention);
        }
        if (globalConfig.TryGetValue("skipZipfileComparison", out string skipCompareStr))
        {
            bool.TryParse(skipCompareStr, out skipCompare);
        }

        return new FolderConfig
        {
            BackupOption = BackupOptions.UseGlobalSetting,
            MaxZipRetention = maxZipRetention,
            SkipCompare = skipCompare
        };
    }

    public void ArchiveFolders(bool prompt = true)
    {
        // Start timer to measure the time taken for archiving
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        try
        {
            // Read the list of folders to archive
            var folders = File.ReadAllLines("folders.txt");
            int totalFolders = folders.Length;  // Total number of folders
            int currentFolderIndex = 0;         // Initialize a counter to keep track of the current folder index

            // Load excluded items. The items can be folders or files or file types or partial names
            var excludedItems = LoadExcludedItems();

            // Load folder configs
            var folderConfigs = FolderConfigManager.LoadFolderConfigs();
            var globalConfig = AppConfigManager.ReadConfigIni("config.ini");

            // Initialize empty log file and keep it open for writing
            File.WriteAllText("log/zip_archive.log", $"Archiving started at {DateTime.Now}/n/n");

            foreach (var folder in folders)
            {
                currentFolderIndex++; // Increment the current folder index at the start of each loop iteration for percentage display

                if (Directory.Exists(folder))
                {
                    // Load folder settings. If no individual settings are found, use global settings
                    FolderConfig folderConfig = LoadFolderSettings(folder, globalConfig);

                    // Check if a new archive should be created for the folder. If not, skip archiving
                    if (ShouldCreateNewArchive(folder, folderConfig, globalConfig, excludedItems))
                    {
                        // Update UI if prompt is true
                        if (prompt)
                        {
                            // Calculate the total size of the files in the folder
                            long totalSize = CalculateFolderSize(folder, excludedItems);

                            // Convert total size to gigabytes
                            double totalSizeGB = totalSize / (1024.0 * 1024.0 * 1024.0);

                            // Check if total size exceeds 1GB
                            if (totalSizeGB > 1)
                            {
                                var result = MessageBox.Show($"The folder {folder} is larger than 1GB ({totalSizeGB:N2} GB). Do you want to continue? Choose No to skip this folder.", "Warning", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);

                                if (result == DialogResult.No)
                                {
                                    // Skip this folder and continue to the next one
                                    continue;
                                }
                                else if (result == DialogResult.Cancel)
                                {
                                    // Cancel the entire archiving process
                                    UpdateStatusLabel("Archiving Cancelled");
                                    return;
                                }
                                // If DialogResult.Yes, continue with the archiving process
                            }
                        }

                        // Display status of completion
                        if (prompt)
                        { // Only display the status if the prompt is shown (not when the process is run in the background}
                            UpdateStatusLabel($"Archiving ({currentFolderIndex}/{totalFolders}): {folder}");
                        }

                        // Set the base filename for the zip archive
                        string baseFileName = $"{DateTime.Now:yyyy-MM-dd} {Path.GetFileName(folder)}";
                        int version = 1;

                        // Generate the search pattern to find all existing versions for today's date
                        string searchPattern = $"{baseFileName} V*.zip";
                        string[] existingArchives = Directory.GetFiles(destinationPath, searchPattern);

                        if (existingArchives.Length > 0)
                        {
                            // Extract version numbers, sort them, and get the highest version
                            var versions = existingArchives.Select(file =>
                            {
                                int fileVersion = 0;
                                string versionPart = Path.GetFileNameWithoutExtension(file).Split(' ').Last().TrimStart('V');
                                int.TryParse(versionPart, out fileVersion);
                                return fileVersion;
                            });
                            version = versions.Any() ? versions.Max() + 1 : version;
                        }

                        // Now, construct the filename with the correct version number
                        string zipFileName = $"{baseFileName} V{version}.zip";
                        var zipFilePath = Path.Combine(destinationPath, zipFileName);

                        // Create a new zip archive for the folder. Skip excluded items.
                        ZipHelper.CreateFromDirectory(
                            folder,
                            zipFilePath,
                            CompressionLevel.Optimal,
                            true,
                            Encoding.UTF8,
                            fileName => !IsExcluded(fileName, excludedItems)  // Should include items not excluded
                        );

                        if (prompt)
                        { // Only display the status if the prompt is shown (not when the process is run in the background}
                            UpdateStatusLabel($"Completed: {folder}");
                        }

                        // Check if max zip retention is set for the folder or in global settings. Use global setting if folder backup option is set to UseGlobalSetting
                        int maxZipRetention = folderConfig?.BackupOption == BackupOptions.UseGlobalSetting
                            ? int.Parse(globalConfig["defaultMaxZipRetention"])
                            : folderConfig?.MaxZipRetention ?? 0;

                        // Delete old zip files of the selected folder if max zip retention is set
                        if (maxZipRetention > 0)
                        {
                            DeleteOldZipFiles(destinationPath, Path.GetFileName(folder), maxZipRetention);
                        }

                    }
                    else
                    {
                        // Skip archiving the folder, no changes detected
                        continue;
                    }
                }
                else
                {
                    if (prompt)
                    { // Only display the status if the prompt is shown (not when the process is run in the background}
                        MessageBox.Show($"Folder not found: {folder}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        UpdateStatusLabel($"Error: Folder not found - {folder}");
                    }
                    LogError($"Folder not found: {folder}");
                }
            }
            if (prompt)
            { // Only display the status if the prompt is shown (not when the process is run in the background}
                UpdateStatusLabel("Archiving Complete");
            }
        }
        catch (Exception ex)
        {
            LogError($"Error during archiving: {ex.Message}");
            if (prompt)
            { // Only display the status if the prompt is shown (not when the process is run in the background}
                MessageBox.Show($"Error during archiving: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateStatusLabel("Error during archiving");
            }
        }
        finally
        {
            // Stop blinking tray icon
            Program.StopBlinking();

            // Stop the timer and log the time taken for archiving
            stopwatch.Stop();
            Debug.WriteLine($"Archiving completed in {stopwatch.Elapsed.TotalSeconds} seconds.");
        }
    }

    private bool ShouldCreateNewArchive(string folderPath, FolderConfig folderConfig, Dictionary<string, string> globalConfig, List<string> excludedItems)
    {
        // Check if folder is set to use global settings and if includeZipInBackup=false in global settings
        if (folderConfig?.BackupOption == BackupOptions.UseGlobalSetting)
        {
            if (globalConfig.TryGetValue("includeZipInBackup", out string includeZipInBackup) && !bool.Parse(includeZipInBackup))
            {
                Debug.WriteLine("Zipfile creation skipped due to global setting.");
                File.AppendAllText("log/zip_archive.log", $"Zipfile creation skipped for {folderPath} due to global setting.\n");
                return false;
            }
        }

        // Check if the folder should be skipped from comparison
        if (IsZipfileComparisonSkipped(folderConfig, globalConfig))
        {
            Debug.WriteLine("Zipfile comparison skipped due to configuration.");
            File.AppendAllText("log/zip_archive.log", $"Zipfile comparison skipped for {folderPath} due to configuration.\n");
            return true;
        }

        var latestZipFilePath = GetLatestZipFilePath(folderPath);
        if (string.IsNullOrEmpty(latestZipFilePath))
        {
            Debug.WriteLine("No existing archive found, creating a new one.");
            File.AppendAllText("log/zip_archive.log", $"No existing archive found for {folderPath}, creating a new one.\n");
            return true; // No archive to compare against, return true.
        }

        Debug.WriteLine($"Comparing against latest archive: {latestZipFilePath}");

        using (ZipArchive archive = ZipFile.OpenRead(latestZipFilePath))
        {
            // Create a dictionary of zip entries with their last write times
            // Normalize paths in the zipEntries dictionary to use forward slashes
            var zipEntries = archive.Entries.ToDictionary(
                entry => entry.FullName.Replace('\\', '/'), // Replace backslashes with forward slashes
                entry => entry.LastWriteTime.DateTime
            );

            // Debug print all entries in the zip archive
            foreach (var entry in zipEntries)
            {
                Debug.WriteLine($"Archive entry: {entry.Key}, LastWriteTime: {entry.Value}");
            }

            // Check if any new files or modified files are present in the folder. Skip excluded items.
            foreach (string filePath in Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories))
            {
                FileInfo localFile = new FileInfo(filePath);
                string relativePath = GetRelativePath(filePath, folderPath)
                    .Replace('\\', '/') // Use forward slashes
                    .TrimStart('/'); // Ensure no leading slash

                Debug.WriteLine($"Checking file: {relativePath}");

                // Check if the local file exists in the zip archive if it's not an excluded item
                if (excludedItems.Contains(relativePath))
                {
                    Debug.WriteLine($"Excluded item found: {relativePath}");
                    continue; // Skip excluded items
                }

                if (IsExcluded(filePath, excludedItems))
                {
                    Debug.WriteLine($"Excluded item found: {relativePath}");
                    continue; // Skip excluded items
                }
                else
                {
                    Debug.WriteLine($"Included item found: {relativePath}");
                }

                // Check if the file is new or modified
                if (!zipEntries.ContainsKey(relativePath))
                {
                    Debug.WriteLine($"New file found, not present in archive: {relativePath}");
                    return true; // File is new, create a new archive.
                }
                else
                {
                    var zipLastWriteTime = zipEntries[relativePath];
                    // Debug.WriteLine($"Found matching archive entry for {relativePath}. Comparing timestamps...");

                    // Add your timestamp comparison logic here
                    TimeSpan timeDifference = localFile.LastWriteTime - zipLastWriteTime;
                    // Debug.WriteLine($"Time difference (in seconds): {timeDifference.TotalSeconds}");

                    if (timeDifference > TimeSpan.FromSeconds(10)) // Using 10 seconds as an example
                    {
                        Debug.WriteLine($"File modified since last archive: {relativePath}, Local: {localFile.LastWriteTime}, Archive: {zipLastWriteTime}");
                        return true; // File has been modified, create a new archive.
                    }
                }
            }
        }

        Debug.WriteLine("No changes detected, no new archive needed.");
        return false; // No changes detected, no need for a new archive.
    }

    private string GetLatestZipFilePath(string folderPath)
    {
        // Extract just the folder name from the full path
        string folderName = new DirectoryInfo(folderPath).Name;

        // Construct the search pattern to match zip files for the folder
        string searchPattern = $"*{folderName} V*.zip";

        Debug.WriteLine("Using search pattern: " + destinationPath + searchPattern);

        // Use destinationPath which points to the backup destination for zip files
        string[] existingArchives = Directory.GetFiles(destinationPath, searchPattern);

        Debug.WriteLine($"{existingArchives.Length} archives found for {folderName}");

        // Check if the array is empty before calling OrderByDescending
        if (existingArchives.Length == 0)
        {
            Debug.WriteLine("No archives found. A new one will be created.");
            return null;
        }

        // Extract and sort the version numbers, and pick the latest archive
        string latestArchive = existingArchives.OrderByDescending(f => f).FirstOrDefault();

        Debug.WriteLine($"Latest archive determined: {latestArchive}");

        // Return the full path of the latest archive
        return latestArchive;
    }

    // Delete old zip files based on max zip retention. Starting from the oldest file, keep the most recent maxZipRetention files. The files are prefixed with timestamp $"{DateTime.Now:yyyy-MM-dd}
    private void DeleteOldZipFiles(string destinationPath, string folder, int maxZipRetention)
    {
        string searchPattern = $"*{folder} V*.zip";
        string[] existingArchives = Directory.GetFiles(destinationPath, searchPattern);

        if (existingArchives.Length <= maxZipRetention)
        {
            return; // No need to delete any files
        }

        // Sort the files by creation time in ascending order (oldest first)
        var filesToDelete = existingArchives.OrderBy(f => new FileInfo(f).CreationTime).Take(existingArchives.Length - maxZipRetention);

        foreach (var file in filesToDelete)
        {
            try
            {
                File.Delete(file);
                Debug.WriteLine($"Deleted old zip file: {file}");
                File.AppendAllText("log/zip_archive.log", $"Deleted old zip file: {file}\n");
            }
            catch (Exception ex)
            {
                LogError($"Error deleting old zip file: {file}. {ex.Message}");
            }
        }
    }

    private string GetRelativePath(string filePath, string folderPath)
    {
        // Assuming folderPath is the full path to the "TC_Settings" folder,
        // and filePath is the full path to a file within that folder.
        string relativePath = filePath.Substring(folderPath.Length + 1).Replace('\\', '/');

        // Prepend the folder name to match the archive entry format.
        // You might need to adjust this part if folderPath or filePath doesn't directly include the folder name.
        string folderName = new DirectoryInfo(folderPath).Name;
        relativePath = $"{folderName}/{relativePath}"; // Now matches format "TC_Settings/Settings 2020-06-24.vssettings"

        return relativePath;
    }


    private bool IsZipfileComparisonSkipped(FolderConfig folderConfig, Dictionary<string, string> globalConfig)
    {
        // If folderConfig is not null and not set to use global setting, use the individual setting
        if (folderConfig != null && folderConfig.BackupOption != BackupOptions.UseGlobalSetting)
        {
            return folderConfig.SkipCompare;
        }

        // Fallback to the global setting
        return globalConfig.TryGetValue("skipZipfileComparison", out string skipComparison) && bool.Parse(skipComparison);
    }


    private long CalculateFolderSize(string folder, List<string> excludedItems)
    {
        UpdateStatusLabel($"Estimating size of: {folder}");

        long totalSize = 0;
        // Calculate total size of all files in the folder
        string[] files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories);
        foreach (string file in files)
        {
            // Skip excluded items
            if (excludedItems.Any(excluded => file.Contains(excluded)))
            {
                continue;
            }

            FileInfo fileInfo = new FileInfo(file);
            totalSize += fileInfo.Length;
        }
        return totalSize;
    }

    private void UpdateStatusLabel(string message)
    {
        if (statusLabel.InvokeRequired)
        {
            statusLabel.Invoke(new Action(() => statusLabel.Text = message));
        }
        else
        {
            statusLabel.Text = message;
        }
    }

    private static void LogError(string message)
    {
        try
        {
            File.AppendAllText(LogFilePath, $"{DateTime.Now}: {message}\n");
        }
        catch
        {
            // Optionally handle logging errors, but be careful to avoid recursive error logging.
        }
    }
}


public static class ZipHelper
{
    public static void CreateFromDirectory(
    string sourceDirectoryName,
    string destinationArchiveFileName,
    CompressionLevel compressionLevel,
    bool includeBaseDirectory,
    Encoding entryNameEncoding,
    Predicate<string> filter)
    {
        if (string.IsNullOrEmpty(sourceDirectoryName))
            throw new ArgumentNullException(nameof(sourceDirectoryName));
        if (string.IsNullOrEmpty(destinationArchiveFileName))
            throw new ArgumentNullException(nameof(destinationArchiveFileName));

        var filesToAdd = new List<string>();
        AddFilesRecursively(sourceDirectoryName, filesToAdd, filter);

        var entryNames = GetEntryNames(filesToAdd.ToArray(), sourceDirectoryName, includeBaseDirectory);

        using (var zipFileStream = new FileStream(destinationArchiveFileName, FileMode.Create))
        using (var archive = new ZipArchive(zipFileStream, ZipArchiveMode.Create))
        {
            for (int i = 0; i < filesToAdd.Count; i++)
            {
                Debug.WriteLine($"Evaluating file {filesToAdd[i]} for inclusion in archive.");
                // File inclusion check is now unnecessary here because we've already filtered in AddFilesRecursively
                archive.CreateEntryFromFile(filesToAdd[i], entryNames[i], compressionLevel);
                Debug.WriteLine($"Adding file to archive: {filesToAdd[i]}");
            }
        }
    }

    private static void AddFilesRecursively(string currentDir, List<string> filesToAdd, Predicate<string> filter)
    {
        // Check if the current directory itself should be excluded
        if (!filter(currentDir))
        {
            Debug.WriteLine($"Excluding directory and its contents: {currentDir}");
            return; // Skip this directory and all its contents
        }

        foreach (var file in Directory.GetFiles(currentDir))
        {
            if (filter(file))
            {
                filesToAdd.Add(file);
                Debug.WriteLine($"Included file: {file}");
            }
            else
            {
                Debug.WriteLine($"Excluded file: {file}");
            }
        }

        foreach (var directory in Directory.GetDirectories(currentDir))
        {
            Debug.WriteLine($"Processing subdirectory: {directory}");
            AddFilesRecursively(directory, filesToAdd, filter);
        }
    }


    private static string[] GetEntryNames(string[] filesToAdd, string sourceDirectoryName, bool includeBaseDirectory)
    {
        var entryNames = new string[filesToAdd.Length];
        for (int i = 0; i < filesToAdd.Length; i++)
        {
            var path = filesToAdd[i].Substring(sourceDirectoryName.Length).TrimStart(Path.DirectorySeparatorChar);
            if (includeBaseDirectory)
                path = Path.Combine(Path.GetFileName(sourceDirectoryName), path);

            entryNames[i] = path;
        }
        return entryNames;
    }
}
