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
        try
        {
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
        catch (Exception ex)
        {
            LogError($"Error scanning directory: {ex.Message}");
            if (statusLabel != null)
            {
                UpdateStatusLabel($"Error scanning directory: {ex.Message}");
            }
            return filesList;
        }
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
            //Debug.WriteLine($"Matching against pattern: {pattern} => Regex: {regexPattern} for path: {normalizedPath}");

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

    public class ArchiveStatus
    {
        public bool NeedsArchive { get; set; }
        public string Message { get; set; }
    }


    public async Task ArchiveFolders(bool prompt = true)
    {
        // Start timer to measure the time taken for archiving
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        try
        {
            // Read the list of folders to archive
            var folders = File.ReadAllLines("folders.txt");
            int totalFolders = folders.Length;  // Total number of folders

            // Load excluded items. The items can be folders or files or file types or partial names
            var excludedItems = LoadExcludedItems();

            // Load folder configs
            var folderConfigs = FolderConfigManager.LoadFolderConfigs();
            var globalConfig = AppConfigManager.ReadConfigIni("config.ini");

            // Initialize empty log file and keep it open for writing
            File.WriteAllText("log/zip_archive.log", $"Archiving started at {DateTime.Now}\n");

            // Start async archiving process for each folder. First asynchronously create a list of files for each folder that are not excluded using FileInfoItem class
            var tasks = folders.Select(async folder =>
            {
                try
                {
                    var folderConfig = LoadFolderSettings(folder, globalConfig); // Load folder settings
                    Debug.WriteLine($"Processing folder: {folder}");
                    var filteredFiles = await ScanDirectoryAsync(folder, excludedItems); // Scan the directory asynchronously and return a list of files that are not excluded
                    Debug.WriteLine($"Files found in folder {folder}: {filteredFiles.Count}");
                    return filteredFiles.Select(file => new FileInfoItem
                    {
                        FilePath = file,
                        LastModified = File.GetLastWriteTime(file),
                        IsExcluded = false
                    }).ToList();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error processing folder {folder}: {ex.Message}");
                    return new List<FileInfoItem>(); // Return an empty list in case of error
                }
            }).ToList();

            var results = await Task.WhenAll(tasks);

            // Debug output to verify the lists
            foreach (var result in results)
            {
                Debug.WriteLine("Processed Directory:");
                foreach (var file in result)
                {
                    Debug.WriteLine($"File: {file.FilePath}, Last Modified: {file.LastModified}, Excluded: {file.IsExcluded}");
                    // Log to file
                    File.AppendAllText("log/zip_archive.log", $"File: {file.FilePath}, Last Modified: {file.LastModified}, Excluded: {file.IsExcluded}\n");
                }
            }

            // Now use ShouldCreateNewArchive Async to determine if a new archive should be created for each folder
            var semaphore = new SemaphoreSlim(5); // Limit to 5 concurrent tasks if needed

            var archiveTasks = folders.Select(async (folder, index) =>
            {
                await semaphore.WaitAsync(); // Wait for the semaphore if limiting concurrency
                try
                {
                    var folderConfig = LoadFolderSettings(folder, globalConfig); // Load folder settings
                    var files = results[index]; // Get the list of files for the folder
                    Debug.WriteLine($"Starting to check archive necessity for folder: {folder} with {files.Count} files.");

                    bool needsArchive = await ShouldCreateNewArchiveAsync(folder, files, folderConfig, globalConfig);
                    string statusMessage = $"Archive check complete for {folder}: " + (needsArchive ? "New archive needed." : "No new archive needed.");

                    Debug.WriteLine(statusMessage);

                    return new ArchiveStatus
                    {
                        NeedsArchive = needsArchive,
                        Message = statusMessage
                    };
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error checking archive for folder {folder}: {ex.Message}");
                    return new ArchiveStatus
                    {
                        NeedsArchive = false,
                        Message = $"Error checking archive for folder {folder}: {ex.Message}"
                    };
                }
                finally
                {
                    semaphore.Release(); // Release semaphore when task is complete
                }
            }).ToList();

            var archiveResults = await Task.WhenAll(archiveTasks);

            // Log output to verify the results and write them to a file
            foreach (var result in archiveResults)
            {
                Debug.WriteLine(result.Message);
                File.AppendAllText("log/zip_archive.log", result.Message + "\n");
            }

            File.AppendAllText("log/zip_archive.log", $"Filelists created in {stopwatch.Elapsed.TotalSeconds} seconds.\n");

            // Now start synchronous archiving of the folders that need a new archive based on the results
            WriteArchivesSync([.. folders], [.. results], [.. archiveResults], globalConfig, destinationPath, prompt);

            // Log the completion of archiving
            File.AppendAllText("log/zip_archive.log", $"Archiving completed at {DateTime.Now} in {stopwatch.Elapsed.TotalSeconds} seconds.\n\n");

            // Update the UI status label if prompt is true
            if (prompt)
            {
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
            // Stop the timer and log the time taken for archiving
            stopwatch.Stop();
            Debug.WriteLine($"Archiving completed in {stopwatch.Elapsed.TotalSeconds} seconds.");
        }
    }

    // Function to archive folders synchronously
    public void WriteArchivesSync(List<string> folders, List<List<FileInfoItem>> fileLists, List<ArchiveStatus> archiveStatuses, Dictionary<string, string> globalConfig, string destinationPath, bool prompt = true)
    {
        int totalFolders = folders.Count;
        for (int i = 0; i < totalFolders; i++)
        {
            // Timer to measure the time taken for each folder
            Stopwatch folderTimer = new Stopwatch();
            folderTimer.Start();

            // Load folder settings. If no individual settings are found, use global settings
            FolderConfig folderConfig = LoadFolderSettings(folders[i], globalConfig);

            if (!archiveStatuses[i].NeedsArchive) continue;  // Use the updated data structure

            // Update UI if prompt is true
            if (prompt)
            {
                UpdateStatusLabel($"Archiving ({i + 1}/{totalFolders}): {folders[i]}");
                //Check the source folder size
                if (IsFolderBigUI(folders[i]))
                {
                    continue;
                }
            }

            var folder = folders[i];
            var files = fileLists[i]; // List of FileInfoItem

            if (!files.Any(file => !file.IsExcluded))
            {
                Debug.WriteLine($"No files to archive in folder {folder}. Skipping ZIP file creation.");
                continue; // Skip ZIP creation as there are no files to include
            }

            Debug.WriteLine($"Archiving {folder}");

            // Check if the directory exists
            if (!Directory.Exists(folder))
            {
                Debug.WriteLine($"Folder not found: {folder}");
                continue;
            }

            string baseFileName = $"{DateTime.Now:yyyy-MM-dd} {Path.GetFileName(folder)}";
            int version = 1;
            string searchPattern = $"{baseFileName} V*.zip";
            string[] existingArchives = Directory.GetFiles(destinationPath, searchPattern);

            // Determine the next version number for the new archive
            if (existingArchives.Length > 0)
            {
                version = existingArchives.Select(file => int.Parse(Path.GetFileNameWithoutExtension(file).Split('V').Last()))
                                          .Max() + 1;
            }

            string zipFileName = $"{baseFileName} V{version}.zip";
            string zipFilePath = Path.Combine(destinationPath, zipFileName);

            // Create a new zip archive for the folder
            CreateZipArchive(folder, zipFilePath, files);

            // Check if max zip retention is set for the folder or in global settings. Use global setting if folder backup option is set to UseGlobalSetting
            int maxZipRetention = folderConfig?.BackupOption == BackupOptions.UseGlobalSetting
                ? int.Parse(globalConfig["defaultMaxZipRetention"])
                : folderConfig?.MaxZipRetention ?? 0;

            // Delete old zip files of the selected folder if max zip retention is set
            if (maxZipRetention > 0)
            {
                DeleteOldZipFiles(destinationPath, Path.GetFileName(folder), maxZipRetention);
            }

            Debug.WriteLine($"Completed archiving: {folder}");
            folderTimer.Stop();
            Debug.WriteLine($"Folder archiving took {folderTimer.Elapsed.TotalSeconds} seconds.");
            // Log the completion of archiving for the folder
            File.AppendAllText("log/zip_archive.log", $"Archiving completed for {folder} in {folderTimer.Elapsed.TotalSeconds} seconds.\n");
        }
    }

    public void CreateZipArchive(string folder, string zipFilePath, List<FileInfoItem> files)
    {
        Debug.WriteLine($"Creating ZIP archive at: {zipFilePath}");
        try
        {
            using (var zip = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
            {
                foreach (var file in files)
                {
                    if (!file.IsExcluded)
                    {
                        // Ensure the relative path is calculated correctly
                        string relativePath = Path.GetRelativePath(folder, file.FilePath);
                        string entryName = Path.Combine(Path.GetFileName(folder), relativePath);
                        Debug.WriteLine($"Adding file: {entryName} to ZIP");
                        zip.CreateEntryFromFile(file.FilePath, entryName, CompressionLevel.Optimal);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"Error creating ZIP archive at {zipFilePath}: {ex.Message}");
            throw;
        }
    }


    private bool IsFolderBigUI(string folder)
    {
        long totalSize = CalculateFolderSize(folder, LoadExcludedItems());
        double totalSizeGB = totalSize / (1024.0 * 1024.0 * 1024.0);
        if (totalSizeGB > 1)
        {
            var result = MessageBox.Show($"The folder {folder} is larger than 1GB ({totalSizeGB:N2} GB). Do you want to continue? Choose No to skip this folder.", "Warning", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
            if (result == DialogResult.No)
            {
                return true;
            }
            else if (result == DialogResult.Cancel)
            {
                UpdateStatusLabel("Archiving Cancelled");
                return true;
            }
        }
        return false;
    }

    public async Task<bool> ShouldCreateNewArchiveAsync(string folderPath, List<FileInfoItem> files, FolderConfig folderConfig, Dictionary<string, string> globalConfig)
    {
        // Skip archive creation based on global settings
        if (folderConfig?.BackupOption == BackupOptions.UseGlobalSetting &&
            globalConfig.TryGetValue("includeZipInBackup", out string includeZipInBackup) &&
            !bool.Parse(includeZipInBackup))
        {
            Debug.WriteLine("Zipfile creation skipped due to global setting.");
            await LogAsync($"Zipfile creation skipped for {folderPath} due to global setting.\n");
            return false;
        }

        // Skip comparison based on folder configuration
        if (IsZipfileComparisonSkipped(folderConfig, globalConfig))
        {
            Debug.WriteLine("Zipfile comparison skipped due to configuration.");
            await LogAsync($"Zipfile comparison skipped for {folderPath} due to configuration.\n");
            return true;
        }

        // Locate the latest zip file
        var latestZipFilePath = GetLatestZipFilePath(folderPath);
        if (string.IsNullOrEmpty(latestZipFilePath))
        {
            Debug.WriteLine("No existing archive found, creating a new one.");
            await LogAsync($"No existing archive found for {folderPath}, creating a new one.\n");
            return true;
        }

        // Accessing the archive asynchronously
        return await Task.Run(() =>
        {
            using (ZipArchive archive = ZipFile.OpenRead(latestZipFilePath))
            {
                // Normalize file paths and map them to their last write times in the archive
                var zipEntries = archive.Entries.ToDictionary(
                    entry => entry.FullName.Replace('\\', '/'),  // Normalize file paths
                    entry => entry.LastWriteTime.DateTime);

                TimeSpan tolerance = TimeSpan.FromSeconds(30);  // Define a tolerance of 30 seconds

                foreach (var fileInfo in files)
                {
                    string relativePath = GetRelativePath(fileInfo.FilePath, folderPath).Replace('\\', '/').TrimStart('/');

                    // Check if the file exists in the archive and compare times if it does
                    if (!zipEntries.ContainsKey(relativePath))
                    {
                        Debug.WriteLine($"File not found in archive: {relativePath}");
                        return true;  // File not found in the archive, need new archive
                    }

                    DateTime archiveTime = zipEntries[relativePath];
                    DateTime localTime = fileInfo.LastModified;
                    Debug.WriteLine($"Comparing: {relativePath}");
                    Debug.WriteLine($"Archive time: {archiveTime}, Local time: {localTime}");

                    // Check if the difference is within the tolerance
                    if (Math.Abs((localTime - archiveTime).TotalSeconds) > tolerance.TotalSeconds)
                    {
                        Debug.WriteLine($"Local file is newer than archive beyond tolerance. File: {relativePath}, Local: {localTime}, Archive: {archiveTime}");
                        return true;  // Local file is newer, need new archive
                    }
                }
                return false;  // No changes detected
            }
        });
    }

    private async Task LogAsync(string message)
    {
        await Task.Run(() =>
        {
            File.AppendAllText("log/zip_archive.log", message);
        });
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

    public static void LogError(string message)
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
