using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace CryptoPortfolioTracker.Views;

public sealed partial class WhatsNewView : Page
{
    public WhatsNewView()
    {
        InitializeComponent();
    }

    private void View_Loaded(object sender, RoutedEventArgs e)
    {
        BuildContent();
    }

    // -----------------------------------------------------------------------
    // Content builder
    // VERPLICHT: voeg een nieuw versieblok toe bij elke release.
    // Meest recente versie staat bovenaan — oudere versies blijven staan.
    // -----------------------------------------------------------------------

    private void BuildContent()
    {
        // ── v1.14 ────────────────────────────────────────────────────────────
        AddVersionHeader("v1.14", "Volledig herbouwd trade-venster");

        AddFeature("🏦", "Exchange-stijl ordervenster",
            "Het plaatsen van een paper trade is volledig herbouwd naar een echt exchange-interface. " +
            "U kiest nu een markttype (Spot / Futures / Margin), een ordertype (Limit / Market) en exchange. " +
            "Futures- en Margin-orders hebben een instelbare hefboom (1× t/m 100×). " +
            "Bij een Limit-order stelt u zelf de gewenste prijs in; bij Market wordt de huidige koers gebruikt. " +
            "SL en TP worden ingevoerd als absolute prijzen met live procentuele weergave naast elk veld. " +
            "Een tweede take-profit niveau (TP2) is optioneel in te schakelen. " +
            "De samenvattingsbalk toont continu de kostprijs, hoeveelheid, R/R-ratio en maximaal risico in USDT. " +
            "Via de snelknoppen 25% / 50% / 75% / Max stelt u snel een bedrag in als percentage van uw " +
            "virtuele handelskapitaal (10.000 USDT). " +
            "Sluit af met de groene 'Open Long'- of rode 'Open Short'-knop.");

        // ── v1.13 ────────────────────────────────────────────────────────────
        AddVersionHeader("v1.13", "Ingebouwde help-pagina");

        AddFeature("📖", "Help & Gebruikshandleiding — volledig in de app",
            "De helppagina is herschreven als interactieve on-page module. " +
            "U vindt hem onderaan het navigatiemenu (vraagteken-icoon). " +
            "De pagina bevat uitklapbare secties voor alle onderwerpen: " +
            "portfolio-beheer, transacties, Trade Advies, Trade Journal, Statistieken, " +
            "Box 3-belasting, instellingen, databronnen en veelgestelde vragen. " +
            "Formules, tips en stapsgewijze instructies zijn direct beschikbaar — zonder PDF of externe browser.");

        // ── v1.12 ────────────────────────────────────────────────────────────
        AddVersionHeader("v1.12", "Belasting · Statistieken-filters");

        AddFeature("🧾", "Belastingpagina — box 3 berekening",
            "Nieuw tabblad 'Belasting' in de Instellingen. Vul uw vermogenswaarden in op de peildatum " +
            "(1 januari) en de app berekent uw box 3 belasting conform de gepubliceerde " +
            "Belastingdienst-tarieven. Ondersteunde jaren: 2022, 2023 en 2024. " +
            "Optioneel kunt u banktegoeden, overige beleggingen, schulden en een fiscaal partner opgeven " +
            "voor een completer beeld. De berekening toont een gedetailleerde breakdown met rendementsgrondslag, " +
            "heffingsvrij vermogen, fictief rendement en verschuldigde belasting. " +
            "De architectuur is voorbereid op meerdere landen — nieuwe calculators kunnen eenvoudig worden toegevoegd.");

        AddFeature("📊", "Statistieken — Live/Paper filter en aangepaste periode",
            "Bovenaan de Statistieken-pagina staan nu twee filters: een type-filter (Alle / Live / Paper) " +
            "en een uitgebreide periodefilter met 'Aangepast'-optie. " +
            "Bij het kiezen van 'Aangepast' verschijnen twee datumkiezers waarmee u " +
            "een vrij te kiezen start- en einddatum kunt instellen. " +
            "Alle samenvattingskaarten, taartdiagrammen en de symboolentabel worden direct " +
            "herladen zodra een filter wijzigt.");

        // ── v1.11 ────────────────────────────────────────────────────────────
        AddVersionHeader("v1.11", "Statistieken-pagina");

        AddFeature("📈", "Statistieken — overzicht van je handelsprestaties",
            "Een nieuwe pagina 'Statistieken' in het menu geeft een volledig overzicht van je trades. " +
            "Bovenaan staan vier samenvattingskaarten: totale P&L, win rate (met W/L-verdeling), " +
            "gemiddelde winst en gemiddeld verlies, en het aantal open posities. " +
            "Drie taartdiagrammen tonen de verdeling Winst/Verlies, Long/Short en Paper/Live. " +
            "Onderaan staat een tabel met de best en slechtst presterende symbolen. " +
            "Gebruik de periodefilter (Alles / Deze maand / Afgelopen 3 maanden / Dit jaar) " +
            "om de statistieken te beperken tot een gewenste tijdsperiode.");

        // ── v1.10 ────────────────────────────────────────────────────────────
        AddVersionHeader("v1.10", "Trade Advies redesign · Paper Trade · Fallback databronnen · Journal vernieuwen");

        AddFeature("📊", "Trade Advies — Analyseer alles in één klik",
            "Nieuw op de Trade Advies-pagina: de knop 'Analyseer alles' analyseert alle coins in je portfolio " +
            "tegelijk (tot 3 gelijktijdig, zodat de server niet overbelast raakt). " +
            "Zodra alle analyses klaar zijn verschijnt een gesorteerde ranglijst: " +
            "Long-signalen bovenaan (sterkste score eerst), daarna Short-signalen, daarna coins zonder signaal. " +
            "Elke rij toont logo, naam, richting, entry/SL/TP, R/R-ratio en databron in één oogopslag. " +
            "Klik op een rij om de volledige analyse van die coin direct te openen.");

        AddFeature("🎨", "Trade Advies — verbeterde opmaak en leesbaarheid",
            "De bedieningsbalk bovenaan de Trade Advies-pagina heeft nu een eigen achtergrond, " +
            "zodat de knoppen en het zoekveld altijd leesbaar zijn — ook als het header-beeld te licht is. " +
            "Twee aparte laadspinners tonen of een losse analyse (Analyseer) of de bulk-analyse (Analyseer alles) bezig is.");

        AddFeature("📝", "Paper Trade vanuit Trade Advies",
            "Op de Trade Advies-pagina verschijnt nu een 'Paper Trade'-knop zodra de analyse " +
            "een Long- of Short-signaal geeft. Klik erop om direct het paper-trade-venster te openen, " +
            "met stop-loss%, take-profit% en richting al vooringevuld vanuit het trade advies. " +
            "De trade wordt direct opgeslagen in het Trade Journal.");

        AddFeature("🔄", "KuCoin als backup databron voor Trade Advies",
            "Als een coin niet op Binance genoteerd staat (bijv. Solana-memecoins of kleinere altcoins) " +
            "probeert de Trade Advies-pagina nu automatisch KuCoin als alternatieve databron. " +
            "KuCoin levert dezelfde timeframes (1H · 4H · 1D · 1W) met echte OHLCV-data. " +
            "Pas als ook KuCoin geen data heeft valt de analyse terug op de lokale koerscache. " +
            "De gebruikte bron is zichtbaar in de statusregel ('bron: KuCoin (PONKE-USDT)').");

        AddFeature("🔄", "Trade Journal — Vernieuwen-knop met tijdstempel",
            "Rechtsboven in de Trade Journal-toolbar staat nu een 'Vernieuwen'-knop. " +
            "Klik erop om orders en PnL-berekeningen direct te herladen zonder de pagina te verlaten. " +
            "Naast de knop toont een tijdstempel ('Prijzen: HH:mm:ss') precies wanneer de " +
            "huidige koersen zijn opgehaald, zodat je altijd weet hoe actueel de PnL-cijfers zijn.");

        // ── v1.8 ─────────────────────────────────────────────────────────────
        AddVersionHeader("v1.8", "Trade Advies — multi-timeframe analyse per coin");

        AddFeature("📋", "Trade Advies-pagina",
            "Een nieuwe pagina geeft voor elk coin in je portfolio een volledige multi-timeframe analyse. " +
            "Selecteer een coin uit de dropdown en klik 'Analyseer' voor een direct rapport met " +
            "Weekly-, Daily-, 4H- en 1H-secties, sleutelniveaus en een concreet trade advies.");

        AddFeature("📡", "Live Binance OHLCV-data (geen API-sleutel)",
            "De analyse haalt real-time candledata op van de gratis Binance Public API: " +
            "Weekly (104 bars), Daily (300 bars), 4H (500 bars) en 1H (200 bars). " +
            "Echte OHLCV-data — niet gesimuleerd uit dagelijkse slotkoersen. " +
            "Coins die niet op Binance noteren vallen automatisch terug op de lokale koerscache.");

        AddFeature("🎯", "Sleutelniveaus — automatische pivot-detectie",
            "Per analyse worden tot 4 weerstand- en 4 steunniveaus berekend via pivot-detectie op de 4H-data. " +
            "Niveaus binnen 1,5% van elkaar worden samengevoegd tot één cluster, " +
            "zodat alleen significante gebieden worden getoond.");

        AddFeature("⚙️", "Trade Setup met entry · stop-loss · targets",
            "Elk advies eindigt met een concreet trade setup: instapprijs (markt of pullback naar EMA21), " +
            "stop-loss op 1,5× ATR14 daily, Target 1 op 2× ATR en Target 2 op 3,5× ATR of het naaste weerstand-/steunniveau. " +
            "R/R-ratio en betrouwbaarheidsindicator (Laag/Gemiddeld/Hoog) zijn direct zichtbaar.");

        // ── v1.9 ─────────────────────────────────────────────────────────────
        AddVersionHeader("v1.9", "Sentiment Collector — automatisch nieuws ophalen");

        AddFeature("📰", "Achtergrond sentiment-collectie (15 min)",
            "De app haalt nu automatisch elke 15 minuten nieuws en posts op uit alle actieve bronnen. " +
            "Geen externe tool of taakplanner nodig — de timer draait direct in de app. " +
            "Start direct bij het openen van een portfolio en stopt netjes bij afsluiten.");

        AddFeature("🌐", "Reddit-connector (geen API-sleutel)",
            "Openbare subreddits worden uitgelezen via de gratis Reddit JSON-API. " +
            "Stel de bron in als 'r/CryptoCurrency' of gewoon 'CryptoCurrency'. " +
            "Titel en berichttekst worden gecombineerd voor een rijker matchingoppervlak.");

        AddFeature("📡", "RSS-connector (nieuwsfeeds)",
            "Elk RSS/Atom-feed kan als bron worden toegevoegd. " +
            "Voorbeelden: CoinDesk, Decrypt, The Block. " +
            "Titel en samenvatting worden samengevoegd voor sentimentanalyse.");

        AddFeature("⚡", "CryptoPanic-connector",
            "Het gratis CryptoPanic-eindpunt (geen API-sleutel) levert tot 50 recente nieuwsberichten. " +
            "Valutacodes in berichten worden automatisch toegevoegd aan de tekst " +
            "zodat coin-koppeling nauwkeuriger is.");

        AddFeature("🔍", "Batch coin-matching in-memory",
            "Elke bron wordt slechts EENMALIG opgehaald per ronde; daarna worden alle coins " +
            "in-memory gekoppeld via symbool (bijv. BTC) of naam (bijv. bitcoin). " +
            "Dit vervangt het oude schema waarbij elke coin × elke bron een apart API-verzoek deed.");

        AddFeature("🗂️", "Bronnen-pagina — ophaalknop & statistieken",
            "De bronnen-pagina toont nu een 'Ophalen'-knop voor directe handmatige run, " +
            "de tijdstip van de laatste run, en twee tellers: totale readings en readings van de afgelopen 24 uur.");

        // ── v1.7 ─────────────────────────────────────────────────────────────
        AddVersionHeader("v1.7", "Analyse — 7 nieuwe technische indicatoren");

        AddFeature("📐", "EMA Cross (9/21) — kruissignaal kort termijn",
            "Detecteert de meest recente kruising van EMA9 en EMA21. " +
            "Bullish cross (▲) = EMA9 kruist EMA21 omhoog, bevestigt een opwaartse trend. " +
            "Bearish cross (▼) = EMA9 kruist EMA21 omlaag. Toont ook het aantal dagen geleden.");

        AddFeature("📊", "RSI (14d) — dagelijkse Relative Strength Index",
            "Toont de RSI op basis van de laatste 14 dagelijkse slotkoersen. " +
            "Onder 30 = oversold (groen); boven 70 = overbought (rood). " +
            "Weegt ook mee in de gecombineerde signaalScore.");

        AddFeature("📏", "MA50% — afstand tot het 50-daags gemiddelde",
            "Geeft aan hoeveel procent de huidige prijs boven (+) of onder (-) het 50-daags " +
            "voortschrijdend gemiddelde ligt. Positief = groen (bullish), negatief = rood (bearish).");

        AddFeature("💪", "ADX — trendsterkte-indicator",
            "De Average Directional Index meet de sterkte van een trend ongeacht de richting. " +
            "Boven 25 = sterke trend (oranje); onder 20 = zijwaartse markt. " +
            "Helpt beoordelen hoe betrouwbaar andere signalen zijn.");

        AddFeature("🎯", "%B — positie binnen de Bollinger Bands",
            "Geeft aan waar de prijs zich bevindt binnen de Bollinger Bands (0–100). " +
            "0 = bij de onderste band (oversold); 100 = bij de bovenste band (overbought); 50 = midlijn. " +
            "Weegt ook mee in de gecombineerde signaalScore.");

        AddFeature("🗜️", "Squeeze — volatiliteitscompressie (BB vs. Keltner)",
            "Detecteert een Bollinger Squeeze: wanneer de Bollinger Bands smaller zijn dan het Keltner Channel " +
            "consolideer de prijs in een laag-volatiliteitsgebied. 'Aan' (groen) = squeeze actief, " +
            "verwacht een uitbraak; 'Af' (grijs) = geen squeeze.");

        AddFeature("🏔️", "52w% — afstand tot 52-weeks hoogte",
            "Toont hoeveel procent de huidige prijs onder het hoogste punt van de laatste 180 " +
            "dagelijkse slotkoersen (circa 6 maanden) ligt. 0% = op het all-time-high van de periode; " +
            "-50% = 50% onder het 52-weeks hoogtepunt.");

        // ── v1.6 ─────────────────────────────────────────────────────────────
        AddVersionHeader("v1.6", "Dashboard · Analyse · Trade Journal · Instellingen");

        AddFeature("📡", "Bronnen-pagina (Bronnen)",
            "Een nieuwe Bronnen-pagina waarmee je alle sentimentbronnen beheert: Reddit-communities, " +
            "RSS-nieuwsfeeds, CryptoPanic-endpoints en Telegram-kanalen. " +
            "17 kwalitatieve bronnen zijn vooraf ingeladen. " +
            "Voeg bronnen toe, bewerk of verwijder ze, en schakel ze per stuk aan/uit " +
            "zonder ze te verwijderen. De betrouwbaarheidsscore bepaalt hoeveel gewicht " +
            "een bron krijgt in de gecombineerde sentimentscore.");

        AddFeature("📊", "Dashboard — Signalen van vandaag",
            "Een nieuwe widget linksonder op het dashboard toont de zes hoogstscorende signalen " +
            "van de huidige evaluatieronde in één oogopslag: naam, richtingsbadge en gecombineerde score. " +
            "De lijst is kleurgecodeerd (Long = groen, Short = rood, Flat = grijs) en " +
            "werkt automatisch bij telkens de signal-engine draait.");

        AddFeature("🌍", "Dashboard — Marktregime met onderbouwing",
            "De Marktregime-kaart rechtsonder op het dashboard toont nu ook de nieuwskoppen die " +
            "geleid hebben tot de huidige BTC-regimeclassificatie (RiskOn / Neutral / RiskOff). " +
            "Tot zes redeneerregels van het laatste BTC-signaal zijn direct zichtbaar op het " +
            "dashboard, zodat je de marktcontext in één oogopslag kunt beoordelen.");

        AddFeature("📈", "Trendgrafieken (sparklines) op de Analyse-pagina",
            "Drie minigrafieken zijn toegevoegd aan de Analyse-pagina: " +
            "1H (laatste 14 1H-slotkoersen), 4H (laatste 30 4H-slotkoersen) en 1D (laatste 90 dagelijkse slotkoersen). " +
            "Elke sparkline is groen bij een positieve lineaire-regressiehelling (opwaartse trend) " +
            "en rood bij een negatieve helling, zodat je momentum per asset direct ziet.");

        AddFeature("📓", "Trade Journal verrijkt",
            "Het Trade Journal is uitgebreid met vijf nieuwe kolommen: " +
            "PnL in USDT én als percentage ten opzichte van de instapprijs, " +
            "R-multiple (hoeveel maal het initiële risico verdiend of verloren), " +
            "een vrij notitieveld per trade (bewerken via het potlood-icoon) en " +
            "een totaal-PnL-balk onderaan voor de actieve filter. " +
            "Open orders kunnen gefilterd worden op All / Open / Closed / Paper / Live.");

        AddFeature("🔔", "Telegram-notificaties",
            "Bij een Long- of Short-signaal boven de ingestelde drempelwaarde stuurt de app " +
            "automatisch een pushbericht naar je Telegram-chat of kanaal. " +
            "Configureer je Bot Token en Chat ID via Instellingen → Telegram Notificaties. " +
            "Gebruik de 'Verbinding testen'-knop om direct te controleren of alles werkt.");

        AddFeature("⚙️", "Instellingen — Signaaldrempel &amp; paper-trading",
            "De sectie 'Signalen &amp; Trading' in de Instellingen bevat een slider voor de minimale " +
            "gecombineerde score (50–85, standaard 60) waarop een signaal opgeslagen en " +
            "getoond wordt, plus een toggle voor paper-trading modus " +
            "(geen orders naar een exchange — alleen simulatie).");

        AddFeature("🛡️", "Instellingen — Risk-guardrails",
            "De sectie 'Risk-guardrails' geeft vier veiligheidslimieten: " +
            "maximaal portfolio-% per trade (1–25 %, standaard 5 %), " +
            "maximaal aantal open posities (1–20, standaard 5), " +
            "dagelijkse verlies-limiet als % van de portfolio (1–30 %, standaard 10 %) en " +
            "een kill-switch die alle geautomatiseerde trading direct pauzeert.");

        AddFeature("🗄️", "Instellingen — Databronnen-tab",
            "Een nieuwe tab 'Databronnen' in de Instellingen geeft een volledig overzicht van " +
            "alle externe en lokale bronnen die de app gebruikt: CoinGecko API, lokale koerscache, " +
            "SQLite-database, Reddit, RSS-nieuwsfeeds, CryptoPanic en Telegram. " +
            "Per bron is beschreven waarvoor hij dient en hoe hij bijgewerkt wordt.");

        // ── v1.5 ─────────────────────────────────────────────────────────────
        AddVersionHeader("v1.5", "UI-verbeteringen");

        AddFeature("↕", "Sorteerbare kolommen op de Analyse-pagina",
            "Klik op een kolomkop op de Analyse-pagina om assets op die indicator te sorteren. " +
            "Klik nogmaals om de volgorde om te draaien. " +
            "Een ▲ of ▼ pijl geeft de actieve sorteerkolom en richting aan.");

        AddFeature("💬", "Kolom-tooltips op de Analyse-pagina",
            "Beweeg over een kolomkop op de Analyse-pagina voor een korte uitleg van de indicator " +
            "en hoe je de waarde interpreteert. Handig voor traders die technische analyse leren.");

        // ── v1.4 ─────────────────────────────────────────────────────────────
        AddVersionHeader("v1.4", "Signal Engine &amp; Paper Trading");

        AddFeature("📊", "Analyse-pagina (Signalen)",
            "Een nieuwe Analyse-pagina toont alle assets met berekende technische indicatoren: " +
            "MACD, Bollinger Bands, ATR, StochRSI, sentimentscore, marktregime en gecombineerde signaalScore. " +
            "Gebruik 'Refresh Analysis' om ruwe TA-indicatoren opnieuw te berekenen, " +
            "of 'Evaluate Signals' om de volledige engine te draaien (TA + sentiment + regime).");

        AddFeature("⚙️", "Signal Engine",
            "De signal-engine combineert technische analyse, sentimentmetingen en het marktregime " +
            "tot één CombinedScore (0–100) en een richtingslabel (Long / Flat / Short) per asset. " +
            "Scores boven 60 zijn bullish; scores onder 40 zijn bearish.");

        AddFeature("🌍", "Marktregime-detectie",
            "De regime-service bewaakt BTC-dominantie en prijstrends om het marktklimaat te " +
            "classificeren als RiskOn, Neutral of RiskOff. " +
            "Dit regime weegt mee in elke signaalScore, zodat bullish TA-signalen worden " +
            "gedempt tijdens een RiskOff-markt.");

        AddFeature("📋", "Trade Journal (basis)",
            "Een nieuw Trade Journal registreert alle paper trades die vanuit de Analyse-pagina " +
            "geplaatst worden. Elk item toont het asset, richting (Long/Short), instapprijs, " +
            "bedrag (USDT), stop-loss, take-profit, status en de redenering achter de trade.");

        AddFeature("📝", "Paper trading vanuit de Analyse-pagina",
            "Druk op het handels-icoon aan het eind van een rij op de Analyse-pagina om het " +
            "paper-trade-dialoogvenster te openen. Vul richting, bedrag (USDT), stop-loss en " +
            "take-profit in en bevestig om de trade te loggen in het Trade Journal.");

        AddFeature("🪙", "Coin-logo's op de Analyse-pagina",
            "Asset-rijen op de Analyse-pagina tonen nu het coin-logo naast de naam, " +
            "zodat je de lijst sneller visueel kunt scannen.");

        // ── v1.3 ─────────────────────────────────────────────────────────────
        AddVersionHeader("v1.3", "Sentiment Collector");

        AddFeature("😊", "Sentimentscores",
            "Een achtergrondservice verzamelt sentimentdata uit meerdere bronnen en slaat een " +
            "voortschrijdende sentimentscore per asset op. De score is zichtbaar op de Analyse-pagina " +
            "onder de kolom Sentiment en wordt meegenomen in de gecombineerde signaalScore.");

        // ── v1.2 ─────────────────────────────────────────────────────────────
        AddVersionHeader("v1.2", "Uitgebreide technische indicatoren");

        AddFeature("📈", "MACD",
            "Moving Average Convergence Divergence. Een positieve waarde duidt op opwaarts momentum; " +
            "een negatieve waarde op neerwaarts momentum.");

        AddFeature("📉", "Bollinger Bands",
            "De bovenste en onderste band bepalen het verwachte koersbereik. " +
            "Een koers boven de bovenste band signaleert overbought; onder de onderste band oversold.");

        AddFeature("📏", "ATR — Average True Range",
            "Meet marktvolatiliteit. Een hogere ATR betekent grotere koersbewegingen en " +
            "vereist doorgaans bredere stop-loss-niveaus.");

        AddFeature("🔁", "StochRSI",
            "Een 0–100 oscillator. Waarden onder 20 wijzen op oversold (potentiële koop); " +
            "waarden boven 80 op overbought (potentiële verkoop).");
    }

    // -----------------------------------------------------------------------
    // Render helpers
    // -----------------------------------------------------------------------

    private void AddVersionHeader(string version, string subtitle)
    {
        if (ContentPanel.Children.Count > 0)
            ContentPanel.Children.Add(new Border { Height = 20 });

        // Version pill + subtitle row
        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Margin = new Thickness(0, 0, 0, 10),
            VerticalAlignment = VerticalAlignment.Center,
        };

        // Version badge
        var badge = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xB8, 0x86, 0x0B)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 3, 10, 3),
            VerticalAlignment = VerticalAlignment.Center,
        };
        badge.Child = new TextBlock
        {
            Text = version,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Colors.Black),
        };
        header.Children.Add(badge);

        // Subtitle
        header.Children.Add(new TextBlock
        {
            Text = subtitle,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xB8, 0x86, 0x0B)),
            VerticalAlignment = VerticalAlignment.Center,
        });

        ContentPanel.Children.Add(header);

        // Separator line
        ContentPanel.Children.Add(new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.FromArgb(0x40, 0xB8, 0x86, 0x0B)),
            Margin = new Thickness(0, 0, 0, 10),
        });
    }

    private void AddFeature(string icon, string title, string description)
    {
        var card = new Border
        {
            Background = (SolidColorBrush)Application.Current.Resources["SystemControlBackgroundChromeMediumLowBrush"],
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 0, 6),
        };

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
        };

        // Icon
        row.Children.Add(new TextBlock
        {
            Text = icon,
            FontSize = 18,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 0, 0),
            Width = 26,
        });

        // Text column
        var textCol = new StackPanel { Spacing = 3 };

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
            TextWrapping = TextWrapping.Wrap,
            Foreground = (SolidColorBrush)Application.Current.Resources["SystemControlForegroundBaseMediumBrush"],
        });

        row.Children.Add(textCol);
        card.Child = row;
        ContentPanel.Children.Add(card);
    }
}
