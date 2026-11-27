using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ReBuns;

public class AppSettings
{
    public string GitHubRepo { get; set; } = "FaZeBuns/re-B.U.N.S";
    public string GitHubToken { get; set; } = "";
    public bool AutoCheckUpdates { get; set; } = true;
    public bool HasCompletedInitialSetup { get; set; } = false;
    public bool HasCompletedLanguageSetup { get; set; } = false;
    public string Language { get; set; } = "en";
    public bool ShowLayoutPickerOnStartup { get; set; } = false;
    public string LastSelectedProfile { get; set; } = "";
    public ControllerType ControllerType { get; set; } = ControllerType.Xbox;

    static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "re-B.U.N.S", "settings.json");

    public static AppSettings Instance { get; private set; } = Load();

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
                if (string.Equals(s.GitHubRepo, "FaZeBuns/re-B.U.N.S", StringComparison.OrdinalIgnoreCase))
                    s.GitHubRepo = "FaZeBuns/re-B.U.N.S";
                return s;
            }
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}

public class ReleaseInfo
{
    public string TagName { get; set; } = "";
    public Version Version { get; set; } = new(1, 0, 0);
    public string Title { get; set; } = "";
    public string Notes { get; set; } = "";
    public string PublishedAt { get; set; } = "";
    public string AssetName { get; set; } = "";
    public string AssetApiUrl { get; set; } = "";
    public string AssetBrowserUrl { get; set; } = "";
    public long AssetSize { get; set; }
    public string HtmlUrl { get; set; } = "";
}

public static class Updater
{
    public static Version CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);

    public static string CurrentVersionString =>
        $"v{CurrentVersion.Major}.{CurrentVersion.Minor}.{Math.Max(0, CurrentVersion.Build)}";

    /// <summary>Resolves a GitHub token if explicitly provided, or empty string for standard anonymous public access.</summary>
    public static string ResolveGitHubToken()
    {
        if (!string.IsNullOrWhiteSpace(AppSettings.Instance.GitHubToken))
            return AppSettings.Instance.GitHubToken.Trim();

        var env = Environment.GetEnvironmentVariable("REBUNS_GITHUB_TOKEN");
        if (!string.IsNullOrWhiteSpace(env))
            return env.Trim();

        // Repository is public — never prompt or query local git credentials or gh CLI
        return "";
    }

    static HttpClient CreateClient(string accept = "application/vnd.github+json")
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("re-B.U.N.S", CurrentVersion.ToString()));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        var token = ResolveGitHubToken();
        if (!string.IsNullOrEmpty(token))
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http;
    }

    public static Version ParseVersionTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return new Version(0, 0, 0);
        var clean = tag.Trim().TrimStart('v', 'V');
        int dash = clean.IndexOf('-');
        if (dash > 0) clean = clean[..dash];
        return Version.TryParse(clean, out var v) ? new Version(v.Major, v.Minor, Math.Max(0, v.Build)) : new Version(0, 0, 0);
    }

    public static async Task<(ReleaseInfo Release, bool IsNewer, string Error)> CheckForUpdateAsync()
    {
        try
        {
            string repo = AppSettings.Instance.GitHubRepo;
            if (string.IsNullOrWhiteSpace(repo) || repo.Equals("FaZeBuns/re-B.U.N.S", StringComparison.OrdinalIgnoreCase))
                repo = "FaZeBuns/re-B.U.N.S";

            using var http = CreateClient();
            var resp = await http.GetAsync($"https://api.github.com/repos/{repo}/releases/latest");
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                return (null, false, "No published releases found on GitHub yet.");
            if (!resp.IsSuccessStatusCode)
                return (null, false, $"GitHub API returned {(int)resp.StatusCode} ({resp.ReasonPhrase}).");

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string tag = root.TryGetProperty("tag_name", out var tn) ? tn.GetString() ?? "" : "";
            var remoteVer = ParseVersionTag(tag);
            var localVer = new Version(CurrentVersion.Major, CurrentVersion.Minor, Math.Max(0, CurrentVersion.Build));

            var info = new ReleaseInfo
            {
                TagName = tag,
                Version = remoteVer,
                Title = root.TryGetProperty("name", out var nm) && !string.IsNullOrWhiteSpace(nm.GetString()) ? nm.GetString()! : tag,
                Notes = root.TryGetProperty("body", out var bd) ? bd.GetString() ?? "" : "",
                PublishedAt = root.TryGetProperty("published_at", out var pa) ? pa.GetString() ?? "" : "",
                HtmlUrl = root.TryGetProperty("html_url", out var hu) ? hu.GetString() ?? "" : ""
            };

            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var a in assets.EnumerateArray())
                {
                    string aname = a.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (!aname.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                    bool isMainExe = aname.Equals("re-BUNS.exe", StringComparison.OrdinalIgnoreCase);
                    if (string.IsNullOrEmpty(info.AssetName) || isMainExe)
                    {
                        info.AssetName = aname;
                        info.AssetApiUrl = a.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                        info.AssetBrowserUrl = a.TryGetProperty("browser_download_url", out var bu) ? bu.GetString() ?? "" : "";
                        info.AssetSize = a.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0;
                    }
                }
            }

            return (info, remoteVer > localVer, null);
        }
        catch (Exception ex)
        {
            return (null, false, ex.Message);
        }
    }

    public static async Task DownloadAndInstallUpdateAsync(ReleaseInfo rel, Action<double, string> onProgress)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "re-BUNS_Update");
        Directory.CreateDirectory(tempDir);
        string assetName = string.IsNullOrEmpty(rel.AssetName) ? "re-BUNS.exe" : rel.AssetName;
        string targetFile = Path.Combine(tempDir, assetName);

        onProgress(0, $"Connecting to GitHub ({rel.TagName})...");

        // Use AssetApiUrl with application/octet-stream so private AND public repos both work
        string downloadUrl = !string.IsNullOrEmpty(rel.AssetApiUrl) ? rel.AssetApiUrl : rel.AssetBrowserUrl;
        if (string.IsNullOrEmpty(downloadUrl))
            throw new InvalidOperationException("This release does not contain an executable asset.");

        using (var http = CreateClient("application/octet-stream"))
        {
            http.Timeout = TimeSpan.FromMinutes(10);
            using var resp = await http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();

            long total = resp.Content.Headers.ContentLength ?? rel.AssetSize;
            await using var stream = await resp.Content.ReadAsStreamAsync();
            await using var fs = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            byte[] buf = new byte[81920];
            long readTotal = 0;
            int r;
            while ((r = await stream.ReadAsync(buf)) > 0)
            {
                await fs.WriteAsync(buf.AsMemory(0, r));
                readTotal += r;
                if (total > 0)
                {
                    double pct = Math.Min(100, readTotal * 100.0 / total);
                    onProgress(pct, $"Downloading {assetName}... {pct:0}% ({readTotal / 1048576.0:0.1} / {total / 1048576.0:0.1} MB)");
                }
                else
                {
                    onProgress(50, $"Downloading {assetName}... ({readTotal / 1048576.0:0.1} MB)");
                }
            }
        }

        onProgress(100, "Installing update and restarting re-B.U.N.S...");
        await Task.Delay(300);

        string currentExe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "re-BUNS.exe");
        int pid = Environment.ProcessId;
        string script = Path.Combine(tempDir, "apply_update.cmd");
        File.WriteAllText(script,
            $"@echo off\r\n" +
            $"timeout /t 1 /nobreak >nul\r\n" +
            $":waitloop\r\n" +
            $"tasklist /FI \"PID eq {pid}\" 2>NUL | find /I \"{pid}\" >NUL\r\n" +
            $"if \"%ERRORLEVEL%\"==\"0\" (\r\n" +
            $"  timeout /t 1 /nobreak >nul\r\n" +
            $"  goto waitloop\r\n" +
            $")\r\n" +
            $"copy /Y \"{targetFile}\" \"{currentExe}\" >nul\r\n" +
            $"start \"\" \"{currentExe}\"\r\n");
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{script}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false
        });

        Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
    }
}

public static class DriverHelper
{
    public const string ViGEmDownloadUrl =
        "https://github.com/nefarius/ViGEmBus/releases/download/v1.22.0/ViGEmBus_1.22.0_x64_x86_arm64.exe";

    public static bool IsViGEmInstalled()
    {
        try
        {
            using var client = new Nefarius.ViGEm.Client.ViGEmClient();
            return true;
        }
        catch
        {
            return File.Exists(Path.Combine(Environment.SystemDirectory, "drivers", "ViGEmBus.sys"));
        }
    }

    public static async Task<(bool Ok, string Message)> InstallViGEmAsync(Action<string> status)
    {
        try
        {
            string installerPath = Path.Combine(Path.GetTempPath(), "ViGEmBus_Setup.exe");

            // 1. Extract embedded ViGEmBus_Setup.exe directly from inside re-B.U.N.S.exe!
            var asm = Assembly.GetExecutingAssembly();
            await using (var resStream = asm.GetManifestResourceStream("ViGEmBus_Setup.exe"))
            {
                if (resStream != null)
                {
                    status("Extracting embedded ViGEmBus driver package...");
                    await using var fs = new FileStream(installerPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await resStream.CopyToAsync(fs);
                }
            }

            // 2. Fallback to download only if embedded resource wasn't present
            if (!File.Exists(installerPath))
            {
                status("Downloading official ViGEmBus v1.22.0 driver...");
                using var http = new HttpClient();
                var bytes = await http.GetByteArrayAsync(ViGEmDownloadUrl);
                await File.WriteAllBytesAsync(installerPath, bytes);
            }

            status("Installing ViGEmBus Virtual Controller Driver...");
            var psi = new ProcessStartInfo(installerPath, "/qn")
            {
                UseShellExecute = true,
                Verb = "runas"
            };
            using var proc = Process.Start(psi);
            if (proc != null) await proc.WaitForExitAsync();

            if (!IsViGEmInstalled())
            {
                var psi2 = new ProcessStartInfo(installerPath) { UseShellExecute = true };
                using var proc2 = Process.Start(psi2);
                if (proc2 != null) await proc2.WaitForExitAsync();
            }

            bool ok = IsViGEmInstalled();
            return (ok, ok ? "ViGEmBus virtual controller driver is installed and ready!" : "Driver installation did not complete.");
        }
        catch (Exception ex)
        {
            return (false, "Driver install error: " + ex.Message);
        }
    }

    public static void CreateShortcut(string lnkPath, string targetExe, string workDir, string description)
    {
        try
        {
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            if (t == null) return;
            object shell = Activator.CreateInstance(t);
            object sc = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
            Type st = sc.GetType();
            st.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { targetExe });
            st.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { workDir });
            st.InvokeMember("Description", BindingFlags.SetProperty, null, sc, new object[] { description });
            st.InvokeMember("IconLocation", BindingFlags.SetProperty, null, sc, new object[] { targetExe + ",0" });
            st.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
        }
        catch { }
    }
}

public class SystemSetupWindow : Window
{
    readonly CheckBox _drvChk, _startMenuChk, _desktopChk;
    readonly TextBlock _status;
    readonly ProgressBar _bar;
    readonly Button _installBtn, _skipBtn;

    public SystemSetupWindow(bool driverMissing)
    {
        Title = "re-B.U.N.S — System & Driver Setup";
        Width = 560;
        Height = 390;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = new LinearGradientBrush(Color.FromRgb(0x72, 0x38, 0x6C), Color.FromRgb(0x29, 0x14, 0x35), 65);
        Foreground = Brushes.White;
        System.Windows.Documents.TextElement.SetForeground(this, Brushes.White);

        var card = new Border
        {
            Margin = new Thickness(16),
            Background = new SolidColorBrush(Color.FromArgb(215, 0x22, 0x10, 0x2C)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(140, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(22)
        };

        var dock = new DockPanel();
        var hdr = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        DockPanel.SetDock(hdr, Dock.Top);
        hdr.Children.Add(new TextBlock
        {
            Text = driverMissing ? "⚠   REQUIRED DRIVER DETECTED MISSING" : "✓   SYSTEM & DRIVER SETUP",
            FontSize = 12,
            FontWeight = FontWeights.ExtraBold,
            Foreground = new SolidColorBrush(driverMissing ? Color.FromRgb(0xFF, 0xC8, 0x57) : Color.FromRgb(0x3D, 0xDC, 0x97))
        });
        hdr.Children.Add(new TextBlock
        {
            Text = driverMissing
                ? "Install ViGEmBus Virtual Controller Driver"
                : "re-B.U.N.S Driver & Shortcut Setup",
            FontSize = 20,
            FontWeight = FontWeights.ExtraBold,
            Margin = new Thickness(0, 4, 0, 4)
        });
        hdr.Children.Add(new TextBlock
        {
            Text = driverMissing
                ? "re-B.U.N.S detected that the ViGEmBus Virtual Controller Driver is not installed on this PC yet. Click 'Install & Continue' to install the bundled driver automatically."
                : "ViGEmBus Virtual Controller Driver is already installed and ready on this PC. You can repair the driver or create shortcuts below.",
            FontSize = 12.5,
            Foreground = new SolidColorBrush(Color.FromRgb(0xD6, 0xC4, 0xDE)),
            TextWrapping = TextWrapping.Wrap
        });
        dock.Children.Add(hdr);

        var footer = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        DockPanel.SetDock(footer, Dock.Bottom);
        _status = new TextBlock
        {
            Text = "All required setup packages are bundled inside re-BUNS.exe (works offline).",
            FontSize = 11.5,
            Foreground = new SolidColorBrush(Color.FromRgb(0xD6, 0xC4, 0xDE)),
            Margin = new Thickness(0, 0, 0, 6)
        };
        footer.Children.Add(_status);

        _bar = new ProgressBar
        {
            Height = 8, Minimum = 0, Maximum = 100, Value = 0,
            Visibility = Visibility.Collapsed,
            Foreground = new SolidColorBrush(Color.FromRgb(0x3D, 0xDC, 0x97)),
            Background = new SolidColorBrush(Color.FromRgb(0x2B, 0x14, 0x38)),
            Margin = new Thickness(0, 0, 0, 10)
        };
        footer.Children.Add(_bar);

        var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        _skipBtn = new Button
        {
            Content = driverMissing ? "Skip for Now" : "Close",
            Padding = new Thickness(16, 8, 16, 8),
            Margin = new Thickness(0, 0, 8, 0)
        };
        _skipBtn.Click += (_, _) => Close();

        _installBtn = new Button
        {
            Content = driverMissing ? "⚡  Install Driver & Continue" : "⚡  Apply Setup",
            Padding = new Thickness(20, 8, 20, 8),
            FontWeight = FontWeights.ExtraBold,
            Background = Brushes.White,
            Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x0C, 0x24)),
            BorderBrush = Brushes.White
        };
        _installBtn.Click += InstallBtn_Click;
        btns.Children.Add(_skipBtn);
        btns.Children.Add(_installBtn);
        footer.Children.Add(btns);
        dock.Children.Add(footer);

        var body = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        _drvChk = new CheckBox
        {
            Content = driverMissing
                ? "Install ViGEmBus Virtual Controller Driver (Required)"
                : "Reinstall / Repair ViGEmBus Virtual Controller Driver (Already installed ✓)",
            IsChecked = driverMissing,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold
        };
        _startMenuChk = new CheckBox
        {
            Content = "Create Start Menu Shortcut for re-B.U.N.S",
            IsChecked = true,
            FontSize = 13
        };
        _desktopChk = new CheckBox
        {
            Content = "Create Desktop Shortcut for re-B.U.N.S",
            IsChecked = false,
            FontSize = 13
        };
        body.Children.Add(_drvChk);
        body.Children.Add(_startMenuChk);
        body.Children.Add(_desktopChk);
        dock.Children.Add(body);

        card.Child = dock;
        Content = card;
    }

    async void InstallBtn_Click(object sender, RoutedEventArgs e)
    {
        _installBtn.IsEnabled = false;
        _skipBtn.IsEnabled = false;
        _bar.Visibility = Visibility.Visible;
        _bar.Value = 25;

        string exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "re-BUNS.exe");
        string dir = Path.GetDirectoryName(exe)!;

        if (_drvChk.IsChecked == true)
        {
            var (ok, msg) = await DriverHelper.InstallViGEmAsync(s => Dispatcher.Invoke(() => _status.Text = s));
            _bar.Value = 75;
            if (!ok)
            {
                MessageBox.Show(msg, "re-B.U.N.S Driver Setup", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        if (_startMenuChk.IsChecked == true)
        {
            string sm = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "re-B.U.N.S.lnk");
            DriverHelper.CreateShortcut(sm, exe, dir, "re-B.U.N.S — GTA 6 Keyboard & Mouse to Xbox Controller");
        }
        if (_desktopChk.IsChecked == true)
        {
            string desk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "re-B.U.N.S.lnk");
            DriverHelper.CreateShortcut(desk, exe, dir, "re-B.U.N.S — GTA 6 Keyboard & Mouse to Xbox Controller");
        }

        _bar.Value = 100;
        _status.Text = "Setup complete!";
        await Task.Delay(250);
        DialogResult = true;
        Close();
    }
}

public class UpdateWindow : Window
{
    readonly ReleaseInfo _rel;
    readonly ProgressBar _bar;
    readonly TextBlock _status;
    readonly Button _installBtn;
    readonly Button _laterBtn;

    public UpdateWindow(ReleaseInfo rel)
    {
        _rel = rel;
        Title = $"re-B.U.N.S Update Available — {rel.TagName}";
        Width = 540;
        Height = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(0x22, 0x10, 0x2C));
        Foreground = Brushes.White;
        System.Windows.Documents.TextElement.SetForeground(this, Brushes.White);

        var root = new Border
        {
            Margin = new Thickness(16),
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x21, 0x28)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0xDC, 0x97)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(20)
        };

        var dock = new DockPanel();

        // Header
        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        DockPanel.SetDock(header, Dock.Top);
        header.Children.Add(new TextBlock
        {
            Text = "NEW GITHUB UPDATE AVAILABLE",
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x3D, 0xDC, 0x97))
        });
        header.Children.Add(new TextBlock
        {
            Text = $"{rel.Title}  ({Updater.CurrentVersionString} → {rel.TagName})",
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 4, 0, 2)
        });
        header.Children.Add(new TextBlock
        {
            Text = $"Repository: {AppSettings.Instance.GitHubRepo}   ·   Asset: {(string.IsNullOrEmpty(rel.AssetName) ? "Source release" : rel.AssetName)}",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0x8B, 0x91, 0xA0))
        });
        dock.Children.Add(header);

        // Footer buttons & progress
        var footer = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        DockPanel.SetDock(footer, Dock.Bottom);

        _status = new TextBlock
        {
            Text = "Click 'Install Update & Restart' to automatically download and apply this update.",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0x8B, 0x91, 0xA0)),
            Margin = new Thickness(0, 0, 0, 6)
        };
        footer.Children.Add(_status);

        _bar = new ProgressBar
        {
            Height = 8,
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Visibility = Visibility.Collapsed,
            Foreground = new SolidColorBrush(Color.FromRgb(0x3D, 0xDC, 0x97)),
            Background = new SolidColorBrush(Color.FromRgb(0x26, 0x2A, 0x33)),
            Margin = new Thickness(0, 0, 0, 12)
        };
        footer.Children.Add(_bar);

        var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        _laterBtn = new Button
        {
            Content = "Remind Me Later",
            Padding = new Thickness(14, 8, 14, 8),
            Margin = new Thickness(0, 0, 8, 0)
        };
        _laterBtn.Click += (_, _) => Close();

        _installBtn = new Button
        {
            Content = "⬇  Install Update & Restart",
            Padding = new Thickness(18, 8, 18, 8),
            FontWeight = FontWeights.Bold,
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x8A, 0x5A)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0xDC, 0x97))
        };
        _installBtn.Click += InstallBtn_Click;

        btns.Children.Add(_laterBtn);
        btns.Children.Add(_installBtn);
        footer.Children.Add(btns);
        dock.Children.Add(footer);

        // Release notes body
        var notesBox = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x15, 0x17, 0x1C)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12)
        };
        var sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        sv.Content = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(rel.Notes) ? "No release notes provided." : rel.Notes,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            LineHeight = 20
        };
        notesBox.Child = sv;
        dock.Children.Add(notesBox);

        root.Child = dock;
        Content = root;
    }

    async void InstallBtn_Click(object sender, RoutedEventArgs e)
    {
        _installBtn.IsEnabled = false;
        _laterBtn.IsEnabled = false;
        _bar.Visibility = Visibility.Visible;
        try
        {
            await Updater.DownloadAndInstallUpdateAsync(_rel, (pct, msg) =>
            {
                Dispatcher.Invoke(() =>
                {
                    _bar.Value = pct;
                    _status.Text = msg;
                });
            });
        }
        catch (Exception ex)
        {
            _installBtn.IsEnabled = true;
            _laterBtn.IsEnabled = true;
            _status.Text = "Update failed: " + ex.Message;
            MessageBox.Show("Could not install update:\n" + ex.Message, "re-B.U.N.S Update", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
