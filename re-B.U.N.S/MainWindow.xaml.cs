using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using WBinding = System.Windows.Data.Binding;

namespace ReBuns;

public partial class MainWindow : Window
{
    readonly Engine _engine = new();
    List<Profile> _profiles;
    Profile _cur;
    PadTarget _sel = PadTarget.DUp;
    string _selKeyInput = null;
    string _lastJson = "";
    ReleaseInfo _pendingUpdate;
    SchematicHoldView _holdView = SchematicHoldView.TapAndHold;
    string _previewCategory = "ON FOOT";
    ControllerType _controllerType = ControllerType.Xbox;

    readonly Dictionary<PadTarget, Shape> _shapes = new();
    readonly Dictionary<PadTarget, TextBlock> _spotTexts = new();
    Ellipse _padLsDot, _padRsDot;
    TextBlock _schematicHeaderText;

    record CalloutRowUI(
        int SlotIndex,
        bool IsLeft,
        Border RowBorder,
        TextBlock ActionText,
        TextBlock KeyBadgeText,
        TextBlock GlyphText,
        Ellipse HoldRing);

    readonly List<CalloutRowUI> _calloutRows = new();

    record KbCap(string Input, Border Box, TextBlock TopText, TextBlock MapText, int Code);
    readonly List<KbCap> _kbCaps = new();

    System.Windows.Forms.NotifyIcon _tray;
    MediaPlayer _introPlayer;
    static readonly Brush DarkBtnFill = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x2B));
    static readonly Brush DarkInk = new SolidColorBrush(Color.FromRgb(0x1A, 0x0C, 0x24));
    static readonly Brush WhiteGrad = new LinearGradientBrush(Colors.White, Color.FromRgb(0xDF, 0xCE, 0xE8), 0);
    static readonly Brush OnFill = new SolidColorBrush(Color.FromRgb(0x3D, 0xDC, 0x97));
    static readonly Brush PinkFill = new SolidColorBrush(Color.FromRgb(0xF6, 0x87, 0xB3));
    static readonly Brush SelStroke = new SolidColorBrush(Color.FromRgb(0xFF, 0xC8, 0x57));
    static readonly Brush DefStroke = new SolidColorBrush(Color.FromRgb(0x48, 0x48, 0x5A));
    static readonly Brush MappedCapBg = new SolidColorBrush(Color.FromRgb(0x4A, 0x22, 0x58));
    static readonly Brush UnmappedCapBg = new SolidColorBrush(Color.FromRgb(0x2C, 0x16, 0x38));

    public MainWindow()
    {
        InitializeComponent();
        VersionText.Text = Updater.CurrentVersionString;

        Localization.CurrentLanguage = AppSettings.Instance.Language;
        InitLanguageCombo();

        BuildPreviewCategories();
        BuildPad();
        BuildKeyboardAndMouse();
        BuildGrid();

        _engine.StateChanged += () => Dispatcher.BeginInvoke(UpdateStatus);
        _profiles = Profile.LoadAll();
        ProfileList.ItemsSource = _profiles;
        int idx = _profiles.FindIndex(p => p.Name == AppSettings.Instance.LastSelectedProfile);
        if (idx < 0) idx = _profiles.FindIndex(p => p.LayoutId == GtaLayoutId.ReBuns);
        ProfileList.SelectedIndex = Math.Max(0, idx);

        _controllerType = _cur?.ControllerType ?? AppSettings.Instance.ControllerType;
        _engine.ControllerType = _controllerType;
        UpdateControllerTypeButtons();

        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        t.Tick += (_, _) => Live();
        t.Start();

        var save = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        save.Tick += (_, _) => SyncProfile();
        save.Start();

        SetupTray();
        RefreshDriverBadge();
        UpdateHoldViewButtons();
        ApplyLanguage();
        UpdateStatus();

        Loaded += async (_, _) =>
        {
            await RunLaunchAnimationAsync();
            if (!AppSettings.Instance.HasCompletedLanguageSetup)
            {
                ShowLanguagePickerDialog();
            }
            // Auto-detect if ViGEmBus driver is missing on this PC; if missing, pop up the built-in setup installer automatically!
            if (!DriverHelper.IsViGEmInstalled())
            {
                new SystemSetupWindow(driverMissing: true) { Owner = this }.ShowDialog();
                RefreshDriverBadge();
            }
            if (!AppSettings.Instance.HasCompletedInitialSetup)
            {
                ShowLayoutPickerDialog();
                AppSettings.Instance.HasCompletedInitialSetup = true;
                AppSettings.Instance.Save();
            }
            if (AppSettings.Instance.AutoCheckUpdates)
                await CheckForUpdatesAsync(silentIfUpToDate: true);
        };
    }

    bool _updatingLangCombo;

    void InitLanguageCombo()
    {
        _updatingLangCombo = true;
        LanguageCombo.Items.Clear();
        int selIdx = 0;
        for (int i = 0; i < Localization.SupportedLanguages.Length; i++)
        {
            var l = Localization.SupportedLanguages[i];
            LanguageCombo.Items.Add($"{l.Flag} {l.NativeName}");
            if (l.Code.Equals(Localization.CurrentLanguage, StringComparison.OrdinalIgnoreCase))
                selIdx = i;
        }
        LanguageCombo.SelectedIndex = selIdx;
        _updatingLangCombo = false;
    }

    void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingLangCombo || LanguageCombo.SelectedIndex < 0 || LanguageCombo.SelectedIndex >= Localization.SupportedLanguages.Length)
            return;
        string code = Localization.SupportedLanguages[LanguageCombo.SelectedIndex].Code;
        Localization.CurrentLanguage = code;
        AppSettings.Instance.Language = code;
        AppSettings.Instance.HasCompletedLanguageSetup = true;
        AppSettings.Instance.Save();
        ApplyLanguage();
        UpdateStatus();
    }

    void ShowLanguagePickerDialog()
    {
        try
        {
            var dlg = new LanguageWindow(AppSettings.Instance.Language) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                Localization.CurrentLanguage = dlg.SelectedLanguage;
                AppSettings.Instance.Language = dlg.SelectedLanguage;
            }
            AppSettings.Instance.HasCompletedLanguageSetup = true;
            AppSettings.Instance.Save();
            InitLanguageCombo();
            ApplyLanguage();
            UpdateStatus();
        }
        catch { }
    }

    void ApplyLanguage()
    {
        try
        {
            Title = Localization.T("AppTitle");
            if (SettingsHeaderTitle != null) SettingsHeaderTitle.Text = Localization.T("Settings");
            if (UpdateBtn != null) UpdateBtn.Content = Localization.T("CheckUpdates");
            if (Footer != null) Footer.Text = Localization.T("HotkeysFooter");
            RefreshDriverBadge();
        }
        catch { }
    }

    void PlayIntroSound()
    {
        try
        {
            string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "re-B.U.N.S");
            Directory.CreateDirectory(dir);
            string mp3Path = System.IO.Path.Combine(dir, "intro.mp3");
            using (var s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("intro.mp3"))
            {
                if (s != null)
                {
                    using var fs = new FileStream(mp3Path, FileMode.Create, FileAccess.Write, FileShare.Read);
                    s.CopyTo(fs);
                }
            }
            if (File.Exists(mp3Path))
            {
                _introPlayer ??= new MediaPlayer();
                _introPlayer.Open(new Uri(mp3Path, UriKind.Absolute));
                _introPlayer.Volume = 1.0;
                _introPlayer.Play();
            }
        }
        catch { }
    }

    async Task RunLaunchAnimationAsync()
    {
        try
        {
            PlayIntroSound();

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            SplashCardScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.84, 1.0, TimeSpan.FromMilliseconds(1400)) { EasingFunction = ease });
            SplashCardScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.84, 1.0, TimeSpan.FromMilliseconds(1400)) { EasingFunction = ease });
            Ring1Scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.75, 1.30, TimeSpan.FromMilliseconds(3400)) { EasingFunction = ease });
            Ring1Scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.75, 1.30, TimeSpan.FromMilliseconds(3400)) { EasingFunction = ease });
            Ring2Scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.65, 1.42, TimeSpan.FromMilliseconds(3600)) { EasingFunction = ease });
            Ring2Scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.65, 1.42, TimeSpan.FromMilliseconds(3600)) { EasingFunction = ease });
            SplashBarFill.BeginAnimation(WidthProperty, new DoubleAnimation(12, 376, TimeSpan.FromMilliseconds(3300)) { EasingFunction = ease });

            await Task.Delay(1100);
            SplashStatusText.Text = "LOADING re-B.U.N.S GTA 6 CONTROL SCHEMES...";
            await Task.Delay(1200);
            SplashStatusText.Text = $"READY  ·  ACTIVE PRESET: {(_cur?.Name ?? "★ re-B.U.N.S").ToUpper()}";
            await Task.Delay(1150);

            var fade = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(420)) { EasingFunction = ease };
            LaunchOverlay.BeginAnimation(OpacityProperty, fade);
            await Task.Delay(430);
            LaunchOverlay.Visibility = Visibility.Collapsed;
        }
        catch
        {
            LaunchOverlay.Visibility = Visibility.Collapsed;
        }
    }

    // ================= GTA 6 Preview Categories (Left Column) =================
    static readonly string[] PreviewCategories =
    {
        "On Foot",
        "Aiming",
        "Melee",
        "Land / Water Vehicles",
        "Aircraft",
        "Bicycle"
    };

    readonly List<(string Name, Ellipse Dot, TextBlock Label)> _previewRows = new();

    void BuildPreviewCategories()
    {
        PreviewList.Children.Clear();
        _previewRows.Clear();
        foreach (var cat in PreviewCategories)
        {
            var row = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromArgb(55, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(4, 9, 8, 9),
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = Brushes.Transparent
            };
            var dock = new DockPanel();
            var outerRing = new Grid { Width = 18, Height = 18 };
            outerRing.Children.Add(new Ellipse
            {
                Stroke = new SolidColorBrush(Color.FromArgb(140, 0xFF, 0xFF, 0xFF)),
                StrokeThickness = 1.5
            });
            var innerDot = new Ellipse
            {
                Width = 10, Height = 10,
                Fill = PinkFill,
                Visibility = cat == "On Foot" ? Visibility.Visible : Visibility.Collapsed
            };
            outerRing.Children.Add(innerDot);
            DockPanel.SetDock(outerRing, Dock.Right);
            dock.Children.Add(outerRing);

            var lbl = new TextBlock
            {
                Text = cat,
                FontSize = 14,
                FontWeight = cat == "On Foot" ? FontWeights.Bold : FontWeights.Normal,
                Foreground = cat == "On Foot" ? Brushes.White : (Brush)FindResource("Dim"),
                VerticalAlignment = VerticalAlignment.Center
            };
            dock.Children.Add(lbl);
            row.Child = dock;

            string captured = cat;
            void UpdatePreviewRowVisual(Border r, TextBlock l, Ellipse ring, bool hovered)
            {
                bool sel = _previewCategory == captured.ToUpperInvariant();
                if (hovered)
                {
                    r.Background = WhiteGrad;
                    l.Foreground = DarkInk;
                    ring.Stroke = DarkInk;
                }
                else
                {
                    r.Background = Brushes.Transparent;
                    l.Foreground = sel ? Brushes.White : (Brush)FindResource("Dim");
                    ring.Stroke = new SolidColorBrush(Color.FromArgb(140, 0xFF, 0xFF, 0xFF));
                }
            }
            var outerEllipse = (Ellipse)outerRing.Children[0];
            row.MouseEnter += (_, _) => UpdatePreviewRowVisual(row, lbl, outerEllipse, true);
            row.MouseLeave += (_, _) => UpdatePreviewRowVisual(row, lbl, outerEllipse, false);
            row.MouseLeftButtonDown += (_, _) =>
            {
                SelectScenario(captured);
                UpdatePreviewRowVisual(row, lbl, outerEllipse, row.IsMouseOver);
            };
            PreviewList.Children.Add(row);
            _previewRows.Add((cat, innerDot, lbl));
        }
    }

    string _activeScenarioName = "On Foot";

    void SelectScenario(string scenarioName)
    {
        if (string.IsNullOrWhiteSpace(scenarioName)) scenarioName = "On Foot";
        _activeScenarioName = scenarioName;
        _previewCategory = scenarioName.ToUpperInvariant();
        _engine.CurrentScenario = scenarioName;
        if (_cur != null) _cur.ActiveScenario = scenarioName;

        if (_schematicHeaderText != null)
            _schematicHeaderText.Text = _previewCategory + (_controllerType == ControllerType.PlayStation ? "  ·  PLAYSTATION" : "  ·  XBOX");

        foreach (var pr in _previewRows)
        {
            bool sel = pr.Name.Equals(scenarioName, StringComparison.OrdinalIgnoreCase);
            pr.Dot.Visibility = sel ? Visibility.Visible : Visibility.Collapsed;
            pr.Label.FontWeight = sel ? FontWeights.Bold : FontWeights.Normal;
            pr.Label.Foreground = sel ? Brushes.White : (Brush)FindResource("Dim");
        }

        if (ScenarioFilterCombo != null && ScenarioFilterCombo.Items.Contains(scenarioName))
            ScenarioFilterCombo.SelectedItem = scenarioName;

        ApplyBindingsFilter();
        Changed();
    }

    // ================= Control Scheme 1/3, 2/3, 3/3 Cycling =================
    void PrevScheme_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        CycleScheme(-1);
    }

    void NextScheme_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        CycleScheme(+1);
    }

    void SchemeBox_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => CycleScheme(+1);

    void CycleScheme(int delta)
    {
        int total = Profile.GtaLayouts.Length;
        int cur = _cur?.LayoutId is not null and not GtaLayoutId.Custom
            ? (int)_cur.LayoutId
            : 1;
        int next = ((cur - 1 + delta + total) % total) + 1;
        SelectOrCreateGtaLayout((GtaLayoutId)next, resetToDefault: false);
    }

    void HoldView_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && int.TryParse(b.Tag?.ToString(), out int v))
        {
            _holdView = (SchematicHoldView)v;
            UpdateHoldViewButtons();
            RefreshPadLabels();
        }
    }

    void CycleHoldView_Click(object sender, RoutedEventArgs e)
    {
        _holdView = (SchematicHoldView)(((int)_holdView + 1) % 3);
        UpdateHoldViewButtons();
        RefreshPadLabels();
    }

    void UpdateHoldViewButtons()
    {
        var btns = new[]
        {
            (ViewBothBtn, SchematicHoldView.TapAndHold),
            (ViewTapBtn, SchematicHoldView.TapOnly),
            (ViewHoldBtn, SchematicHoldView.HoldOnly)
        };
        foreach (var (btn, mode) in btns)
        {
            bool active = _holdView == mode;
            btn.Background = active ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x3D, 0x1F, 0x4C));
            btn.Foreground = active ? new SolidColorBrush(Color.FromRgb(0x2B, 0x16, 0x38)) : Brushes.White;
            btn.BorderBrush = active ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x75, 0x4A, 0x84));
        }
    }

    void XboxToggle_Click(object sender, RoutedEventArgs e) => SetControllerType(ControllerType.Xbox);
    void PsToggle_Click(object sender, RoutedEventArgs e) => SetControllerType(ControllerType.PlayStation);

    void SetControllerType(ControllerType type)
    {
        _controllerType = type;
        if (_cur != null)
        {
            _cur.ControllerType = type;
            try { _cur.Save(); } catch { }
        }
        AppSettings.Instance.ControllerType = type;
        AppSettings.Instance.Save();
        _engine.ControllerType = type;

        if (_engine.Running)
        {
            _engine.Stop();
            _engine.Start();
        }

        UpdateControllerTypeButtons();
        RefreshPadLabels();
    }

    void UpdateControllerTypeButtons()
    {
        bool isXbox = _controllerType == ControllerType.Xbox;
        var inactiveBg = Brushes.Transparent;
        var inactiveBorder = Brushes.Transparent;
        var inactiveFg = new SolidColorBrush(Color.FromRgb(0xC8, 0xA2, 0xC8));

        if (XboxToggleBtn != null && PsToggleBtn != null)
        {
            XboxToggleBtn.Background = isXbox ? new SolidColorBrush(Color.FromRgb(0x10, 0x7C, 0x10)) : inactiveBg;
            XboxToggleBtn.BorderBrush = isXbox ? new SolidColorBrush(Color.FromRgb(0x3D, 0xDC, 0x97)) : inactiveBorder;
            XboxToggleBtn.Foreground = isXbox ? Brushes.White : inactiveFg;

            PsToggleBtn.Background = !isXbox ? new SolidColorBrush(Color.FromRgb(0x00, 0x37, 0x91)) : inactiveBg;
            PsToggleBtn.BorderBrush = !isXbox ? new SolidColorBrush(Color.FromRgb(0x56, 0x9C, 0xD6)) : inactiveBorder;
            PsToggleBtn.Foreground = !isXbox ? Brushes.White : inactiveFg;
        }

        if (SchemeXboxBtn != null && SchemePsBtn != null)
        {
            SchemeXboxBtn.Background = isXbox ? new SolidColorBrush(Color.FromRgb(0x10, 0x7C, 0x10)) : new SolidColorBrush(Color.FromRgb(0x2B, 0x16, 0x38));
            SchemeXboxBtn.BorderBrush = isXbox ? new SolidColorBrush(Color.FromRgb(0x3D, 0xDC, 0x97)) : new SolidColorBrush(Color.FromRgb(0x5A, 0x33, 0x66));
            SchemeXboxBtn.Foreground = isXbox ? Brushes.White : inactiveFg;

            SchemePsBtn.Background = !isXbox ? new SolidColorBrush(Color.FromRgb(0x00, 0x37, 0x91)) : new SolidColorBrush(Color.FromRgb(0x2B, 0x16, 0x38));
            SchemePsBtn.BorderBrush = !isXbox ? new SolidColorBrush(Color.FromRgb(0x56, 0x9C, 0xD6)) : new SolidColorBrush(Color.FromRgb(0x5A, 0x33, 0x66));
            SchemePsBtn.Foreground = !isXbox ? Brushes.White : inactiveFg;
        }
    }

    void OpenLayoutPicker_Click(object sender, RoutedEventArgs e) => ShowLayoutPickerDialog();

    void ShowLayoutPickerDialog()
    {
        var curId = _cur?.LayoutId ?? GtaLayoutId.ReBuns;
        var dlg = new GtaLayoutWindow(curId) { Owner = this };
        if (dlg.ShowDialog() == true)
            SelectOrCreateGtaLayout(dlg.SelectedLayout, dlg.ResetRequested);
    }

    void SelectOrCreateGtaLayout(GtaLayoutId id, bool resetToDefault)
    {
        if (id == GtaLayoutId.Custom) return;
        var info = Profile.GtaLayouts.FirstOrDefault(x => x.Id == id) ?? Profile.GtaLayouts[0];
        var existing = _profiles.FirstOrDefault(p => p.LayoutId == id || string.Equals(p.Name, info.ProfileName, StringComparison.OrdinalIgnoreCase));

        if (existing != null && resetToDefault)
        {
            try { File.Delete(existing.FilePath); } catch { }
            int idx = _profiles.IndexOf(existing);
            var fresh = Profile.CreateGta6Preset(id);
            fresh.Save();
            _profiles[idx] = fresh;
            existing = fresh;
        }
        else if (existing == null)
        {
            existing = Profile.CreateGta6Preset(id);
            existing.Save();
            _profiles.Add(existing);
        }

        ProfileList.Items.Refresh();
        ProfileList.SelectedItem = existing;
        UpdateSchemeHeader();
    }

    void UpdateSchemeHeader()
    {
        var curId = _cur?.LayoutId ?? GtaLayoutId.Custom;
        var info = Profile.GtaLayouts.FirstOrDefault(x => x.Id == curId);
        if (info != null)
        {
            SchemeNameText.Text = info.SchemeName;
            SchemeIndexText.Text = $"{info.Index}  /  {Profile.GtaLayouts.Length}";
        }
        else
        {
            SchemeNameText.Text = _cur?.Name ?? "Custom";
            SchemeIndexText.Text = "Custom";
        }
    }

    // ================= Driver & Updates =================
    void RefreshDriverBadge()
    {
        bool ok = DriverHelper.IsViGEmInstalled();
        DriverBtn.Content = ok ? "✓ ViGEmBus Ready" : "⚠ Install ViGEmBus Driver";
        DriverBtn.Foreground = ok
            ? new SolidColorBrush(Color.FromRgb(0x3D, 0xDC, 0x97))
            : new SolidColorBrush(Color.FromRgb(0xFF, 0xC8, 0x57));
    }

    void DriverBtn_Click(object sender, RoutedEventArgs e)
    {
        bool missing = !DriverHelper.IsViGEmInstalled();
        new SystemSetupWindow(driverMissing: missing) { Owner = this }.ShowDialog();
        RefreshDriverBadge();
    }

    async void UpdateBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingUpdate != null)
        {
            new UpdateWindow(_pendingUpdate) { Owner = this }.ShowDialog();
            return;
        }
        await CheckForUpdatesAsync(silentIfUpToDate: false);
    }

    async Task CheckForUpdatesAsync(bool silentIfUpToDate)
    {
        UpdateBtn.IsEnabled = false;
        UpdateStatusText.Text = "Checking GitHub for updates...";
        var (rel, isNewer, err) = await Updater.CheckForUpdateAsync();
        UpdateBtn.IsEnabled = true;

        if (err != null)
        {
            UpdateStatusText.Text = silentIfUpToDate ? "" : "Update check: " + err;
            if (!silentIfUpToDate)
                MessageBox.Show($"Could not check for updates from {AppSettings.Instance.GitHubRepo}:\n\n{err}",
                    "re-B.U.N.S Update Check", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (isNewer && rel != null)
        {
            _pendingUpdate = rel;
            UpdateBtn.Content = $"⬇ Update to {rel.TagName}";
            UpdateBtn.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x8A, 0x5A));
            UpdateBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0xDC, 0x97));
            UpdateStatusText.Text = $"New release {rel.TagName} available on GitHub!";
            new UpdateWindow(rel) { Owner = this }.ShowDialog();
        }
        else
        {
            UpdateStatusText.Text = "";
            if (!silentIfUpToDate)
                MessageBox.Show($"You are running the latest version of re-B.U.N.S ({Updater.CurrentVersionString}).\n\nLatest GitHub release: {rel?.TagName ?? Updater.CurrentVersionString}",
                    "re-B.U.N.S is Up to Date", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    // ================= Tab 1: GTA 6 White Controller Schematic & 18 Callouts =================
    record PadSpot(PadTarget T, double X, double Y, double R, string Text, bool Rect = false);

    static readonly PadSpot[] Spots =
    {
        new(PadTarget.LT, 465, 162, 16, "LT", true), new(PadTarget.RT, 615, 162, 16, "RT", true),
        new(PadTarget.LB, 455, 192, 15, "LB", true), new(PadTarget.RB, 625, 192, 15, "RB", true),
        new(PadTarget.Guide, 540, 218, 15, "❖"),
        new(PadTarget.Back, 512, 250, 9, "⧉"), new(PadTarget.Start, 568, 250, 9, "≡"),
        new(PadTarget.Y, 618, 226, 13, "Y"), new(PadTarget.X, 596, 248, 13, "X"),
        new(PadTarget.B, 640, 248, 13, "B"), new(PadTarget.A, 618, 270, 13, "A"),
        new(PadTarget.LS, 465, 250, 22, "L"), new(PadTarget.RS, 585, 312, 22, "R"),
        new(PadTarget.DUp, 502, 294, 10, "▲"), new(PadTarget.DDown, 502, 330, 10, "▼"),
        new(PadTarget.DLeft, 484, 312, 10, "◀"), new(PadTarget.DRight, 520, 312, 10, "▶"),
    };

    void BuildPad()
    {
        Pad.Children.Clear();
        _shapes.Clear();
        _calloutRows.Clear();

        // 1. Subtle Vice City Palm Tree Silhouettes in Bottom-Right (matching GTA 6 screenshots!)
        try
        {
            DrawPalmSilhouette(720, 555, 360, -18);
            DrawPalmSilhouette(855, 560, 310, 8);
            DrawPalmSilhouette(975, 560, 345, -10);
        }
        catch { }

        void DrawPalmSilhouette(double baseX, double baseY, double height, double tiltX)
        {
            try
            {
                var palmBrush = new SolidColorBrush(Color.FromArgb(48, 0x10, 0x05, 0x18));
                double topX = baseX + tiltX, topY = baseY - height;
                double cpX = baseX + tiltX * 0.4;
                double cpY = baseY - height * 0.5;

                // Use Invariant culture so numbers always format with '.' instead of ',' regardless of Windows regional settings
                string trunkPath = FormattableString.Invariant($"M {baseX:0.#},{baseY:0.#} Q {cpX:0.#},{cpY:0.#} {topX:0.#},{topY:0.#}");
                var trunk = new System.Windows.Shapes.Path
                {
                    Stroke = palmBrush,
                    StrokeThickness = 11,
                    Data = Geometry.Parse(trunkPath)
                };
                Pad.Children.Add(trunk);
                string[] fronds =
                {
                    FormattableString.Invariant($"M {topX:0.#},{topY:0.#} Q {topX - 65:0.#},{topY - 30:0.#} {topX - 115:0.#},{topY + 25:0.#}"),
                    FormattableString.Invariant($"M {topX:0.#},{topY:0.#} Q {topX - 55:0.#},{topY - 5:0.#}  {topX - 95:0.#},{topY + 60:0.#}"),
                    FormattableString.Invariant($"M {topX:0.#},{topY:0.#} Q {topX - 35:0.#},{topY - 55:0.#} {topX - 75:0.#},{topY - 25:0.#}"),
                    FormattableString.Invariant($"M {topX:0.#},{topY:0.#} Q {topX + 45:0.#},{topY - 50:0.#} {topX + 90:0.#},{topY - 15:0.#}"),
                    FormattableString.Invariant($"M {topX:0.#},{topY:0.#} Q {topX + 65:0.#},{topY - 20:0.#} {topX + 115:0.#},{topY + 30:0.#}"),
                    FormattableString.Invariant($"M {topX:0.#},{topY:0.#} Q {topX + 50:0.#},{topY + 5:0.#}  {topX + 85:0.#},{topY + 65:0.#}"),
                };
                foreach (var f in fronds)
                {
                    Pad.Children.Add(new System.Windows.Shapes.Path
                    {
                        Stroke = palmBrush,
                        StrokeThickness = 7,
                        StrokeEndLineCap = PenLineCap.Round,
                        Data = Geometry.Parse(f)
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to draw decorative palm: {ex.Message}");
            }
        }

        // 2. Top Center Header: "ON FOOT" + Vertical Pointer Line down to controller (centered at X = 540)
        _schematicHeaderText = new TextBlock
        {
            Text = "ON FOOT",
            FontSize = 22,
            FontWeight = FontWeights.ExtraBold,
            Foreground = Brushes.White
        };
        _schematicHeaderText.Measure(new Size(300, 50));
        Canvas.SetLeft(_schematicHeaderText, 540 - _schematicHeaderText.DesiredSize.Width / 2);
        Canvas.SetTop(_schematicHeaderText, 18);
        Pad.Children.Add(_schematicHeaderText);

        Pad.Children.Add(new Line
        {
            X1 = 540, Y1 = 54, X2 = 540, Y2 = 165,
            Stroke = new SolidColorBrush(Color.FromRgb(0xF6, 0x87, 0xB3)),
            StrokeThickness = 1.6
        });

        // 3. White Xbox Series Controller Body in Center (shifted +50px right so X center = 540)
        var shadow = new System.Windows.Shapes.Path
        {
            Fill = new SolidColorBrush(Color.FromArgb(65, 0x00, 0x00, 0x00)),
            Data = Geometry.Parse("M 470,185 L 610,185 C 655,185 685,205 698,255 C 715,320 728,395 704,424 C 685,445 658,425 636,388 " +
                                  "C 622,366 605,358 580,358 L 500,358 C 475,358 458,366 444,388 C 422,425 395,445 376,424 " +
                                  "C 352,395 365,320 382,255 C 395,205 425,185 470,185 Z")
        };
        Canvas.SetTop(shadow, 8);
        Pad.Children.Add(shadow);

        var body = new System.Windows.Shapes.Path
        {
            Fill = new LinearGradientBrush(Color.FromRgb(0xFB, 0xF9, 0xFD), Color.FromRgb(0xDD, 0xCE, 0xE8), 90),
            Stroke = new SolidColorBrush(Color.FromRgb(0x1C, 0x14, 0x24)),
            StrokeThickness = 2.5,
            Data = Geometry.Parse("M 470,185 L 610,185 C 655,185 685,205 698,255 C 715,320 728,395 704,424 C 685,445 658,425 636,388 " +
                                  "C 622,366 605,358 580,358 L 500,358 C 475,358 458,366 444,388 C 422,425 395,445 376,424 " +
                                  "C 352,395 365,320 382,255 C 395,205 425,185 470,185 Z")
        };
        Pad.Children.Add(body);

        // Dark D-Pad circular dish
        var dpadDish = new Ellipse
        {
            Width = 58, Height = 58,
            Fill = new SolidColorBrush(Color.FromRgb(0x28, 0x28, 0x32)),
            Stroke = new SolidColorBrush(Color.FromRgb(0x45, 0x45, 0x55)),
            StrokeThickness = 1.5
        };
        Canvas.SetLeft(dpadDish, 502 - 29);
        Canvas.SetTop(dpadDish, 312 - 29);
        Pad.Children.Add(dpadDish);

        // Controller buttons on the white controller
        _spotTexts.Clear();
        foreach (var s in Spots)
        {
            Shape sh = s.Rect
                ? new Rectangle { Width = s.R * 2.5, Height = s.R * 1.2, RadiusX = 5, RadiusY = 5 }
                : new Ellipse { Width = s.R * 2, Height = s.R * 2 };
            sh.Fill = DarkBtnFill;
            sh.Stroke = DefStroke;
            sh.StrokeThickness = 1.5;
            sh.Cursor = System.Windows.Input.Cursors.Hand;
            sh.Tag = s.T;
            Canvas.SetLeft(sh, s.X - sh.Width / 2);
            Canvas.SetTop(sh, s.Y - sh.Height / 2);
            sh.MouseLeftButtonDown += (_, e) =>
            {
                Select(s.T);
                if (e.ClickCount == 2) BeginAddKeyForSelected();
            };
            Pad.Children.Add(sh);
            _shapes[s.T] = sh;

            var tb = new TextBlock
            {
                Text = ControllerNames.Glyph(s.T, _controllerType),
                FontWeight = FontWeights.ExtraBold,
                FontSize = s.R >= 13 ? 11 : 8.5,
                Foreground = Brushes.White,
                IsHitTestVisible = false
            };
            tb.Measure(new Size(100, 100));
            Canvas.SetLeft(tb, s.X - tb.DesiredSize.Width / 2);
            Canvas.SetTop(tb, s.Y - tb.DesiredSize.Height / 2);
            Pad.Children.Add(tb);
            _spotTexts[s.T] = tb;
        }

        // Live stick deflection dots on the controller sticks
        _padLsDot = new Ellipse { Width = 8, Height = 8, Fill = OnFill, IsHitTestVisible = false };
        _padRsDot = new Ellipse { Width = 8, Height = 8, Fill = PinkFill, IsHitTestVisible = false };
        Pad.Children.Add(_padLsDot);
        Pad.Children.Add(_padRsDot);

        // 4. Build the 9 Left Callout Rows & 9 Right Callout Rows (345px wide each, plenty of space!)
        for (int i = 0; i < 9; i++)
        {
            double y = 74 + i * 48;
            BuildCalloutSlot(i, isLeft: true, x: 4, y: y, width: 345);
            BuildCalloutSlot(i, isLeft: false, x: 731, y: y, width: 345);
        }
    }

    void BuildCalloutSlot(int slotIdx, bool isLeft, double x, double y, double width)
    {
        var actionTxt = new TextBlock
        {
            FontSize = 14.5,
            FontWeight = FontWeights.ExtraBold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        };

        var keyBadgeTxt = new TextBlock
        {
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x3D, 0xDC, 0x97)),
            VerticalAlignment = VerticalAlignment.Center
        };
        var keyPill = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(145, 0x1A, 0x0C, 0x24)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(150, 0x3D, 0xDC, 0x97)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(6, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = keyBadgeTxt
        };

        // Xbox Button Icon Badge with outer Hold Ring
        var iconGrid = new Grid { Width = 32, Height = 32, VerticalAlignment = VerticalAlignment.Center };
        iconGrid.Children.Add(new Ellipse
        {
            Width = 26, Height = 26,
            Fill = new SolidColorBrush(Color.FromRgb(0x1E, 0x16, 0x26)),
            Stroke = new SolidColorBrush(Color.FromArgb(200, 0xFF, 0xFF, 0xFF)),
            StrokeThickness = 1.3
        });
        var holdRing = new Ellipse
        {
            Width = 32, Height = 32,
            Stroke = Brushes.White,
            StrokeThickness = 2.2,
            StrokeDashArray = new DoubleCollection { 4, 1.5 },
            Visibility = Visibility.Collapsed
        };
        iconGrid.Children.Add(holdRing);
        var glyphTxt = new TextBlock
        {
            FontSize = 10.5,
            FontWeight = FontWeights.ExtraBold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        iconGrid.Children.Add(glyphTxt);

        var rowPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = isLeft ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (isLeft)
        {
            rowPanel.Children.Add(keyPill);
            rowPanel.Children.Add(actionTxt);
            iconGrid.Margin = new Thickness(10, 0, 0, 0);
            rowPanel.Children.Add(iconGrid);
        }
        else
        {
            iconGrid.Margin = new Thickness(0, 0, 10, 0);
            rowPanel.Children.Add(iconGrid);
            rowPanel.Children.Add(actionTxt);
            rowPanel.Children.Add(keyPill);
        }

        var rowBorder = new Border
        {
            Width = width,
            Height = 38,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(6, 2, 6, 2),
            Cursor = System.Windows.Input.Cursors.Hand,
            Child = rowPanel
        };
        Canvas.SetLeft(rowBorder, x);
        Canvas.SetTop(rowBorder, y);

        var ui = new CalloutRowUI(slotIdx, isLeft, rowBorder, actionTxt, keyBadgeTxt, glyphTxt, holdRing);
        rowBorder.MouseEnter += (_, _) => ApplyCalloutRowColors(ui);
        rowBorder.MouseLeave += (_, _) => ApplyCalloutRowColors(ui);
        rowBorder.MouseLeftButtonDown += (_, e) =>
        {
            var spec = GetCalloutSpec(ui);
            if (spec != null)
            {
                Select(spec.Target);
                if (e.ClickCount == 2) BeginAddKeyForSelected();
            }
        };
        Pad.Children.Add(rowBorder);
        _calloutRows.Add(ui);
    }

    void ApplyCalloutRowColors(CalloutRowUI ui)
    {
        var spec = GetCalloutSpec(ui);
        bool isSelected = spec != null && spec.Target == _sel;
        bool isHovered = ui.RowBorder.IsMouseOver;

        if (isHovered || isSelected)
        {
            // GTA 6 white highlight bar -> dynamically switch text to dark so it is always high contrast!
            ui.RowBorder.Background = WhiteGrad;
            ui.RowBorder.BorderBrush = Brushes.White;
            ui.ActionText.Foreground = DarkInk;
            ui.HoldRing.Stroke = DarkInk;
        }
        else
        {
            ui.RowBorder.Background = Brushes.Transparent;
            ui.RowBorder.BorderBrush = Brushes.Transparent;
            ui.ActionText.Foreground = Brushes.White;
            ui.HoldRing.Stroke = Brushes.White;
        }
    }

    CalloutSpec GetCalloutSpec(CalloutRowUI ui)
    {
        var curId = _cur?.LayoutId ?? GtaLayoutId.Default;
        var info = Profile.GtaLayouts.FirstOrDefault(x => x.Id == curId) ?? Profile.GtaLayouts[0];
        var sideList = info.Callouts.Where(c => c.IsLeft == ui.IsLeft).ToArray();
        return ui.SlotIndex < sideList.Length ? sideList[ui.SlotIndex] : null;
    }

    // ================= Tab 3: Keyboard & Mouse Schematic =================
    record KeySpec(string Label, string Input, double W = 1.0);

    void BuildKeyboardAndMouse()
    {
        KbCanvas.Children.Clear();
        _kbCaps.Clear();

        var rows = new List<(double y, KeySpec[] keys)>
        {
            (18, new KeySpec[]
            {
                new("Esc","Escape",1.1), new("F1","F1"), new("F2","F2"), new("F3","F3"), new("F4","F4"),
                new("F5","F5"), new("F6","F6"), new("F7","F7"), new("F8","F8"),
                new("F9","F9"), new("F10","F10"), new("F11","F11"), new("F12","F12"),
                new("Del","Delete",1.1)
            }),
            (72, new KeySpec[]
            {
                new("`","Oemtilde"), new("1","D1"), new("2","D2"), new("3","D3"), new("4","D4"), new("5","D5"),
                new("6","D6"), new("7","D7"), new("8","D8"), new("9","D9"), new("0","D0"),
                new("-","OemMinus"), new("=","Oemplus"), new("Backspace","Back",1.9)
            }),
            (120, new KeySpec[]
            {
                new("Tab","Tab",1.45), new("Q","Q"), new("W","W"), new("E","E"), new("R","R"), new("T","T"),
                new("Y","Y"), new("U","U"), new("I","I"), new("O","O"), new("P","P"),
                new("[","OemOpenBrackets"), new("]","OemCloseBrackets"), new("\\","OemPipe",1.45)
            }),
            (168, new KeySpec[]
            {
                new("Caps","Capital",1.75), new("A","A"), new("S","S"), new("D","D"), new("F","F"), new("G","G"),
                new("H","H"), new("J","J"), new("K","K"), new("L","L"),
                new(";","OemSemicolon"), new("'","OemQuotes"), new("Enter","Return",2.15)
            }),
            (216, new KeySpec[]
            {
                new("L-Shift","LShiftKey",2.25), new("Z","Z"), new("X","X"), new("C","C"), new("V","V"),
                new("B","B"), new("N","N"), new("M","M"), new(",","Oemcomma"), new(".","OemPeriod"),
                new("/","OemQuestion"), new("R-Shift","RShiftKey",2.65)
            }),
            (264, new KeySpec[]
            {
                new("L-Ctrl","LControlKey",1.4), new("Win","LWin",1.25), new("L-Alt","LMenu",1.4),
                new("Space","Space",6.3),
                new("R-Alt","RMenu",1.4), new("R-Ctrl","RControlKey",1.4)
            })
        };

        const double unit = 44, gap = 4, startX = 14;
        foreach (var (y, keys) in rows)
        {
            double x = startX;
            foreach (var k in keys)
            {
                double w = k.W * unit + (k.W - 1) * gap;
                AddKeyCap(k.Label, k.Input, x, y, w, 44);
                x += w + gap;
            }
        }

        AddKeyCap("▲", "Up", 590, 326, 46, 40);
        AddKeyCap("◀", "Left", 540, 370, 46, 40);
        AddKeyCap("▼", "Down", 590, 370, 46, 40);
        AddKeyCap("▶", "Right", 640, 370, 46, 40);

        var legend = new TextBlock
        {
            Text = "Click any key or mouse button to configure Single Tap, Long Hold (◎), or Double-Tap actions.",
            FontSize = 12,
            Foreground = (Brush)FindResource("Dim"),
            Width = 500,
            TextWrapping = TextWrapping.Wrap
        };
        Canvas.SetLeft(legend, 16);
        Canvas.SetTop(legend, 342);
        KbCanvas.Children.Add(legend);

        var mouseShell = new Border
        {
            Width = 220, Height = 320,
            Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x16, 0x39)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x6D, 0x43, 0x7A)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(70, 70, 90, 90)
        };
        Canvas.SetLeft(mouseShell, 720);
        Canvas.SetTop(mouseShell, 24);
        KbCanvas.Children.Add(mouseShell);

        AddKeyCap("L-Click", "Mouse1", 732, 42, 88, 74);
        AddKeyCap("R-Click", "Mouse2", 840, 42, 88, 74);
        AddKeyCap("Whl ↑", "WheelUp", 804, 122, 52, 34);
        AddKeyCap("M-Click", "Mouse3", 804, 160, 52, 34);
        AddKeyCap("Whl ↓", "WheelDown", 804, 198, 52, 34);
        AddKeyCap("Mouse 5", "Mouse5", 730, 145, 64, 40);
        AddKeyCap("Mouse 4", "Mouse4", 730, 190, 64, 40);

        var moveBadge = new Border
        {
            Width = 176, Height = 64,
            Background = new SolidColorBrush(Color.FromRgb(0x3B, 0x1D, 0x47)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xF6, 0x87, 0xB3)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(8),
            Child = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new TextBlock { Text = "MOUSE LOOK", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Accent"), HorizontalAlignment = HorizontalAlignment.Center },
                    new TextBlock { Text = "→ Analog Look Stick", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,2,0,0) }
                }
            }
        };
        Canvas.SetLeft(moveBadge, 742);
        Canvas.SetTop(moveBadge, 254);
        KbCanvas.Children.Add(moveBadge);
    }

    void AddKeyCap(string label, string input, double x, double y, double w, double h)
    {
        var top = new TextBlock
        {
            Text = label, FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var map = new TextBlock
        {
            Text = "", FontSize = 10, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x3D, 0xDC, 0x97)),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        sp.Children.Add(top);
        sp.Children.Add(map);

        var box = new Border
        {
            Width = w, Height = h,
            Background = UnmappedCapBg,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x33, 0x64)),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(2),
            Cursor = System.Windows.Input.Cursors.Hand,
            Child = sp
        };
        Canvas.SetLeft(box, x);
        Canvas.SetTop(box, y);
        box.MouseLeftButtonDown += (_, _) => SelectKeyCap(input);
        KbCanvas.Children.Add(box);
        _kbCaps.Add(new KbCap(input, box, top, map, InputNames.ToCode(input)));
    }

    void SelectKeyCap(string input)
    {
        _selKeyInput = input;
        RefreshKeyboardCaps();
        BuildKeyInspector();
    }

    void RefreshKeyboardCaps()
    {
        if (_cur == null) return;
        foreach (var cap in _kbCaps)
        {
            if (cap.Input is "LWin" or "RWin")
            {
                cap.MapText.Text = "LOCK";
                cap.MapText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xC8, 0x57));
                cap.Box.Background = new SolidColorBrush(Color.FromRgb(0x45, 0x2A, 0x20));
                cap.Box.BorderBrush = cap.Input == _selKeyInput ? SelStroke : new SolidColorBrush(Color.FromRgb(0xC8, 0x92, 0x2A));
                continue;
            }
            var binds = _cur.Bindings.Where(b => string.Equals(b.Input, cap.Input, StringComparison.OrdinalIgnoreCase) && b.Target != PadTarget.None).ToList();
            bool has = binds.Count > 0;
            cap.MapText.Text = has ? string.Join("+", binds.Select(b => InputNames.ShortTarget(b.Target, _controllerType))) : "—";
            cap.MapText.Foreground = has ? new SolidColorBrush(Color.FromRgb(0x3D, 0xDC, 0x97)) : (Brush)FindResource("Dim");
            cap.Box.Background = has ? MappedCapBg : UnmappedCapBg;
            cap.Box.BorderBrush = cap.Input == _selKeyInput ? SelStroke : new SolidColorBrush(has ? Color.FromRgb(0x3D, 0xDC, 0x97) : Color.FromRgb(0x55, 0x33, 0x64));
            cap.Box.BorderThickness = new Thickness(cap.Input == _selKeyInput ? 2.5 : 1.5);
        }
    }

    void BuildKeyInspector()
    {
        KbInspectorPanel.Children.Clear();
        if (_cur == null || string.IsNullOrEmpty(_selKeyInput)) return;

        KbSelTitle.Text = InputNames.Pretty(_selKeyInput);
        if (_selKeyInput is "LWin" or "RWin")
        {
            KbSelSub.Text = "Windows Key is reserved as the global Lock / Unlock toggle while the virtual controller is running.";
            return;
        }

        var binds = _cur.Bindings.Where(b => string.Equals(b.Input, _selKeyInput, StringComparison.OrdinalIgnoreCase)).ToList();
        KbSelSub.Text = binds.Count == 0
            ? "No controller output assigned yet. Add one below (you can even bind one action on Single Tap and a different action on Long Hold or Double Tap!):"
            : $"{binds.Count} controller action(s) bound to {InputNames.Pretty(_selKeyInput)}:";

        foreach (var b in binds)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x0E, 0x26)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x5A, 0x33, 0x66)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 8)
            };
            var sp = new StackPanel();

            var topRow = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var delBtn = new Button { Content = "✕", Padding = new Thickness(8, 2, 8, 2) };
            delBtn.Click += (_, _) => { _cur.Bindings.Remove(b); Changed(); BuildKeyInspector(); };
            DockPanel.SetDock(delBtn, Dock.Right);
            topRow.Children.Add(delBtn);

            var cbTarget = new ComboBox
            {
                ItemsSource = Enum.GetValues<PadTarget>(),
                SelectedItem = b.Target,
                Width = 165,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            cbTarget.SelectionChanged += (_, _) => { if (cbTarget.SelectedItem is PadTarget pt) { b.Target = pt; Changed(); } };
            topRow.Children.Add(cbTarget);
            sp.Children.Add(topRow);

            var actRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 4) };
            actRow.Children.Add(new TextBlock { Text = "When Key: ", Width = 72, VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)FindResource("Dim"), FontSize = 11.5 });
            var cbAct = new ComboBox { ItemsSource = Enum.GetValues<KeyActivator>(), SelectedItem = b.Activator, Width = 120 };
            cbAct.SelectionChanged += (_, _) => { if (cbAct.SelectedItem is KeyActivator ka) { b.Activator = ka; Changed(); } };
            actRow.Children.Add(cbAct);
            sp.Children.Add(actRow);

            var modeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 4) };
            modeRow.Children.Add(new TextBlock { Text = "Output: ", Width = 72, VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)FindResource("Dim"), FontSize = 11.5 });
            var cbMode = new ComboBox { ItemsSource = Enum.GetValues<BindMode>(), SelectedItem = b.Mode, Width = 120 };
            cbMode.SelectionChanged += (_, _) => { if (cbMode.SelectedItem is BindMode bm) { b.Mode = bm; Changed(); } };
            modeRow.Children.Add(cbMode);
            sp.Children.Add(modeRow);

            var noteBox = new TextBox { Text = b.Note ?? "", Margin = new Thickness(0, 4, 0, 0) };
            noteBox.TextChanged += (_, _) => { b.Note = noteBox.Text; SyncProfile(); };
            sp.Children.Add(noteBox);

            card.Child = sp;
            KbInspectorPanel.Children.Add(card);
        }

        var addBtn = new Button
        {
            Content = $"＋ Add Action to {InputNames.Pretty(_selKeyInput)}",
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 4, 0, 0)
        };
        addBtn.Click += (_, _) =>
        {
            _cur.Bindings.Add(new Binding { Input = _selKeyInput, Target = PadTarget.A });
            Changed();
            BuildKeyInspector();
        };
        KbInspectorPanel.Children.Add(addBtn);
    }

    // ================= Refresh GTA 6 Controller Schematic & Inspector =================
    string GetKeysSummaryForTarget(PadTarget t)
    {
        if (_cur == null) return "—";
        if (t == PadTarget.LStickUp && _cur.LayoutId != GtaLayoutId.LeftHanded)
            return "W A S D";
        if (t == PadTarget.RStickUp && _cur.LayoutId == GtaLayoutId.LeftHanded)
            return "W A S D";
        if ((t == PadTarget.RStickUp && _cur.Mouse.Stick == MouseStick.Right) ||
            (t == PadTarget.LStickUp && _cur.Mouse.Stick == MouseStick.Left))
            return "Mouse";

        var keys = _cur.Bindings.Where(b => b.Target == t).Select(b =>
        {
            string k = InputNames.Pretty(b.Input);
            return b.Activator switch
            {
                KeyActivator.SingleTap => $"{k}(Tap)",
                KeyActivator.LongHold => $"{k}(Hold)",
                KeyActivator.DoubleTap => $"{k}(2x)",
                _ => k
            };
        }).ToList();
        return keys.Count == 0 ? "—" : string.Join(" / ", keys.Take(3));
    }

    void RefreshPadLabels()
    {
        UpdateSchemeHeader();

        if (_schematicHeaderText != null)
        {
            _schematicHeaderText.Text = _previewCategory + (_controllerType == ControllerType.PlayStation ? "  ·  PLAYSTATION" : "  ·  XBOX");
            _schematicHeaderText.Measure(new Size(400, 50));
            Canvas.SetLeft(_schematicHeaderText, 540 - _schematicHeaderText.DesiredSize.Width / 2);
        }

        foreach (var (t, tb) in _spotTexts)
        {
            tb.Text = ControllerNames.Glyph(t, _controllerType);
            tb.Measure(new Size(100, 100));
            var s = Spots.FirstOrDefault(x => x.T == t);
            if (s != null) Canvas.SetLeft(tb, s.X - tb.DesiredSize.Width / 2);
        }

        // Update all 18 GTA 6 Callout rows
        foreach (var ui in _calloutRows)
        {
            var spec = GetCalloutSpec(ui);
            if (spec == null) continue;

            ui.GlyphText.Text = ControllerNames.Glyph(spec.Target, _controllerType);
            bool showHoldRing = false;
            string label;

            if (spec.HoldLabel == null)
            {
                label = spec.TapLabel;
            }
            else
            {
                switch (_holdView)
                {
                    case SchematicHoldView.TapOnly:
                        label = spec.TapLabel;
                        break;
                    case SchematicHoldView.HoldOnly:
                        label = spec.HoldLabel;
                        showHoldRing = true;
                        break;
                    case SchematicHoldView.TapAndHold:
                    default:
                        label = $"{spec.TapLabel}  /  ◎ {spec.HoldLabel}";
                        showHoldRing = true;
                        break;
                }
            }

            ui.ActionText.Text = ControllerNames.FormatLabel(label, _controllerType);
            ui.HoldRing.Visibility = showHoldRing ? Visibility.Visible : Visibility.Collapsed;
            ui.KeyBadgeText.Text = GetKeysSummaryForTarget(spec.Target);
            ApplyCalloutRowColors(ui);
        }

        foreach (var (t, sh) in _shapes)
        {
            sh.Stroke = t == _sel ? SelStroke : DefStroke;
            sh.StrokeThickness = t == _sel ? 2.8 : 1.5;
        }

        RefreshKeyboardCaps();

        // Populate bottom-left inspector for selected controller action
        SelKeys.Items.Clear();
        AddKeyBtn.IsEnabled = _sel != PadTarget.None && _cur != null;
        if (_sel == PadTarget.None || _cur == null) return;

        var curId = _cur.LayoutId == GtaLayoutId.Custom ? GtaLayoutId.Default : _cur.LayoutId;
        var schemeInfo = Profile.GtaLayouts.FirstOrDefault(x => x.Id == curId) ?? Profile.GtaLayouts[0];
        var matchSpec = schemeInfo.Callouts.FirstOrDefault(c => c.Target == _sel);
        var glyph = ControllerNames.Glyph(_sel, _controllerType);

        SelName.Text = matchSpec != null
            ? ControllerNames.FormatLabel(matchSpec.HoldLabel != null ? $"{matchSpec.TapLabel}  /  ◎ {matchSpec.HoldLabel}  [{glyph}]" : $"{matchSpec.TapLabel}  [{glyph}]", _controllerType)
            : $"Controller: {ControllerNames.ButtonName(_sel, _controllerType)}";

        var matching = _cur.Bindings.Where(b => b.Target == _sel).ToList();
        SelSub.Text = matchSpec?.HoldLabel != null
            ? ControllerNames.FormatLabel($"Tap key = {matchSpec.TapLabel}  ·  Hold key = {matchSpec.HoldLabel} (or bind a dedicated Double-Tap / Long-Hold key below):", _controllerType)
            : $"{matching.Count} keyboard/mouse binding(s) assigned:";

        foreach (var b in matching)
        {
            var rowBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x0E, 0x26)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x5A, 0x33, 0x66)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 3, 6, 3),
                Margin = new Thickness(0, 0, 0, 4)
            };
            var row = new DockPanel();
            var del = new Button { Content = "✕", Padding = new Thickness(6, 1, 6, 1), FontSize = 10.5 };
            del.Click += (_, _) => { _cur.Bindings.Remove(b); Changed(); };
            DockPanel.SetDock(del, Dock.Right);
            row.Children.Add(del);

            var cbAct = new ComboBox
            {
                ItemsSource = Enum.GetValues<KeyActivator>(),
                SelectedItem = b.Activator,
                Width = 92,
                FontSize = 11,
                Margin = new Thickness(4, 0, 4, 0)
            };
            cbAct.SelectionChanged += (_, _) =>
            {
                if (cbAct.SelectedItem is KeyActivator ka)
                {
                    b.Activator = ka;
                    if (ka == KeyActivator.DoubleTap && b.Mode == BindMode.Hold)
                        b.Mode = BindMode.HoldPulse;
                    Changed();
                }
            };
            DockPanel.SetDock(cbAct, Dock.Right);
            row.Children.Add(cbAct);

            row.Children.Add(new TextBlock
            {
                Text = InputNames.Pretty(b.Input) + (b.Mode == BindMode.HoldPulse ? " [◎ 650ms]" : ""),
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0x3D, 0xDC, 0x97))
            });
            rowBorder.Child = row;
            SelKeys.Items.Add(rowBorder);
        }
    }

    void Select(PadTarget t) { _sel = t; RefreshPadLabels(); }

    void AddKey_Click(object sender, RoutedEventArgs e) => BeginAddKeyForSelected();

    void BeginAddKeyForSelected()
    {
        if (_sel == PadTarget.None || _cur == null) return;
        var target = _sel;
        AddKeyBtn.Content = "PRESS ANY KEY OR MOUSE…";
        int actIdx = NewBindActCombo.SelectedIndex;
        var act = actIdx switch
        {
            1 => KeyActivator.SingleTap,
            2 => KeyActivator.LongHold,
            3 => KeyActivator.DoubleTap,
            _ => KeyActivator.Regular
        };
        var mode = act switch
        {
            KeyActivator.SingleTap => BindMode.TapPulse,
            KeyActivator.LongHold => BindMode.HoldPulse,
            KeyActivator.DoubleTap => BindMode.HoldPulse,
            _ => BindMode.Hold
        };
        _engine.Capture = name => Dispatcher.BeginInvoke(() =>
        {
            _cur.Bindings.Add(new Binding { Input = name, Target = target, Activator = act, Mode = mode });
            AddKeyBtn.Content = "＋ BIND KEY / MOUSE";
            Changed();
        });
    }

    static readonly string[] AllScenarioOptions =
    {
        "All", "On Foot", "Land / Water Vehicles", "Melee", "Aiming", "Aircraft", "Bicycle"
    };

    // ================= Tab 4: All Bindings Grid =================
    void BuildGrid()
    {
        if (ScenarioFilterCombo != null)
        {
            ScenarioFilterCombo.Items.Clear();
            ScenarioFilterCombo.Items.Add("All Scenarios");
            foreach (var sc in AllScenarioOptions) ScenarioFilterCombo.Items.Add(sc);
            ScenarioFilterCombo.SelectedIndex = 0;
        }

        var keyCol = new DataGridTemplateColumn { Header = "Key / Mouse Input", Width = 155 };
        var f = new FrameworkElementFactory(typeof(Button));
        f.SetBinding(ContentProperty, new WBinding("Input") { Converter = new PrettyConv() });
        f.SetValue(PaddingProperty, new Thickness(8, 3, 8, 3));
        f.SetValue(MarginProperty, new Thickness(4, 2, 4, 2));
        f.SetValue(FontWeightProperty, FontWeights.Bold);
        f.AddHandler(Button.ClickEvent, new RoutedEventHandler(CaptureCell_Click));
        keyCol.CellTemplate = new DataTemplate { VisualTree = f };
        Grid.Columns.Add(keyCol);
        Grid.Columns.Add(new DataGridComboBoxColumn
        {
            Header = "Scenario", Width = 160, ItemsSource = AllScenarioOptions,
            SelectedItemBinding = new WBinding("Scenario") { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }
        });
        Grid.Columns.Add(new DataGridComboBoxColumn
        {
            Header = "Controller Button", Width = 145, ItemsSource = Enum.GetValues<PadTarget>(),
            SelectedItemBinding = new WBinding("Target") { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }
        });
        Grid.Columns.Add(new DataGridComboBoxColumn
        {
            Header = "Key Activator (Tap / Hold / 2x)", Width = 195, ItemsSource = Enum.GetValues<KeyActivator>(),
            SelectedItemBinding = new WBinding("Activator") { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }
        });
        Grid.Columns.Add(new DataGridComboBoxColumn
        {
            Header = "Output Mode", Width = 130, ItemsSource = Enum.GetValues<BindMode>(),
            SelectedItemBinding = new WBinding("Mode") { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }
        });
        Grid.Columns.Add(new DataGridTextColumn { Header = "Amount %", Width = 80, Binding = new WBinding("Amount") });
        Grid.Columns.Add(new DataGridTextColumn { Header = "GTA 6 Action / Note", Width = new DataGridLength(1, DataGridLengthUnitType.Star), Binding = new WBinding("Note") });
        var hdr = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        hdr.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x35, 0x1B, 0x42))));
        hdr.Setters.Add(new Setter(ForegroundProperty, FindResource("Accent")));
        hdr.Setters.Add(new Setter(FontWeightProperty, FontWeights.Bold));
        hdr.Setters.Add(new Setter(PaddingProperty, new Thickness(8, 6, 8, 6)));
        Grid.ColumnHeaderStyle = hdr;
    }

    void ScenarioFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyBindingsFilter();

    void FilterBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyBindingsFilter();

    void ApplyBindingsFilter()
    {
        if (_cur == null) return;
        string q = FilterBox?.Text.Trim() ?? "";
        string scFilter = ScenarioFilterCombo?.SelectedItem as string ?? "All Scenarios";

        IEnumerable<Binding> list = _cur.Bindings;
        if (scFilter != "All Scenarios" && scFilter != "All")
        {
            list = list.Where(b =>
                string.IsNullOrEmpty(b.Scenario) ||
                b.Scenario.Equals("All", StringComparison.OrdinalIgnoreCase) ||
                b.Scenario.Equals(scFilter, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrEmpty(q))
        {
            list = list.Where(b =>
                InputNames.Pretty(b.Input).Contains(q, StringComparison.OrdinalIgnoreCase) ||
                b.Input.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                b.Target.ToString().Contains(q, StringComparison.OrdinalIgnoreCase) ||
                b.Activator.ToString().Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (b.Scenario ?? "").Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (b.Note ?? "").Contains(q, StringComparison.OrdinalIgnoreCase));
        }
        Grid.ItemsSource = list.ToList();
    }

    class PrettyConv : IValueConverter
    {
        public object Convert(object v, Type t, object p, System.Globalization.CultureInfo c)
        {
            var s = v as string ?? "";
            return string.IsNullOrEmpty(s) ? "[Click to Bind]" : InputNames.Pretty(s);
        }
        public object ConvertBack(object v, Type t, object p, System.Globalization.CultureInfo c) => throw new NotSupportedException();
    }

    void CaptureCell_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not Binding b) return;
        var btn = (Button)sender;
        btn.Content = "Press key…";
        _engine.Capture = name => Dispatcher.BeginInvoke(() => { b.Input = name; Changed(); });
    }

    void AddBinding_Click(object s, RoutedEventArgs e)
    {
        if (_cur == null) return;
        FilterBox.Text = "";
        string sc = ScenarioFilterCombo?.SelectedItem as string ?? _activeScenarioName;
        if (sc == "All Scenarios") sc = _activeScenarioName;
        var b = new Binding { Input = "", Target = PadTarget.A, Scenario = sc };
        _cur.Bindings.Add(b);
        ApplyBindingsFilter();
        Changed();
        Grid.SelectedItem = b;
        Grid.ScrollIntoView(b);
    }

    void RemoveBinding_Click(object s, RoutedEventArgs e)
    {
        foreach (var b in Grid.SelectedItems.OfType<Binding>().ToList()) _cur.Bindings.Remove(b);
        FilterBox_TextChanged(null, null);
        Changed();
    }

    // ================= Tab 2 & Tab 5: Controller/Mouse & Updates Panels =================
    void BuildSettings()
    {
        var sp = SettingsPanel;
        sp.Children.Clear();
        UpdatesPanel.Children.Clear();
        if (_cur == null) return;
        var p = _cur;

        Border Card(StackPanel host, string title, out StackPanel body)
        {
            body = new StackPanel();
            var b = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x14, 0x35)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x6D, 0x43, 0x7A)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 14),
                Child = body
            };
            body.Children.Add(new TextBlock
            {
                Text = title, FontWeight = FontWeights.ExtraBold, FontSize = 14,
                Foreground = (Brush)FindResource("Accent"), Margin = new Thickness(0, 0, 0, 10)
            });
            host.Children.Add(b);
            return b;
        }

        // Mouse → Analog Look Stick Card
        Card(sp, "MOUSE → ANALOG LOOK STICK SETTINGS", out var c2);
        c2.Children.Add(new TextBlock { Text = "Mouse Movement Drives Stick (Right Stick for Default/Classic, Left Stick for Left-Handed)", FontSize = 12, Foreground = (Brush)FindResource("Dim") });
        var st = new ComboBox
        {
            ItemsSource = Enum.GetValues<MouseStick>(), SelectedItem = p.Mouse.Stick,
            Width = 180, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 8)
        };
        st.SelectionChanged += (_, _) => { p.Mouse.Stick = (MouseStick)st.SelectedItem; Changed(); };
        c2.Children.Add(st);

        AddSlider(c2, "Horizontal Look Sensitivity (X)", 1, 60, p.Mouse.SensitivityX, v => p.Mouse.SensitivityX = v);
        AddSlider(c2, "Vertical Look Sensitivity (Y)", 1, 60, p.Mouse.SensitivityY, v => p.Mouse.SensitivityY = v);
        AddSlider(c2, "Anti-Deadzone % (Initial Stick Deflection — overcomes GTA 6 stick deadzone)", 0, 60, p.Mouse.AntiDeadzone, v => p.Mouse.AntiDeadzone = v);
        AddSlider(c2, "Aim Response Curve (1.0 = Linear, <1 = Faster Start, >1 = Precision Aim)", 0.3, 3.0, p.Mouse.Curve, v => p.Mouse.Curve = v);
        AddSlider(c2, "Mouse Smoothing Window (ms)", 0, 60, p.Mouse.SmoothingMs, v => p.Mouse.SmoothingMs = v);
        AddCheck(c2, "Invert Vertical Look (Y-Axis)", p.Mouse.InvertY, v => p.Mouse.InvertY = v);

        // Lock & Process Rules Card
        Card(sp, "WINDOWS KEY LOCK & TARGET GAME PROCESSES", out var c1);
        c1.Children.Add(new TextBlock { Text = "Windows Key Lock / Unlock Behavior", FontSize = 12, Foreground = (Brush)FindResource("Dim"), Margin = new Thickness(0, 0, 0, 4) });
        var um = new ComboBox
        {
            ItemsSource = new[] { "Toggle Mode — Tap [Windows Key] to Lock / Unlock", "Hold Mode — Hold [Windows Key] to temporarily Unlock" },
            SelectedIndex = (int)p.UnlockMode, Width = 380, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 8)
        };
        um.SelectionChanged += (_, _) => p.UnlockMode = (UnlockMode)um.SelectedIndex;
        c1.Children.Add(um);

        AddCheck(c1, "Block unmapped keyboard keys from reaching the game while locked", p.BlockUnmappedKeys, v => p.BlockUnmappedKeys = v);
        AddCheck(c1, "Auto-pause remapping when none of the target processes below is focused", p.OnlyWhenProcessFocused, v => p.OnlyWhenProcessFocused = v);

        var procHdr = new DockPanel { Margin = new Thickness(0, 8, 0, 4) };
        var runningCombo = new ComboBox { Width = 220, HorizontalAlignment = HorizontalAlignment.Right };
        void RefreshRunningProcs()
        {
            var names = new List<string>();
            try
            {
                var procs = Process.GetProcesses();
                foreach (var pr in procs)
                {
                    try
                    {
                        if (pr.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(pr.ProcessName))
                        {
                            names.Add(pr.ProcessName + ".exe");
                        }
                    }
                    catch
                    {
                        // Some protected processes (anticheats like EAC/Vanguard, system svchost/csrss)
                        // throw Win32Exception (Access Denied) or InvalidOperationException (Process Exited)
                        // when accessing MainWindowHandle. Safely ignore them.
                    }
                    finally
                    {
                        try { pr.Dispose(); } catch { }
                    }
                }
            }
            catch
            {
                // Fallback if process enumeration as a whole fails
            }

            var distinctNames = names
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            distinctNames.Insert(0, "+ Add running app...");
            runningCombo.ItemsSource = distinctNames;
            runningCombo.SelectedIndex = 0;
        }
        RefreshRunningProcs();
        runningCombo.DropDownOpened += (_, _) => RefreshRunningProcs();
        DockPanel.SetDock(runningCombo, Dock.Right);
        procHdr.Children.Add(runningCombo);
        procHdr.Children.Add(new TextBlock
        {
            Text = "Target Game Processes (one .exe per line)",
            FontSize = 12, Foreground = (Brush)FindResource("Dim"), VerticalAlignment = VerticalAlignment.Center
        });
        c1.Children.Add(procHdr);

        var procs = new TextBox
        {
            Text = string.Join(Environment.NewLine, p.Processes),
            AcceptsReturn = true, Height = 85, Margin = new Thickness(0, 2, 0, 4),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        procs.TextChanged += (_, _) => p.Processes = procs.Text.Split('\n').Select(x => x.Trim()).Where(x => x != "").ToList();
        runningCombo.SelectionChanged += (_, _) =>
        {
            if (runningCombo.SelectedIndex > 0 && runningCombo.SelectedItem is string exe)
            {
                if (!p.Processes.Contains(exe, StringComparer.OrdinalIgnoreCase))
                {
                    p.Processes.Add(exe);
                    procs.Text = string.Join(Environment.NewLine, p.Processes);
                }
                runningCombo.SelectedIndex = 0;
            }
        };
        c1.Children.Add(procs);

        // Updates & Driver Card in Tab 5
        Card(UpdatesPanel, "GITHUB AUTO-UPDATER & VIGEMBUS DRIVER", out var c3);
        c3.Children.Add(new TextBlock
        {
            Text = $"Installed Version: {Updater.CurrentVersionString}   ·   ViGEmBus Driver: {(DriverHelper.IsViGEmInstalled() ? "Installed ✓" : "Not Installed")}",
            Foreground = Brushes.White,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 10)
        });
        AddCheck(c3, "Automatically check GitHub for new updates every time re-B.U.N.S opens", AppSettings.Instance.AutoCheckUpdates, v =>
        {
            AppSettings.Instance.AutoCheckUpdates = v;
            AppSettings.Instance.Save();
        });
        var showWizardBtn = new Button
        {
            Content = "🎮  Open 4-Preset Selector Wizard (1/4 ★ re-B.U.N.S · 2/4 Default · 3/4 Classic · 4/4 Left-Handed)",
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(14, 7, 14, 7),
            Margin = new Thickness(0, 6, 0, 6)
        };
        showWizardBtn.Click += (_, _) => ShowLayoutPickerDialog();
        c3.Children.Add(showWizardBtn);
        c3.Children.Add(new TextBlock { Text = "GitHub Repository (owner/repo)", FontSize = 12, Foreground = (Brush)FindResource("Dim"), Margin = new Thickness(0, 8, 0, 2) });
        var repoBox = new TextBox { Text = AppSettings.Instance.GitHubRepo, Width = 340, HorizontalAlignment = HorizontalAlignment.Left };
        repoBox.LostFocus += (_, _) => { AppSettings.Instance.GitHubRepo = repoBox.Text.Trim(); AppSettings.Instance.Save(); };
        c3.Children.Add(repoBox);

        c3.Children.Add(new TextBlock
        {
            Text = "Optional GitHub Token (only needed on other PCs while repository is Private; uses gh/git auth automatically on this PC)",
            FontSize = 11.5,
            Foreground = (Brush)FindResource("Dim"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 2)
        });
        var tokBox = new TextBox { Text = AppSettings.Instance.GitHubToken, Width = 440, HorizontalAlignment = HorizontalAlignment.Left };
        tokBox.LostFocus += (_, _) => { AppSettings.Instance.GitHubToken = tokBox.Text.Trim(); AppSettings.Instance.Save(); };
        c3.Children.Add(tokBox);

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        var chkNow = new Button { Content = "⟳  Check for GitHub Updates Now", FontWeight = FontWeights.Bold };
        chkNow.Click += async (_, _) => await CheckForUpdatesAsync(silentIfUpToDate: false);
        var drvNow = new Button { Content = "🎮  Verify / Reinstall ViGEmBus Driver" };
        drvNow.Click += DriverBtn_Click;
        btnRow.Children.Add(chkNow);
        btnRow.Children.Add(drvNow);
        c3.Children.Add(btnRow);

        static void AddCheck(StackPanel parent, string t, bool v, Action<bool> set)
        {
            var c = new CheckBox { Content = t, IsChecked = v };
            c.Click += (_, _) => set(c.IsChecked == true);
            parent.Children.Add(c);
        }
        static void AddSlider(StackPanel parent, string t, double min, double max, double v, Action<double> set)
        {
            var lbl = new TextBlock
            {
                Text = $"{t}: {v:0.##}",
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 2),
                FontSize = 12.5
            };
            var s = new Slider { Minimum = min, Maximum = max, Value = v, Width = 440, HorizontalAlignment = HorizontalAlignment.Left };
            s.ValueChanged += (_, e) => { set(Math.Round(e.NewValue, 2)); lbl.Text = $"{t}: {e.NewValue:0.##}"; };
            parent.Children.Add(lbl);
            parent.Children.Add(s);
        }
    }

    // ================= Profiles =================
    void ProfileList_SelectionChanged(object s, SelectionChangedEventArgs e)
    {
        if (ProfileList.SelectedItem is not Profile p) return;
        SyncProfile();
        _cur = p;
        _controllerType = p.ControllerType;
        _engine.ControllerType = _controllerType;
        UpdateControllerTypeButtons();
        AppSettings.Instance.LastSelectedProfile = p.Name;
        AppSettings.Instance.Save();
        _lastJson = "";
        FilterBox.Text = "";
        Grid.ItemsSource = _cur.Bindings;
        BuildSettings();
        BuildKeyInspector();
        UpdateSchemeHeader();
        Changed();
    }

    void Changed()
    {
        Grid.Items.Refresh();
        RefreshPadLabels();
        SyncProfile();
    }

    void SyncProfile()
    {
        if (_cur == null) return;
        var json = JsonSerializer.Serialize(_cur);
        if (json == _lastJson) return;
        _lastJson = json;
        _engine.SetProfile(_cur);
        try { _cur.Save(); } catch { }
        RefreshPadLabels();
    }

    void AddProfile(Profile p)
    {
        while (_profiles.Any(x => x.Name == p.Name)) p.Name += " (2)";
        p.Save();
        _profiles.Add(p);
        ProfileList.Items.Refresh();
        ProfileList.SelectedItem = p;
    }

    void New_Click(object s, RoutedEventArgs e) => AddProfile(new Profile { Name = "Custom Profile" });
    void Dup_Click(object s, RoutedEventArgs e) { if (_cur != null) { var c = _cur.Clone(); c.LayoutId = GtaLayoutId.Custom; c.Name += " Copy"; AddProfile(c); } }
    void Del_Click(object s, RoutedEventArgs e)
    {
        if (_cur == null || MessageBox.Show($"Delete profile '{_cur.Name}'?", "re-B.U.N.S", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        try { File.Delete(_cur.FilePath); } catch { }
        _profiles.Remove(_cur);
        if (_profiles.Count == 0) { var g = Profile.Gta6Default(); g.Save(); _profiles.Add(g); }
        ProfileList.Items.Refresh();
        ProfileList.SelectedIndex = 0;
    }

    void ResetGta_Click(object s, RoutedEventArgs e)
    {
        var id = (_cur?.LayoutId is not null and not GtaLayoutId.Custom) ? _cur.LayoutId : GtaLayoutId.Default;
        var info = Profile.GtaLayouts.FirstOrDefault(x => x.Id == id) ?? Profile.GtaLayouts[0];
        if (MessageBox.Show($"Restore Control Scheme '{info.SchemeName} ({info.Index}/3)' back to its default GTA 6 keybinds?",
            "Restore Default", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        SelectOrCreateGtaLayout(id, resetToDefault: true);
    }

    void Import_Click(object s, RoutedEventArgs e)
    {
        string initDir = @"C:\Users\Public\Documents\reWASD\Profiles";
        if (!Directory.Exists(initDir)) initDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var dlg = new OpenFileDialog { Filter = "reWASD config (*.rewasd)|*.rewasd", InitialDirectory = initDir };
        if (dlg.ShowDialog() != true) return;
        try { AddProfile(RewasdImporter.Import(dlg.FileName)); }
        catch (Exception ex) { MessageBox.Show("Import failed: " + ex.Message); }
    }

    void Folder_Click(object s, RoutedEventArgs e) => Process.Start("explorer.exe", Profile.Folder);

    // ================= Engine / Live UI =================
    async void StartBtn_Click(object s, RoutedEventArgs e)
    {
        if (_engine.Running)
        {
            _engine.Stop();
        }
        else
        {
            SyncProfile();
            _engine.ControllerType = _controllerType;
            // Show the persistent Windows Key / Ctrl+Alt+Delete popup first (does NOT auto-close; locks as soon as user clicks Got It!)
            ShowVirtualPadStartedPopup();
            if (!_engine.Start())
            {
                if (MessageBox.Show(_engine.Error + "\n\nWould you like re-B.U.N.S to install the ViGEmBus Virtual Controller Driver right now?",
                    "ViGEmBus Driver Required", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    var (ok, msg) = await DriverHelper.InstallViGEmAsync(st => StatusText.Text = st);
                    RefreshDriverBadge();
                    if (ok) _engine.Start();
                    else MessageBox.Show(msg, "re-B.U.N.S");
                }
            }
        }
        UpdateStatus();
    }

    void ShowVirtualPadStartedPopup()
    {
        try
        {
            var popup = new VirtualPadStartedWindow(_cur?.Name ?? "★ re-B.U.N.S", _controllerType) { Owner = this };
            popup.ShowDialog();
        }
        catch { }
    }

    void UpdateStatus()
    {
        StartBtn.Content = _engine.Running ? Localization.T("StopPad") : Localization.T("StartPad");
        StartBtn.Background = _engine.Running
            ? new SolidColorBrush(Color.FromRgb(0x9A, 0x28, 0x42))
            : new SolidColorBrush(Color.FromRgb(0x1E, 0x8A, 0x5A));
        StartBtn.BorderBrush = _engine.Running
            ? new SolidColorBrush(Color.FromRgb(0xFF, 0x80, 0x9B))
            : new SolidColorBrush(Color.FromRgb(0x3D, 0xDC, 0x97));

        if (!string.Equals(_activeScenarioName, _engine.CurrentScenario, StringComparison.OrdinalIgnoreCase))
        {
            _activeScenarioName = _engine.CurrentScenario;
            _previewCategory = _activeScenarioName.ToUpperInvariant();
            if (_schematicHeaderText != null)
                _schematicHeaderText.Text = _previewCategory + (_controllerType == ControllerType.PlayStation ? "  ·  PLAYSTATION" : "  ·  XBOX");
            foreach (var pr in _previewRows)
            {
                bool sel = pr.Name.Equals(_activeScenarioName, StringComparison.OrdinalIgnoreCase);
                pr.Dot.Visibility = sel ? Visibility.Visible : Visibility.Collapsed;
                pr.Label.FontWeight = sel ? FontWeights.Bold : FontWeights.Normal;
                pr.Label.Foreground = sel ? Brushes.White : (Brush)FindResource("Dim");
            }
        }

        string text; Color c;
        if (!_engine.Running) { text = Localization.T("Stopped"); c = Color.FromRgb(0x3B, 0x1D, 0x47); }
        else if (!_engine.ProcessOk) { text = Localization.T("WaitingGame"); c = Color.FromRgb(0x6B, 0x5A, 0x1E); }
        else if (_engine.Locked) { text = $"{Localization.T("Running")} · {_engine.CurrentScenario.ToUpperInvariant()}"; c = Color.FromRgb(0x1E, 0x8A, 0x5A); }
        else { text = Localization.T("Paused"); c = Color.FromRgb(0x8A, 0x3A, 0x2E); }
        StatusText.Text = text;
        StatusPill.Background = new SolidColorBrush(c);
        if (_tray != null) _tray.Text = ("re-B.U.N.S – " + text)[..Math.Min(63, ("re-B.U.N.S – " + text).Length)];
    }

    void Live()
    {
        var s = _engine.Snapshot;
        foreach (var (t, sh) in _shapes)
        {
            bool on = s.Has(t)
                || (t == PadTarget.LT && s.LT > 0) || (t == PadTarget.RT && s.RT > 0);
            sh.Fill = on ? OnFill : DarkBtnFill;
        }

        double lxN = s.LX / 32767.0, lyN = -s.LY / 32767.0;
        double rxN = s.RX / 32767.0, ryN = -s.RY / 32767.0;

        Canvas.SetLeft(_padLsDot, 465 + lxN * 18 - 4);
        Canvas.SetTop(_padLsDot, 250 + lyN * 18 - 4);
        Canvas.SetLeft(_padRsDot, 585 + rxN * 18 - 4);
        Canvas.SetTop(_padRsDot, 312 + ryN * 18 - 4);

        Canvas.SetLeft(LsDot, 48 + lxN * 38 - 5);
        Canvas.SetTop(LsDot, 48 + lyN * 38 - 5);
        Canvas.SetLeft(RsDot, 48 + rxN * 38 - 5);
        Canvas.SetTop(RsDot, 48 + ryN * 38 - 5);

        LsText.Text = $"LS: {s.LX,6}, {s.LY,6}";
        RsText.Text = $"RS: {s.RX,6}, {s.RY,6}";
        LiveText.Text = $"LT: {s.LT,3}   RT: {s.RT,3}\nButtons: {s.FormatButtons()}";
        FgProc.Text = string.IsNullOrEmpty(_engine.ForegroundProcess) ? "" : "Focused: " + _engine.ForegroundProcess;

        foreach (var cap in _kbCaps)
        {
            if (cap.Code <= 0) continue;
            bool down = _engine.IsRawDown(cap.Code);
            cap.TopText.Foreground = down
                ? new SolidColorBrush(Color.FromRgb(0x3D, 0xDC, 0x97))
                : Brushes.White;
        }
    }

    void SetupTray()
    {
        try
        {
            System.Drawing.Icon appIcon = null;
            try
            {
                var iconUri = new Uri("pack://application:,,,/re-B.U.N.S.ico", UriKind.Absolute);
                var streamInfo = Application.GetResourceStream(iconUri);
                if (streamInfo?.Stream != null)
                {
                    using (streamInfo.Stream)
                    {
                        appIcon = new System.Drawing.Icon(streamInfo.Stream);
                    }
                }
            }
            catch { }

            if (appIcon == null)
            {
                try
                {
                    if (!string.IsNullOrEmpty(Environment.ProcessPath))
                        appIcon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath);
                }
                catch { }
            }

            appIcon ??= System.Drawing.SystemIcons.Application;

            _tray = new System.Windows.Forms.NotifyIcon
            {
                Icon = appIcon,
                Visible = true,
                Text = "re-B.U.N.S"
            };
            _tray.DoubleClick += (_, _) => { Show(); WindowState = WindowState.Normal; Activate(); };
            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Open re-B.U.N.S", null, (_, _) => { Show(); WindowState = WindowState.Normal; Activate(); });
            menu.Items.Add("Start / Stop Virtual Controller", null, (_, _) => StartBtn_Click(null, null));
            menu.Items.Add("Check for GitHub Updates", null, async (_, _) => await CheckForUpdatesAsync(false));
            menu.Items.Add("-");
            menu.Items.Add("Exit", null, (_, _) => Close());
            _tray.ContextMenuStrip = menu;
            StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) Hide(); };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to setup system tray: {ex.Message}");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        SyncProfile();
        _engine.Dispose();
        try
        {
            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
            }
        }
        catch { }
        base.OnClosed(e);
    }
}
