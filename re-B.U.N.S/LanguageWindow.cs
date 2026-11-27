using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ReBuns;

public class LanguageWindow : Window
{
    public string SelectedLanguage { get; private set; } = "en";

    static readonly Brush DarkInk = new SolidColorBrush(Color.FromRgb(0x1A, 0x0C, 0x24));
    static readonly Brush PlumCard = new SolidColorBrush(Color.FromArgb(220, 0x2E, 0x16, 0x3A));
    static readonly Brush ActiveCard = new SolidColorBrush(Color.FromArgb(245, 0x56, 0x24, 0x60));
    static readonly Brush NeonCyan = new SolidColorBrush(Color.FromRgb(0x00, 0xE5, 0xFF));
    static readonly Brush NeonGreen = new SolidColorBrush(Color.FromRgb(0x3D, 0xDC, 0x97));

    public LanguageWindow(string currentLanguage)
    {
        SelectedLanguage = string.IsNullOrWhiteSpace(currentLanguage) ? "en" : currentLanguage;
        Title = "re-B.U.N.S — Select Language";
        Width = 860;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = new LinearGradientBrush(Color.FromRgb(0x72, 0x38, 0x6C), Color.FromRgb(0x22, 0x10, 0x2C), 65);
        Foreground = Brushes.White;
        TextElement.SetForeground(this, Brushes.White);

        var outer = new Border
        {
            Margin = new Thickness(16),
            Background = new SolidColorBrush(Color.FromArgb(225, 0x1E, 0x0C, 0x28)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(140, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(24)
        };

        var dock = new DockPanel();

        // Top Header
        var hdr = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
        DockPanel.SetDock(hdr, Dock.Top);
        hdr.Children.Add(new TextBlock
        {
            Text = "WELCOME TO re-B.U.N.S   ·   SELECT LANGUAGE",
            FontSize = 13, FontWeight = FontWeights.ExtraBold,
            Foreground = NeonCyan
        });
        hdr.Children.Add(new TextBlock
        {
            Text = "Choose Your Language / Επιλέξτε τη γλώσσα σας",
            FontSize = 24, FontWeight = FontWeights.ExtraBold,
            Foreground = Brushes.White, Margin = new Thickness(0, 4, 0, 4)
        });
        hdr.Children.Add(new TextBlock
        {
            Text = "You can change your language anytime from the top bar dropdown in the main application.",
            FontSize = 12.5, Foreground = new SolidColorBrush(Color.FromRgb(0xDF, 0xCE, 0xE8))
        });
        dock.Children.Add(hdr);

        // Bottom Continue Button
        var bottomBar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        DockPanel.SetDock(bottomBar, Dock.Bottom);

        var continueBtn = new Button
        {
            Content = "CONFIRM & CONTINUE  ▶",
            FontSize = 14, FontWeight = FontWeights.ExtraBold,
            Padding = new Thickness(24, 10, 24, 10),
            Background = NeonGreen, Foreground = DarkInk,
            BorderBrush = Brushes.White, BorderThickness = new Thickness(1.5),
            Cursor = Cursors.Hand
        };
        continueBtn.Click += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
        bottomBar.Children.Add(continueBtn);
        dock.Children.Add(bottomBar);

        // Language Grid
        var grid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 6, 0, 6) };
        var cards = new List<(Border border, string code)>();

        foreach (var lang in Localization.SupportedLanguages)
        {
            var isCurrent = lang.Code.Equals(SelectedLanguage, StringComparison.OrdinalIgnoreCase);
            var card = new Border
            {
                Background = isCurrent ? ActiveCard : PlumCard,
                BorderBrush = isCurrent ? NeonCyan : new SolidColorBrush(Color.FromArgb(90, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(isCurrent ? 2 : 1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(6),
                MinHeight = 66,
                Cursor = Cursors.Hand
            };

            var row = new DockPanel { LastChildFill = true };
            var badge = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x3D, 0x1E, 0x4E)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(120, 0x00, 0xE5, 0xFF)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Width = 44,
                Height = 36,
                Margin = new Thickness(0, 0, 14, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = lang.Code.ToUpperInvariant(),
                    FontSize = 14,
                    FontWeight = FontWeights.ExtraBold,
                    Foreground = NeonCyan,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            DockPanel.SetDock(badge, Dock.Left);
            row.Children.Add(badge);

            var checkBlock = new TextBlock
            {
                Text = "✓ SELECTED",
                FontSize = 11, FontWeight = FontWeights.Bold,
                Foreground = NeonCyan,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                Visibility = isCurrent ? Visibility.Visible : Visibility.Collapsed
            };
            DockPanel.SetDock(checkBlock, Dock.Right);
            row.Children.Add(checkBlock);

            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            textStack.Children.Add(new TextBlock
            {
                Text = lang.NativeName,
                FontSize = 15.5, FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 2)
            });
            textStack.Children.Add(new TextBlock
            {
                Text = lang.EnglishName,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0xC8, 0xAF, 0xD4))
            });
            row.Children.Add(textStack);
            card.Child = row;

            string code = lang.Code;
            card.MouseLeftButtonDown += (_, _) =>
            {
                SelectedLanguage = code;
                foreach (var c in cards)
                {
                    bool sel = c.code == code;
                    c.border.Background = sel ? ActiveCard : PlumCard;
                    c.border.BorderBrush = sel ? NeonCyan : new SolidColorBrush(Color.FromArgb(90, 0xFF, 0xFF, 0xFF));
                    c.border.BorderThickness = new Thickness(sel ? 2 : 1);
                    if (c.border.Child is DockPanel dp)
                    {
                        foreach (UIElement el in dp.Children)
                        {
                            if (el is TextBlock tb && tb.Text == "✓ SELECTED")
                                tb.Visibility = sel ? Visibility.Visible : Visibility.Collapsed;
                        }
                    }
                }
                Localization.CurrentLanguage = code;
                continueBtn.Content = $"{Localization.T("StartPad")}  ▶";
            };

            card.MouseEnter += (_, _) =>
            {
                if (SelectedLanguage != code)
                    card.Background = new SolidColorBrush(Color.FromArgb(240, 0x3E, 0x1E, 0x4C));
            };
            card.MouseLeave += (_, _) =>
            {
                if (SelectedLanguage != code)
                    card.Background = PlumCard;
            };

            cards.Add((card, code));
            grid.Children.Add(card);
        }

        dock.Children.Add(grid);
        outer.Child = dock;
        Content = outer;
    }
}
