using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Windows.Forms;
using System.Drawing;
using System.Reflection;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Text.Json;

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
    private CheckBox includeZipInBackupCheckBox;
    private CheckBox onlyMakeZipBackupCheckBox;
    private Button closeButton;
    private Label lastBackupLabel;
    private Label backupDestinationLabel;
    private Label lastSystemImageBackupLabel;
    private Label globalSettingsLabel;
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
    // Controls for individual zip backup settings
    private ComboBox backupOptionComboBox;
    private Label maxZipRetentionLabel;
    private NumericUpDown maxZipRetentionUpDown;
    private CheckBox skipCompareCheckBox;



    private int previousIndex = -1; // To track the item index that the tooltip was last shown for

    public SettingsForm()
    {
        // Get the current assembly
        var assembly = Assembly.GetExecutingAssembly();
        // Get the version of the current assembly
        var version = assembly.GetName().Version;

        // Set up the form
        Text = $"PtecBU Settings - App version: {version}";
        Size = new Size(670, 500);
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
        // Subscribe to the SelectedIndexChanged event
        foldersListBox.SelectedIndexChanged += new EventHandler(foldersListBox_SelectedIndexChanged);

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

        // Initialize the individual folder settings controls
        backupOptionComboBox = new ComboBox
        {
            Location = new Point(10, 250),
            Width = 170,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Visible = false,
        };
        backupOptionComboBox.Items.AddRange(new object[] {
        "Use global setting",
        "Only make zip backup",
        "Include zip in normal backup"
        });


        maxZipRetentionLabel = new Label
        {
            Text = "Max zip files stored (0=no limit):",
            AutoSize = true,
            Visible = false,
            Location = new Point(10, 282)
        };

        maxZipRetentionUpDown = new NumericUpDown
        {
            Location = new Point(190, 280),
            Width = 35,
            Minimum = 0,
            Maximum = 365,
            Value = 0,
            Visible = false
        };

        skipCompareCheckBox = new CheckBox
        {
            Text = "Skip file comparison",
            AutoSize = true,
            Visible = false,
            Location = new Point(10, 305)
        };

        Controls.Add(backupOptionComboBox);
        Controls.Add(maxZipRetentionLabel);
        Controls.Add(maxZipRetentionUpDown);
        Controls.Add(skipCompareCheckBox);

        // Add event handlers to handle changes in the UI and update folder settings
        backupOptionComboBox.SelectedIndexChanged += (s, e) =>
        {
            SaveFolderSettings(foldersListBox.SelectedItem.ToString());
        };

        maxZipRetentionUpDown.ValueChanged += (s, e) =>
        {
            SaveFolderSettings(foldersListBox.SelectedItem.ToString());
        };

        skipCompareCheckBox.CheckedChanged += (s, e) =>
        {
            SaveFolderSettings(foldersListBox.SelectedItem.ToString());
        };


        ArchiveFoldersButton = new Button()
        {
            Location = new Point(10, 340), // Adjust these values to place the button appropriately
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

        // Create the label for the Archiving status
        ArchiveStatusLabel = new Label()
        {
            Location = new Point(235, 345), // Adjust these values to place the label appropriately
            AutoSize = true,
            Text = ""   // Empty placeholder for archiving status
        };
        Controls.Add(ArchiveStatusLabel);

        // Create the label for the last successful backup
        lastBackupLabel = new Label()
        {
            Location = new Point(10, 370), // Adjust these values to place the label appropriately
            AutoSize = true
        };
        Controls.Add(lastBackupLabel);

        //Get and display backup destination
        backupDestinationLabel = new Label()
        {
            Location = new Point(10, 390), // Adjust these values to place the label appropriately
            Text = "Backup destination: " + BackupManager.GetBackupDestination(),
            AutoSize = true
        };
        // Subscribe to the Click event to open the destination when clicked
        backupDestinationLabel.Click += BackupDestinationLabel_Click;
        Controls.Add(backupDestinationLabel);

        lastSystemImageBackupLabel = new Label()
        {
            Location = new Point(10, 410), // Adjust these values to place the label appropriately
            Text = "Last System backup: ",
            AutoSize = true
        };
        Controls.Add(lastSystemImageBackupLabel);

        systemImageBackupButton = new Button()
        {
            Location = new Point(10, 430), // Adjust these values to place the button appropriately
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

        // Update the last successful backup label
        UpdateLastSuccessfulBackup();

        // Create the global settings label
        globalSettingsLabel = new Label()
        {
            Location = new Point(350, 355), // Adjust these values to place the label appropriately
            Text = "Global Settings",
            AutoSize = true
        };
        Controls.Add(globalSettingsLabel);

        // Create the "Launch on Windows Startup" checkbox
        launchOnStartupCheckBox = new CheckBox()
        {
            Location = new Point(350, 375), // Adjust these values to place the checkbox appropriately
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

        // CheckBox for including zip in backup
        includeZipInBackupCheckBox = new CheckBox
        {
            Location = new Point(350, 395), // Adjust as needed
            Text = "Include Zip in Backup",
            AutoSize = true
        };
        includeZipInBackupCheckBox.CheckedChanged += IncludeZipInBackupCheckBox_CheckedChanged;
        Controls.Add(includeZipInBackupCheckBox);
        // Read the value from the config.ini file and set the checkbox accordingly
        var config = AppConfigManager.ReadConfigIni("config.ini");
        if (config.TryGetValue("includeZipInBackup", out string includeZipInBackupValue))
        {
            includeZipInBackupCheckBox.Checked = includeZipInBackupValue.ToLower() == "true";
        }

        // CheckBox for only make zip backup
        onlyMakeZipBackupCheckBox = new CheckBox
        {
            Location = new Point(350, 415), // Adjust as needed
            Text = "Only Make Zip Backup",
            AutoSize = true
        };
        onlyMakeZipBackupCheckBox.CheckedChanged += OnlyMakeZipBackupCheckBox_CheckedChanged;
        Controls.Add(onlyMakeZipBackupCheckBox);
        // Read the value from the config.ini file and set the checkbox accordingly
        if (config.TryGetValue("onlyMakeZipBackup", out string onlyMakeZipBackupValue))
        {
            onlyMakeZipBackupCheckBox.Checked = onlyMakeZipBackupValue.ToLower() == "true";
        }

        openConfigFileLinkLabel = new LinkLabel()
        {
            Location = new Point(350, 435), // Adjust these values to place the label appropriately
            Text = "Open config.ini",
            AutoSize = true
        };
        openConfigFileLinkLabel.LinkClicked += new LinkLabelLinkClickedEventHandler(OpenConfigFileLinkLabel_LinkClicked);
        Controls.Add(openConfigFileLinkLabel);

        // Create the Close button
        closeButton = new Button()
        {
            Location = new Point(560, 430), // Adjust these values to place the button in the right lower corner
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

    private void foldersListBox_SelectedIndexChanged(object sender, EventArgs e)
    {
        var isSelected = foldersListBox.SelectedIndex != -1;
        backupOptionComboBox.Visible = isSelected;
        maxZipRetentionLabel.Visible = isSelected;
        maxZipRetentionUpDown.Visible = isSelected;
        skipCompareCheckBox.Visible = isSelected;

        if (isSelected)
        {
            // Load settings for the selected folder or set defaults if no individual settings
            LoadFolderSettings(foldersListBox.SelectedItem.ToString());
        }
        else
        {
            backupOptionComboBox.Visible = false;
            maxZipRetentionLabel.Visible = false;
            maxZipRetentionUpDown.Visible = false;
            skipCompareCheckBox.Visible = false;
        }
    }

    private void LoadFolderSettings(string folderPath)
    {
        var folderConfigs = FolderConfigManager.LoadFolderConfigs();

        // Attempt to load individual settings for the selected folder
        if (folderConfigs.TryGetValue(folderPath, out var config))
        {
            // Set the UI elements to reflect the folder's individual settings
            backupOptionComboBox.SelectedIndex = (int)config.BackupOption;
            maxZipRetentionUpDown.Value = config.MaxZipRetention;
            skipCompareCheckBox.Checked = config.SkipCompare;
        }
        else
        {
            // No individual settings found, load global settings
            var globalConfig = AppConfigManager.ReadConfigIni("config.ini");
            backupOptionComboBox.SelectedIndex = globalConfig.ContainsKey("defaultBackupOption")
                ? int.Parse(globalConfig["defaultBackupOption"])
                : 0; // Default to 'UseGlobalSetting' if not specified
            maxZipRetentionUpDown.Value = globalConfig.ContainsKey("defaultMaxZipRetention")
                ? int.Parse(globalConfig["defaultMaxZipRetention"])
                : 0; // Default to 0 for no limit if not specified
            skipCompareCheckBox.Checked = globalConfig.ContainsKey("skipZipfileComparison")
                ? bool.Parse(globalConfig["defaultSkipCompare"])
                : false; // Default to false if not specified
        }
    }


    private void SaveFolderSettings(string folderPath)
    {
        var folderConfigs = FolderConfigManager.LoadFolderConfigs();

        // Update or add new settings
        folderConfigs[folderPath] = new FolderConfig
        {
            FolderPath = folderPath,
            BackupOption = (BackupOptions)backupOptionComboBox.SelectedIndex,
            MaxZipRetention = (int)maxZipRetentionUpDown.Value,
            SkipCompare = skipCompareCheckBox.Checked
        };

        FolderConfigManager.SaveFolderConfigs(folderConfigs);
    }


    public class FolderConfig
    {
        public string FolderPath { get; set; }
        public BackupOptions BackupOption { get; set; }
        public int MaxZipRetention { get; set; }
        public bool SkipCompare { get; set; }
    }

    public enum BackupOptions
    {
        UseGlobalSetting,
        OnlyMakeZipBackup,
        IncludeZipInNormalBackup
    }

    public static class FolderConfigManager
    {
        private static readonly string FolderConfigPath = "folder_config.json";

        public static Dictionary<string, FolderConfig> LoadFolderConfigs()
        {
            if (File.Exists(FolderConfigPath))
            {
                string json = File.ReadAllText(FolderConfigPath);
                return JsonSerializer.Deserialize<Dictionary<string, FolderConfig>>(json);
            }

            return new Dictionary<string, FolderConfig>();
        }

        public static void SaveFolderConfigs(Dictionary<string, FolderConfig> folderConfigs)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(folderConfigs, options);
            File.WriteAllText(FolderConfigPath, json);
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

    private void IncludeZipInBackupCheckBox_CheckedChanged(object sender, EventArgs e)
    {
        // Update config.ini with the new value
        UpdateConfigIni("includeZipInBackup", includeZipInBackupCheckBox.Checked ? "true" : "false");
    }

    private void OnlyMakeZipBackupCheckBox_CheckedChanged(object sender, EventArgs e)
    {
        // Update config.ini with the new value
        UpdateConfigIni("onlyMakeZipBackup", onlyMakeZipBackupCheckBox.Checked ? "true" : "false");
    }

    private void UpdateConfigIni(string key, string value)
    {
        // Define the path to the config.ini file
        string filePath = "config.ini";

        // Check if the config file exists
        if (!File.Exists(filePath))
        {
            Console.WriteLine("Config file not found.");
            return;
        }

        // Read all lines from the config file
        var lines = File.ReadAllLines(filePath).ToList();

        // Flag to check if key is found and updated
        bool keyFound = false;

        // Go through each line to find and update the key
        for (int i = 0; i < lines.Count; i++)
        {
            // Check if the line contains the key (ignoring comment lines)
            if (!lines[i].TrimStart().StartsWith("//") && lines[i].Contains(key))
            {
                // Replace the line with the new key-value pair
                lines[i] = $"{key}={value}";
                keyFound = true;
                break; // Exit the loop since the key has been found and updated
            }
        }

        // If the key wasn't found in the existing lines, add it as a new entry
        if (!keyFound)
        {
            lines.Add($"{key}={value}");
        }

        // Write the updated lines back to the config file
        File.WriteAllLines(filePath, lines);
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

    public FolderArchiver(Label statusLabel = null)
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
            if (statusLabel != null)
            {
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
                    if (ShouldCreateNewArchive(folder))
                    {
                        if (prompt)
                        {
                            // Calculate the total size of the files in the folder
                            long totalSize = CalculateFolderSize(folder);

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
    }

    private bool ShouldCreateNewArchive(string folderPath)
    {
        if (IsZipfileComparisonSkipped())
        {
            Debug.WriteLine("Zipfile comparison skipped due to configuration.");
            return true;
        }

        var latestZipFilePath = GetLatestZipFilePath(folderPath);
        if (string.IsNullOrEmpty(latestZipFilePath))
        {
            Debug.WriteLine("No existing archive found, creating a new one.");
            return true; // No archive to compare against, return true.
        }

        Debug.WriteLine($"Comparing against latest archive: {latestZipFilePath}");

        using (ZipArchive archive = ZipFile.OpenRead(latestZipFilePath))
        {
            var zipEntries = archive.Entries.ToDictionary(entry => entry.FullName, entry => entry.LastWriteTime.DateTime);

            // print out the entries for debugging
            foreach (var entry in zipEntries)
            {
                Debug.WriteLine($"Archive entry: {entry.Key} (Modified: {entry.Value})");
            }

            foreach (string filePath in Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories))
            {
                FileInfo localFile = new FileInfo(filePath);
                string relativePath = GetRelativePath(filePath, folderPath)
                    .Replace('\\', '/') // Use forward slashes
                    .TrimStart('/'); // Ensure no leading slash

                // Debug.WriteLine($"Checking file: {relativePath}");

                // Check if the local file exists in the zip archive
                if (!zipEntries.ContainsKey(relativePath))
                {
                    Debug.WriteLine($"New file found, not present in archive: {relativePath}");
                    return true; // File is new, create a new archive.
                }
                else
                {
                    var zipLastWriteTime = zipEntries[relativePath];
                    Debug.WriteLine($"Found matching archive entry for {relativePath}. Comparing timestamps...");

                    // Add your timestamp comparison logic here
                    TimeSpan timeDifference = localFile.LastWriteTime - zipLastWriteTime;
                    Debug.WriteLine($"Time difference (in seconds): {timeDifference.TotalSeconds}");

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


    private bool IsZipfileComparisonSkipped()
    {
        var config = AppConfigManager.ReadConfigIni("config.ini");
        if (config.TryGetValue("skipZipfileComparison", out string skipComparison) && skipComparison.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return false;
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

// Helper class to manage the application configuration
public static class AppConfigManager
{
    public static Dictionary<string, string> ReadConfigIni(string filePath)
    {
        var config = new Dictionary<string, string>();

        // Check if the file exists to avoid FileNotFoundException
        if (File.Exists(filePath))
        {
            var lines = File.ReadAllLines(filePath);
            foreach (var line in lines)
            {
                // Skip empty lines and comments
                if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith(";"))
                {
                    var parts = line.Split('=', 2); // Split the line into key and value
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim();
                        var value = parts[1].Trim();
                        config[key] = value;
                    }
                }
            }
        }
        else
        {
            // Optionally, log the error or throw an exception if the file is not found
            MessageBox.Show($"Configuration file not found: {filePath}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        return config;
    }
}

