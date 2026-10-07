using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Media;

namespace CryptoPortfolioTracker.Dialogs;

/// <summary>
/// API-sleutels en modelnamen per AI-aanbieder (v1.49). Sleutels worden DPAPI-versleuteld opgeslagen
/// (gebonden aan deze Windows-gebruiker) en nooit teruggetoond; een leeg veld laat de bestaande sleutel staan.
/// </summary>
public sealed class AiKeysDialog : ContentDialog
{
    private readonly IAiResearchService _service;
    private readonly List<(AiApiProvider Provider, PasswordBox Key, TextBox Model, CheckBox Remove)> _rows = new();

    public AiKeysDialog(IAiResearchService service, Settings settings)
    {
        _service = service;
        RequestedTheme = settings.AppTheme;
        Title = "AI-sleutels en modellen";
        PrimaryButtonText = "Opslaan";
        CloseButtonText = "Annuleren";
        DefaultButton = ContentDialogButton.Primary;
        Resources["ContentDialogMaxWidth"] = 760.0;
        Content = BuildBody();
        PrimaryButtonClick += (_, _) => Save();
    }

    private UIElement BuildBody()
    {
        var stack = new StackPanel { Spacing = 10, Padding = new Thickness(0, 0, 14, 0) };
        stack.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap, Opacity = 0.85,
            Text = "Optioneel. Zonder sleutel gebruik je de gratis websites in de tabbladen. Met een sleutel stelt de app de vraag zelf " +
                   "(met je portfolio als context) en leest het antwoord voor slimme acties. Gratis sleutels: Gemini, Groq, OpenRouter, Mistral. " +
                   "Sleutels worden versleuteld opgeslagen op deze pc en alleen naar de eigen aanbieder gestuurd.",
        });

        foreach (var p in AiCatalog.ApiProviders)
        {
            bool has = _service.HasKey(p.Id);
            var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            header.Children.Add(new TextBlock { Text = p.Name, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            header.Children.Add(new TextBlock
            {
                Text = p.HasFreeTier ? "gratis tier" : "betaald", FontSize = 11, VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(p.HasFreeTier ? Microsoft.UI.Colors.MediumSeaGreen : Microsoft.UI.Colors.DarkGoldenrod),
            });
            if (has)
                header.Children.Add(new TextBlock { Text = "✓ sleutel opgeslagen", FontSize = 11, Opacity = 0.7, VerticalAlignment = VerticalAlignment.Center });
            header.Children.Add(new HyperlinkButton { Content = "Sleutel aanmaken", NavigateUri = new Uri(p.KeyUrl), Padding = new Thickness(4, 0, 4, 0) });

            var key = new PasswordBox { PlaceholderText = has ? "•••••• (laat leeg om te behouden)" : "API-sleutel plakken", Width = 330 };
            var model = new TextBox { Text = _service.GetModel(p.Id), Width = 230, PlaceholderText = p.DefaultModel };
            ToolTipService.SetToolTip(model, $"Modelnaam. Standaard: {p.DefaultModel}. {p.Note}");
            var remove = new CheckBox { Content = "Wissen", MinWidth = 0, IsEnabled = has, VerticalAlignment = VerticalAlignment.Center };

            var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            line.Children.Add(key);
            line.Children.Add(model);
            line.Children.Add(remove);

            var card = new StackPanel { Spacing = 4, Padding = new Thickness(10, 8, 10, 8), CornerRadius = new CornerRadius(6),
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"] };
            card.Children.Add(header);
            card.Children.Add(new TextBlock { Text = p.Note, FontSize = 11, Opacity = 0.7 });
            card.Children.Add(line);
            stack.Children.Add(card);
            _rows.Add((p, key, model, remove));
        }

        return new ScrollViewer { Content = stack, MaxHeight = 560, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private void Save()
    {
        foreach (var (p, key, model, remove) in _rows)
        {
            if (remove.IsChecked == true) _service.SaveKey(p.Id, null);
            else if (!string.IsNullOrWhiteSpace(key.Password)) _service.SaveKey(p.Id, key.Password);
            _service.SaveModel(p.Id, string.IsNullOrWhiteSpace(model.Text) ? p.DefaultModel : model.Text);
        }
    }
}
