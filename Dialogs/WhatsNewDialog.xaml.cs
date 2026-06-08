using CryptoPortfolioTracker.Services;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace CryptoPortfolioTracker.Dialogs;

public sealed partial class WhatsNewDialog : ContentDialog
{
    private readonly Settings _appSettings;

    public WhatsNewDialog(Settings appSettings)
    {
        _appSettings = appSettings;
        InitializeComponent();
    }

    private void WhatsNewDialog_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        BuildContent();
    }

    // -----------------------------------------------------------------------
    // Content builder
    // -----------------------------------------------------------------------

    private void BuildContent()
    {
        AddVersionHeader("v1.33  —  3% Trading-tool · Gekalibreerd scoremodel · Robuustere advieslogica");

        AddFeature("🎯", "Nieuw tabblad: 3% Trading",
            "Gekalibreerd scoremodel met netto-doel van +3% na fees. Fase 1 meet per scoreklasse de " +
            "werkelijke historische hitrate en expectancy; Fase 2 scoort je coins en koppelt ze aan die meting. " +
            "Liquiditeit en funding/positionering fungeren als gatekeeper.");

        AddFeature("🔗", "Regime, correlatie & detailvenster",
            "Marktregime via BTC EMA50/200 + dominantie. Een gediversifieerde shortlist voorkomt vijf keer " +
            "dezelfde BTC-weddenschap. Elke setup heeft een detailvenster met indicatoren, S/R, positionering, " +
            "invalidatieniveau en aankomende macro-events (FOMC/CPI/NFP).");

        AddFeature("🛡️", "Veiliger trade-advies",
            "Trade Advies en Pattern Trading markeren nu ongeldige setups (bv. ATR=0 → SL op de instap) en " +
            "waarschuwen bij krappe risk/reward (< 1,5:1). Trade Advies toont ook liquiditeit, funding en " +
            "event-risico als markt-context.");

        AddFeature("📝", "3% Trading — paper trades & forward-test",
            "Activeer een setup uit de Live Scan als paper trade en volg ze in het nieuwe tabblad 'Paper Trades' " +
            "met live win-rate en P&L. Vernieuwen vult pending orders en laat TP/SL triggeren op de actuele koers.");

        AddVersionHeader("v1.32  —  Setup Tracker verbeterd · Instaptijden · TDD-testraamwerk");

        AddFeature("⚠️", "Bevestiging bij handmatig sluiten vóór TP1",
            "Als TP1 nog niet bereikt is bij het handmatig markeren als Gewonnen, " +
            "verschijnt een dialoog met de resterende afstand tot TP1.");

        AddFeature("📥", "Instap- en sluitingstijden op setupkaarten",
            "Elke setupkaart toont wanneer de entry werd geraakt (📥) en wanneer TP1/SL viel (📤). " +
            "Bestaande trades zijn teruggevuld met de AddedAt-datum.");

        AddVersionHeader("v1.31  —  Patroonherkenning verbeterd · Trade Advies consistent met Pattern Trading");

        AddFeature("📐", "Wedge-herkenning gecorrigeerd",
            "Falling/Rising Wedge lijnen lopen nu geometrisch correct naar elkaar toe (≥30% convergentie). " +
            "Swing-detectie filtert micro-swings <0,5% en keerpuntpatronen vereisen een voorafgaande trend van ≥15%.");

        AddFeature("🔗", "Trade Advies = Pattern Trading score",
            "Score en richting in Trade Advies worden nu live berekend met dezelfde engine als Pattern Trading. " +
            "Dezelfde coin kan niet meer tegelijk Long én Short tonen.");

        AddVersionHeader("v1.30  —  Setup Tracker · Papertrade vanuit patronen · S/R met TF-label");

        AddFeature("📋", "Setup Tracker",
            "Nieuw tabblad om gevolgde patroon-setups bij te houden. " +
            "Win rate dashboard toont of >50% van de setups winstgevend sluit. " +
            "Status wordt automatisch bijgewerkt zodra de koers TP1 of SL raakt.");

        AddFeature("📝", "Neem papertrade vanuit Pattern Trading",
            "Directe knop per coin om de papertrade-dialog te openen met entry, SL en TP vooringevuld.");

        AddFeature("📊", "S/R-labels met timeframe",
            "Lijnen in de grafiek tonen nu 'S-4H' of 'R-1D' zodat je meteen ziet hoe zwaar een niveau weegt.");

        AddVersionHeader("v1.25  —  Pattern Trading: 15M timeframe · Watchlijst UX · Sorteren & Filteren");

        AddFeature("⏱", "15-minuten timeframe",
            "Vier timeframes: 1D, 4H, 1H én 15M. Biasbadges en patronen op alle vier in de coin-kaarten. " +
            "'15M'-knop in de grafiek. TF-conflict-waarschuwing wanneer 1D en 4H tegengesteld zijn.");

        AddFeature("👁", "Watchlijst vernieuwd",
            "Eigen uitklapbaar paneel met alle watchlist-coins als chips (direct verwijderbaar). " +
            "Zoekvak met voortgangsring en duidelijke foutmelding bij CoinGecko rate-limiting. " +
            "Dubbele-uitvoering bij suggestie-klik is verholpen.");

        AddFeature("📊", "Sorteren & filteren",
            "Sorteer op score, 24u verandering of breakout-afstand. " +
            "Filterknop per timeframe (1D/4H/1H/15M). Zoeken in geladen resultaten op naam of symbol.");

        AddVersionHeader("v1.10  —  Trade Advies redesign · Analyseer alles · Fallback databronnen");

        AddFeature("📊", "Trade Advies — Analyseer alles in één klik",
            "De nieuwe knop 'Analyseer alles' analyseert alle portfolio-coins tegelijk en toont een " +
            "gesorteerde ranglijst: Long-signalen bovenaan (sterkste score eerst), daarna Short, dan Geen signaal. " +
            "Klik op een rij om de volledige analyse van die coin direct te openen.");

        AddFeature("🎨", "Trade Advies — verbeterde opmaak",
            "De bedieningsbalk heeft nu een eigen achtergrond (altijd leesbaar) en twee " +
            "aparte laadspinners voor losse analyse en bulk-analyse.");

        AddFeature("📝", "Paper Trade vanuit Trade Advies",
            "Op de Trade Advies-pagina verschijnt nu een 'Paper Trade'-knop zodra de analyse " +
            "een Long- of Short-signaal geeft. Stop-loss, take-profit en richting zijn " +
            "automatisch vooringevuld vanuit het trade advies.");

        AddFeature("🔄", "Fallback databronnen (KuCoin · Gate.io · MEXC)",
            "Als een coin niet op Binance staat probeert de Trade Advies-analyse automatisch " +
            "KuCoin, daarna Gate.io en daarna MEXC als databron. Pas als alle vier bronnen falen " +
            "valt de analyse terug op de lokale koerscache.");

        AddFeature("🔄", "Trade Journal — Vernieuwen-knop met tijdstempel",
            "Een nieuwe 'Vernieuwen'-knop rechtsboven in het Trade Journal laadt orders en " +
            "PnL-berekeningen direct opnieuw. Een tijdstempel toont precies wanneer de " +
            "huidige koersen zijn opgehaald.");

        AddVersionHeader("v1.6  —  Dashboard Widgets, Sources & Sparklines");

        AddFeature("📡", "Sources page (Bronnen)",
            "A new Bronnen page lets you manage all sentiment sources: Reddit communities, " +
            "RSS news feeds, CryptoPanic endpoints and Telegram channels. " +
            "17 high-quality sources are pre-seeded out of the box. " +
            "Add, edit or remove sources at any time and toggle each one active/inactive " +
            "without deleting it. The reliability score controls how much weight a source " +
            "gets when computing the combined sentiment score.");

        AddFeature("📊", "Dashboard — Today's Signals widget",
            "A new widget on the dashboard (bottom-left) shows the six highest-scoring signals " +
            "from today's evaluation at a glance: asset name, direction badge and combined score. " +
            "The list is colour-coded (Long = green, Short = red, Flat = grey) and updates " +
            "every time the signal engine runs.");

        AddFeature("🌍", "Dashboard — Market Regime card with reasoning",
            "The Market Regime card (bottom-right) now also displays the key headlines that " +
            "led to the current BTC regime classification (RiskOn / Neutral / RiskOff). " +
            "Up to six reasoning lines from the latest BTC signal are shown directly on the " +
            "dashboard so you can judge market context at a glance without opening the Analysis page.");

        AddFeature("📈", "Sparkline trend charts on Analysis page",
            "Three mini line-chart columns have been added to the Analysis page: " +
            "1H (last 14 1H closes), 4H (last 30 4H closes) and 1D (last 90 daily closes). " +
            "Each sparkline is green when the linear-regression slope is positive (uptrend) " +
            "and red when it is negative (downtrend), giving an instant visual read of " +
            "short-, medium- and long-term price momentum per asset.");

        AddFeature("⚙️", "Settings — Signal thresholds & paper-trading toggle",
            "A new 'Signalen & Trading' section in Settings lets you set the minimum signal " +
            "score required before a signal is acted upon (slider 50–85, default 60) and " +
            "switch between paper-trading and live-trading mode with a single toggle.");

        AddFeature("🛡️", "Settings — Risk guardrails",
            "A 'Risk-guardrails' section in Settings gives you four safety limits: " +
            "maximum portfolio % per trade (1–25 %, default 5 %), " +
            "maximum number of open positions (1–20, default 5), " +
            "daily loss limit as % of portfolio (1–30 %, default 10 %) and " +
            "a kill-switch toggle that halts all automated trading instantly.");

        AddVersionHeader("v1.5  —  UI Enhancements");

        AddFeature("↕", "Sortable columns on Analysis page",
            "Click any column header on the Analysis page to sort assets by that indicator. " +
            "Click the same header again to reverse the sort order. " +
            "A ▲ or ▼ arrow indicates the active sort column and direction.");

        AddFeature("💬", "Column tooltips on Analysis page",
            "Hover over a column header on the Analysis page to see a short explanation " +
            "of what the indicator measures and how to interpret the value. " +
            "Helpful for traders who are still learning technical analysis.");

        AddVersionHeader("v1.4  —  Signal Engine & Paper Trading");

        AddFeature("📊", "Analysis page (Signals view)",
            "A new Analysis page lists all your assets with computed technical indicators: " +
            "MACD, Bollinger Bands (upper/lower), ATR, StochRSI, a sentiment score, " +
            "the current market regime and a combined signal score. " +
            "Use Refresh Analysis to recalculate the raw TA indicators, " +
            "or Evaluate Signals to run the full engine (TA + sentiment + regime).");

        AddFeature("⚙️", "Signal Engine",
            "The signal engine combines technical analysis scores, sentiment readings and " +
            "the current market regime to produce a single CombinedScore (0–100) and a " +
            "Direction label (Long / Flat / Short) per asset. " +
            "Scores above 60 are considered bullish; scores below 40 are bearish.");

        AddFeature("🌍", "Market Regime detection",
            "The regime service monitors BTC dominance and price trends to classify the " +
            "overall market environment as RiskOn, Neutral or RiskOff. " +
            "This regime is factored into every signal score so that bullish TA signals " +
            "are dampened during a RiskOff market.");

        AddFeature("📋", "Trade Journal",
            "A new Trade Journal page records all paper trades placed from the Analysis page. " +
            "Each entry shows the asset, direction (Long/Short), entry price, amount (USDT), " +
            "stop-loss, take-profit, status and the reasoning behind the trade. " +
            "Use the journal to review your paper-trading performance over time.");

        AddFeature("📝", "Paper trading from Analysis page",
            "Press the trade icon (📋) at the end of any row on the Analysis page to open " +
            "the paper-trade dialog. Fill in direction, amount (USDT), stop-loss and " +
            "take-profit levels and confirm to log the trade in the Trade Journal.");

        AddFeature("🪙", "Coin logos on Analysis page",
            "Asset rows on the Analysis page now show the coin logo next to the name, " +
            "making it faster to scan the list visually.");

        AddVersionHeader("v1.3  —  Sentiment Collector");

        AddFeature("😊", "Sentiment scores",
            "A background service collects sentiment data from multiple sources and stores " +
            "a rolling sentiment score per asset. The score is visible on the Analysis page " +
            "under the Sentiment column and is included in the combined signal score.");

        AddVersionHeader("v1.2  —  Extended Technical Indicators");

        AddFeature("📈", "MACD",
            "Moving Average Convergence Divergence. A positive value indicates upward momentum; " +
            "a negative value indicates downward momentum.");

        AddFeature("📉", "Bollinger Bands",
            "The upper and lower bands define the expected price range. " +
            "A price above the upper band signals overbought conditions; " +
            "a price below the lower band signals oversold conditions.");

        AddFeature("📏", "ATR — Average True Range",
            "Measures market volatility. A higher ATR means larger price swings " +
            "and wider stop-loss levels are typically needed.");

        AddFeature("🔁", "StochRSI",
            "A 0–100 oscillator. Values below 20 indicate oversold conditions (potential buy); " +
            "values above 80 indicate overbought conditions (potential sell).");
    }

    // -----------------------------------------------------------------------
    // Helper methods
    // -----------------------------------------------------------------------

    private void AddVersionHeader(string text)
    {
        // Spacer above each section (except the very first)
        if (ContentPanel.Children.Count > 0)
        {
            ContentPanel.Children.Add(new Border { Height = 16 });
        }

        var tb = new TextBlock
        {
            Text = text,
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xB8, 0x86, 0x0B)), // DarkGoldenrod
            Margin = new Microsoft.UI.Xaml.Thickness(0, 0, 0, 8),
        };
        ContentPanel.Children.Add(tb);

        // Horizontal rule
        ContentPanel.Children.Add(new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.FromArgb(0x40, 0xB8, 0x86, 0x0B)),
            Margin = new Microsoft.UI.Xaml.Thickness(0, 0, 0, 8),
        });
    }

    private void AddFeature(string icon, string title, string description)
    {
        // Row panel: icon + text block
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Microsoft.UI.Xaml.Thickness(4, 0, 0, 10),
        };

        // Icon label
        row.Children.Add(new TextBlock
        {
            Text = icon,
            FontSize = 16,
            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Top,
            Margin = new Microsoft.UI.Xaml.Thickness(0, 1, 0, 0),
            Width = 24,
        });

        // Text column: bold title + description
        var textCol = new StackPanel { Spacing = 2 };

        textCol.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
        });

        textCol.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 12,
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
            Foreground = (SolidColorBrush)Microsoft.UI.Xaml.Application.Current.Resources["SystemControlForegroundBaseMediumBrush"],
            MaxWidth = 730,
        });

        row.Children.Add(textCol);
        ContentPanel.Children.Add(row);
    }
}
