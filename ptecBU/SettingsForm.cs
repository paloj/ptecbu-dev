using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Windows.Forms;
using System.Drawing;
using System.Reflection;
using System.Diagnostics;
using System.Threading.Tasks;

class SettingsForm : Form
{
    private ListBox foldersListBox;
    private Button addFolderButton;
    private Button removeFolderButton;
    private ListBox excludedItemsListBox;
    private TextBox addExcludedItemTextBox;
    private Button addExcludedItemButton;
    private Button removeExcludedItemButton;
    private CheckBox launchOnStartupCheckBox;
    private Button closeButton;
    private Label lastBackupLabel;
    private Label backupDestinationLabel;
    private Label lastSystemImageBackupLabel;
    public Label ArchiveStatusLabel;
    private LinkLabel openConfigFileLinkLabel;
    private Button systemImageBackupButton;
    private Button ArchiveFoldersButton;
    private Button CheckUpdatesButton;
    private ToolTip fullPathToolTip = new()
    {
        AutoPopDelay = 5000,
        InitialDelay = 100,
        ReshowDelay = 500,
        ShowAlways = true
    };

    private int previousIndex = -1; // To track the item index that the tooltip was last shown for

    public SettingsForm()
    {
        // Get the current assembly
        var assembly = Assembly.GetExecutingAssembly();
        // Get the version of the current assembly
        var version = assembly.GetName().Version;

        // Set up the form
        Text = $"PtecBU Settings - App version: {version}";
        Size = new Size(670, 465);
        FormBorderStyle = FormBorderStyle.FixedSingle; // Make the form non-resizable

        // Set the form's icon
        this.Icon = new Icon("Resources/white.ico");

        // Create the label
        var foldersLabel = new Label()
        {
            Text = "Backup folders list",
            Location = new Point(10, 10),
            AutoSize = true
        };
        Controls.Add(foldersLabel);

        // Create the list box for the folders
        foldersListBox = new ListBox()
        {
            Location = new Point(10, 30),
            Size = new Size(200, 200),
            DrawMode = DrawMode.OwnerDrawFixed // Set the draw mode to owner draw fixed
        };

        // Subscribe to the DrawItem event
        foldersListBox.DrawItem += new DrawItemEventHandler(FoldersListBox_DrawItem);
        foldersListBox.MouseMove += new MouseEventHandler(FoldersListBox_MouseMove);

        // Add the ListBox to the form's controls
        Controls.Add(foldersListBox);

        // Create the add folder button
        addFolderButton = new Button()
        {
            Location = new Point(220, 30),
            Text = "Add Folder",
        };
        addFolderButton.Click += OnAddFolder;
        Controls.Add(addFolderButton);

        // Create the remove folder button
        removeFolderButton = new Button()
        {
            Location = new Point(220, 60),
            Text = "Remove Folder",
        };
        removeFolderButton.Click += OnRemoveFolder;
        Controls.Add(removeFolderButton);

        // Create the text box for adding excluded items
        addExcludedItemTextBox = new TextBox()
        {
            Location = new Point(350, 240),
            Size = new Size(200, 20),
        };
        Controls.Add(addExcludedItemTextBox);

        // Create the label
        var excludedItemsLabel = new Label()
        {
            Text = "Exclude list. One item per line.",
            Location = new Point(350, 10),
            AutoSize = true
        };
        Controls.Add(excludedItemsLabel);

        // Create the list box for the excluded items
        excludedItemsListBox = new ListBox()
        {
            Location = new Point(350, 30),
            Size = new Size(200, 200),
        };
        Controls.Add(excludedItemsListBox);

        // Create the add excluded item button
        addExcludedItemButton = new Button()
        {
            Location = new Point(560, 240),
            Text = "Add Excluded Item",
        };
        addExcludedItemButton.Click += OnAddExcludedItem;
        Controls.Add(addExcludedItemButton);

        // Create the remove excluded item button
        removeExcludedItemButton = new Button()
        {
            Location = new Point(560, 40),
            Text = "Remove Excluded Item",
        };
        removeExcludedItemButton.Click += OnRemoveExcludedItem;
        Controls.Add(removeExcludedItemButton);

        // Create the label for the last successful backup
        lastBackupLabel = new Label()
        {
            Location = new Point(10, 340), // Adjust these values to place the label appropriately
            AutoSize = true
        };
        Controls.Add(lastBackupLabel);

        // Create the label for the Archiving status
        ArchiveStatusLabel = new Label()
        {
            Location = new Point(10, 265), // Adjust these values to place the label appropriately
            AutoSize = true,
            Text = ""   // Empty placeholder for archiving status
        };
        Controls.Add(ArchiveStatusLabel);

        //Get and display backup destination
        backupDestinationLabel = new Label()
        {
            Location = new Point(10, 360), // Adjust these values to place the label appropriately
            Text = "Backup destination: " + BackupManager.GetBackupDestination(),
            AutoSize = true
        };
        // Subscribe to the Click event to open the destination when clicked
        backupDestinationLabel.Click += BackupDestinationLabel_Click;
        Controls.Add(backupDestinationLabel);

        lastSystemImageBackupLabel = new Label()
        {
            Location = new Point(10, 380), // Adjust these values to place the label appropriately
            Text = "Last System backup: ",
            AutoSize = true
        };
        Controls.Add(lastSystemImageBackupLabel);

        openConfigFileLinkLabel = new LinkLabel()
        {
            Location = new Point(10, 320), // Adjust these values to place the label appropriately
            Text = "Open config.ini",
            AutoSize = true
        };
        openConfigFileLinkLabel.LinkClicked += new LinkLabelLinkClickedEventHandler(OpenConfigFileLinkLabel_LinkClicked);
        Controls.Add(openConfigFileLinkLabel);

        systemImageBackupButton = new Button()
        {
            Location = new Point(10, 395), // Adjust these values to place the button appropriately
            AutoSize = true, // Enable auto-sizing to adjust the button's width based on its text content
            AutoSizeMode = AutoSizeMode.GrowAndShrink, // Allow the button to grow and shrink
            Height = 25, // Specify the desired height
            Text = "Create System Image Backup now",
        };
        systemImageBackupButton.Click += OnSystemImageBackup;
        Controls.Add(systemImageBackupButton);

        // Calculate the new location for the CheckUpdatesButton
        int spaceBetweenButtons = 10; // Adjust the space between the buttons as needed
        Point newLocationForCheckUpdatesButton = new Point(
            systemImageBackupButton.Location.X + systemImageBackupButton.Width + spaceBetweenButtons,
            systemImageBackupButton.Location.Y);
        CheckUpdatesButton = new Button()
        {
            Location = newLocationForCheckUpdatesButton, // Use the dynamically calculated location
            AutoSize = true, // Enable auto-sizing to adjust the button's width based on its text content
            AutoSizeMode = AutoSizeMode.GrowAndShrink, // Allow the button to grow and shrink
            Height = 25, // Specify the desired height
            Text = "Online update",
        };
        CheckUpdatesButton.Click += CheckUpdatesButton_Click;
        Controls.Add(CheckUpdatesButton);

        ArchiveFoldersButton = new Button()
        {
            Location = new Point(10, 235), // Adjust these values to place the button appropriately
            AutoSize = true, // Enable auto-sizing to adjust the button's width based on its text content
            AutoSizeMode = AutoSizeMode.GrowAndShrink, // Allow the button to grow and shrink
            Height = 25, // Specify the desired height
            Text = "Zip all folders to backup location now",
        };
        // Create a tooltip and associate it with the button
        ToolTip archiveToolTip = new();
        // Set up the tooltip text for the button.
        archiveToolTip.SetToolTip(ArchiveFoldersButton, "Click to backup folders on the list to zip files.(Might take long time!)");

        ArchiveFoldersButton.Click += ArchiveFoldersButton_Click;
        Controls.Add(ArchiveFoldersButton);

        // Update the last successful backup label
        UpdateLastSuccessfulBackup();

        // Create the "Launch on Windows Startup" checkbox
        launchOnStartupCheckBox = new CheckBox()
        {
            Location = new Point(350, 280), // Adjust these values to place the checkbox appropriately
            Text = "Launch on Windows Startup",
            AutoSize = true
        };
        // Subscribe to the CheckedChanged event
        launchOnStartupCheckBox.CheckedChanged += LaunchOnStartupCheckBox_CheckedChanged;
        Controls.Add(launchOnStartupCheckBox);

        // Update the checkbox based on the Windows Registry setting for application launch on startup
        using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
        {
            if (key != null)
            {
                // Check if the registry value exists
                if (key.GetValue("ptecBU") != null)
                {
                    // The registry value exists, so set the checkbox to be checked
                    launchOnStartupCheckBox.Checked = true;
                }
                else
                {
                    // The registry value does not exist, so set the checkbox to be unchecked
                    launchOnStartupCheckBox.Checked = false;
                }
            }
        }

        // Create the Close button
        closeButton = new Button()
        {
            Location = new Point(560, 395), // Adjust these values to place the button in the right lower corner
            Text = "Close",
            Height = 25,
        };
        closeButton.Click += OnClose;
        Controls.Add(closeButton);

        // Populate the foldersListBox from a file
        if (File.Exists("folders.txt"))
        {
            using (StreamReader reader = new StreamReader("folders.txt"))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    foldersListBox.Items.Add(line);
                }
            }
        }

        // Populate the excludedItemsListBox from a file
        if (File.Exists("excludedItems.txt"))
        {
            using (StreamReader reader = new StreamReader("excludedItems.txt"))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    excludedItemsListBox.Items.Add(line);
                }
            }
        }


    }

    // Display tooltip for the listbox items
    private void FoldersListBox_MouseMove(object sender, MouseEventArgs e)
    {
        // Determine which item is currently under the mouse
        int index = foldersListBox.IndexFromPoint(e.Location);
        if (index != -1 && index != previousIndex)
        {
            // Update the tooltip text with the full path of the current item
            fullPathToolTip.SetToolTip(foldersListBox, foldersListBox.Items[index].ToString());
            previousIndex = index; // Update the last shown tooltip index
        }
        else if (index == -1) // Mouse is not over any item
        {
            fullPathToolTip.Hide(foldersListBox);
            previousIndex = -1; // Reset the index as there's no item under the mouse
        }
    }

    // Handle listbox drawing so that items aligns to the right
    private void FoldersListBox_DrawItem(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0)
            return;

        string fullPath = ((ListBox)sender).Items[e.Index].ToString();
        string textToShow = fullPath;

        // Measure the width of the entire string
        SizeF fullStringSize = e.Graphics.MeasureString(fullPath, e.Font);

        // If the string width exceeds the available width, adjust it
        if (fullStringSize.Width > e.Bounds.Width)
        {
            string ellipsis = "...";
            SizeF ellipsisSize = e.Graphics.MeasureString(ellipsis, e.Font);
            int charsToFit = fullPath.Length;

            // Reduce the string size until it fits, including ellipsis
            while (charsToFit > 0 && (e.Graphics.MeasureString(fullPath.Substring(fullPath.Length - charsToFit), e.Font).Width + ellipsisSize.Width) > e.Bounds.Width)
            {
                charsToFit--;
            }

            // Prepare the textToShow with ellipsis indicating text is trimmed
            textToShow = ellipsis + fullPath.Substring(fullPath.Length - charsToFit);
        }

        // Set string format to near alignment (since we're manually adjusting the string to show its end)
        StringFormat stringFormat = new StringFormat
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Center
        };

        // Drawing the background
        e.DrawBackground();

        // Drawing the text
        using (Brush textBrush = new SolidBrush(e.ForeColor))
        {
            e.Graphics.DrawString(textToShow, e.Font, textBrush, e.Bounds, stringFormat);
        }

        // Draw the focus rectangle around the item
        e.DrawFocusRectangle();
    }

    private void OnAddFolder(object sender, EventArgs e)
    {
        // Handle add folder clicked
        using (var dialog = new FolderBrowserDialog())
        {
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                // Add the selected folder to the list box
                foldersListBox.Items.Add(dialog.SelectedPath);
            }
        }
        //update folders.txt file
        WriteListBoxContentsToFile(foldersListBox, "folders.txt");
    }

    private void OnRemoveFolder(object sender, EventArgs e)
    {
        // Handle remove folder clicked
        if (foldersListBox.SelectedItem != null)
        {
            foldersListBox.Items.Remove(foldersListBox.SelectedItem);
        }
        //update folders.txt file
        WriteListBoxContentsToFile(foldersListBox, "folders.txt");
    }

    private void OnAddExcludedItem(object sender, EventArgs e)
    {
        // Handle add excluded item clicked
        // Add the text in the text box to the list box
        string item = addExcludedItemTextBox.Text.Trim();
        if (!string.IsNullOrEmpty(item))
        {
            excludedItemsListBox.Items.Add(item);
            addExcludedItemTextBox.Clear();
        }
        // update excludedItems.txt file
        WriteListBoxContentsToFile(excludedItemsListBox, "excludedItems.txt");
    }

    private void OnRemoveExcludedItem(object sender, EventArgs e)
    {
        // Handle remove excluded item clicked
        // For now, just remove the selected item from the list box
        if (excludedItemsListBox.SelectedItem != null)
        {
            excludedItemsListBox.Items.Remove(excludedItemsListBox.SelectedItem);
        }
        // update excludedItems.txt file
        WriteListBoxContentsToFile(excludedItemsListBox, "excludedItems.txt");
    }

    private void OpenConfigFileLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
    {
        // Path to your config.ini file
        string filePath = "config.ini";

        // Open the file in Notepad
        Process.Start("notepad.exe", filePath);
    }

    private void UpdateLastSuccessfulBackup()
    {
        string filePath = "lastBackup.txt"; // replace with your path

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
                        lastBackupLabel.Text = $"Last backup: {lastBackupDateTime:d.M.yyyy H:mm}";
                    }
                    else
                    {
                        lastBackupLabel.Text = "Last successful backup: Date format is incorrect";
                    }
                    break;
                }
            }
        }
        else
        {
            lastBackupLabel.Text = "Last successful backup: Never";
        }

        //Update last system backup label
        string sysimgfilePath = "lastsystemimage.txt"; // replace with your path

        string absolutePath = Path.GetFullPath(sysimgfilePath);
        //MessageBox.Show($"Checking existence of file at: {absolutePath}");

        if (File.Exists(absolutePath))
        {
            using (StreamReader sr = File.OpenText(sysimgfilePath))
            {
                string s = "";
                while ((s = sr.ReadLine()) != null)
                {
                    // assuming that your date is in the first line of the file
                    if (DateTime.TryParse(s, out DateTime lastBackupDateTime))
                    {
                        lastSystemImageBackupLabel.Text = $"Last system image backup: {lastBackupDateTime:d.M.yyyy H:mm}";
                    }
                    else
                    {
                        lastSystemImageBackupLabel.Text = "Last system image backup: Date format is incorrect";
                    }
                    break;
                }
            }
        }
        else
        {
            lastSystemImageBackupLabel.Text = "Last system image backup: Never";
        }

        //lastSystemImageBackupLabel.Text = "Last system image backup: Never";
    }

    private void OnClose(object sender, EventArgs e)
    {
        // Handle Close clicked
        UpdateRegistryForStartup();

        // Hide the form
        this.Hide();

        // Restart the application
        Application.Restart();
    }

    private void UpdateRegistryForStartup() // Function to update the autostart registry entry
    {
        using Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
        if (key != null)
        {
            if (launchOnStartupCheckBox.Checked)
            {
                // Get the directory of the current executable
                string exeDirectory = Path.GetDirectoryName(Application.ExecutablePath);

                // Build the path to the launcher executable
                string launcherPath = Path.Combine(exeDirectory, "Launcher", "Launcher.exe");

                // Add the value to the registry to run the launcher on startup
                key.SetValue("ptecBU", $"\"{launcherPath}\"");
            }
            else
            {
                // Remove the value from the registry to prevent the application from running on startup
                key.DeleteValue("ptecBU", false);
            }
        }
    }

    private static void WriteListBoxContentsToFile(ListBox listBox, string filePath) // Function to save contents of listbox to file
    {
        using StreamWriter writer = new(filePath);
        foreach (var item in listBox.Items)
        {
            if (item != null)
            {
                string line = item.ToString();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    writer.WriteLine(line);
                }
            }
        }
    }

    private void OnSystemImageBackup(object sender, EventArgs e)
    {
        string settingsIniPath = "config.ini";
        string destination = null;

        if (File.Exists(settingsIniPath))
        {
            var lines = File.ReadAllLines(settingsIniPath);
            foreach (var line in lines)
            {
                if (line.StartsWith("systemimagedestination="))
                {
                    destination = line.Substring("systemimagedestination=".Length).Trim();
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(destination))
        {
            MessageBox.Show("Set system image backup destination in config.ini", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            // Define the process start info
            ProcessStartInfo psi = new()
            {
                FileName = "system_backup.bat",
                Arguments = destination,
                Verb = "runas", // This ensures the process is run as administrator
                UseShellExecute = true, // This is necessary to allow the "runas" verb
            };

            // Launch the BAT file with the specified start info
            System.Diagnostics.Process.Start(psi);

            // Write the current date and time to lastsystemimage.txt
            File.WriteAllText("lastsystemimage.txt", DateTime.Now.ToString());

            // Update the last system image backup label
            UpdateLastSuccessfulBackup();
        }
        catch (Exception ex)
        {
            // Handle any exceptions that occur
            MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // Archive all folders to zip files button
    private void ArchiveFoldersButton_Click(object sender, EventArgs e)
    {
        var archiver = new FolderArchiver(ArchiveStatusLabel);
        Task.Run(() => archiver.ArchiveFolders());
    }

    // Update registry if the autostart checkbox status changes
    private void LaunchOnStartupCheckBox_CheckedChanged(object sender, EventArgs e)
    {
        UpdateRegistryForStartup();
    }

    private void CheckUpdatesButton_Click(object sender, EventArgs e)
    {
        string launcherPath = AppDomain.CurrentDomain.BaseDirectory;
        string updaterPath = Path.Combine(launcherPath, "updater", "updater.exe");

        try
        {
            Process.Start(updaterPath);
        }
        catch (Exception ex)
        {
            string logPath = Path.Combine(launcherPath, "updater_log.txt");
            string logContent = $"Could not start updater. Error: {ex.Message}\nMain App Path: {updaterPath}";
            File.WriteAllText(logPath, logContent);
            Console.WriteLine(logContent);
        }
    }

    private void BackupDestinationLabel_Click(object sender, EventArgs e)
    {
        // Extract the backup destination path from the label's text
        string path = backupDestinationLabel.Text.Replace("Backup destination: ", "");

        // Check if the directory exists before trying to open it
        if (Directory.Exists(path))
        {
            // Open the path in File Explorer
            System.Diagnostics.Process.Start("explorer.exe", path);
        }
        else
        {
            MessageBox.Show("The path does not exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

public class FolderArchiver
{
    private Label statusLabel;
    private string destinationPath;
    private const string LogFilePath = "archive_err.log";

    public FolderArchiver(Label statusLabel=null)
    {        
        this.statusLabel = statusLabel;        
        ReadConfig();
    }

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
            if (statusLabel != null){
                UpdateStatusLabel($"Error reading config file: {ex.Message}");
            }
        }
    }

    public void ArchiveFolders(bool prompt = true)
    {
        try
        {
            var folders = File.ReadAllLines("folders.txt");
            int totalFolders = folders.Length;  // Total number of folders
            int currentFolderIndex = 0;         // Initialize a counter to keep track of the current folder index

            foreach (var folder in folders)
            {
                currentFolderIndex++; // Increment the current folder index at the start of each loop iteration for percentage display

                if (Directory.Exists(folder))
                {
                    // Calculate the total size of the files in the folder
                    long totalSize = CalculateFolderSize(folder);

                    // Convert total size to gigabytes
                    double totalSizeGB = totalSize / (1024.0 * 1024.0 * 1024.0);

                    // Check if total size exceeds 1GB
                    if (totalSizeGB > 1 && prompt)
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

                    // Display status of completion
                    if (prompt)
                    { // Only display the status if the prompt is shown (not when the process is run in the background}
                        UpdateStatusLabel($"Archiving ({currentFolderIndex}/{totalFolders}): {folder}");
                    }
                    string baseFileName = $"{DateTime.Now:yyyy-MM-dd} {Path.GetFileName(folder)}";
                    string zipFileName = $"{baseFileName} V1.zip";
                    int version = 1;

                    while (File.Exists(Path.Combine(destinationPath, zipFileName)))
                    {
                        version++;
                        zipFileName = $"{baseFileName} V{version}.zip";
                    }

                    var zipFilePath = Path.Combine(destinationPath, zipFileName);

                    ZipFile.CreateFromDirectory(folder, zipFilePath, CompressionLevel.Optimal, true);
                    if (prompt)
                    { // Only display the status if the prompt is shown (not when the process is run in the background}
                    UpdateStatusLabel($"Completed: {folder}");
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
            UpdateStatusLabel("Archiving Complete");
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
    }

    private long CalculateFolderSize(string folder)
    {
        UpdateStatusLabel($"Estimating size of: {folder}");

        long totalSize = 0;
        // Calculate total size of all files in the folder
        string[] files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories);
        foreach (string file in files)
        {
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
