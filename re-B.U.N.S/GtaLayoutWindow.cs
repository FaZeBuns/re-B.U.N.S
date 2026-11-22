using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ReBuns;

public class GtaLayoutWindow : Window
{
    public GtaLayoutId SelectedLayout { get; private set; }
    public bool ResetRequested { get; private set; }

    static readonly Brush DarkInk = new SolidColorBrush(Color.FromRgb(0x1A, 0x0C, 0x24));
    static readonly Brush PlumCard = new SolidColorBrush(Color.FromArgb(200, 0x2E, 0x16, 0x3A));
    static readonly Brush ActiveCard = new SolidColorBrush(Color.FromArgb(235, 0x4B, 0x23, 0x52));

    public GtaLayoutWindow(GtaLayoutId currentLayout)
    {
        int total = Profile.GtaLayouts.Length;
        SelectedLayout = currentLayout == GtaLayoutId.Custom ? GtaLayoutId.ReBuns : currentLayout;
        Title = "re-B.U.N.S — Select GTA 6 Control Scheme (1/4 ★ re-B.U.N.S · 2/4 Default · 3/4 Classic · 4/4 Left-Handed)";
        Width = 1280;
        Height = 690;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = new LinearGradientBrush(Color.FromRgb(0x72, 0x38, 0x6C), Color.FromRgb(0x29, 0x14, 0x35), 65);
        Foreground = Brushes.White;
        TextElement.SetForeground(this, Brushes.White);

        var outer = new Border
        {
            Margin = new Thickness(14),
            Background = new SolidColorBrush(Color.FromArgb(200, 0x22, 0x10, 0x2C)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(130, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(20)
        };

        var dock = new DockPanel();

        // Top Header
        var hdr = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        DockPanel.SetDock(hdr, Dock.Top);
        hdr.Children.Add(new TextBlock
        {
            Text = "SETTINGS   /   CONTROL SCHEME",
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xF6, 0x87, 0xB3))
        });
        hdr.Children.Add(new TextBlock
        {
            Text = "SELECT YOUR GTA 6 CONTROL SCHEME (1 / 4 ★ re-B.U.N.S  ·  2 / 4 DEFAULT  ·  3 / 4 CLASSIC  ·  4 / 4 LEFT-HANDED)",
            FontSize = 20.5,
            FontWeight = FontWeights.ExtraBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 3, 0, 3)
        });
        hdr.Children.Add(new TextBlock
        {
            Text = "Preset 1/4 (★ re-B.U.N.S) is your custom GTA 6 keybind setup. Presets 2/4, 3/4, and 4/4 match the 3 GTA 6 in-game Control Schemes. Your choice is saved automatically.",
            FontSize = 12.5,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE2, 0xD4, 0xE8)),
            TextWrapping = TextWrapping.Wrap
        });
        dock.Children.Add(hdr);

        // Bottom Footer
        var footer = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        DockPanel.SetDock(footer, Dock.Bottom);

        var closeBtn = new Button
        {
            Content = "CONTINUE WITH SELECTED (B)",
            Padding = new Thickness(20, 8, 20, 8),
            FontWeight = FontWeights.Bold
        };
        closeBtn.Click += (_, _) =>
        {
            AppSettings.Instance.HasCompletedInitialSetup = true;
            AppSettings.Instance.Save();
            DialogResult = false;
            Close();
        };
        DockPanel.SetDock(closeBtn, Dock.Right);
        footer.Children.Add(closeBtn);

        var hintTxt = new TextBlock
        {
            Text = "Tip: This screen only pops up the first time you open re-B.U.N.S. You can reopen it anytime using 'Compare All 4 Presets'.",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0xD6, 0xC4, 0xDE)),
            VerticalAlignment = VerticalAlignment.Center
        };
        footer.Children.Add(hintTxt);
        dock.Children.Add(footer);

        // 4 Columns for the 4 Presets
        var grid = new UniformGrid { Columns = total, Rows = 1 };
        foreach (var info in Profile.GtaLayouts)
        {
            bool isCur = info.Id == currentLayout;
            var card = new Border
            {
                Margin = new Thickness(5, 2, 5, 2),
                Background = isCur ? ActiveCard : PlumCard,
                BorderBrush = isCur ? Brushes.White : new SolidColorBrush(Color.FromArgb(100, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(isCur ? 2.5 : 1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(12, 10, 12, 10)
            };

            var cdock = new DockPanel();

            // White Gradient Header Bar inside each card
            var whiteBar = new Border
            {
                Background = isCur
                    ? new LinearGradientBrush(Colors.White, Color.FromRgb(0xC8, 0xB2, 0xD2), 0)
                    : new SolidColorBrush(Color.FromArgb(55, 0xFF, 0xFF, 0xFF)),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 0, 8)
            };
            DockPanel.SetDock(whiteBar, Dock.Top);
            var wbDock = new DockPanel();
            var idxTxt = new TextBlock
            {
                Text = $"{info.Index} / {total}",
                FontWeight = FontWeights.ExtraBold,
                FontSize = 13.5,
                Foreground = isCur ? DarkInk : Brushes.White
            };
            DockPanel.SetDock(idxTxt, Dock.Right);
            wbDock.Children.Add(idxTxt);
            wbDock.Children.Add(new TextBlock
            {
                Text = info.SchemeName,
                FontWeight = FontWeights.ExtraBold,
                FontSize = 15,
                Foreground = isCur ? DarkInk : Brushes.White
            });
            whiteBar.Child = wbDock;
            cdock.Children.Add(whiteBar);

            // Bottom buttons
            var btnStack = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            DockPanel.SetDock(btnStack, Dock.Bottom);

            var selectBtn = new Button
            {
                Content = isCur ? $"✓ ACTIVE ({info.Index}/{total})" : $"SELECT {info.SchemeName.ToUpper()} ({info.Index}/{total})",
                Padding = new Thickness(8, 8, 8, 8),
                FontSize = 11.5,
                FontWeight = FontWeights.ExtraBold,
                Background = isCur ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x3D, 0x1F, 0x4C)),
                Foreground = isCur ? DarkInk : Brushes.White,
                BorderBrush = Brushes.White
            };
            selectBtn.Click += (_, _) =>
            {
                SelectedLayout = info.Id;
                ResetRequested = false;
                AppSettings.Instance.HasCompletedInitialSetup = true;
                AppSettings.Instance.Save();
                DialogResult = true;
                Close();
            };
            btnStack.Children.Add(selectBtn);

            var resetBtn = new Button
            {
                Content = "↺ RESTORE DEFAULT (X)",
                Padding = new Thickness(6, 5, 6, 5),
                Margin = new Thickness(0, 4, 0, 0),
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold
            };
            resetBtn.Click += (_, _) =>
            {
                if (MessageBox.Show($"Restore '{info.SchemeName} ({info.Index}/{total})' back to its default keybinds?",
                    "Restore Default", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    SelectedLayout = info.Id;
                    ResetRequested = true;
                    AppSettings.Instance.HasCompletedInitialSetup = true;
                    AppSettings.Instance.Save();
                    DialogResult = true;
                    Close();
                }
            };
            btnStack.Children.Add(resetBtn);
            cdock.Children.Add(btnStack);

            // All 18 Callout rows fitted cleanly without any scrollbar
            var listStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            foreach (var c in info.Callouts)
            {
                string actionText = c.HoldLabel == null
                    ? c.TapLabel
                    : $"{c.TapLabel} · ◎ {c.HoldLabel}";
                var row = new DockPanel { Margin = new Thickness(0, 1.3, 0, 1.3) };
                var badge = new Border
                {
                    Width = 28,
                    Background = new SolidColorBrush(Color.FromArgb(110, 0x14, 0x08, 0x1C)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(165, 0xFF, 0xFF, 0xFF)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(9),
                    Padding = new Thickness(2, 0.5, 2, 0.5),
                    Margin = new Thickness(0, 0, 6, 0),
                    Child = new TextBlock
                    {
                        Text = c.ButtonGlyph,
                        FontSize = 9,
                        FontWeight = FontWeights.ExtraBold,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                };
                DockPanel.SetDock(badge, Dock.Left);
                row.Children.Add(badge);
                row.Children.Add(new TextBlock
                {
                    Text = actionText,
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                listStack.Children.Add(row);
            }
            cdock.Children.Add(listStack);

            card.Child = cdock;
            grid.Children.Add(card);
        }

        dock.Children.Add(grid);
        outer.Child = dock;
        Content = outer;
    }
}

/// <summary>Popup shown whenever the Virtual Pad is started, informing the user of Windows Key (Pause/Play) and Ctrl+Alt+Delete (Stop). Does NOT auto-close.</summary>
public class VirtualPadStartedWindow : Window
{
    public VirtualPadStartedWindow(string activeSchemeName)
    {
        Title = "re-B.U.N.S — Virtual Pad Controls";
        Width = 540;
        Height = 310;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        TextElement.SetForeground(this, Brushes.White);

        var card = new Border
        {
            Margin = new Thickness(10),
            Background = new LinearGradientBrush(Color.FromRgb(0x3B, 0x1D, 0x47), Color.FromRgb(0x1E, 0x0E, 0x26), 65),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0xDC, 0x97)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(24, 20, 24, 20)
        };

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = "🎮   VIRTUAL PAD READY",
            FontSize = 13,
            FontWeight = FontWeights.ExtraBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x3D, 0xDC, 0x97))
        });
        stack.Children.Add(new TextBlock
        {
            Text = $"Active Preset: {activeSchemeName}",
            FontSize = 18,
            FontWeight = FontWeights.ExtraBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 4, 0, 14)
        });

        // Hotkey Row 1: Windows Key -> Pause / Play
        stack.Children.Add(MakeHotkeyRow(
            "WINDOWS KEY",
            "Pause & Play Virtual Pad Controls",
            "Unlocks/locks mouse & keyboard without opening the Windows Start Menu."));

        // Hotkey Row 2: Ctrl + Alt + Delete -> Stop Virtual Pad
        stack.Children.Add(MakeHotkeyRow(
            "CTRL + ALT + DELETE",
            "Stop Virtual Pad Completely",
            "Immediately stops and disconnects the virtual Xbox controller."));

        var footer = new DockPanel { Margin = new Thickness(0, 14, 0, 0) };
        var okBtn = new Button
        {
            Content = "GOT IT — START & LOCK",
            Padding = new Thickness(20, 8, 20, 8),
            FontWeight = FontWeights.ExtraBold,
            Background = Brushes.White,
            Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x0C, 0x24)),
            BorderBrush = Brushes.White
        };
        okBtn.Click += (_, _) => { DialogResult = true; Close(); };
        DockPanel.SetDock(okBtn, Dock.Right);
        footer.Children.Add(okBtn);

        footer.Children.Add(new TextBlock
        {
            Text = "Click 'Got It' (or press Enter / Space) to lock controls.",
            FontSize = 11.5,
            Foreground = new SolidColorBrush(Color.FromRgb(0xD6, 0xC4, 0xDE)),
            VerticalAlignment = VerticalAlignment.Center
        });
        stack.Children.Add(footer);

        card.Child = stack;
        Content = card;

        KeyDown += (_, e) =>
        {
            if (e.Key is Key.Enter or Key.Space or Key.Escape)
            {
                DialogResult = true;
                Close();
            }
        };
    }

    static Border MakeHotkeyRow(string keyLabel, string title, string sub)
    {
        var row = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(130, 0x16, 0x0A, 0x1D)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(100, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 8)
        };
        var dock = new DockPanel();
        var keyPill = new Border
        {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 5, 10, 5),
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = keyLabel,
                FontSize = 11.5,
                FontWeight = FontWeights.ExtraBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x0C, 0x24))
            }
        };
        DockPanel.SetDock(keyPill, Dock.Left);
        dock.Children.Add(keyPill);

        var txtStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        txtStack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 13,
            FontWeight = FontWeights.ExtraBold,
            Foreground = Brushes.White
        });
        txtStack.Children.Add(new TextBlock
        {
            Text = sub,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0xD6, 0xC4, 0xDE))
        });
        dock.Children.Add(txtStack);
        row.Child = dock;
        return row;
    }
}
