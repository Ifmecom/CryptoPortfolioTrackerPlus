using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace CryptoPortfolioTracker.Views;

public sealed partial class HelpView : Page
{
    public HelpView()
    {
        InitializeComponent();
    }

    private void View_Loaded(object sender, RoutedEventArgs e)
    {
        BuildContent();
    }

    // ───────────────────────────────────────────────────────────────────────────
    // Content builder
    // Elke sectie = één Expander. Voeg nieuwe secties toe onderaan de juiste
    // categorie. Gebruik AddSection(...) voor een nieuwe expander en
    // AddParagraph / AddBullets / AddNote / AddFormula voor de inhoud.
    // ───────────────────────────────────────────────────────────────────────────

    private void BuildContent()
    {
        // ── Aan de slag ───────────────────────────────────────────────────────
        AddCategoryHeader("🚀  Aan de slag");

        AddSection("Portfolio aanmaken en wisselen", items =>
        {
            AddParagraph(items,
                "Bij de eerste start vraagt de app om een portfolionaam. U kunt meerdere portfolio's aanmaken en " +
                "er tussen wisselen via het menu-item 'Portfolio wisselen'. Elke portfolio heeft een eigen " +
                "SQLite-database die lokaal op uw computer wordt opgeslagen.");
            AddBullets(items, new[]
            {
                "Nieuw portfolio: ga naar 'Portfolio wisselen' → klik 'Nieuw portfolio'.",
                "Naam wijzigen: klik het potloodpictogram naast de portfolionaam.",
                "Verwijderen: klik het prullenbakpictogram (niet ongedaan te maken).",
                "Kopiëren: maak een kopie van een bestaand portfolio als back-up of testomgeving.",
            });
        });

        AddSection("Coins toevoegen aan de bibliotheek", items =>
        {
            AddParagraph(items,
                "Voordat u een coin aan uw portfolio kunt toevoegen, moet deze in de Coin-bibliotheek staan. " +
                "De bibliotheek bevat alle door CoinGecko ondersteunde coins.");
            AddBullets(items, new[]
            {
                "Ga naar 'Coin-bibliotheek' in het menu.",
                "Zoek via de zoekbalk op naam of ticker.",
                "Klik op de ster om de coin als favoriet te markeren.",
                "Coins die al in uw portfolio zitten zijn grijs weergegeven.",
            });
        });

        AddSection("Transacties invoeren", items =>
        {
            AddParagraph(items,
                "Alle aankopen, verkopen en overdrachten worden als transacties opgeslagen. " +
                "Per transactie legt u vast: datum, type (koop/verkoop/overdracht), hoeveelheid, prijs en account.");
            AddBullets(items, new[]
            {
                "Open de Assets-pagina en klik op een asset om de details te zien.",
                "Klik op '+ Transactie' om een nieuwe transactie toe te voegen.",
                "Koop-transacties verhogen uw positie; verkoop-transacties verlagen ze.",
                "Overdrachten verplaatsen hoeveelheid tussen accounts zonder P&L-effect.",
                "MEXC-transacties kunnen automatisch worden gesynchroniseerd via de MEXC API-koppeling in Instellingen.",
            });
        });

        // ── Portfolio & Assets ────────────────────────────────────────────────
        AddCategoryHeader("📊  Portfolio & Assets");

        AddSection("P&L berekening (winst en verlies)", items =>
        {
            AddParagraph(items,
                "De P&L (Profit & Loss) wordt berekend als het verschil tussen de huidige marktwaarde en de gemiddelde kostprijs:");
            AddFormula(items, "P&L = (Huidige Prijs − Gemiddelde Kostprijs) × Hoeveelheid");
            AddParagraph(items,
                "De gemiddelde kostprijs is het gewogen gemiddelde van alle aankooptransacties. " +
                "Verkopen verlagen de hoeveelheid maar wijzigen de gemiddelde kostprijs niet.");
            AddBullets(items, new[]
            {
                "P&L % = (P&L / Totale Investering) × 100.",
                "Gerealiseerde P&L: winst/verlies op voltooide (verkochte) posities.",
                "Niet-gerealiseerde P&L: theoretisch resultaat op open posities op basis van actuele koers.",
                "Dagelijkse verandering: gebaseerd op de 24u-prijswijziging van CoinGecko.",
            });
        });

        AddSection("Accounts & spreiding", items =>
        {
            AddParagraph(items,
                "Met accounts kunt u uw coins over meerdere wallets of exchanges verdelen. " +
                "Elke coin kan op meerdere accounts staan; de totaalwaarde is de som van alle accounts.");
            AddBullets(items, new[]
            {
                "Maak accounts aan via de 'Accounts'-pagina.",
                "Wijs bij elke transactie een account toe.",
                "De taartgrafiek op de Dashboard-pagina toont de spreiding over accounts.",
                "Gebruik 'Overdracht' als transactietype om coins tussen accounts te verplaatsen.",
            });
        });

        AddSection("Narratieven & thematische groepering", items =>
        {
            AddParagraph(items,
                "Narratieven zijn thematische labels (bijv. 'DeFi', 'AI', 'Layer-2') waarmee u coins kunt groeperen. " +
                "Dit helpt bij het analyseren van sectorale spreiding.");
            AddBullets(items, new[]
            {
                "Maak narratieven aan via de 'Narratieven'-pagina.",
                "Wijs narratieven toe via de Coin-bibliotheek of het contextmenu van een asset.",
                "De Dashboard-pagina toont de P&L per narratief.",
            });
        });

        AddSection("Prijsniveaus instellen", items =>
        {
            AddParagraph(items,
                "Bij elk coin kunt u prijsniveaus markeren: een koersdoel, een stoploss of een interessant instapniveau. " +
                "De app geeft een melding als de koers een ingesteld niveau raakt.");
            AddBullets(items, new[]
            {
                "Ga naar de 'Prijsniveaus'-pagina en voeg een niveau toe.",
                "Typen: Target (doel), StopLoss, Support, Resistance, Entry.",
                "Kies 'Waarschuw me' om een Telegram-notificatie te ontvangen als het niveau wordt geraakt.",
            });
        });

        // ── Trade Journal ─────────────────────────────────────────────────────
        AddCategoryHeader("📓  Trade Journal");

        AddSection("Trades registreren", items =>
        {
            AddParagraph(items,
                "Het Trade Journal is bedoeld voor het bijhouden van bewuste handelsbeslissingen, " +
                "inclusief motivatie, resultaat en lessen. Een trade bestaat uit een entry en een exit.");
            AddBullets(items, new[]
            {
                "Open 'Trade Journal' in het menu en klik '+ Nieuwe trade'.",
                "Vul in: coin, richting (Long/Short), type (Live/Paper), entry-prijs, hoeveelheid en datum.",
                "Voeg een notitie toe met uw redenering ('Setup', 'Confluences').",
                "Sluit een trade door een exit-prijs en -datum in te vullen.",
            });
        });

        AddSection("R-multiple & prestatiemeting", items =>
        {
            AddParagraph(items,
                "De R-multiple (R) meet het resultaat als veelvoud van uw initiële risico (1R). " +
                "Een R van 2.0 betekent dat u tweemaal uw risico verdiend heeft.");
            AddFormula(items, "R = (Exit − Entry) / (Entry − StopLoss)   [Long]");
            AddFormula(items, "R = (Entry − Exit) / (StopLoss − Entry)   [Short]");
            AddParagraph(items, "Streef naar een gemiddelde R > 1.0 over meerdere trades voor een positieve verwachting.");
            AddBullets(items, new[]
            {
                "Paper trades worden meegeteld in statistieken maar apart gefilterd.",
                "De Statistieken-pagina toont win rate, gemiddelde R, Sharpe en totale P&L.",
            });
        });

        // ── Trade Advies ──────────────────────────────────────────────────────
        AddCategoryHeader("🎯  Trade Advies");

        AddSection("Hoe werkt de analyse?", items =>
        {
            AddParagraph(items,
                "De Trade Advies-pagina combineert drie signaaltypen tot één CombinedScore (0–100). " +
                "Op basis van deze score geeft de app een Long, Short of Geen signaal.");
            AddFormula(items, "CombinedScore = 50 + ((TaScore×0.60 + Sentiment×0.30 + Regime×0.10 − 50) × multiplier)");
            AddBullets(items, new[]
            {
                "TaScore (60%): gemiddelde van RSI, MACD, Bollinger, ATR en StochRSI over 1D/4H/1H.",
                "Sentiment (30%): genormaliseerde sentimentscore (Reddit, RSS, CryptoPanic, Telegram).",
                "Regime (10%): marktregime-score op basis van 50/200 MA-verhouding en trending/ranging.",
                "Score > 60 = Long-signaal; Score < 40 = Short-signaal; anders Geen signaal.",
            });
        });

        AddSection("Databronnen voor koersdata", items =>
        {
            AddParagraph(items,
                "De analyse haalt OHLCV-data (Open/High/Low/Close/Volume) op bij meerdere exchanges. " +
                "De app gebruikt een fallback-volgorde:");
            AddBullets(items, new[]
            {
                "1. Binance — primaire bron voor de meeste coins.",
                "2. KuCoin — fallback als de coin niet op Binance staat.",
                "3. Gate.io — tweede fallback.",
                "4. MEXC — derde fallback.",
                "Timeframes: 1D (dagelijks), 4H (vier-uurlijks), 1H (uurlijks).",
            });
        });

        AddSection("SL, TP en R/R uitleg", items =>
        {
            AddParagraph(items, "De app berekent automatisch stoploss en take-profit op basis van ATR (Average True Range):");
            AddFormula(items, "StopLoss   = Entry ± 1.5 × ATR");
            AddFormula(items, "TakeProfit1 = Entry ± 2.0 × ATR");
            AddFormula(items, "TakeProfit2 = Entry ± 3.5 × ATR");
            AddParagraph(items, "De Risk/Reward-ratio (R/R) is de verhouding tussen potentiële winst (TP1) en risico (SL).");
            AddNote(items, "ATR wordt berekend over 14 periodes op de dagelijkse tijdsframe.");
        });

        AddSection("Pivotpunten detectie", items =>
        {
            AddParagraph(items,
                "De app detecteert support- en resistance-niveaus via pivotpunten. " +
                "Een pivot high is een koers die hoger is dan de 5 candles ervoor én erna. " +
                "Clusters binnen 1,5% van elkaar worden samengevoegd tot één niveau.");
            AddBullets(items, new[]
            {
                "Lookback: 5 candles links en rechts.",
                "Clusterdrempel: 1,5% — niveaus binnen dit bereik worden samengevoegd.",
                "Sterkte: het aantal keer dat een niveau is geraakt.",
                "De sterkste pivots worden als support/resistance in de analyse gebruikt.",
            });
        });

        // ── Signalen ──────────────────────────────────────────────────────────
        AddCategoryHeader("📡  Signalen & TA-indicatoren");

        AddSection("Technische indicatoren uitgelegd", items =>
        {
            AddParagraph(items, "De app berekent de volgende indicatoren op basis van lokale dagelijkse koersdata (MarketChart JSON):");
            AddBullets(items, new[]
            {
                "RSI (14): Relative Strength Index. > 70 = overbought, < 30 = oversold.",
                "MA50 / MA200: voortschrijdend gemiddelde. Golden Cross (MA50 > MA200) = bullish.",
                "MACD: momentum-indicator. Signaal = MACD-lijn kruist signaallijn.",
                "Bollinger Bands (20, 2σ): volatiliteitsbanden rondom MA20.",
                "ATR (14): gemiddeld dagbereik, maatstaf voor volatiliteit.",
                "StochRSI (14, 3, 3): stochastische versie van RSI. > 80 overbought, < 20 oversold.",
            });
            AddNote(items, "Indicatoren worden berekend via de Skender.Stock.Indicators-bibliotheek op basis van lokale JSON-bestanden. Geen real-time data vereist.");
        });

        AddSection("Signaalregels configureren", items =>
        {
            AddParagraph(items,
                "Via de Instellingen kunt u signaalregels aanpassen: welke indicatoren meewegen, " +
                "drempelwaarden voor Long/Short en of meldingen verstuurd worden.");
            AddBullets(items, new[]
            {
                "Ga naar Instellingen → Signalen.",
                "Schakel individuele indicatoren aan/uit.",
                "Stel de minimale CombinedScore in voor een signaalmelding.",
                "Verbind met Telegram voor push-notificaties (zie §Notificaties).",
            });
        });

        // ── Statistieken ──────────────────────────────────────────────────────
        AddCategoryHeader("📈  Statistieken");

        AddSection("Overzicht van de statistieken-pagina", items =>
        {
            AddParagraph(items,
                "De Statistieken-pagina geeft een samenvatting van uw handelsprestaties op basis van de trades in het Journal.");
            AddBullets(items, new[]
            {
                "Totale P&L: som van gerealiseerde P&L van alle afgesloten trades.",
                "Win Rate: percentage winstgevende trades. Win Rate = Wins / (Wins + Losses) × 100.",
                "Gemiddelde winst / verlies: gemiddelde P&L van winnende resp. verliezende trades.",
                "Open posities: aantal trades zonder exit-datum.",
            });
        });

        AddSection("Periodefilter & aangepaste datum", items =>
        {
            AddParagraph(items,
                "Boven de statistieken staan twee filters: trade-type (Alle/Live/Paper) en periode.");
            AddBullets(items, new[]
            {
                "Alles: alle trades ongeacht datum.",
                "Deze maand: trades met entry in de huidige kalendermaand.",
                "Afgelopen 3 maanden: trades van de afgelopen 90 dagen.",
                "Dit jaar: trades vanaf 1 januari van het huidige jaar.",
                "Aangepast: kies een eigen start- en einddatum via de datumkiezers.",
            });
        });

        // ── Belasting ─────────────────────────────────────────────────────────
        AddCategoryHeader("🧾  Belasting (Box 3 — Nederland)");

        AddSection("Hoe werkt de Box 3-berekening?", items =>
        {
            AddParagraph(items,
                "De berekening volgt de post-kerstarrest-methode (2022 en later). " +
                "U geeft de werkelijke waarde van uw vermogenscomponenten op de peildatum (1 januari) op. " +
                "De app berekent de belasting in vijf stappen:");
            AddBullets(items, new[]
            {
                "1. Grondslag = max(0, Assets − Schulden)",
                "2. Belastbaar bedrag = max(0, Grondslag − Heffingsvrij Vermogen)",
                "3. Allocatie naar categorieën (evenredig aan aandeel in grondslag)",
                "4. Fictief rendement per categorie × categorie-aandeel",
                "5. Belasting = Totaal fictief rendement × Belastingtarief",
            });
            AddFormula(items, "Fictief rendement Crypto = Crypto-grondslag / Totale grondslag × Overige-beleggingen-tarief × Belastbaar bedrag");
            AddNote(items, "Banktegoeden hebben een lager fictief rendementspercentage dan overige beleggingen (incl. crypto). De exacte percentages verschillen per jaar.");
        });

        AddSection("Tarieven per jaar", items =>
        {
            AddParagraph(items, "De app ondersteunt de volgende belastingjaren:");
            AddBullets(items, new[]
            {
                "2022: HVV € 50.650 · Spaartarief 0,00% · Overig 5,53% · Belastingtarief 31%",
                "2023: HVV € 57.000 · Spaartarief 0,92% · Overig 6,17% · Belastingtarief 32%",
                "2024: HVV € 57.000 · Spaartarief 1,03% · Overig 6,04% · Belastingtarief 36%",
                "Bij een fiscale partner wordt het heffingsvrij vermogen verdubbeld.",
            });
            AddNote(items, "De berekening is informatief. Raadpleeg altijd een belastingadviseur voor uw definitieve aangifte.");
        });

        AddSection("Invoervelden belastingpagina", items =>
        {
            AddParagraph(items, "Op de Belasting-tab in Instellingen vult u de volgende waarden in op de peildatum (1 januari van het belastingjaar):");
            AddBullets(items, new[]
            {
                "Cryptoportfolio waarde (€): gebruik de Dashboard-waarde op 1 januari.",
                "Banktegoeden (€): saldo op al uw bankrekeningen.",
                "Overige beleggingen (€): aandelen, obligaties, vastgoedfondsen e.d.",
                "Schulden (€): schulden boven de drempelwaarde (€ 3.400 per persoon in 2024).",
                "Fiscaal partner: vink aan als u een fiscale partner heeft (verdubbelt HVV).",
            });
        });

        // ── Instellingen ──────────────────────────────────────────────────────
        AddCategoryHeader("⚙️  Instellingen");

        AddSection("Algemene instellingen", items =>
        {
            AddBullets(items, new[]
            {
                "Thema: kies Licht, Donker of Systeem (volgt Windows-thema).",
                "Taal: Nederlands of Engels. Herstart de app na een taalwijziging.",
                "Valuta: de basisvaluta voor waardeweergave (standaard EUR).",
                "Automatisch bijwerken: de app controleert bij start op een nieuwe versie.",
                "Koersen bijwerken: schakel automatische koersverversing aan/uit.",
            });
        });

        AddSection("Notificaties via Telegram", items =>
        {
            AddParagraph(items,
                "De app kan meldingen sturen via een Telegram-bot als een signaal of prijsniveau wordt geraakt.");
            AddBullets(items, new[]
            {
                "1. Maak een bot aan via @BotFather in Telegram en kopieer het API-token.",
                "2. Start een chat met uw bot en stuur een willekeurig bericht.",
                "3. Haal uw Chat ID op via: https://api.telegram.org/bot{TOKEN}/getUpdates",
                "4. Vul het token en chat ID in onder Instellingen → Notificaties.",
                "5. Klik 'Test' om te controleren of de verbinding werkt.",
            });
        });

        AddSection("Exchange API-koppelingen", items =>
        {
            AddParagraph(items,
                "Voor automatische transactie-import (momenteel MEXC) voert u een API-sleutel in. " +
                "De sleutel wordt versleuteld opgeslagen met RSA.");
            AddBullets(items, new[]
            {
                "Ga naar Instellingen → Exchange-accounts.",
                "Voer de API-key en Secret in (lees-rechten zijn voldoende — geen handelsrechten nodig).",
                "De app synchroniseert spot-trades automatisch bij elke start.",
                "Binance, KuCoin, Gate.io en MEXC worden ondersteund voor koersdata (geen transactie-import).",
            });
            AddNote(items, "Bewaar uw API-secret op een veilige plek. De app slaat deze versleuteld op, maar de plaintext is nooit meer terug te halen na het opslaan.");
        });

        // ── Databronnen ───────────────────────────────────────────────────────
        AddCategoryHeader("🌐  Databronnen & privacy");

        AddSection("Overzicht externe API's", items =>
        {
            AddBullets(items, new[]
            {
                "CoinGecko (gratis tier): actuele koersen, 24u-volume, marktkapitalisatie en historische data.",
                "Binance REST API: OHLCV-data voor Trade Advies (geen account vereist).",
                "KuCoin REST API: OHLCV-data als Binance-fallback.",
                "Gate.io REST API: OHLCV-data als tweede fallback.",
                "MEXC REST API: OHLCV-data én transactie-import (API-key vereist voor import).",
                "CryptoPanic: nieuwsscore voor sentimentberekening (optioneel API-key voor meer data).",
                "Reddit (publieke feed): r/CryptoCurrency posts voor sentimentanalyse.",
                "RSS-feeds: configureerbare nieuwsbronnen voor sentimentanalyse.",
                "Telegram Bot API: push-notificaties (vereist eigen bot-token).",
            });
        });

        AddSection("Lokale opslag & privacy", items =>
        {
            AddParagraph(items,
                "Alle portfoliodata wordt lokaal opgeslagen. Er worden geen portfoliogegevens naar externe servers gestuurd.");
            AddBullets(items, new[]
            {
                "Database: SQLite-bestand in %AppData%\\CryptoPortfolioTracker\\",
                "Koersgrafieken: MarketChart_{id}.json in de Charts-map (lokale cache).",
                "Logbestand: log.txt in %AppData%\\CryptoPortfolioTracker\\ (max. 3 dagen bewaard).",
                "Instellingen: settings.json in %AppData%\\CryptoPortfolioTracker\\",
                "API-sleutels worden nooit in plaintext opgeslagen — altijd RSA-versleuteld.",
            });
        });

        // ── FAQ ───────────────────────────────────────────────────────────────
        AddCategoryHeader("❓  Veelgestelde vragen");

        AddSection("Koersen worden niet bijgewerkt — wat nu?", items =>
        {
            AddBullets(items, new[]
            {
                "Controleer uw internetverbinding.",
                "CoinGecko heeft een limiet op het aantal gratis API-verzoeken per minuut. Wacht even en probeer opnieuw.",
                "Controleer of de coin in de Coin-bibliotheek als actief staat.",
                "Bekijk het logbestand (log.txt) voor foutmeldingen.",
            });
        });

        AddSection("De app start niet of crasht direct", items =>
        {
            AddBullets(items, new[]
            {
                "Controleer of er geen andere instantie van de app al actief is (systeemvak of taakbeheer).",
                "Herstart de computer en probeer opnieuw.",
                "Controleer het logbestand in %AppData%\\CryptoPortfolioTracker\\log.txt.",
                "Verwijder het bestand 'settings.json' om de instellingen te resetten (portfoliodata blijft bewaard).",
            });
        });

        AddSection("Hoe maak ik een back-up?", items =>
        {
            AddParagraph(items,
                "Kopieer de volledige map %AppData%\\CryptoPortfolioTracker\\ naar een veilige locatie. " +
                "De .db-bestanden bevatten al uw portfoliodata.");
            AddBullets(items, new[]
            {
                "Herstel: kopieer de .db-bestanden terug en herstart de app.",
                "U kunt ook via 'Portfolio wisselen' → 'Kopieer portfolio' een kopie binnen de app aanmaken.",
            });
        });

        AddSection("Mijn MEXC-trades worden niet gesynchroniseerd", items =>
        {
            AddBullets(items, new[]
            {
                "Controleer of de API-key is ingevoerd onder Instellingen → Exchange-accounts.",
                "Zorg dat de API-key lees- en handelshistorie-rechten heeft.",
                "De app synchroniseert alleen spot-trades — geen futures of margin.",
                "Raadpleeg het logbestand voor de exacte foutmelding.",
            });
        });
    }

    // ───────────────────────────────────────────────────────────────────────────
    // Helper-methoden
    // ───────────────────────────────────────────────────────────────────────────

    private void AddCategoryHeader(string title)
    {
        var tb = new TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 184, 134, 11)), // DarkGoldenrod
            Margin = new Thickness(4, 16, 0, 4),
        };
        ContentPanel.Children.Add(tb);
    }

    private void AddSection(string header, Action<StackPanel> contentBuilder)
    {
        var inner = new StackPanel { Spacing = 6, Margin = new Thickness(4, 0, 4, 0) };
        contentBuilder(inner);

        var expander = new Expander
        {
            Header = header,
            Content = inner,
            IsExpanded = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        ContentPanel.Children.Add(expander);
    }

    private static void AddParagraph(StackPanel parent, string text)
    {
        parent.Children.Add(new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Opacity = 0.9,
        });
    }

    private static void AddBullets(StackPanel parent, string[] items)
    {
        var sp = new StackPanel { Spacing = 3, Margin = new Thickness(8, 2, 0, 2) };
        foreach (var item in items)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            row.Children.Add(new TextBlock { Text = "•", FontSize = 13, Opacity = 0.7 });
            row.Children.Add(new TextBlock
            {
                Text = item,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                Opacity = 0.85,
            });
            sp.Children.Add(row);
        }
        parent.Children.Add(sp);
    }

    private static void AddFormula(StackPanel parent, string formula)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(30, 184, 134, 11)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 2, 0, 2),
        };
        border.Child = new TextBlock
        {
            Text = formula,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
        };
        parent.Children.Add(border);
    }

    private static void AddNote(StackPanel parent, string note)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(20, 100, 149, 237)),  // cornflowerblue tint
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 2, 0, 2),
        };
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        sp.Children.Add(new TextBlock { Text = "ℹ", FontSize = 13 });
        sp.Children.Add(new TextBlock
        {
            Text = note,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Opacity = 0.85,
        });
        border.Child = sp;
        parent.Children.Add(border);
    }
}
