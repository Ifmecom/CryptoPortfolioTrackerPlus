using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Media;

namespace CryptoPortfolioTracker.Dialogs;

/// <summary>
/// Toont de uitleg van één pagina (v1.48) — geopend via de ⓘ-knop rechtsboven in de header.
/// Alleen opmaak; de inhoud komt uit <see cref="PageHelpCatalog"/>.
/// </summary>
public sealed class PageHelpDialog : ContentDialog
{
    public PageHelpDialog(PageHelp help, ElementTheme theme)
    {
        RequestedTheme    = theme;
        CloseButtonText   = "Sluiten";
        DefaultButton     = ContentDialogButton.Close;
        Title             = BuildTitle(help.Title);
        Content           = BuildBody(help);
        Resources["ContentDialogMaxWidth"] = 820.0;
    }

    private static UIElement BuildTitle(string title)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        panel.Children.Add(new FontIcon
        {
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            Glyph      = "",
            FontSize   = 22,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.DarkGoldenrod),
        });
        panel.Children.Add(new TextBlock
        {
            Text       = $"Uitleg — {title}",
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.DarkGoldenrod),
        });
        return panel;
    }

    private static UIElement BuildBody(PageHelp help)
    {
        var stack = new StackPanel { Spacing = 12, Padding = new Thickness(0, 0, 14, 0) };

        stack.Children.Add(new TextBlock
        {
            Text         = help.Intro,
            TextWrapping = TextWrapping.Wrap,
            FontSize     = 15,
            Margin       = new Thickness(0, 0, 0, 4),
        });

        foreach (var section in help.Sections)
            stack.Children.Add(BuildSection(section));

        return new ScrollViewer
        {
            Content   = stack,
            MaxHeight = 620,
            Width     = 740,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
    }

    private static UIElement BuildSection(PageHelpSection section)
    {
        var inner = new StackPanel { Spacing = 6 };
        inner.Children.Add(new TextBlock
        {
            Text       = $"{section.Icon}  {section.Heading}",
            FontSize   = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.DarkGoldenrod),
        });

        foreach (var point in section.Points)
        {
            var row = new Grid { ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            row.Children.Add(new TextBlock { Text = "•", Opacity = 0.7 });
            var text = new TextBlock { Text = point, TextWrapping = TextWrapping.Wrap, LineHeight = 20 };
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            inner.Children.Add(row);
        }

        return new Border
        {
            Child           = inner,
            Padding         = new Thickness(14, 10, 14, 12),
            CornerRadius    = new CornerRadius(8),
            // Halftransparant grijs: werkt in licht én donker thema.
            Background      = new SolidColorBrush(Windows.UI.Color.FromArgb(0x18, 0x80, 0x80, 0x80)),
            BorderBrush     = new SolidColorBrush(Windows.UI.Color.FromArgb(0x40, 0x80, 0x80, 0x80)),
            BorderThickness = new Thickness(1),
        };
    }
}
