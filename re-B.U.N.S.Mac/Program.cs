using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.Themes.Fluent;
using Path = System.IO.Path;

namespace ReBuns;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Any(a => a.Equals("--self-test", StringComparison.OrdinalIgnoreCase)))
            return RunSelfTest();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<MacApp>()
            .UsePlatformDetect()
            .LogToTrace();

    static int RunSelfTest()
    {
        Console.WriteLine("=== re-B.U.N.S macOS Native Self-Test ===");
        Console.WriteLine($"OS: {RuntimeInformationText()}");

        // 1. Test all 4 GTA 6 presets & 18 Tap/Hold callout specs
        var profiles = Profile.LoadAll();
        Console.WriteLine($"Loaded {profiles.Count} presets: {string.Join(", ", profiles.Select(p => p.Name))}");
        if (profiles.Count < 4) throw new Exception("Expected all 4 GTA 6 presets.");
        foreach (var info in Profile.GtaLayouts)
        {
            if (info.Callouts.Length != 18)
                throw new Exception($"Preset {info.SchemeName} expected 18 callouts, got {info.Callouts.Length}");
        }

        // 2. Test macOS CoreGraphics native library symbols if on macOS
        if (OperatingSystem.IsMacOS())
        {
            bool trusted = MacEngine.IsAccessibilityTrusted();
            Console.WriteLine($"macOS CoreGraphics AXIsProcessTrusted() call succeeded (trusted={trusted})");
        }

        // 3. Test MacEngine 1000 Hz Virtual Xbox 360 Controller mapping + simulated WASD + Mouse Look + RT shoot
        using var engine = new MacEngine();
        var rebuns = Profile.CreateGta6Preset(GtaLayoutId.ReBuns);
        engine.SetProfile(rebuns);
        engine.Start();

        // Simulate W (Move Forward), Mouse1 (RT Shoot), and Mouse Look (+30, -15)
        engine.SimulateInputForTest(InputNames.ToCode("W"), true, mouseDx: 30, mouseDy: -15);
        engine.SimulateInputForTest(InputNames.ToCode("Mouse1"), true);
        engine.StepTick(4.0);

        var snap = engine.Snapshot;
        string json = snap.ToW3CGamepadJson();
        Console.WriteLine($"Simulated Tick Output -> LY={snap.LY}, RT={snap.RT}, RX={snap.RX}, RY={snap.RY}");
        Console.WriteLine($"W3C Gamepad JSON: {json}");
        if (snap.LY <= 0 || snap.RT == 0 || snap.RX == 0)
            throw new Exception("Virtual pad simulation did not produce expected stick/trigger deflection.");

        // 4. Verify Browser Virtual Gamepad Extension files
        MacEngine.EnsureBrowserExtensionInstalled();
        if (!File.Exists(Path.Combine(MacEngine.BrowserExtensionDir, "manifest.json")))
            throw new Exception("Browser virtual gamepad extension manifest was not created.");

        // 5. Test Local HTTP Virtual Gamepad Bridge & HTML Emulator
        using var client = new HttpClient();
        string padResp = client.GetStringAsync("http://127.0.0.1:27654/pad").GetAwaiter().GetResult();
        if (!padResp.Contains("\"axes\"") || !padResp.Contains("\"buttons\""))
            throw new Exception("Local Gamepad HTTP Bridge returned invalid JSON: " + padResp);
        Console.WriteLine("Local Gamepad HTTP Bridge /pad returned valid W3C Gamepad JSON ✓");

        string emuHtml = client.GetStringAsync("http://127.0.0.1:27654/").GetAwaiter().GetResult();
        if (!emuHtml.Contains("Virtual Xbox 360 Controller Emulator"))
            throw new Exception("Local HTTP server did not return valid Gamepad Emulator HTML.");
        Console.WriteLine("Local Gamepad HTML Emulator served successfully at http://127.0.0.1:27654/ ✓");

        engine.Stop();
        Console.WriteLine("=== ALL re-B.U.N.S macOS SELF-TESTS PASSED ✓ ===");
        return 0;
    }

    static string RuntimeInformationText() =>
        $"{System.Runtime.InteropServices.RuntimeInformation.OSDescription} ({System.Runtime.InteropServices.RuntimeInformation.OSArchitecture})";
}

public class MacAppSettings
{
    public string GitHubRepo { get; set; } = "FaZeBuns/re-B.U.N.S";
    public bool AutoCheckUpdates { get; set; } = true;
    public bool HasCompletedInitialSetup { get; set; }
    public string LastSelectedProfile { get; set; } = "";

    static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "re-B.U.N.S", "settings.json");

    public static MacAppSettings Instance { get; } = Load();

    static MacAppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<MacAppSettings>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { }
        return new();
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

public class MacApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MacMainWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }
}

public class MacMainWindow : Window
{
    readonly MacEngine _engine = new();
    readonly List<Profile> _profiles;
    Profile _cur;
    PadTarget _sel = PadTarget.DUp;
    SchematicHoldView _holdView = SchematicHoldView.TapAndHold;
    ControllerType _controllerType = ControllerType.Xbox;

    Button _xboxToggleBtn;
    Button _psToggleBtn;

    readonly TextBlock _schemeNameText = new() { FontSize = 17, FontWeight = FontWeight.ExtraBold, Foreground = new SolidColorBrush(Color.Parse("#1A0C24")), VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _schemeIdxText = new() { FontSize = 15, FontWeight = FontWeight.ExtraBold, Foreground = new SolidColorBrush(Color.Parse("#1A0C24")), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
    readonly TextBlock _statusText = new() { Text = "STOPPED", FontWeight = FontWeight.Bold, FontSize = 12, Foreground = Brushes.White };
    readonly Border _statusPill = new() { CornerRadius = new CornerRadius(4), Padding = new Thickness(12, 6), Background = new SolidColorBrush(Color.Parse("#3B1D47")), Margin = new Thickness(0, 0, 10, 0) };
    readonly Button _startBtn = new() { Content = "▶  START VIRTUAL PAD", FontWeight = FontWeight.ExtraBold, Padding = new Thickness(16, 7), Background = new SolidColorBrush(Color.Parse("#1E8A5A")), Foreground = Brushes.White };
    readonly TextBlock _selName = new() { Text = "Click any action or button →", FontSize = 15, FontWeight = FontWeight.ExtraBold, Foreground = Brushes.White };
    readonly TextBlock _selSub = new() { Text = "Click any callout row to customize its macOS key or Tap/Hold/Double-Tap mode.", FontSize = 11.5, Foreground = new SolidColorBrush(Color.Parse("#E0D0E8")), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 8) };
    readonly StackPanel _selKeys = new() { Spacing = 4 };
    readonly Button _addKeyBtn = new() { Content = "＋ BIND KEY / MOUSE", FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Stretch };
    readonly TextBlock _liveText = new() { FontSize = 12, Foreground = Brushes.White };
    readonly Border _launchOverlay;
    readonly TextBlock _splashStatus;
    readonly Border _splashBar;

    record CalloutUI(int Slot, bool IsLeft, Border Row, TextBlock ActionTxt, TextBlock KeyTxt, TextBlock GlyphTxt);
    readonly List<CalloutUI> _callouts = new();

    public MacMainWindow()
    {
        Title = "re-B.U.N.S (macOS) — GTA 6 Control Scheme & Keyboard/Mouse Remapper";
        Width = 1420;
        Height = 860;
        MinWidth = 1100;
        MinHeight = 700;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.Parse("#72386C"), 0.0),
                new GradientStop(Color.Parse("#532856"), 0.52),
                new GradientStop(Color.Parse("#291435"), 1.0)
            }
        };

        var logoBmp = LoadLogoBitmap();
        if (logoBmp != null) Icon = new WindowIcon(logoBmp);

        _profiles = Profile.LoadAll();
        int idx = _profiles.FindIndex(p => p.Name == MacAppSettings.Instance.LastSelectedProfile);
        if (idx < 0) idx = _profiles.FindIndex(p => p.LayoutId == GtaLayoutId.ReBuns);
        _cur = _profiles[Math.Max(0, idx)];
        _controllerType = _cur.ControllerType;
        _engine.SetProfile(_cur);

        var rootGrid = new Grid();

        // Main UI
        var mainDock = new DockPanel();

        // Header
        var hdr = new DockPanel { Margin = new Thickness(28, 16, 28, 8) };
        DockPanel.SetDock(hdr, Dock.Top);

        var rightHdr = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(rightHdr, Dock.Right);

        // Controller Layout Toggle (Xbox / PlayStation)
        _xboxToggleBtn = MakeGtaButton("🎮 Xbox", _controllerType == ControllerType.Xbox);
        _psToggleBtn = MakeGtaButton("🎮 PlayStation", _controllerType == ControllerType.PlayStation);
        _xboxToggleBtn.Click += (_, _) => SetMacControllerType(ControllerType.Xbox);
        _psToggleBtn.Click += (_, _) => SetMacControllerType(ControllerType.PlayStation);
        var padToggleGroup = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
        padToggleGroup.Children.Add(_xboxToggleBtn);
        padToggleGroup.Children.Add(_psToggleBtn);
        rightHdr.Children.Add(padToggleGroup);

        var cloudBtn = MakeGtaButton("🌐 Launch Cloud", false);
        cloudBtn.Click += (_, _) => MacEngine.LaunchCloudGamingBrowser();
        rightHdr.Children.Add(cloudBtn);

        var emuBtn = MakeGtaButton("🎮 Open Controller Emulator", false);
        emuBtn.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo("http://127.0.0.1:27654/") { UseShellExecute = true }); } catch { }
        };
        rightHdr.Children.Add(emuBtn);

        var accessBtn = MakeGtaButton(MacEngine.IsAccessibilityTrusted() ? "✓ macOS Ready" : "⚠ Grant Accessibility", false);
        accessBtn.Click += (_, _) => ShowMacSetupDialog();
        rightHdr.Children.Add(accessBtn);

        _statusPill.Child = _statusText;
        rightHdr.Children.Add(_statusPill);

        _startBtn.Click += StartBtn_Click;
        rightHdr.Children.Add(_startBtn);
        hdr.Children.Add(rightHdr);

        var leftHdr = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center };
        if (logoBmp != null)
            leftHdr.Children.Add(new Image { Source = logoBmp, Width = 58, Height = 42 });
        leftHdr.Children.Add(new TextBlock { Text = "SETTINGS", FontSize = 26, FontWeight = FontWeight.ExtraBold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });
        leftHdr.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.Parse("#3D1F4C")),
            BorderBrush = new SolidColorBrush(Color.Parse("#754A84")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = "v1.6.4 · macOS", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(Color.Parse("#E0D0E8")) }
        });
        leftHdr.Children.Add(new TextBlock { Text = "re-B.U.N.S · GTA 6 KEYBOARD & MOUSE", FontSize = 12, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(Color.Parse("#E0D0E8")), VerticalAlignment = VerticalAlignment.Center });
        hdr.Children.Add(leftHdr);
        mainDock.Children.Add(hdr);

        // Footer
        var footer = new DockPanel { Margin = new Thickness(28, 8, 28, 14) };
        DockPanel.SetDock(footer, Dock.Bottom);
        var rightFoot = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        DockPanel.SetDock(rightFoot, Dock.Right);
        var cycleHoldBtn = MakeGtaButton("⟳ CYCLE TAP / HOLD VIEW", false);
        cycleHoldBtn.Click += (_, _) => { _holdView = (SchematicHoldView)(((int)_holdView + 1) % 3); RefreshSchematic(); };
        var resetBtn = MakeGtaButton("ⓧ RESTORE DEFAULT", false);
        resetBtn.Click += (_, _) => ResetCurrentScheme();
        rightFoot.Children.Add(cycleHoldBtn);
        rightFoot.Children.Add(resetBtn);
        footer.Children.Add(rightFoot);
        footer.Children.Add(new TextBlock
        {
            Text = "[⌘ COMMAND KEY] = Pause / Play Virtual Pad   ·   [CONTROL + OPTION + DELETE] = Stop Virtual Pad",
            Foreground = new SolidColorBrush(Color.Parse("#E0D0E8")),
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        mainDock.Children.Add(footer);

        // Center Content Grid (Left Control Scheme Selector + Right GTA 6 Controller Schematic)
        var contentGrid = new Grid
        {
            Margin = new Thickness(28, 8, 28, 8),
            ColumnDefinitions = new ColumnDefinitions("320,2,*")
        };

        // Left Column
        var leftCol = new DockPanel { Margin = new Thickness(0, 0, 20, 0) };
        var leftTop = new StackPanel { Spacing = 8 };
        DockPanel.SetDock(leftTop, Dock.Top);
        leftTop.Children.Add(new TextBlock { Text = "CONTROL SCHEME", FontSize = 18, FontWeight = FontWeight.ExtraBold, Foreground = Brushes.White });

        // White gradient selector box ("★ re-B.U.N.S   1 / 4   < >")
        var whiteBox = new Border
        {
            Height = 46,
            CornerRadius = new CornerRadius(3),
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Colors.White, 0.0),
                    new GradientStop(Color.Parse("#F3EDF6"), 0.58),
                    new GradientStop(Color.Parse("#9B7CA6"), 1.0)
                }
            },
            Padding = new Thickness(14, 0, 10, 0)
        };
        var wbDock = new DockPanel { VerticalAlignment = VerticalAlignment.Center };
        var wbRight = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(wbRight, Dock.Right);
        wbRight.Children.Add(_schemeIdxText);
        var prevBtn = MakeGtaButton("❮", false); prevBtn.Padding = new Thickness(8, 3); prevBtn.Click += (_, _) => CycleScheme(-1);
        var nextBtn = MakeGtaButton("❯", false); nextBtn.Padding = new Thickness(8, 3); nextBtn.Click += (_, _) => CycleScheme(+1);
        wbRight.Children.Add(prevBtn);
        wbRight.Children.Add(nextBtn);
        wbDock.Children.Add(wbRight);
        wbDock.Children.Add(_schemeNameText);
        whiteBox.Child = wbDock;
        leftTop.Children.Add(whiteBox);

        // Mouse sensitivity sliders card
        var mouseCard = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#261230")),
            BorderBrush = new SolidColorBrush(Color.Parse("#724880")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 8, 0, 0)
        };
        var mouseStack = new StackPanel { Spacing = 4 };
        mouseStack.Children.Add(new TextBlock { Text = "MOUSE → ANALOG LOOK STICK", FontSize = 11, FontWeight = FontWeight.ExtraBold, Foreground = new SolidColorBrush(Color.Parse("#F687B3")) });
        var sensLbl = new TextBlock { Text = $"Sensitivity: {_cur.Mouse.SensitivityX:0.#}   ·   Anti-Deadzone: {_cur.Mouse.AntiDeadzone:0.#}%", FontSize = 12, Foreground = Brushes.White };
        var sensSlider = new Slider { Minimum = 1, Maximum = 60, Value = _cur.Mouse.SensitivityX };
        sensSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property == RangeBase.ValueProperty)
            {
                _cur.Mouse.SensitivityX = _cur.Mouse.SensitivityY = Math.Round(sensSlider.Value, 1);
                sensLbl.Text = $"Sensitivity: {_cur.Mouse.SensitivityX:0.#}   ·   Anti-Deadzone: {_cur.Mouse.AntiDeadzone:0.#}%";
                _cur.Save();
            }
        };
        mouseStack.Children.Add(sensLbl);
        mouseStack.Children.Add(sensSlider);
        mouseStack.Children.Add(_liveText);
        mouseCard.Child = mouseStack;
        leftTop.Children.Add(mouseCard);
        leftCol.Children.Add(leftTop);

        // Bottom-left selected action inspector
        var inspCard = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#261230")),
            BorderBrush = new SolidColorBrush(Color.Parse("#724880")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 10, 0, 0)
        };
        DockPanel.SetDock(inspCard, Dock.Bottom);
        var inspStack = new StackPanel { Spacing = 4 };
        inspStack.Children.Add(new TextBlock { Text = "SELECTED CONTROLLER ACTION", FontSize = 10.5, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(Color.Parse("#F687B3")) });
        inspStack.Children.Add(_selName);
        inspStack.Children.Add(_selSub);
        inspStack.Children.Add(new ScrollViewer { MaxHeight = 140, Content = _selKeys });
        _addKeyBtn.Click += (_, _) =>
        {
            _addKeyBtn.Content = "PRESS ANY KEY OR MOUSE…";
            var target = _sel;
            _engine.Capture = name => Dispatcher.UIThread.Post(() =>
            {
                _cur.Bindings.Add(new Binding { Input = name, Target = target });
                _cur.Save();
                _engine.SetProfile(_cur);
                _addKeyBtn.Content = "＋ BIND KEY / MOUSE";
                RefreshSchematic();
            });
        };
        inspStack.Children.Add(_addKeyBtn);
        inspCard.Child = inspStack;
        leftCol.Children.Add(inspCard);

        Grid.SetColumn(leftCol, 0);
        contentGrid.Children.Add(leftCol);

        var div = new Border { Background = new SolidColorBrush(Color.Parse("#623870")), Width = 1.5 };
        Grid.SetColumn(div, 1);
        contentGrid.Children.Add(div);

        // Right Area: 18 Callout Rows + Center White Xbox Controller
        var rightGrid = new Grid
        {
            Margin = new Thickness(16, 0, 0, 0),
            ColumnDefinitions = new ColumnDefinitions("1.25*,220,1.25*"),
            RowDefinitions = new RowDefinitions("Auto,*")
        };
        var onFootHdr = new TextBlock
        {
            Text = "ON FOOT",
            FontSize = 22,
            FontWeight = FontWeight.ExtraBold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 12)
        };
        Grid.SetColumnSpan(onFootHdr, 3);
        rightGrid.Children.Add(onFootHdr);

        var leftCalloutsStack = new StackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        var rightCalloutsStack = new StackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        for (int i = 0; i < 9; i++)
        {
            leftCalloutsStack.Children.Add(CreateCalloutRow(i, true));
            rightCalloutsStack.Children.Add(CreateCalloutRow(i, false));
        }
        Grid.SetRow(leftCalloutsStack, 1); Grid.SetColumn(leftCalloutsStack, 0);
        Grid.SetRow(rightCalloutsStack, 1); Grid.SetColumn(rightCalloutsStack, 2);
        rightGrid.Children.Add(leftCalloutsStack);
        rightGrid.Children.Add(rightCalloutsStack);

        // Center Controller Display
        var centerStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 10 };
        if (logoBmp != null)
            centerStack.Children.Add(new Image { Source = logoBmp, Width = 220, Height = 220 });
        Grid.SetRow(centerStack, 1); Grid.SetColumn(centerStack, 1);
        rightGrid.Children.Add(centerStack);

        Grid.SetColumn(rightGrid, 2);
        contentGrid.Children.Add(rightGrid);
        mainDock.Children.Add(contentGrid);
        rootGrid.Children.Add(mainDock);

        // Launch Splash Overlay
        _splashStatus = new TextBlock { Text = "INITIALIZING re-B.U.N.S macOS ENGINE...", FontSize = 12, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(Color.Parse("#E0D0E8")), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 0) };
        _splashBar = new Border { Width = 30, Height = 6, HorizontalAlignment = HorizontalAlignment.Left, CornerRadius = new CornerRadius(3), Background = new SolidColorBrush(Color.Parse("#3DDC97")) };
        var splashStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        if (logoBmp != null)
            splashStack.Children.Add(new Image { Source = logoBmp, Width = 240, Height = 240, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 12) });
        splashStack.Children.Add(new TextBlock { Text = "re-B.U.N.S", FontSize = 36, FontWeight = FontWeight.ExtraBold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center });
        splashStack.Children.Add(new TextBlock { Text = "GTA 6 KEYBOARD & MOUSE CONTROLLER ENGINE · macOS", FontSize = 13, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(Color.Parse("#F687B3")), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 20) });
        splashStack.Children.Add(new Border { Width = 380, Height = 8, Background = new SolidColorBrush(Color.Parse("#2B1438")), CornerRadius = new CornerRadius(4), Padding = new Thickness(1), Child = _splashBar });
        splashStack.Children.Add(_splashStatus);

        _launchOverlay = new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#471E4A"), 0), new GradientStop(Color.Parse("#14081C"), 1) }
            },
            Child = splashStack
        };
        rootGrid.Children.Add(_launchOverlay);

        Content = rootGrid;

        _engine.StateChanged += () => Dispatcher.UIThread.Post(UpdateStatus);
        RefreshSchematic();
        UpdateStatus();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        timer.Tick += (_, _) =>
        {
            var s = _engine.Snapshot;
            _liveText.Text = $"LS: {s.LX},{s.LY}   RS: {s.RX},{s.RY}   LT:{s.LT} RT:{s.RT}   [{s.FormatButtons()}]";
        };
        timer.Start();

        Opened += async (_, _) =>
        {
            await RunMacIntroAsync();
            var cmdArgs = Environment.GetCommandLineArgs();
            int pIdx = Array.FindIndex(cmdArgs, a => a.Equals("--render-preview", StringComparison.OrdinalIgnoreCase));
            if (pIdx >= 0 && pIdx + 1 < cmdArgs.Length)
            {
                await Task.Delay(350);
                try
                {
                    var rtb = new RenderTargetBitmap(new PixelSize((int)Bounds.Width, (int)Bounds.Height), new Vector(96, 96));
                    rtb.Render(this);
                    rtb.Save(cmdArgs[pIdx + 1]);
                }
                catch { }
                Close();
            }
        };
    }

    static Bitmap LoadLogoBitmap()
    {
        try
        {
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Logo.png");
            if (s != null) return new Bitmap(s);
        }
        catch { }
        return null;
    }

    static void PlayMacIntroSound()
    {
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "re-B.U.N.S");
            Directory.CreateDirectory(dir);
            string mp3 = Path.Combine(dir, "intro.mp3");
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("intro.mp3"))
            {
                if (s != null)
                {
                    using var fs = new FileStream(mp3, FileMode.Create, FileAccess.Write, FileShare.Read);
                    s.CopyTo(fs);
                }
            }
            if (OperatingSystem.IsMacOS() && File.Exists(mp3))
            {
                Process.Start(new ProcessStartInfo("afplay", $"\"{mp3}\"") { UseShellExecute = false, CreateNoWindow = true });
            }
        }
        catch { }
    }

    async Task RunMacIntroAsync()
    {
        PlayMacIntroSound();
        for (int i = 1; i <= 20; i++)
        {
            _splashBar.Width = 18 * i;
            if (i == 7) _splashStatus.Text = "LOADING re-B.U.N.S GTA 6 CONTROL SCHEMES...";
            if (i == 14) _splashStatus.Text = $"READY  ·  ACTIVE PRESET: {_cur.Name.ToUpper()}";
            await Task.Delay(170);
        }
        _launchOverlay.IsVisible = false;
    }

    static Button MakeGtaButton(string text, bool whiteActive) => new()
    {
        Content = text,
        FontWeight = FontWeight.Bold,
        FontSize = 12,
        Padding = new Thickness(12, 6),
        Background = whiteActive ? Brushes.White : new SolidColorBrush(Color.Parse("#3D1F4C")),
        Foreground = whiteActive ? new SolidColorBrush(Color.Parse("#1A0C24")) : Brushes.White,
        BorderBrush = whiteActive ? Brushes.White : new SolidColorBrush(Color.Parse("#7A4E8A")),
        BorderThickness = new Thickness(1)
    };

    Border CreateCalloutRow(int slot, bool isLeft)
    {
        var actionTxt = new TextBlock { FontSize = 12.0, FontWeight = FontWeight.ExtraBold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
        var keyTxt = new TextBlock { FontSize = 11, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(Color.Parse("#3DDC97")), VerticalAlignment = VerticalAlignment.Center };
        var keyPill = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#22102C")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3DDC97")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2),
            Margin = new Thickness(6, 0),
            Child = keyTxt
        };
        var glyphTxt = new TextBlock { FontSize = 10.5, FontWeight = FontWeight.ExtraBold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var glyphBadge = new Border
        {
            Width = 30, Height = 30,
            CornerRadius = new CornerRadius(15),
            Background = new SolidColorBrush(Color.Parse("#1E1626")),
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(1.5),
            Child = glyphTxt
        };

        var sp = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = isLeft ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (isLeft) { sp.Children.Add(keyPill); sp.Children.Add(actionTxt); sp.Children.Add(glyphBadge); }
        else { sp.Children.Add(glyphBadge); sp.Children.Add(actionTxt); sp.Children.Add(keyPill); }

        var row = new Border
        {
            Height = 38,
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(8, 2),
            Background = Brushes.Transparent,
            Child = sp
        };
        var ui = new CalloutUI(slot, isLeft, row, actionTxt, keyTxt, glyphTxt);
        row.PointerPressed += (_, _) =>
        {
            var spec = GetSpec(ui);
            if (spec != null) { _sel = spec.Target; RefreshSchematic(); }
        };
        _callouts.Add(ui);
        return row;
    }

    CalloutSpec GetSpec(CalloutUI ui)
    {
        var curId = _cur?.LayoutId ?? GtaLayoutId.ReBuns;
        var info = Profile.GtaLayouts.FirstOrDefault(x => x.Id == curId) ?? Profile.GtaLayouts[0];
        var side = info.Callouts.Where(c => c.IsLeft == ui.IsLeft).ToArray();
        return ui.Slot < side.Length ? side[ui.Slot] : null;
    }

    void CycleScheme(int delta)
    {
        int total = Profile.GtaLayouts.Length;
        int cur = _cur?.LayoutId is not null and not GtaLayoutId.Custom ? (int)_cur.LayoutId : 1;
        int next = ((cur - 1 + delta + total) % total) + 1;
        var id = (GtaLayoutId)next;
        var info = Profile.GtaLayouts.First(x => x.Id == id);
        var existing = _profiles.FirstOrDefault(p => p.LayoutId == id);
        if (existing == null) { existing = Profile.CreateGta6Preset(id); existing.Save(); _profiles.Add(existing); }
        _cur = existing;
        MacAppSettings.Instance.LastSelectedProfile = _cur.Name;
        MacAppSettings.Instance.Save();
        _engine.SetProfile(_cur);
        RefreshSchematic();
    }

    void ResetCurrentScheme()
    {
        var id = _cur?.LayoutId is not null and not GtaLayoutId.Custom ? _cur.LayoutId : GtaLayoutId.ReBuns;
        int idx = _profiles.IndexOf(_cur);
        _cur = Profile.CreateGta6Preset(id);
        _cur.Save();
        if (idx >= 0) _profiles[idx] = _cur;
        _engine.SetProfile(_cur);
        RefreshSchematic();
    }

    void SetMacControllerType(ControllerType type)
    {
        _controllerType = type;
        if (_cur != null)
        {
            _cur.ControllerType = type;
            _cur.Save();
        }
        if (_xboxToggleBtn != null && _psToggleBtn != null)
        {
            bool isXbox = type == ControllerType.Xbox;
            _xboxToggleBtn.Background = isXbox ? Brushes.White : new SolidColorBrush(Color.Parse("#3D1F4C"));
            _xboxToggleBtn.Foreground = isXbox ? new SolidColorBrush(Color.Parse("#1A0C24")) : Brushes.White;
            _psToggleBtn.Background = !isXbox ? Brushes.White : new SolidColorBrush(Color.Parse("#3D1F4C"));
            _psToggleBtn.Foreground = !isXbox ? new SolidColorBrush(Color.Parse("#1A0C24")) : Brushes.White;
        }
        RefreshSchematic();
    }

    void RefreshSchematic()
    {
        var curId = _cur?.LayoutId ?? GtaLayoutId.ReBuns;
        var info = Profile.GtaLayouts.FirstOrDefault(x => x.Id == curId) ?? Profile.GtaLayouts[0];
        _schemeNameText.Text = info.SchemeName;
        _schemeIdxText.Text = $"{info.Index} / {Profile.GtaLayouts.Length}";

        foreach (var ui in _callouts)
        {
            var spec = GetSpec(ui);
            if (spec == null) continue;
            ui.GlyphTxt.Text = ControllerNames.Glyph(spec.Target, _controllerType);
            string actionText = spec.HoldLabel == null
                ? spec.TapLabel
                : _holdView switch
                {
                    SchematicHoldView.TapOnly => spec.TapLabel,
                    SchematicHoldView.HoldOnly => $"◎ {spec.HoldLabel}",
                    _ => $"{spec.TapLabel} / ◎ {spec.HoldLabel}"
                };
            ui.ActionTxt.Text = ControllerNames.FormatLabel(actionText, _controllerType);

            var keys = _cur.Bindings.Where(b => b.Target == spec.Target).Select(b => InputNames.Pretty(b.Input)).ToList();
            if (spec.Target == PadTarget.LStickUp && _cur.LayoutId != GtaLayoutId.LeftHanded) ui.KeyTxt.Text = "W A S D";
            else if (spec.Target == PadTarget.RStickUp && _cur.Mouse.Stick == MouseStick.Right) ui.KeyTxt.Text = "Mouse";
            else ui.KeyTxt.Text = keys.Count == 0 ? "—" : string.Join(" / ", keys.Take(3));

            bool isSel = spec.Target == _sel;
            ui.Row.Background = isSel ? Brushes.White : Brushes.Transparent;
            ui.ActionTxt.Foreground = isSel ? new SolidColorBrush(Color.Parse("#1A0C24")) : Brushes.White;
        }

        _selKeys.Children.Clear();
        var matchSpec = info.Callouts.FirstOrDefault(c => c.Target == _sel);
        var glyph = ControllerNames.Glyph(_sel, _controllerType);
        _selName.Text = matchSpec != null
            ? ControllerNames.FormatLabel(matchSpec.HoldLabel != null ? $"{matchSpec.TapLabel} / ◎ {matchSpec.HoldLabel} [{glyph}]" : $"{matchSpec.TapLabel} [{glyph}]", _controllerType)
            : $"Controller: {ControllerNames.ButtonName(_sel, _controllerType)}";
        foreach (var b in _cur.Bindings.Where(b => b.Target == _sel).ToList())
        {
            var row = new DockPanel();
            var del = MakeGtaButton("✕", false);
            del.Padding = new Thickness(6, 2);
            del.Click += (_, _) => { _cur.Bindings.Remove(b); _cur.Save(); _engine.SetProfile(_cur); RefreshSchematic(); };
            DockPanel.SetDock(del, Dock.Right);
            row.Children.Add(del);
            row.Children.Add(new TextBlock { Text = $"{InputNames.Pretty(b.Input)}  ({b.Activator} · {b.Mode})", Foreground = new SolidColorBrush(Color.Parse("#3DDC97")), FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center });
            _selKeys.Children.Add(row);
        }
    }

    void StartBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_engine.Running)
        {
            _engine.Stop();
        }
        else
        {
            ShowVirtualPadPopupAndStart();
        }
        UpdateStatus();
    }

    async void ShowVirtualPadPopupAndStart()
    {
        var dlg = new Window
        {
            Title = "re-B.U.N.S — Virtual Pad Controls",
            Width = 520, Height = 290,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#261230"))
        };
        var sp = new StackPanel { Margin = new Thickness(22), Spacing = 10 };
        sp.Children.Add(new TextBlock { Text = "🎮  VIRTUAL PAD READY (macOS)", FontSize = 13, FontWeight = FontWeight.ExtraBold, Foreground = new SolidColorBrush(Color.Parse("#3DDC97")) });
        sp.Children.Add(new TextBlock { Text = $"Active Preset: {_cur.Name}", FontSize = 18, FontWeight = FontWeight.ExtraBold, Foreground = Brushes.White });
        sp.Children.Add(new TextBlock { Text = "•  [⌘ COMMAND KEY]  →  Pause & Play Virtual Pad Controls", FontSize = 13.5, FontWeight = FontWeight.Bold, Foreground = Brushes.White });
        sp.Children.Add(new TextBlock { Text = "•  [CONTROL + OPTION + DELETE]  →  Stop Virtual Pad Completely", FontSize = 13.5, FontWeight = FontWeight.Bold, Foreground = Brushes.White });
        sp.Children.Add(new TextBlock { Text = "•  Local Virtual Xbox 360 Bridge active on http://127.0.0.1:27654/pad", FontSize = 12, Foreground = new SolidColorBrush(Color.Parse("#E0D0E8")) });
        var btn = MakeGtaButton("GOT IT — START & LOCK", true);
        btn.HorizontalAlignment = HorizontalAlignment.Right;
        btn.Click += (_, _) => dlg.Close();
        sp.Children.Add(btn);
        dlg.Content = sp;
        await dlg.ShowDialog(this);

        if (!_engine.Start())
            ShowMacSetupDialog();
        UpdateStatus();
    }

    async void ShowMacSetupDialog()
    {
        var dlg = new Window
        {
            Title = "re-B.U.N.S macOS Accessibility & Virtual Pad Setup",
            Width = 520, Height = 260,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#261230"))
        };
        var sp = new StackPanel { Margin = new Thickness(22), Spacing = 10 };
        bool ok = MacEngine.IsAccessibilityTrusted();
        sp.Children.Add(new TextBlock { Text = ok ? "✓ macOS Accessibility Permission Granted" : "⚠ macOS Accessibility Permission Required", FontSize = 16, FontWeight = FontWeight.ExtraBold, Foreground = Brushes.White });
        sp.Children.Add(new TextBlock
        {
            Text = "On macOS, enable re-B.U.N.S in System Settings → Privacy & Security → Accessibility so it can lock and remap your keyboard & mouse into the virtual Xbox 360 controller.",
            FontSize = 12.5, Foreground = new SolidColorBrush(Color.Parse("#E0D0E8")), TextWrapping = TextWrapping.Wrap
        });
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        var openSettings = MakeGtaButton("Open macOS Accessibility Settings", true);
        openSettings.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo("open", "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility") { UseShellExecute = true }); } catch { }
            dlg.Close();
        };
        var close = MakeGtaButton("Close", false);
        close.Click += (_, _) => dlg.Close();
        row.Children.Add(close);
        row.Children.Add(openSettings);
        sp.Children.Add(row);
        dlg.Content = sp;
        await dlg.ShowDialog(this);
    }

    void UpdateStatus()
    {
        _startBtn.Content = _engine.Running ? "■  STOP VIRTUAL PAD" : "▶  START VIRTUAL PAD";
        _startBtn.Background = new SolidColorBrush(Color.Parse(_engine.Running ? "#9A2842" : "#1E8A5A"));
        _statusText.Text = !_engine.Running ? "STOPPED" : (_engine.Locked ? "LOCKED · CONTROLLER ON (⌘ = Unlock)" : "UNLOCKED · Press ⌘ to Lock");
    }
}
