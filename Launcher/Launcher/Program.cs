// How to build:
// dotnet publish -r win-x64 -c Release /p:PublishSingleFile=true --self-contained=false

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Launcher
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await Task.Delay(30000);  // 30sec delay

            string launcherPath = AppDomain.CurrentDomain.BaseDirectory;
            string mainAppPath = Path.Combine(launcherPath, @"..\ptecBU.exe");

            try
            {
                Process.Start(mainAppPath);
            }
            catch (Exception ex)
            {
                string logPath = Path.Combine(launcherPath, "launcher_log.txt");
                string logContent = $"Could not start the ptecBU application. Error: {ex.Message}\nMain App Path: {mainAppPath}";
                File.WriteAllText(logPath, logContent);
                Console.WriteLine(logContent);
            }
        }
    }
}
