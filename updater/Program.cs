// How to build:
// dotnet publish -r win-x64 -c Release /p:PublishSingleFile=true --self-contained=false

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

class Updater
{
    static readonly HttpClient client = new HttpClient();
    static readonly string updaterDirectory = AppContext.BaseDirectory;
    static readonly string appExe = Path.Combine(updaterDirectory, "..", "ptecBU.exe");
    static readonly string onlineVersionURL = "https://priatec.fi/ptecbu/currentversion.txt";
    static readonly string downloadURL = "https://priatec.fi/ptecbu/ptecbu-setup.exe";
    
    static async Task Main(string[] args)
    {
        Console.WriteLine("Checking for updates...");

        var localVersion = GetLocalVersion();
        var onlineVersion = await GetOnlineVersion();

        // Check if fetching online version failed
        if (onlineVersion == null || onlineVersion.Equals(new Version("0.0.0.0")))
        {
            Console.WriteLine("Failed to fetch online version. Update check aborted.");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            return; // Exit the program or handle accordingly
        }
        Console.WriteLine($"Found online version {onlineVersion}");

        if (localVersion >= onlineVersion)
        {
            Console.WriteLine("Your application is up to date.");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            return;
        }

        Console.WriteLine("An update is available. Starting update process...");
        // Try downloading the update
        var downloadSuccess = await TryDownloadUpdate();

        if (downloadSuccess)
        {
            Console.WriteLine("Closing ptecBU app if running...");
            // Close the running app only if the update was successfully downloaded
            CloseRunningApp();
            Console.WriteLine("Starting installer...");
            StartInstaller();
        }
        else
        {
            Console.WriteLine("Failed to download update. Update process aborted.");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }

    static Version GetLocalVersion()
    {
        if (File.Exists(appExe))
        {
            try
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(appExe);
                // Check if the FileVersion is null or empty
                if (string.IsNullOrEmpty(versionInfo.FileVersion))
                {
                    Console.WriteLine($"No version information found in {appExe}. Using default version.");
                    return new Version("0.0.0.0");
                }
                Console.WriteLine($"Found local version {versionInfo.FileVersion}");
                return new Version(versionInfo.FileVersion);
            }
            catch (Exception e)
            {
                // This catch block handles any unexpected errors that might occur when reading the version info
                Console.WriteLine($"Error reading version information from {appExe}: {e.Message}. Using default version.");
                return new Version("0.0.0.0");
            }
        }
        else
        {
            Console.WriteLine($"{appExe} not found, assuming version 0.0.0.0.");
            return new Version("0.0.0.0");
        }
    }

    static async Task<Version> GetOnlineVersion()
    {
        try
        {
            var versionString = await client.GetStringAsync(onlineVersionURL);
            // HttpClient.GetStringAsync should not return null, but this check can be a safety measure
            // for unexpected behavior or future changes in .NET's HttpClient implementation.
            if (versionString == null)
            {
                Console.WriteLine("Received null version string. Using default version.");
                return new Version("0.0.0.0"); // Example default version
            }
            return new Version(versionString.Trim());
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine($"Error fetching online version: {e.Message}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"An error occurred: {e.Message}");
        }
        // Return a default version if an exception occurs or if the versionString is null.
        // This ensures the method always returns a valid Version object.
        return new Version("0.0.0.0");
    }


    static void CloseRunningApp()
    {
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(appExe)))
        {
            process.Kill();
        }
    }

    static async Task<bool> TryDownloadUpdate()
    {
        try
        {
            var response = await client.GetAsync(downloadURL);
            if (response.IsSuccessStatusCode)
            {
                using var fs = new FileStream("ptecbu-setup.exe", FileMode.Create);
                await response.Content.CopyToAsync(fs);
                Console.WriteLine("Update downloaded successfully.");
                return true;
            }
            else
            {
                Console.WriteLine($"Failed to download update. HTTP error {(int)response.StatusCode} - {response.ReasonPhrase}");
                return false;
            }
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine($"Error downloading update: {e.Message}");
            return false;
        }
        catch (Exception e)
        {
            Console.WriteLine($"An error occurred while downloading update: {e.Message}");
            return false;
        }
    }


    static void StartInstaller()
    {
        Process.Start("ptecbu-setup.exe");
    }
}

