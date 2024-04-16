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
using Microsoft.VisualBasic;
using System.Text.RegularExpressions;
using System.Text;

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
    private CheckBox globalSkipCompareCheckBox;
    private Label globalMaxZipRetentionUpDownLabel;
    private NumericUpDown globalMaxZipRetentionUpDown;
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

    private ToolTip LabelToolTip = new()
    {
        AutoPopDelay = 5000,
        InitialDelay = 100,
        ReshowDelay = 500,
        ShowAlways = true
    };

    // Controls for individual zip backup settings
    public Label individualSettingsLabel;
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
        // Subscribe to the MouseWheel event to change the selected index
        foldersListBox.MouseWheel += (s, e) =>
        {
            int newIndex = foldersListBox.SelectedIndex - Math.Sign(e.Delta);
            if (newIndex >= 0 && newIndex < foldersListBox.Items.Count)
            {
                foldersListBox.SelectedIndex = newIndex;
            }
        };

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
        LabelToolTip.SetToolTip(addFolderButton, "Add a folder to the backup list.");

        // Create the remove folder button
        removeFolderButton = new Button()
        {
            Location = new Point(220, 60),
            Text = "Remove Folder",
        };
        removeFolderButton.Click += OnRemoveFolder;
        Controls.Add(removeFolderButton);
        LabelToolTip.SetToolTip(removeFolderButton, "Remove the selected folder from the backup list.");

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
        LabelToolTip.SetToolTip(addExcludedItemButton, "Add an item to the excluded list.");

        // Create the remove excluded item button
        removeExcludedItemButton = new Button()
        {
            Location = new Point(560, 40),
            Text = "Remove Excluded Item",
        };
        removeExcludedItemButton.Click += OnRemoveExcludedItem;
        Controls.Add(removeExcludedItemButton);
        LabelToolTip.SetToolTip(removeExcludedItemButton, "Remove the selected item from the excluded list.");

        // Initialize the individual folder settings controls

        individualSettingsLabel = new Label
        {
            Text = "Select folder to see individual settings",
            AutoSize = true,
            Location = new Point(10, 233)
        };
        Controls.Add(individualSettingsLabel);

        backupOptionComboBox = new ComboBox
        {
            Location = new Point(10, 250),
            Width = 180,
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
        LabelToolTip.SetToolTip(maxZipRetentionLabel, "Maximum number of zip files to keep in the backup folder for selected folder. Set to 0 for no limit.");

        maxZipRetentionUpDown = new NumericUpDown
        {
            Location = new Point(190, 280),
            Width = 35,
            Minimum = 0,
            Maximum = 365,
            Value = 0,
            Visible = false,
            Enabled = false
        };
        LabelToolTip.SetToolTip(maxZipRetentionUpDown, "Maximum number of zip files to keep in the backup folder for selected folder. Set to 0 for no limit.");

        skipCompareCheckBox = new CheckBox
        {
            Text = "Skip file comparison",
            AutoSize = true,
            Visible = false,
            Location = new Point(10, 305)
        };
        LabelToolTip.SetToolTip(skipCompareCheckBox, "Skip file comparison inside previous zip file when making zip backup.(Creates new zip file every time)");

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
        LabelToolTip.SetToolTip(ArchiveFoldersButton, "Backup all folders to zip files in the backup location. This may take a long time depending on the folder size.");

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
        LabelToolTip.SetToolTip(backupDestinationLabel, "Click to open the backup destination folder.");

        lastSystemImageBackupLabel = new Label()
        {
            Location = new Point(10, 410), // Adjust these values to place the label appropriately
            Text = "Last System backup: ",
            AutoSize = true
        };
        Controls.Add(lastSystemImageBackupLabel);
        LabelToolTip.SetToolTip(lastSystemImageBackupLabel, "");

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
        LabelToolTip.SetToolTip(CheckUpdatesButton, "Check for updates online.");

        // Update the last successful backup label
        UpdateLastSuccessfulBackup();

        // Create the global settings label
        globalSettingsLabel = new Label()
        {
            Location = new Point(350, 305), // Adjust these values to place the label appropriately
            Text = "Global Settings",
            AutoSize = true
        };
        Controls.Add(globalSettingsLabel);

        // Create the "Launch on Windows Startup" checkbox
        launchOnStartupCheckBox = new CheckBox()
        {
            Location = new Point(350, 325), // Adjust these values to place the checkbox appropriately
            Text = "Launch on Windows Startup",
            AutoSize = true
        };
        // Subscribe to the CheckedChanged event
        launchOnStartupCheckBox.CheckedChanged += LaunchOnStartupCheckBox_CheckedChanged;
        Controls.Add(launchOnStartupCheckBox);
        LabelToolTip.SetToolTip(launchOnStartupCheckBox, "Launch the application automatically when Windows starts.");

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
            Location = new Point(350, 345), // Adjust as needed
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
        LabelToolTip.SetToolTip(includeZipInBackupCheckBox, "Create a zip file of each folder to the backup folder when making a backup.");

        // CheckBox for only make zip backup
        onlyMakeZipBackupCheckBox = new CheckBox
        {
            Location = new Point(350, 365), // Adjust as needed
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
        LabelToolTip.SetToolTip(onlyMakeZipBackupCheckBox, "Create a zip file of each folder to the backup folder without making a normal backup.");

        // Create the global skip compare checkbox
        globalSkipCompareCheckBox = new CheckBox
        {
            Location = new Point(350, 385), // Adjust as needed
            Text = "Skip file comparison",
            AutoSize = true
        };
        globalSkipCompareCheckBox.CheckedChanged += GlobalSkipCompareCheckBox_CheckedChanged;
        Controls.Add(globalSkipCompareCheckBox);
        // Read the value from the config.ini file and set the checkbox accordingly
        if (config.TryGetValue("skipZipfileComparison", out string skipZipfileComparisonValue))
        {
            globalSkipCompareCheckBox.Checked = skipZipfileComparisonValue.ToLower() == "true";
        }
        LabelToolTip.SetToolTip(globalSkipCompareCheckBox, "Skip file comparison inside previous zip file when making zip backup.(Creates new zip file every time)");

        // Create the global max zip retention label
        globalMaxZipRetentionUpDownLabel = new Label
        {
            Text = "Max Zip files stored (0=no limit):",
            AutoSize = true,
            Location = new Point(390, 410) // Adjust as needed
        };
        Controls.Add(globalMaxZipRetentionUpDownLabel);
        LabelToolTip.SetToolTip(globalMaxZipRetentionUpDownLabel, "Maximum number of zip files to keep in the backup folder for all folders. Set to 0 for no limit.");

        globalMaxZipRetentionUpDown = new NumericUpDown
        {
            Location = new Point(350, 405), // Adjust as needed
            Width = 35,
            Height = 10,
            Minimum = 0,
            Maximum = 365,
            Value = 0
        };
        Controls.Add(globalMaxZipRetentionUpDown);
        // Read the value from the config.ini file and set the numeric up-down accordingly
        if (config.TryGetValue("defaultMaxZipRetention", out string defaultMaxZipRetentionValue))
        {
            globalMaxZipRetentionUpDown.Value = int.Parse(defaultMaxZipRetentionValue);
        }
        // Subscribe to the ValueChanged event to update the config.ini file
        globalMaxZipRetentionUpDown.ValueChanged += (s, e) =>
        {
            UpdateConfigIni("defaultMaxZipRetention", globalMaxZipRetentionUpDown.Value.ToString());
        };

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
            LoadFolderSettingsUi(foldersListBox.SelectedItem.ToString());
        }
        else
        {
            backupOptionComboBox.Visible = false;
            maxZipRetentionLabel.Visible = false;
            maxZipRetentionUpDown.Visible = false;
            skipCompareCheckBox.Visible = false;
            individualSettingsLabel.Text = $"Select folder to see individual settings";

            maxZipRetentionUpDown.Enabled = false;
            skipCompareCheckBox.Enabled = false;
        }
        // maxZipRetentionUpDown and skipCompareCheckBox is enabled only if the selected backup option is not 'UseGlobalSetting'
        maxZipRetentionUpDown.Enabled = isSelected && backupOptionComboBox.SelectedIndex != 0;
        skipCompareCheckBox.Enabled = isSelected && backupOptionComboBox.SelectedIndex != 0;
    }

    private void LoadFolderSettingsUi(string folderPath)
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
                ? bool.Parse(globalConfig["skipZipfileComparison"])
                : false; // Default to false if not specified
        }
        // Update the label to show the folder path.
        individualSettingsLabel.Text = $"Settings for: {TrimPath(folderPath)}";
    }
    //Function to trim long folder path and show only the end part
    private string TrimPath(string path, int maxLength = 30)
    {
        if (path.Length <= maxLength)
        {
            return path;
        }
        // Trim the path to the last maxLength characters. Add "..." at the beginning to indicate the path is trimmed and two first characters to show the root folder
        return path.Substring(0, 2) + ".." + path.Substring(path.Length - maxLength);
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

        // Enable or disable the maxZipRetentionUpDown and skipCompareCheckBox based on the selected backup option
        maxZipRetentionUpDown.Enabled = backupOptionComboBox.SelectedIndex != 0;
        skipCompareCheckBox.Enabled = backupOptionComboBox.SelectedIndex != 0;

        // If the selected backup option is 'UseGlobalSetting', set the maxZipRetention and skipCompareCheckBox value to the global setting
        if (backupOptionComboBox.SelectedIndex == 0)
        {
            maxZipRetentionUpDown.Value = globalMaxZipRetentionUpDown.Value;
            skipCompareCheckBox.Checked = globalSkipCompareCheckBox.Checked;
        }

        FolderConfigManager.SaveFolderConfigs(folderConfigs);
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
            string ellipsis = fullPath.Substring(0, 3) + "..";
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
            // Remove folder settings from the folder config file
            var folderConfigs = FolderConfigManager.LoadFolderConfigs();
            folderConfigs.Remove(foldersListBox.SelectedItem.ToString());

            // Save the updated folder configs
            FolderConfigManager.SaveFolderConfigs(folderConfigs);
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
        string filePath = "log/lastBackup.log"; // replace with your path

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
        string sysimgfilePath = "log/lastsystemimage.log"; // replace with your path

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

            // Write the current date and time to lastsystemimage.log
            File.WriteAllText("log/lastsystemimage.log", DateTime.Now.ToString());

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

    private void GlobalSkipCompareCheckBox_CheckedChanged(object sender, EventArgs e)
    {
        // Update config.ini with the new value
        UpdateConfigIni("skipZipfileComparison", globalSkipCompareCheckBox.Checked ? "true" : "false");
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
            string logPath = Path.Combine(launcherPath, "log/updater_err.log");
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

    private bool IsExcluded(string filePath, List<string> excludedItems)
    {
        // Normalize the file path to use uniform directory separators
        string normalizedPath = filePath.Replace('\\', '/');

        // Debug messages for excluded items
        Debug.WriteLine($"Checking: {filePath} with normalized path: {normalizedPath}");

        foreach (var pattern in excludedItems)
        {
            // Convert wildcard pattern to regex, escaping special characters
            string regexPattern = Regex.Escape(pattern)
                                      .Replace("\\*", ".*") // Replace '*' with '.*' for regex
                                      .Replace("\\?", ".")  // Replace '?' with '.' for regex
                                      + ".*"; // Ensure directory exclusion covers all subdirectories and files

            if (Regex.IsMatch(normalizedPath, regexPattern, RegexOptions.IgnoreCase))
            {
                Debug.WriteLine($"Excluded by regex match: {filePath}");
                return true;
            }
        }
        return false;
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
                            fileName => !IsExcluded(fileName, excludedItems)
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
            var zipEntries = archive.Entries.ToDictionary(entry => entry.FullName, entry => entry.LastWriteTime.DateTime);

            // Check if any new files or modified files are present in the folder. Skip excluded items.
            foreach (string filePath in Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories))
            {
                FileInfo localFile = new FileInfo(filePath);
                string relativePath = GetRelativePath(filePath, folderPath)
                    .Replace('\\', '/') // Use forward slashes
                    .TrimStart('/'); // Ensure no leading slash

                // Debug.WriteLine($"Checking file: {relativePath}");

                // Check if the local file exists in the zip archive if it's not an excluded item
                if (excludedItems.Contains(relativePath))
                {
                    Debug.WriteLine($"Excluded item found: {relativePath}");
                    continue; // Skip excluded items
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
                if (!filter(filesToAdd[i]))
                {
                    continue;
                }
                archive.CreateEntryFromFile(filesToAdd[i], entryNames[i], compressionLevel);
            }
        }
    }

    private static void AddFilesRecursively(string currentDir, List<string> filesToAdd, Predicate<string> filter)
    {
        // Log entering the directory
        Debug.WriteLine($"Processing directory: {currentDir}");

        // Process each file in the current directory
        foreach (var file in Directory.GetFiles(currentDir))
        {
            // Normalize file path for consistent filtering
            string normalizedFile = file.Replace('\\', '/');
            // Log processing of the file
            if (!filter(normalizedFile))
            {
                filesToAdd.Add(file);
                Debug.WriteLine($"Included file: {file}");
            }
            else
            {
                Debug.WriteLine($"Excluded file: {file}");
            }
        }

        // Recursively process subdirectories
        foreach (var directory in Directory.GetDirectories(currentDir))
        {
            // Normalize directory path for consistent filtering
            string normalizedDir = directory.Replace('\\', '/');
            if (!filter(normalizedDir))
            {
                AddFilesRecursively(directory, filesToAdd, filter);
            }
            else
            {
                Debug.WriteLine($"Excluded directory: {directory}");
            }
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

