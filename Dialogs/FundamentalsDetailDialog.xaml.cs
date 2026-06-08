using System;
using CryptoPortfolioTracker.Converters;
using CryptoPortfolioTracker.Models;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace CryptoPortfolioTracker.Dialogs;

/// <summary>
/// Detail-popup met alle fundamentele cijfers en de factor-onderbouwing van één coin.
/// Content wordt programmatisch opgebouwd in <see cref="BuildContent"/>.
/// </summary>
public sealed partial class FundamentalsDetailDialog : ContentDialog
{
    private readonly CoinFundamentals _f;
    private readonly Settings _settings;

    private static readonly SolidColorBrush Green   = new(Color.FromArgb(255, 60, 179, 113));
    private static readonly SolidColorBrush Red     = new(Color.FromArgb(255, 205, 92, 92));
    private static readonly SolidColorBrush Orange  = new(Color.FromArgb(255, 255, 167, 38));
    private static readonly SolidColorBrush Neutral = new(Color.FromArgb(255, 160, 160, 160));

    public FundamentalsDetailDialog(CoinFundamentals fundamentals, Settings settings)
    {
        _f        = fundamentals;
        _settings = settings;
        InitializeComponent();
        Title             = $"{_f.Name}  ({_f.Symbol})  —  fundamentals";
        PrimaryButtonText = "Sluiten";
    }

    private void Dialog_Loading(FrameworkElement sender, object args)
    {
        if (sender.ActualTheme != _settings.AppTheme)
            sender.RequestedTheme = _settings.AppTheme;
        BuildContent();
    }

    private void BuildContent()
    {
        ContentPanel.Children.Clear();

        // ── Score-kop ──────────────────────────────────────────────────────────
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        var badge = new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding      = new Thickness(18, 10, 18, 10),
            Background   = ScoreBrush(_f.TotalScore),
            VerticalAlignment = VerticalAlignment.Center,
        };
        badge.Child = new TextBlock
        {
            Text = $"{_f.TotalScore:0}", FontSize = 30, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Colors.White), HorizontalAlignment = HorizontalAlignment.Center,
        };
        head.Children.Add(badge);
        var headText = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        headText.Children.Add(new TextBlock { Text = _f.Verdict, FontSize = 18, FontWeight = FontWeights.SemiBold });
        string rankStr = _f.MarketCapRank.HasValue ? $"#{_f.MarketCapRank.Value}" : "rang onbekend";
        headText.Children.Add(new TextBlock
        {
            Text = $"Data-score {_f.DataScore:0} · Betrouwbaarheid {_f.Confidence:0}%  ({rankStr})",
            FontSize = 12, Foreground = Neutral,
        });
        if (_f.UpdatedAt > DateTime.MinValue)
            headText.Children.Add(new TextBlock { Text = $"Bijgewerkt: {_f.UpdatedAt.ToLocalTime():dd-MM-yyyy HH:mm}", FontSize = 11, Foreground = Neutral });
        head.Children.Add(headText);
        ContentPanel.Children.Add(head);

        // ── Factor-subscores ─────────────────────────────────────────────────────
        ContentPanel.Children.Add(Header("🎯 Factor-subscores (0-100)"));
        ContentPanel.Children.Add(ScoreBar("Tokenomics (aanbod & verwatering)", _f.ScoreTokenomics));
        ContentPanel.Children.Add(ScoreBar("Liquiditeit (volume/MC)", _f.ScoreLiquidity));
        ContentPanel.Children.Add(ScoreBar("Waardering (rank & extremen)", _f.ScoreValuation));
        ContentPanel.Children.Add(ScoreBar("Community", _f.ScoreCommunity));
        ContentPanel.Children.Add(ScoreBar("Development (GitHub)", _f.ScoreDevelopment));
        ContentPanel.Children.Add(ScoreBar("Projectvolledigheid", _f.ScoreProject));

        // ── Waardering & aanbod ──────────────────────────────────────────────────
        ContentPanel.Children.Add(Header("💰 Waardering & aanbod"));
        var g1 = Grid3();
        AddKv(g1, 0, "Market cap", Functions.FormatUsdCompact(_f.MarketCap), null);
        AddKv(g1, 1, "FDV", Functions.FormatUsdCompact(_f.Fdv), null);
        AddKv(g1, 2, "FDV / MC", _f.MarketCap > 0 && _f.Fdv > 0 ? Functions.FormatRatioX(_f.Fdv / _f.MarketCap) : "—",
              _f.MarketCap > 0 && _f.Fdv / _f.MarketCap > 3 ? Red : Neutral);
        ContentPanel.Children.Add(g1);
        var g2 = Grid3();
        AddKv(g2, 0, "24u volume", Functions.FormatUsdCompact(_f.TotalVolume), null);
        AddKv(g2, 1, "Volume / MC", _f.MarketCap > 0 ? $"{_f.TotalVolume / _f.MarketCap * 100:0.0}%" : "—", null);
        AddKv(g2, 2, "Circulerend", Functions.FormatSupply(_f.CirculatingSupply), null);
        ContentPanel.Children.Add(g2);
        var g3 = Grid3();
        AddKv(g3, 0, "Totaal aanbod", Functions.FormatSupply(_f.TotalSupply), null);
        AddKv(g3, 1, "Max aanbod", _f.MaxSupply > 0 ? Functions.FormatSupply(_f.MaxSupply) : "∞ / onbekend", null);
        double circPct = _f.MaxSupply > 0 ? _f.CirculatingSupply / _f.MaxSupply * 100 : 0;
        AddKv(g3, 2, "% van max in omloop", _f.MaxSupply > 0 ? $"{circPct:0.0}%" : "—", null);
        ContentPanel.Children.Add(g3);

        // ── Extremen ─────────────────────────────────────────────────────────────
        ContentPanel.Children.Add(Header("📈 Koers-extremen"));
        var g4 = Grid3();
        AddKv(g4, 0, "vs ATH", Functions.FormatPercentSigned(_f.AthChangePct), _f.AthChangePct < -70 ? Orange : Neutral);
        AddKv(g4, 1, "ATH-datum", _f.AthDate is { } ad ? ad.ToString("dd-MM-yyyy") : "—", Neutral);
        AddKv(g4, 2, "vs ATL", Functions.FormatPercentSigned(_f.AtlChangePct), Green);
        ContentPanel.Children.Add(g4);

        // ── Community & development ──────────────────────────────────────────────
        ContentPanel.Children.Add(Header("👥 Community & development"));
        var g5 = Grid3();
        AddKv(g5, 0, "Twitter-volgers", Functions.FormatSupply(_f.TwitterFollowers), null);
        AddKv(g5, 1, "Reddit-leden", Functions.FormatSupply(_f.RedditSubscribers), null);
        AddKv(g5, 2, "Sentiment ↑", _f.SentimentUpPct > 0 ? $"{_f.SentimentUpPct:0}%" : "—", null);
        ContentPanel.Children.Add(g5);
        var g6 = Grid3();
        AddKv(g6, 0, "GitHub-sterren", Functions.FormatSupply(_f.GithubStars), null);
        AddKv(g6, 1, "Commits / 4 wkn", _f.CommitCount4Weeks.ToString(), _f.CommitCount4Weeks == 0 ? Orange : Green);
        AddKv(g6, 2, "Merged PR's", Functions.FormatSupply(_f.PullRequestsMerged), null);
        ContentPanel.Children.Add(g6);

        // ── Project ──────────────────────────────────────────────────────────────
        ContentPanel.Children.Add(Header("🏗️ Project"));
        if (!string.IsNullOrWhiteSpace(_f.Categories))
            ContentPanel.Children.Add(new TextBlock { Text = "Sector: " + _f.Categories, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        if (_f.GenesisDate is { } gd)
            ContentPanel.Children.Add(new TextBlock { Text = $"Lancering: {gd:dd-MM-yyyy}  ({(DateTime.UtcNow - gd).TotalDays / 365.25:0.0} jaar track record)", FontSize = 12, Foreground = Neutral });

        var links = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        AddLink(links, "🌐 Website", _f.HomepageUrl);
        AddLink(links, "📄 Whitepaper", _f.WhitepaperUrl);
        AddLink(links, "💻 GitHub", _f.GithubUrl);
        AddLink(links, "𝕏 Twitter", string.IsNullOrWhiteSpace(_f.TwitterHandle) ? "" : $"https://twitter.com/{_f.TwitterHandle}");
        AddLink(links, "👽 Reddit", _f.SubredditUrl);
        if (links.Children.Count > 0) ContentPanel.Children.Add(links);

        if (!string.IsNullOrWhiteSpace(_f.Description))
        {
            var desc = _f.Description;
            if (desc.Length > 900) desc = desc.Substring(0, 900) + "…";
            ContentPanel.Children.Add(new TextBlock
            {
                Text = desc, FontSize = 12, TextWrapping = TextWrapping.Wrap,
                Foreground = Neutral, Margin = new Thickness(0, 4, 0, 0),
            });
        }

        // ── Handmatige due-diligence (Sprint C) ──────────────────────────────────
        ContentPanel.Children.Add(Header("📝 Handmatige due-diligence"));
        bool anyDd = _f.DdTeam.HasValue || _f.DdProductMaturity.HasValue || _f.DdAdoption.HasValue || _f.DdRevenue.HasValue || _f.DdUnlocks.HasValue;
        var ddGrid = Grid3();
        AddKv(ddGrid, 0, "Team", DdText(_f.DdTeam), Neutral);
        AddKv(ddGrid, 1, "Product-maturiteit", DdText(_f.DdProductMaturity), Neutral);
        AddKv(ddGrid, 2, "Adoptie", DdText(_f.DdAdoption), Neutral);
        ContentPanel.Children.Add(ddGrid);
        var ddGrid2 = Grid3();
        AddKv(ddGrid2, 0, "Revenue", DdText(_f.DdRevenue), Neutral);
        AddKv(ddGrid2, 1, "Unlocks-risico", DdText(_f.DdUnlocks), Neutral);
        AddKv(ddGrid2, 2, "", "", Neutral);
        ContentPanel.Children.Add(ddGrid2);
        if (!anyDd)
            ContentPanel.Children.Add(new InfoBar
            {
                IsOpen = true, IsClosable = false, Severity = InfoBarSeverity.Informational,
                Title = "Nog niet handmatig beoordeeld",
                Message = "Team, maturiteit, adoptie, revenue en unlocks zijn niet automatisch meetbaar uit CoinGecko. " +
                          "Handmatige invoer (en SWOT/risico-rapport) volgt in een latere versie en verhoogt de betrouwbaarheid van de totaalscore.",
            });

        // ── Disclaimer ───────────────────────────────────────────────────────────
        ContentPanel.Children.Add(new TextBlock
        {
            Text = "Cijfers via CoinGecko. De score is een transparant modeloordeel over meetbare fundamentals, geen beleggingsadvies.",
            FontSize = 10, Foreground = Neutral, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0),
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static string DdText(int? v) => v.HasValue ? $"{v.Value}/10" : "—";

    private static SolidColorBrush ScoreBrush(double s) => s switch
    {
        >= 90 => new(Color.FromArgb(255, 0x1A, 0x5C, 0x2E)),
        >= 80 => new(Color.FromArgb(255, 0x27, 0x96, 0x42)),
        >= 70 => new(Color.FromArgb(255, 0x3C, 0xB3, 0x71)),
        >= 60 => new(Color.FromArgb(255, 0xE6, 0x7E, 0x22)),
        >= 50 => new(Color.FromArgb(255, 0xCD, 0x5C, 0x5C)),
        _     => new(Color.FromArgb(255, 0xC0, 0x39, 0x2B)),
    };

    private static TextBlock Header(string text) => new()
    {
        Text = text, FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 2),
    };

    private UIElement ScoreBar(string label, double score)
    {
        var sp = new StackPanel { Spacing = 2 };
        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var lbl = new TextBlock { Text = label, FontSize = 12 };
        var val = new TextBlock { Text = $"{score:0}", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = ScoreBrush(score) };
        Grid.SetColumn(val, 1);
        top.Children.Add(lbl);
        top.Children.Add(val);
        sp.Children.Add(top);
        sp.Children.Add(new ProgressBar { Value = Math.Max(0, Math.Min(100, score)), Maximum = 100, Height = 6, Foreground = ScoreBrush(score) });
        return sp;
    }

    private static Grid Grid3()
    {
        var g = new Grid { ColumnSpacing = 10, Margin = new Thickness(0, 2, 0, 0) };
        for (int i = 0; i < 3; i++)
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        return g;
    }

    private static void AddKv(Grid grid, int col, string label, string val, SolidColorBrush? color)
    {
        var sp = new StackPanel { Spacing = 1 };
        sp.Children.Add(new TextBlock { Text = label, FontSize = 11, Foreground = Neutral });
        sp.Children.Add(new TextBlock
        {
            Text = val, FontSize = 13, FontWeight = FontWeights.SemiBold,
            Foreground = color ?? (SolidColorBrush)Application.Current.Resources["TextFillColorPrimaryBrush"],
        });
        Grid.SetColumn(sp, col);
        grid.Children.Add(sp);
    }

    private static void AddLink(StackPanel panel, string text, string url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;
        panel.Children.Add(new HyperlinkButton { Content = text, NavigateUri = uri, FontSize = 12, Padding = new Thickness(4, 2, 4, 2) });
    }
}
