using System;
using System.Collections.Generic;
using System.Linq;
using CryptoPortfolioTracker.Converters;
using CryptoPortfolioTracker.Models;
using CryptoPortfolioTracker.Services;
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

    // Due-diligence invoercontrols (uitgelezen bij opslaan)
    private readonly List<(CheckBox enabled, Slider slider)> _ddControls = new();
    private TextBox? _ddNotes;

    /// <summary>True wanneer de gebruiker op 'Opslaan' klikte (DD-waarden zijn op _f gezet).</summary>
    public bool Saved { get; private set; }

    public FundamentalsDetailDialog(CoinFundamentals fundamentals, Settings settings)
    {
        _f        = fundamentals;
        _settings = settings;
        InitializeComponent();
        Title              = $"{_f.Name}  ({_f.Symbol})  —  fundamentals";
        PrimaryButtonText  = "Opslaan";
        CloseButtonText    = "Sluiten";
        DefaultButton      = ContentDialogButton.Close;
        PrimaryButtonClick += OnSaveClick;
    }

    private void OnSaveClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // Lees de DD-sliders uit: uitgevinkt = niet beoordeeld (null).
        var values = _ddControls
            .Select(c => c.enabled.IsChecked == true ? (int?)(int)c.slider.Value : null)
            .ToList();

        _f.DdTeam            = values.ElementAtOrDefault(0);
        _f.DdProductMaturity = values.ElementAtOrDefault(1);
        _f.DdAdoption        = values.ElementAtOrDefault(2);
        _f.DdRevenue         = values.ElementAtOrDefault(3);
        _f.DdUnlocks         = values.ElementAtOrDefault(4);
        _f.DdNotes           = _ddNotes?.Text ?? string.Empty;
        Saved = true;
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
        ContentPanel.Children.Add(ScoreBar("Tokenomics (aanbod & verwatering)", _f.ScoreTokenomics,
            "Circulerend aandeel van het max. aanbod + FDV/market-cap-overhang. Hoger = minder toekomstige verwatering. Weegt 25% in de data-score."));
        ContentPanel.Children.Add(ScoreBar("Liquiditeit (volume/MC)", _f.ScoreLiquidity,
            "24u-volume gedeeld door market cap. Gezonde band ~2-30%; extreem hoog kan op wash-trading wijzen. Weegt 20%."));
        ContentPanel.Children.Add(ScoreBar("Waardering (rank & extremen)", _f.ScoreValuation,
            "Vooral market-cap rang (gevestigdheid), licht bijgesteld op herstel vanaf de bodem (ATL). Weegt 15%."));
        ContentPanel.Children.Add(ScoreBar("Community", _f.ScoreCommunity,
            "Twitter- en Reddit-bereik (log-schaal) + sentiment. Weegt 15%."));
        ContentPanel.Children.Add(ScoreBar("Development (GitHub)", _f.ScoreDevelopment,
            "Recente commits (4 wkn), sterren en gemergede PR's. 0 bij coins zonder gekoppelde publieke repo. Weegt 15%."));
        ContentPanel.Children.Add(ScoreBar("Projectvolledigheid", _f.ScoreProject,
            "Aanwezigheid van homepage, whitepaper, repo, sector en track record (leeftijd). Weegt 9% (of 10% zonder TVL)."));
        if (_f.Tvl > 0)
            ContentPanel.Children.Add(ScoreBar("On-chain (TVL)", _f.ScoreOnChain,
                "Omvang van de Total Value Locked + market-cap/TVL-efficiëntie (DefiLlama). Weegt 12%; alleen voor DeFi-coins met TVL. Bij niet-DeFi-coins vervalt deze factor en tellen de overige zwaarder."));

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

        // On-chain TVL (DefiLlama) — alleen voor DeFi-protocollen
        if (_f.Tvl > 0)
        {
            double mcapTvl = _f.MarketCap > 0 ? _f.MarketCap / _f.Tvl : 0;
            var gtvl = Grid3();
            AddKv(gtvl, 0, "TVL (DefiLlama)", Functions.FormatUsdCompact(_f.Tvl), null);
            AddKv(gtvl, 1, "Market cap / TVL", mcapTvl > 0 ? Functions.FormatRatioX(mcapTvl) : "—",
                  mcapTvl > 15 ? Red : mcapTvl is > 0 and < 2 ? Green : Neutral);
            AddKv(gtvl, 2, "Categorie", string.IsNullOrWhiteSpace(_f.TvlCategory) ? "—" : _f.TvlCategory, Neutral);
            ContentPanel.Children.Add(gtvl);
        }

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

        // #6: eigen app-sentiment (Reddit/RSS) dat de Community-factor mee voedt
        if (_f.AppSentiment != 0)
            ContentPanel.Children.Add(new TextBlock
            {
                Text = $"App-sentiment (Reddit/RSS): {_f.AppSentiment:+0.00;-0.00} — verwerkt in de Community-score.",
                FontSize = 11, Foreground = _f.AppSentiment > 0 ? Green : Red, Margin = new Thickness(0, 2, 0, 0),
            });

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

        // ── Handmatige due-diligence (bewerkbaar) ────────────────────────────────
        ContentPanel.Children.Add(Header("📝 Handmatige due-diligence (0-10)"));
        ContentPanel.Children.Add(new TextBlock
        {
            Text = "Beoordeel zelf wat niet automatisch meetbaar is. Vink 'beoordeeld' aan en zet de score; " +
                   "ingevulde factoren tellen mee in de totaalscore en verhogen de betrouwbaarheid. Klik 'Opslaan'.",
            FontSize = 11, Foreground = Neutral, TextWrapping = TextWrapping.Wrap,
        });
        _ddControls.Clear();
        AddDdRow("Team & organisatie", _f.DdTeam);
        AddDdRow("Product-maturiteit",  _f.DdProductMaturity);
        AddDdRow("Adoptie & groei",     _f.DdAdoption);
        AddDdRow("Revenue / business-model", _f.DdRevenue);
        AddDdRow("Unlock-/vesting-risico (hoog cijfer = laag risico)", _f.DdUnlocks);

        _ddNotes = new TextBox
        {
            PlaceholderText = "Notities bij je beoordeling…",
            Text = _f.DdNotes ?? string.Empty,
            AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            Height = 64, Margin = new Thickness(0, 4, 0, 0),
        };
        ContentPanel.Children.Add(_ddNotes);

        // ── Analyse-rapport (SWOT & risico) ──────────────────────────────────────
        var report = FundamentalsReportBuilder.Build(_f);
        ContentPanel.Children.Add(Header("📋 Analyse-rapport (SWOT & risico)"));
        ContentPanel.Children.Add(new TextBlock
        {
            Text = report.ExecutiveSummary, FontSize = 12, TextWrapping = TextWrapping.Wrap,
        });

        var riskRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 2, 0, 0) };
        var riskBadge = new Border
        {
            CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 2, 8, 2),
            Background = report.RiskLevel == "HIGH" ? Red : report.RiskLevel == "MEDIUM" ? Orange : Green,
        };
        riskBadge.Child = new TextBlock { Text = $"Risico: {report.RiskLevel}", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Colors.White) };
        riskRow.Children.Add(riskBadge);
        riskRow.Children.Add(new TextBlock { Text = report.ValuationVerdict, FontSize = 11, Foreground = Neutral, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
        ContentPanel.Children.Add(riskRow);

        AddBulletList("✅ Sterktes", report.Strengths, Green);
        AddBulletList("⚠️ Zwaktes", report.Weaknesses, Red);
        AddBulletList("🚀 Kansen", report.Opportunities, new SolidColorBrush(Color.FromArgb(255, 96, 165, 250)));
        AddBulletList("⛈️ Bedreigingen", report.Threats, Orange);

        if (report.TopRisks.Count > 0)
        {
            ContentPanel.Children.Add(new TextBlock { Text = "Top-risico's", FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 0) });
            int n = 1;
            foreach (var risk in report.TopRisks)
                ContentPanel.Children.Add(new TextBlock { Text = $"{n++}. {risk}", FontSize = 11, Foreground = Neutral, TextWrapping = TextWrapping.Wrap });
        }
        ContentPanel.Children.Add(new TextBlock
        {
            Text = "Het rapport is rule-based op de huidige cijfers; na opslaan van je due-diligence opnieuw openen om het bij te werken.",
            FontSize = 10, Foreground = Neutral, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0),
        });

        // ── Disclaimer ───────────────────────────────────────────────────────────
        ContentPanel.Children.Add(new TextBlock
        {
            Text = "Cijfers via CoinGecko. De score is een transparant modeloordeel over meetbare fundamentals, geen beleggingsadvies.",
            FontSize = 10, Foreground = Neutral, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0),
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private void AddDdRow(string label, int? current)
    {
        var grid = new Grid { ColumnSpacing = 10, Margin = new Thickness(0, 2, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var lbl = new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        Grid.SetColumn(lbl, 0); grid.Children.Add(lbl);

        var chk = new CheckBox { IsChecked = current.HasValue, Content = "beoordeeld", FontSize = 11, MinWidth = 0, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(chk, 1); grid.Children.Add(chk);

        var slider = new Slider { Minimum = 0, Maximum = 10, StepFrequency = 1, Value = current ?? 5, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(slider, 2); grid.Children.Add(slider);

        var val = new TextBlock { Text = $"{(int)(current ?? 5)}/10", FontSize = 12, FontWeight = FontWeights.SemiBold, MinWidth = 38, VerticalAlignment = VerticalAlignment.Center };
        slider.ValueChanged += (s, e) => val.Text = $"{(int)slider.Value}/10";
        Grid.SetColumn(val, 3); grid.Children.Add(val);

        _ddControls.Add((chk, slider));
        ContentPanel.Children.Add(grid);
    }

    private void AddBulletList(string title, List<string> items, SolidColorBrush color)
    {
        if (items is null || items.Count == 0) return;
        ContentPanel.Children.Add(new TextBlock { Text = title, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = color, Margin = new Thickness(0, 4, 0, 0) });
        foreach (var it in items)
            ContentPanel.Children.Add(new TextBlock { Text = "•  " + it, FontSize = 11, TextWrapping = TextWrapping.Wrap });
    }

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

    private UIElement ScoreBar(string label, double score, string tooltip = "")
    {
        var sp = new StackPanel { Spacing = 2 };
        if (!string.IsNullOrEmpty(tooltip))
            ToolTipService.SetToolTip(sp, tooltip);
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
