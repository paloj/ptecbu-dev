using System;
using System.IO;
using System.Windows.Forms;
using System.Drawing;
using System.Reflection;

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
    private Button saveButton;
    private Button cancelButton;
    private Label lastBackupLabel;
    private Label backupDestinationLabel;

    public SettingsForm()
    {
        // Get the current assembly
        var assembly = Assembly.GetExecutingAssembly();
        // Get the version of the current assembly
        var version = assembly.GetName().Version;

        // Set up the form
        Text = $"Settings - App version: {version}";
        Size = new Size(800, 420);
        FormBorderStyle = FormBorderStyle.FixedSingle; // Make the form non-resizable

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
        };
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

        // Update the last successful backup label
        UpdateLastSuccessfulBackup();

        //Get and display backup destination
        backupDestinationLabel = new Label()
        {
            Location = new Point(10, 360), // Adjust these values to place the label appropriately
            Text = "Backup destination: " + BackupManager.GetBackupDestination(),
            AutoSize = true
        };
        Controls.Add(backupDestinationLabel);

        // Create the "Launch on Windows Startup" checkbox
        launchOnStartupCheckBox = new CheckBox()
        {
            Location = new Point(350, 280), // Adjust these values to place the checkbox appropriately
            Text = "Launch on Windows Startup",
            AutoSize = true
        };
        Controls.Add(launchOnStartupCheckBox);

        // Update the checkbox based on the Windows Registry setting for application launch on startup
        using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\RunOnce", true))
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

        // Create the save button
        saveButton = new Button()
        {
            Location = new Point(600, 340), // Adjust these values to place the button in the right lower corner
            Text = "Save",
        };
        saveButton.Click += OnSave;
        Controls.Add(saveButton);

        // Create the cancel button
        cancelButton = new Button()
        {
            Location = new Point(690, 340), // Adjust these values to place the button in the right lower corner
            Text = "Cancel",
        };
        cancelButton.Click += OnCancel;
        Controls.Add(cancelButton);

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
    }

    private void OnRemoveFolder(object sender, EventArgs e)
    {
        // Handle remove folder clicked
        // For now, just remove the selected item from the list box
        if (foldersListBox.SelectedItem != null)
        {
            foldersListBox.Items.Remove(foldersListBox.SelectedItem);
        }
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
    }

    private void OnRemoveExcludedItem(object sender, EventArgs e)
    {
        // Handle remove excluded item clicked
        // For now, just remove the selected item from the list box
        if (excludedItemsListBox.SelectedItem != null)
        {
            excludedItemsListBox.Items.Remove(excludedItemsListBox.SelectedItem);
        }
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
    }

    private void OnCancel(object sender, EventArgs e)
    {
        // Handle cancel clicked
        // For now, just hide the form
        this.Hide();
    }

    private void OnSave(object sender, EventArgs e)
    {
        // Update the Windows Registry to launch the application on startup
        using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\RunOnce", true))
        {
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

        // Write the contents of the foldersListBox to a file
        using (StreamWriter writer = new StreamWriter("folders.txt"))
        {
            foreach (var item in foldersListBox.Items)
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

        // Write the contents of the excludedItemsListBox to a file
        using (StreamWriter writer = new StreamWriter("excludedItems.txt"))
        {
            foreach (var item in excludedItemsListBox.Items)
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

        this.Hide();
    }
}