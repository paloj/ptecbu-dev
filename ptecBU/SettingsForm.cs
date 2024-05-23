#region Using directives
using System.Windows.Forms;
using System.Reflection;
using System.Diagnostics;
using System.Text.Json;
using BlueMystic;
#endregion
class SettingsForm : Form
{
    #region Fields
    private DarkModeCS DM = null; //<- Line 1
    private ListBox foldersListBox;
    private Button addFolderButton;
    private Button removeFolderButton;
    private ListBox excludedItemsListBox;
    private TextBox addExcludedItemTextBox;
    private Button addExcludedItemButton;
    private Button removeExcludedItemButton;
    private CheckBox launchOnStartupCheckBox;
    private Label globalBackupOptionComboBoxLabel;
    private ComboBox globalBackupOptionComboBox;
    private Button closeButton;
    private Label lastBackupLabel;
    private Label backupDestinationLabel;
    private Label lastSystemImageBackupLabel;
    private Label globalSettingsLabel;
    private CheckBox globalSkipCompareCheckBox;
    private Label globalMaxZipRetentionUpDownLabel;
    private NumericUpDown globalMaxZipRetentionUpDown;
    private LinkLabel openConfigFileLinkLabel;
    private Button RunBackupButton;
    private Button ArchiveFoldersButton;
    private Button systemImageBackupButton;
    private Button CheckUpdatesButton;
    private StatusStrip statusStrip;
    private ToolStripStatusLabel statusLabel;
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
    private ComboBox individualBackupOptionComboBox;
    private Label maxZipRetentionLabel;
    private NumericUpDown maxZipRetentionUpDown;
    private CheckBox skipCompareCheckBox;



    private int previousIndex = -1; // To track the item index that the tooltip was last shown for

    #endregion
    #region Constructor
    public SettingsForm()
    {
        DM = new DarkModeCS(this);
        // Get the current assembly
        var assembly = Assembly.GetExecutingAssembly();
        // Get the version of the current assembly
        var version = assembly.GetName().Version;

        // Set up the form
        Text = $"PtecBU Settings - App version: {version}";
        Size = new Size(670, 540);
        FormBorderStyle = FormBorderStyle.FixedSingle; // Make the form non-resizable
        MaximizeBox = false; // Disable the maximize button
        // Set the form's icon
        this.Icon = new Icon("Resources/white.ico");

        // create the status strip
        statusStrip = new StatusStrip();
        // Add frame border to the status strip
        statusStrip.SizingGrip = false;
        // Add the status label to the status strip
        statusLabel = new ToolStripStatusLabel();
        statusStrip.Items.Add(statusLabel);
        statusLabel.Text = "Ready";
        // Add margin to the status label
        statusLabel.Margin = new Padding(10, 0, 0, 0);
        // Add the status strip to the form's controls
        Controls.Add(statusStrip);
        // Subscribe to the BackupManager's StatusUpdated event to update the status label
        BackupManager.StatusUpdated += UpdateStatusLabelSafe;
        FolderArchiver.StatusUpdated += UpdateStatusLabelSafe;
        this.FormClosing += FormClosing_Event;  // Subscribe to the FormClosing event to unsubscribe from the BackupManager's StatusUpdated event

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
                // Update the tooltip text with the full path of the current item
                fullPathToolTip.SetToolTip(foldersListBox, foldersListBox.Items[newIndex].ToString());
                // Update status label with the full path of the current item
                UpdateStatusLabelSafe(foldersListBox.Items[newIndex].ToString());
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

        individualBackupOptionComboBox = new ComboBox
        {
            Location = new Point(10, 250),
            Width = 180,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Visible = false,
        };
        individualBackupOptionComboBox.Items.AddRange(new object[] {
        "Use global setting",
        "Only make zip backup",
        "Include zip in normal backup",
        "Only make normal backup"
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

        Controls.Add(individualBackupOptionComboBox);
        Controls.Add(maxZipRetentionLabel);
        Controls.Add(maxZipRetentionUpDown);
        Controls.Add(skipCompareCheckBox);

        // Add event handlers to handle changes in the UI and update folder settings
        individualBackupOptionComboBox.SelectedIndexChanged += (s, e) =>
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

        // Create the Run Backup button
        RunBackupButton = new Button()
        {
            Location = new Point(10, 370), // Adjust these values to place the button appropriately
            AutoSize = true, // Enable auto-sizing to adjust the button's width based on its text content
            AutoSizeMode = AutoSizeMode.GrowAndShrink, // Allow the button to grow and shrink
            Height = 25, // Specify the desired height
            Text = "Run Backup",
        };
        RunBackupButton.Click += (s, e) =>
        {
            // Run the backup process asynchronously
            Task.Run(() => Program.OnBackupNow(s, e, true));
        };
        Controls.Add(RunBackupButton);

        ArchiveFoldersButton = new Button()
        {
            Location = new Point(10, 400), // Adjust these values to place the button appropriately
            AutoSize = true, // Enable auto-sizing to adjust the button's width based on its text content
            AutoSizeMode = AutoSizeMode.GrowAndShrink, // Allow the button to grow and shrink
            Height = 25, // Specify the desired height
            Text = "Create Zip Backups Now",
        };
        LabelToolTip.SetToolTip(ArchiveFoldersButton, "Backup all folders to zip files in the backup location. This may take a long time depending on the folder size.");

        ArchiveFoldersButton.Click += ArchiveFoldersButton_Click;
        Controls.Add(ArchiveFoldersButton);

        // Create the label for the last successful backup
        lastBackupLabel = new Label()
        {
            Location = new Point(10, 330), // Adjust these values to place the label appropriately
            AutoSize = true
        };
        Controls.Add(lastBackupLabel);

        lastSystemImageBackupLabel = new Label()
        {
            Location = new Point(10, 350), // Adjust these values to place the label appropriately
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
            Text = "Create System Image Backup",
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

        //Get and display backup destination
        backupDestinationLabel = new Label()
        {
            Location = new Point(10, 460), // Adjust these values to place the label appropriately
            Text = "Backup destination: " + Path.Combine(ConfigurationManager.Config["destination"], Environment.MachineName),
            AutoSize = true
        };
        // Subscribe to the Click event to open the destination when clicked
        backupDestinationLabel.Click += BackupDestinationLabel_Click;
        Controls.Add(backupDestinationLabel);
        LabelToolTip.SetToolTip(backupDestinationLabel, "Click to open the backup destination folder.");

        // Update the last successful backup label
        UpdateLastSuccessfulBackup();

        // Create the global settings label
        globalSettingsLabel = new Label()
        {
            Location = new Point(350, 305), // Adjust these values to place the label appropriately
            Text = "Global Settings",
            // Set the AutoSize property to true to automatically adjust the label's size based on its content
            AutoSize = true
        };
        // Set the font style to bold and underline
        globalSettingsLabel.Font = new Font(globalSettingsLabel.Font, FontStyle.Bold | FontStyle.Underline);
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

        // Create the global backup option label
        globalBackupOptionComboBoxLabel = new Label()
        {
            Text = "Backup options:",
            AutoSize = true,
            Location = new Point(347, 342) // Adjust these values to place the label appropriately
        };
        Controls.Add(globalBackupOptionComboBoxLabel);

        // Create the global backup option combo box
        globalBackupOptionComboBox = new ComboBox()
        {
            Location = new Point(350, 360), // Adjust these values to place the combo box appropriately
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 200
        };
        globalBackupOptionComboBox.Items.AddRange(new object[] {
        "Only make normal backup",
        "Only make zip backup",
        "Include zip in normal backup"
        });
        Controls.Add(globalBackupOptionComboBox);
        // Read the value from the config.ini file and set the combo box accordingly
        var config = ConfigurationManager.Config;
        if (config.TryGetValue("includeZipInBackup", out string includeZipInBackupValue) && config.TryGetValue("onlyMakeZipBackup", out string onlyMakeZipBackupValue))
        {
            if (includeZipInBackupValue.Equals("true", StringComparison.CurrentCultureIgnoreCase) && onlyMakeZipBackupValue.Equals("true", StringComparison.CurrentCultureIgnoreCase))
            {
                globalBackupOptionComboBox.SelectedIndex = 1;
            }
            else if (onlyMakeZipBackupValue.Equals("true", StringComparison.CurrentCultureIgnoreCase))
            {
                globalBackupOptionComboBox.SelectedIndex = 1;
            }
            else if (includeZipInBackupValue.Equals("true", StringComparison.CurrentCultureIgnoreCase))
            {
                globalBackupOptionComboBox.SelectedIndex = 2;
            }
            else
            {
                globalBackupOptionComboBox.SelectedIndex = 0;
            }
        }
        // Subscribe to the SelectedIndexChanged event to update the config.ini file
        globalBackupOptionComboBox.SelectedIndexChanged += (s, e) =>
        {
            if (globalBackupOptionComboBox.SelectedIndex == 0)
            {
                ConfigurationManager.UpdateIniFile("onlyMakeZipBackup", "false");
                ConfigurationManager.UpdateIniFile("includeZipInBackup", "false");
                // Update status label
                statusLabel.Text = "Global backup option set to 'Only make normal backup'.";
                // Hide the global skip compare checkbox if the global backup option is set to 'Only make normal backup'
                globalSkipCompareCheckBox.Visible = false;
                // Hide the global max zip retention label and up-down if the global backup option is set to 'Only make normal backup'
                globalMaxZipRetentionUpDownLabel.Visible = globalMaxZipRetentionUpDown.Visible = false;
            }
            else if (globalBackupOptionComboBox.SelectedIndex == 1)
            {
                ConfigurationManager.UpdateIniFile("onlyMakeZipBackup", "true");
                ConfigurationManager.UpdateIniFile("includeZipInBackup", "false");
                // Update status label
                statusLabel.Text = "Global backup option set to 'Only make zip backup'.";
                // Show the global skip compare checkbox if the global backup option is set to 'Only make zip backup'
                globalSkipCompareCheckBox.Visible = true;
                // Show the global max zip retention label and up-down if the global backup option is set to 'Only make zip backup'
                globalMaxZipRetentionUpDownLabel.Visible = globalMaxZipRetentionUpDown.Visible = true;
            }
            else if (globalBackupOptionComboBox.SelectedIndex == 2)
            {
                ConfigurationManager.UpdateIniFile("onlyMakeZipBackup", "false");
                ConfigurationManager.UpdateIniFile("includeZipInBackup", "true");
                // Update status label
                statusLabel.Text = "Global backup option set to 'Include zip in normal backup'.";
                // Show the global skip compare checkbox if the global backup option is set to 'Include zip in normal backup'
                globalSkipCompareCheckBox.Visible = true;
                // Show the global max zip retention label and up-down if the global backup option is set to 'Include zip in normal backup'
                globalMaxZipRetentionUpDownLabel.Visible = globalMaxZipRetentionUpDown.Visible = true;
            }
        };

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
        // Hide the global skip compare checkbox if the global backup option is set to 'Only make normal backup'
        globalSkipCompareCheckBox.Visible = globalBackupOptionComboBox.SelectedIndex != 0;

        // Create the global max zip retention label
        globalMaxZipRetentionUpDownLabel = new Label
        {
            Text = "Max Zip files stored (0=no limit):",
            AutoSize = true,
            Location = new Point(390, 410) // Adjust as needed
        };
        Controls.Add(globalMaxZipRetentionUpDownLabel);
        LabelToolTip.SetToolTip(globalMaxZipRetentionUpDownLabel, "Maximum number of zip files to keep in the backup location for each folder. Set to 0 for no limit.");

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
            ConfigurationManager.UpdateIniFile("defaultMaxZipRetention", globalMaxZipRetentionUpDown.Value.ToString());
        };
        // Set the tooltip for the global max zip retention up-down
        LabelToolTip.SetToolTip(globalMaxZipRetentionUpDown, "Maximum number of zip files to keep in the backup folder for all folders. Set to 0 for no limit.");
        // Hide the global max zip retention label and up-down if the global backup option is set to 'Only make normal backup'
        globalMaxZipRetentionUpDownLabel.Visible = globalMaxZipRetentionUpDown.Visible = globalBackupOptionComboBox.SelectedIndex != 0;

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

        // Set the status stript text to current status
        statusStripUpdate("Ready", true);
    }
    #endregion

    private void foldersListBox_SelectedIndexChanged(object sender, EventArgs e)
    {
        var isSelected = foldersListBox.SelectedIndex != -1;
        individualBackupOptionComboBox.Visible = isSelected;
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
            individualBackupOptionComboBox.Visible = false;
            maxZipRetentionLabel.Visible = false;
            maxZipRetentionUpDown.Visible = false;
            skipCompareCheckBox.Visible = false;
            individualSettingsLabel.Text = $"Select folder to see individual settings";

            maxZipRetentionUpDown.Enabled = false;
            skipCompareCheckBox.Enabled = false;
        }
        // maxZipRetentionUpDown and skipCompareCheckBox is enabled only if the selected backup option is not 'UseGlobalSetting'
        maxZipRetentionUpDown.Enabled = isSelected && individualBackupOptionComboBox.SelectedIndex != 0;
        skipCompareCheckBox.Enabled = isSelected && individualBackupOptionComboBox.SelectedIndex != 0;
    }

    private void LoadFolderSettingsUi(string folderPath)
    {
        var folderConfigs = FolderConfigManager.LoadFolderConfigs();

        // Attempt to load individual settings for the selected folder
        if (folderConfigs.TryGetValue(folderPath, out var config))
        {
            // Set the UI elements to reflect the folder's individual settings
            individualBackupOptionComboBox.SelectedIndex = (int)config.BackupOption;
            maxZipRetentionUpDown.Value = config.MaxZipRetention;
            skipCompareCheckBox.Checked = config.SkipCompare;
        }
        else
        {
            // No individual settings found, load global settings
            var globalConfig = ConfigurationManager.Config;
            individualBackupOptionComboBox.SelectedIndex = globalConfig.ContainsKey("defaultBackupOption")
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
            BackupOption = (BackupOptions)individualBackupOptionComboBox.SelectedIndex,
            MaxZipRetention = (int)maxZipRetentionUpDown.Value,
            SkipCompare = skipCompareCheckBox.Checked
        };

        // Enable or disable the maxZipRetentionUpDown and skipCompareCheckBox based on the selected backup option
        maxZipRetentionUpDown.Enabled = individualBackupOptionComboBox.SelectedIndex != 0;
        skipCompareCheckBox.Enabled = individualBackupOptionComboBox.SelectedIndex != 0;

        // If the selected backup option is 'UseGlobalSetting', set the maxZipRetention and skipCompareCheckBox value to the global setting
        if (individualBackupOptionComboBox.SelectedIndex == 0)
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
            // Update status label with the full path of the current item
            statusLabel.Text = foldersListBox.Items[index].ToString();
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
        using var dialog = new FolderBrowserDialog();
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            string selectedPath = dialog.SelectedPath;
            foldersListBox.Items.Add(selectedPath);
            //update folders.txt file
            WriteListBoxContentsToFile(foldersListBox, "folders.txt");
            // Update statuslabel
            statusLabel.Text = $"Folder {selectedPath} added to backup list.";
        }
    }
    private void OnRemoveFolder(object sender, EventArgs e)
    {
        string folderPath = foldersListBox.SelectedItem.ToString();
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
        // Update statuslabel
        statusLabel.Text = $"Folder {folderPath} removed from backup list.";
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
        // Update statuslabel
        statusLabel.Text = $"Item {item} added to excluded list.";
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
        // Update statuslabel
        statusLabel.Text = $"Item removed from excluded list.";
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

    private void statusStripUpdate(string text, bool checkSettings = false)
    {
        if (checkSettings)
        {
            // Perform settings check or other initialization tasks
            CheckSettings();  // This is a hypothetical method that checks settings
        }
        else
        {
            // Update the status label text
            statusLabel.Text = text;
        }
        // Check if status is not "Ready" and set the status strip color accordingly
        statusStrip.BackColor = statusLabel.Text != "Ready" ? Color.Red : SystemColors.Control;

    }

    private void CheckSettings()
    {
        // Check application settings and update status based on the results

        // Check if foldersListBox is empty
        if (foldersListBox.Items.Count == 0)
        {
            statusLabel.Text = "No folders selected for backup.";
        }
        else
        {
            statusLabel.Text = "Ready";
        }

        // Check if backup destination is set
        if (string.IsNullOrEmpty(ConfigurationManager.Config["destination"]))
        {
            statusLabel.Text = "Backup destination not set.";
        }
        else // Check if the backup destination is reachable
        {
            if (!BackupManager.IsBackupLocationReachable(Path.Combine(ConfigurationManager.Config["destination"], Environment.MachineName)))
            {
                statusLabel.Text = "Backup destination is not reachable.";
            }
            else
            {
                statusLabel.Text = "Ready";
            }
        }
    }

    private void OnSystemImageBackup(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(ConfigurationManager.Config["systemimagedestination"]))
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
                Arguments = ConfigurationManager.Config["systemimagedestination"],
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
    private async void ArchiveFoldersButton_Click(object sender, EventArgs e)
    {
        Program.IsBackupInProgress = true;
        statusLabel.Text = "Creating zip backups for all folders. This may take a while...";

        try
        {
            var archiver = new FolderArchiver();
            Program.UpdateTrayMenuItem();
            await Task.Run(() => archiver.ArchiveFolders());
            statusLabel.Text = "Archiving complete.";
        }
        catch (Exception ex)
        {
            statusLabel.Text = "Error during archiving: " + ex.Message;
        }
        finally
        {
            Program.IsBackupInProgress = false;
        }
    }


    private void UpdateStatusLabelSafe(string message)
    {
        if (this.statusStrip.InvokeRequired)
        {
            this.statusStrip.Invoke(new Action(() => UpdateStatusLabel(message)));
        }
        else
        {
            UpdateStatusLabel(message);
        }
    }

    private void UpdateStatusLabel(string message)
    {
        // Ensure the status label is not null before updating
        if (statusLabel != null)
        {
            statusLabel.Text = message;
        }
    }

    // Update registry if the autostart checkbox status changes
    private void LaunchOnStartupCheckBox_CheckedChanged(object sender, EventArgs e)
    {
        UpdateRegistryForStartup();
        // Update status label
        string text = launchOnStartupCheckBox.Checked ? "Application will start on Windows startup." : "Application will not start on Windows startup.";
        UpdateStatusLabelSafe(text);
    }

    private void GlobalSkipCompareCheckBox_CheckedChanged(object sender, EventArgs e)
    {
        // Update config.ini with the new value
        ConfigurationManager.UpdateIniFile("skipZipfileComparison", globalSkipCompareCheckBox.Checked ? "true" : "false");
        // Update status label
        statusLabel.Text = globalSkipCompareCheckBox.Checked ? "File comparison will be skipped when making zip backups." : "File comparison will be performed when making zip backups.";
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

    private void FormClosing_Event(object sender, FormClosingEventArgs e)
    {
        // Unsubscribe to prevent memory leaks
        BackupManager.StatusUpdated -= UpdateStatusLabelSafe;
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
    IncludeZipInNormalBackup,
    OnlyMakeNormalBackup
}
