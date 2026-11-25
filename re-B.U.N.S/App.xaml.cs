using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using Microsoft.Win32;

namespace ReBuns;

public partial class App : System.Windows.Application
{
    static readonly string Log = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "re-B.U.N.S", "crash.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        // Enforce InvariantCulture across all threads so floating-point coordinates and geometries (like WPF paths)
        // never fail on European or international Windows locales where ',' is the decimal mark.
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

        Directory.CreateDirectory(Path.GetDirectoryName(Log)!);
        DispatcherUnhandledException += (_, a) =>
        {
            var baseEx = a.Exception.GetBaseException();
            File.AppendAllText(Log, $"{DateTime.Now}: {a.Exception}\nBase Exception: {baseEx}\n\n");
            string userMsg = $"re-B.U.N.S encountered an unexpected error:\n\n{baseEx.Message}\n\nDetails saved to:\n{Log}";
            MessageBox.Show(userMsg, "re-B.U.N.S Error", MessageBoxButton.OK, MessageBoxImage.Error);
            a.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, a) =>
            File.AppendAllText(Log, $"{DateTime.Now}: {a.ExceptionObject}\n");

        if (e.Args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase)))
        {
            RunUninstall();
            Shutdown();
            return;
        }

        base.OnStartup(e);
    }

    static void RunUninstall()
    {
        if (MessageBox.Show("Uninstall re-B.U.N.S from this computer?\n\n(Your saved profiles in %AppData%\\re-B.U.N.S will be kept.)",
            "Uninstall re-B.U.N.S", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            foreach (var name in new[] { "re-B.U.N.S.lnk", "re-B.U.N.S.lnk" })
            {
                string desktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), name);
                string userDesktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), name);
                string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), name);
                if (File.Exists(desktop)) File.Delete(desktop);
                if (File.Exists(userDesktop)) File.Delete(userDesktop);
                if (File.Exists(startMenu)) File.Delete(startMenu);
            }
            Registry.LocalMachine.DeleteSubKeyTree(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\re-B.U.N.S", false);
            Registry.LocalMachine.DeleteSubKeyTree(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\re-B.U.N.S", false);
        }
        catch { }

        string exe = Environment.ProcessPath ?? "";
        string dir = Path.GetDirectoryName(exe) ?? "";
        if (!string.IsNullOrEmpty(dir) && (dir.Contains("re-B.U.N.S", StringComparison.OrdinalIgnoreCase) || dir.Contains("re-B.U.N.S", StringComparison.OrdinalIgnoreCase)))
        {
            string cmd = Path.Combine(Path.GetTempPath(), "rebuns_uninst.cmd");
            File.WriteAllText(cmd, $"@echo off\r\ntimeout /t 2 /nobreak >nul\r\nrmdir /s /q \"{dir}\"\r\n");
            Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{cmd}\"") { CreateNoWindow = true, UseShellExecute = false });
        }
        MessageBox.Show("re-B.U.N.S has been uninstalled.", "re-B.U.N.S", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
