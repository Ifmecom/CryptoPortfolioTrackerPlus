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
        // ── v1.36 ────────────────────────────────────────────────────────────
        AddVersionHeader("v1.36", "Risk-managed trading · positiegrootte op risico");

        AddFeature("📐", "Positiegrootte op basis van risico",
            "In de paper-trade dialoog vul je nu een 'Risico %' in (standaard je ingestelde max % per trade) en " +
            "berekent de knop het inlegbedrag zó dat je verlies bij de stop-loss precies dat percentage van je " +
            "kapitaal is. Een live indicator laat continu zien hoeveel % er op het spel staat en wáárschuwt zodra " +
            "je je eigen limiet overschrijdt. Een actieve kill-switch toont een duidelijke melding.");

        AddFeature("🛡️", "Risico-dashboard in het Trade Journal",
            "Een nieuwe knop 'Risico' opent een portfolio-breed overzicht: aantal open posities (vs je limiet), " +
            "totaal open risico (som van verlies-bij-SL), blootstelling, dag-P&L en de dagelijkse verlieslimiet — " +
            "met duidelijke guardrail-waarschuwingen wanneer je een grens nadert of overschrijdt. Zo benut je " +
            "eindelijk álle risk-instellingen die je hebt ingesteld.");

        AddFeature("⚖️", "Kies je kapitaalbasis: paper of echte portfolio",
            "In Instellingen → Risk-guardrails kies je nu waartegen de risico-berekeningen rekenen: het virtuele " +
            "paper-kapitaal (bedrag instelbaar) óf je werkelijke portfoliowaarde. Zowel de positiegrootte als het " +
            "risico-dashboard volgen die keuze — geen verwarrende mix meer van paper-sizing met je echte saldo.");

        // ── v1.35 ────────────────────────────────────────────────────────────
        AddVersionHeader("v1.35", "Synergie tussen tabs · kwaliteitsoordeel & feedback-loop");

        AddFeature("Ⓕ", "Fundamentele kwaliteit naast je technische setups",
            "De Fundamental Score (0-100) + verdict verschijnt nu als badge in Pattern Trading, 3% Trading, Trade Advies " +
            "én de Setup Tracker, naast de technische score. Zo zie je in één oogopslag of een mooie technische setup ook " +
            "fundamenteel hout snijdt — of juist een 'Avoid' is.");

        AddFeature("📈", "Score-kalibratie: wat werkte er in jouw praktijk?",
            "De Setup Tracker toont nu per scoreklasse (0-40 / 41-60 / 61-80 / 81-100) de wérkelijk behaalde win-rate " +
            "en gemiddelde R uit je gesloten setups. Dit is de feedback-loop op de TradabilityScore: je ziet of een " +
            "hogere score in de praktijk ook echt beter presteerde (klassen met te weinig trades worden gedimd getoond).");

        AddFeature("🔗", "Portfolio-correlatie met BTC",
            "Nieuwe knop in de Assets-header opent een diversificatie-analyse: per holding de correlatie met BTC " +
            "(op 60 dagrendementen) plus een waarde-gewogen oordeel — beweegt je portfolio grotendeels met BTC mee, " +
            "of is het goed gespreid? Hergebruikt de correlatie-engine van de 3% Trading-tool.");

        AddFeature("🧭", "Gedeelde marktcontext-balk",
            "Pattern Trading, 3% Trading en de Setup Tracker tonen nu bovenaan dezelfde compacte marktcontext: " +
            "het BTC-regime (Risk-On/Neutraal/Risk-Off), de Fear & Greed-index en het eerstvolgende macro-event " +
            "(FOMC/CPI/NFP/PCE). Eén gedeelde, gecachte bron — zo handel je altijd met het bredere plaatje in beeld.");

        AddFeature("💬", "Eigen sentiment voedt de Fundamentals-score",
            "Het sentiment dat de app zelf verzamelt (Reddit/RSS) telt nu mee in de Community-factor van de " +
            "Fundamental Score — een bescheiden bijsturing bovenop de CoinGecko-cijfers. Het detailvenster toont " +
            "de gebruikte sentiment-waarde. Geen extra API-calls: de data lag al klaar op de coin.");

        AddFeature("💧", "Liquiditeitscheck in Pattern Trading",
            "Nieuwe knop 'Check liquiditeit' haalt voor de getoonde setups het Binance-orderboek op en labelt elke " +
            "setup als Liquide / Matig / ⚠ Dun (op basis van bid-ask spread en orderboekdiepte) — dezelfde " +
            "liquiditeits-gatekeeper als 3% Trading. Bewust on-demand (apart van de scan) zodat de patroonanalyse snel blijft.");

        AddFeature("🕒", "Macro-events met tijd in jouw tijdzone",
            "FOMC, CPI, NFP en PCE tonen nu de precieze releasetijd omgerekend naar jouw lokale tijdzone " +
            "(FOMC 14:00 ET, data-releases 08:30 ET → automatisch met zomertijd verrekend). Zichtbaar in de " +
            "marktcontext-balk, het 3%-detailvenster en het Trade Advies.");

        // ── v1.34 ────────────────────────────────────────────────────────────
        AddVersionHeader("v1.34", "Fundamentele analyse · Fundamental Score (0-100) per coin");

        AddFeature("📊", "Nieuw tabblad: Fundamentals",
            "Een nieuwe pagina maakt de fundamentals van je coins inzichtelijk en kent een objectieve " +
            "Fundamental Score (0-100) toe volgens een professioneel due-diligence-raamwerk. De lijst toont " +
            "per coin de market cap, FDV, 24u-volume, volume/market-cap-ratio, circulerend aanbod en de score " +
            "met verdict (Exceptional → Avoid). Coins worden gesorteerd op score.");

        AddFeature("🔎", "Per coin ophalen + detailvenster",
            "Klik op 'Analyseer' bij een coin om de fundamentals on-demand bij CoinGecko op te halen (aanbod, " +
            "FDV, ATH/ATL, categorieën, links/whitepaper, GitHub-activiteit en community-cijfers). Het detailvenster " +
            "toont alle cijfers plus de zes factor-subscores (Tokenomics, Liquiditeit, Waardering, Community, " +
            "Development, Projectvolledigheid) met balken, zodat je ziet wáárom een coin zijn score krijgt.");

        AddFeature("🤝", "Eerlijke hybride score",
            "Wat meetbaar is, scoort de tool automatisch. Wat dat niet is — team, product-maturiteit, adoptie, " +
            "revenue en token-unlocks — komt via handmatige due-diligence (binnenkort) en verhoogt de " +
            "betrouwbaarheid van de totaalscore. Geen verzonnen cijfers: een 'Betrouwbaarheid'-indicator laat " +
            "zien hoeveel van het raamwerk daadwerkelijk is onderbouwd.");

        AddFeature("💾", "Opgeslagen met datum — geen onnodige API-calls",
            "Eenmaal opgehaalde fundamentals worden met een datum bewaard en blijven staan; bij het openen wordt " +
            "niets opnieuw afgeroepen. Elke coin toont hoe oud de data is en krijgt na de versheidsdrempel een " +
            "'verouderd'-label. De knop 'Verouderde' haalt in één keer alleen op wat nodig is en slaat verse coins over. " +
            "De versheidsdrempel (standaard 7 dagen) is instelbaar op de pagina.");

        AddFeature("⭐", "Favorieten + 'Refresh favorieten'",
            "Markeer tot 10 coins als favoriet met de ster; favorieten staan bovenaan en zijn met één filter te tonen. " +
            "De knop 'Favorieten' ververst in één klik alleen je favoriete coins — ideaal om je vaste shortlist " +
            "actueel te houden zonder alles af te roepen.");

        AddFeature("📝", "Handmatige due-diligence + SWOT-rapport",
            "In het detailvenster beoordeel je nu zelf team, product-maturiteit, adoptie, revenue en unlock-risico " +
            "via sliders (0-10). Je oordeel telt mee in de totaalscore en tilt de betrouwbaarheid omhoog. Daaronder " +
            "staat een automatisch SWOT-rapport: sterktes, zwaktes, kansen, bedreigingen, een risiconiveau " +
            "(LAAG/MIDDEL/HOOG), een heuristische waarderingsconclusie en de top-risico's — volledig afgeleid van " +
            "de cijfers, geen black box.");

        AddFeature("🔒", "On-chain TVL via DefiLlama",
            "Voor DeFi-protocollen toont de Fundamentals-tool nu de Total Value Locked (TVL) en de market-cap/TVL-ratio — " +
            "een sterke waarderingsmaat die ook in het SWOT-rapport meeweegt. Gratis via DefiLlama, zonder API-sleutel. " +
            "Coins zonder DeFi-protocol (zoals BTC) hebben simpelweg geen TVL. Let op: token-unlock-schema's zitten achter " +
            "de betaalde DefiLlama-API en zijn daarom niet opgenomen — unlock-risico beoordeel je via de DD-slider.");

        AddFeature("⚖️", "TVL telt nu mee in de Fundamental Score",
            "De On-Chain-factor (TVL-omvang + market-cap/TVL-efficiëntie) weegt nu 12% mee in de score van DeFi-coins. " +
            "Voor niet-DeFi-coins (zonder TVL) vervalt de factor en worden de overige factoren gehernormaliseerd, zodat " +
            "ze niet oneerlijk worden afgestraft. De factor-balken in het detailvenster tonen de On-Chain-score wanneer van toepassing.");

        // ── v1.33 ────────────────────────────────────────────────────────────
        AddVersionHeader("v1.33", "3% Trading-tool · Gekalibreerd scoremodel · Robuustere advieslogica");

        AddFeature("🎯", "Nieuw tabblad: 3% Trading",
            "Een gekalibreerd scoremodel met een vast netto-doel van +3% na fees en slippage. " +
            "Fase 1 (Kalibratie) draait een backtest over maximaal 1000 candles en meet per scoreklasse " +
            "(0-40 / 41-60 / 61-80 / 81-100) de werkelijke hitrate en expectancy. Fase 2 (Live Scan) " +
            "scoort al je coins en koppelt de score aan die gemeten hitrate — een 'kans' is dus een " +
            "historisch gemeten waarschijnlijkheid, geen aanname.");

        AddFeature("📊", "Zeven-factor model met gatekeepers",
            "Trend, momentum, volume/OBV, volatiliteit en support/resistance bepalen de score. " +
            "Liquiditeit (orderboek-spread & diepte) en positionering (funding rate, long/short-ratio via " +
            "Binance Futures) fungeren als gatekeeper: setups met te dunne liquiditeit of extreme funding " +
            "worden eruit gefilterd, hoe mooi de technische analyse ook is.");

        AddFeature("🔗", "Marktregime + correlatie-diversificatie",
            "Het regime wordt bepaald via BTC EMA50/200 (golden/death cross) plus BTC-dominantie. " +
            "De live scan bouwt een gediversifieerde shortlist: onderling sterk gecorreleerde alts tellen " +
            "als één trade, zodat je niet vijf keer dezelfde BTC-weddenschap aangaat. Elke setup heeft een " +
            "detailvenster met indicatoren, S/R, positionering, invalidatieniveau en aankomende macro-events.");

        AddFeature("🛡️", "Trade Advies & Pattern Trading: ongeldige setups gemarkeerd",
            "SL/TP-niveaus worden nu bij het genereren gevalideerd. Een degenerate setup (bijvoorbeeld als de " +
            "ATR tijdelijk 0 is, waardoor de stop-loss op de instapprijs zou vallen) wordt duidelijk als " +
            "ongeldig gemarkeerd, en een krappe risk/reward (< 1,5:1) krijgt een waarschuwing.");

        AddFeature("💧", "Markt-context in Trade Advies",
            "Het advies toont nu ook liquiditeit (spread & orderboekdiepte), positionering (funding & " +
            "long/short-ratio) en een waarschuwing voor macro-events (FOMC, CPI, NFP, PCE) binnen de horizon.");

        AddFeature("🧪", "Robuustere kern + meer tests",
            "De setup-niveau-berekeningen zijn ontdubbeld naar één gedeelde, geteste rekenkern, en de " +
            "API-aanroepen delen één cache. Het testraamwerk telt nu 183 unit tests (was 40).");

        AddFeature("📝", "3% Trading — paper trades activeren & forward-testen",
            "Vanuit de Live Scan kun je een setup nu met één klik als paper trade activeren (zelfde dialoog " +
            "als elders, met entry/SL/TP voorgevuld). Een nieuw tabblad 'Paper Trades' toont uitsluitend deze " +
            "3%-trades met live win-rate en P&L. Druk op Vernieuwen om pending orders te vullen en TP/SL te laten " +
            "triggeren op de actuele koers — zo test je met live data of de strategie echt werkt, zonder echt geld.");

        // ── v1.32 ────────────────────────────────────────────────────────────
        AddVersionHeader("v1.32", "Setup Tracker verbeterd · Instap-/sluitingstijden · TDD-testraamwerk");

        AddFeature("⚠️", "Bevestiging vereist als TP1 nog niet bereikt is",
            "Als je een trade handmatig als Gewonnen markeert terwijl de koers TP1 nog niet heeft aangeraakt, " +
            "verschijnt er een bevestigingsdialoog met de afstand tot TP1 in procenten. " +
            "Dit voorkomt dat trades per ongeluk worden gesloten op de huidige koers in plaats van op het doelopunt.");

        AddFeature("📥", "Instap- en sluitingstijden op Setup Tracker-kaarten",
            "Elke setup toont nu wanneer de entry-prijs werd geraakt (📥) en wanneer TP1/SL werd bereikt (📤). " +
            "De tijd wordt weergegeven als HH:mm voor vandaag, als datum+tijd voor oudere trades. " +
            "Bestaande Won/Lost/Open trades zijn automatisch gevuld met de AddedAt-datum als benadering.");

        AddFeature("🧪", "Unit test-raamwerk opgezet (TDD)",
            "40 unit tests dekken de kernlogica af: Setup Tracker TP/SL-detectie (13 tests), " +
            "model-berekeningen zoals PnlPct en RiskReward (8 tests), " +
            "patroonherkenning-score (6 tests) en string-formatters (8 tests). " +
            "Tests draaien met dotnet test en zijn altijd uitvoerbaar zonder VS-build.");

        // ── v1.31 ────────────────────────────────────────────────────────────
        AddVersionHeader("v1.31", "Patroonherkenning verbeterd · Trade Advies consistent met Pattern Trading");

        AddFeature("📐", "Falling/Rising Wedge — correcte geometrische convergentie",
            "Wigpatronen worden nu herkend op basis van echte geometrische convergentie: " +
            "de bandbreedte aan het einde moet ≥30% smaller zijn dan aan het begin. " +
            "De ingetekende lijnen lopen zichtbaar naar elkaar toe — een falling wedge staat nu bovenaan breder en " +
            "onderaan smal, zoals het hoort. Kanalen worden uitgesloten als de lijnen convergeren.");

        AddFeature("📊", "Swing-detectie — significantiefilter van 0,5%",
            "Kleine micro-swings die binnen 0,5% van hun buurstaven liggen worden niet meer als zwaaipunt geteld. " +
            "Dit elimineert ruis en zorgt dat trendlijnen alleen op echte koerswendepunten zijn gebaseerd.");

        AddFeature("🔍", "Keerpuntpatronen vereisen een voorafgaande trend",
            "Hoofd-en-schouders, dubbele top/bodem en inverse varianten worden alleen herkend als er " +
            "vooraf een duidelijke trend aanwezig is (≥15% koersbeweging over de laatste 50 dagbars). " +
            "Dit voorkomt dat keerpuntpatronen worden gesignaleerd in een zijwaartse markt.");

        AddFeature("⏳", "Verlopen patronen worden gefilterd",
            "Patronen waarbij de huidige koers het sleutelniveau (neckline, top, bodem) al meer dan 8% heeft " +
            "gepasseerd worden automatisch buiten beschouwing gelaten — de setup is al uitgespeeld.");

        AddFeature("🔗", "Trade Advies en Pattern Trading altijd consistent",
            "De score en richting in Trade Advies worden nu berekend met dezelfde live engine als Pattern Trading " +
            "(op basis van vers opgehaalde OHLCV-data: EMA-cross, RSI, MACD, ADX, %B, Squeeze). " +
            "Eerder gebruikte Trade Advies een verouderde DB-waarde van de Signal Engine, " +
            "waardoor dezelfde coin tegelijkertijd Long en Short kon tonen. Dit is opgelost.");

        // ── v1.30 ────────────────────────────────────────────────────────────
        AddVersionHeader("v1.30", "Setup Tracker · S/R met timeframe-label · Brede zoekbalk");

        AddFeature("📋", "Setup Tracker — win rate monitoring",
            "Nieuw tabblad 'Setup Tracker' in het hoofdmenu. Klik op 'Volg trade setup' bij een Pattern Trading coin " +
            "om de setup te locken met entry, SL, TP1, TP2 en patronen. " +
            "De tracker meet automatisch of de koers TP1 of de SL bereikt en werkt de status bij na elke analyse-run. " +
            "Bovenin zie je je win rate — doel is >50% winstgevende closes. " +
            "Filter op Watching / Gewonnen / Verloren / Verlopen en verwijder afgeronde setups handmatig.");

        AddFeature("📝", "Neem papertrade vanuit Pattern Trading",
            "Nieuwe knop 'Neem papertrade' op elke coin-kaart. De bestaande papertrade-dialog opent automatisch " +
            "met entry, stop-loss, TP1 en TP2 alvast ingevuld vanuit de patroonanalyse. " +
            "Pas aan waar nodig en bevestig om de trade te loggen in het Trade Journal.");

        AddFeature("📊", "S/R-lijnen in chart met timeframe-label",
            "Support- en resistancelijnen in de interactieve grafiek tonen nu het timeframe waarop ze zijn vastgesteld: " +
            "bijv. 'S-4H' of 'R-1D'. Zo is in één oogopslag duidelijk hoe zwaar een niveau weegt.");

        AddFeature("🔍", "Bredere zoekbalk in watchlijst-paneel",
            "Het zoekvak voor het toevoegen van coins aan de watchlijst is flink verbreed " +
            "zodat lange coin-namen niet worden afgekapt.");

        AddFeature("🟡", "Setup Tracker — 'In Trade'-status en live koers",
            "De Setup Tracker detecteert nu automatisch wanneer een trade is ingegaan: " +
            "zodra de live koers de instapprijs bereikt (Long: koers ≤ entry; Short: koers ≥ entry) " +
            "schakelt de status over van 'Watching' naar '🟡 In Trade'. " +
            "De live koers verschijnt direct onder de coin-naam en ticker op elke kaart, " +
            "samen met de procentuele afstand tot de instapprijs (+ = richting van winst). " +
            "Bovenin de pagina staat een extra teller 'In Trade' naast Watching, Won en Lost. " +
            "Filter snel op alle lopende trades via de '🟡 In Trade'-knop in de filterbalk.");

        AddFeature("📊", "Setup Tracker — P&L en ongerealiseerde P&L",
            "Elke kaart toont nu het P&L-resultaat, passend bij de status: " +
            "Gewonnen/Verloren setups tonen het gerealiseerde rendement in % ten opzichte van de instapprijs. " +
            "Lopende trades (In Trade) tonen de ongerealiseerde P&L % live op basis van de actuele koers. " +
            "Beide waarden zijn groen bij winst en rood bij verlies.");

        AddFeature("💬", "Tooltips doorvoeren in de hele app",
            "Alle status-badges, filterknoppen, sorteeropties en actieknoppen in Setup Tracker, " +
            "Pattern Trading, Trade Journal en Signalen hebben nu informatieve tooltips. " +
            "Beweeg over een status-badge om een uitleg te zien van wat die status betekent.");

        AddFeature("⚡", "Setup Tracker — automatische koersverversing",
            "De Setup Tracker ververst de live koersen en statussen automatisch na elke " +
            "koersupdatecyclus van de app — zonder dat je handmatig op 'Vernieuwen' hoeft te klikken. " +
            "Een tijdstempel naast de Vernieuwen-knop toont wanneer de prijzen voor het laatst zijn bijgewerkt.");

        AddFeature("📈", "Statistieken — Setup Strategie tab",
            "Een nieuw tabblad 'Setup Strategie' in de Statistieken-pagina laat zien hoe goed je setupstrategie presteert. " +
            "Kernmetrics: Win Rate TP1 (hoeveel setups TP1 bereiken), Win Rate TP2, Profit Factor en verwachte opbrengst per setup. " +
            "Breakdowntabellen tonen de win rate per richting (Long/Short), per score-klasse en per marktregime (BTC). " +
            "De periode-filter van het Trade Journal tabblad geldt ook voor de setup statistieken.");

        AddFeature("🔗", "Setups gekoppeld aan paper trades",
            "Wanneer je een paper trade opent voor een coin die al in de Setup Tracker staat, " +
            "worden de order en de setup automatisch aan elkaar gekoppeld. " +
            "Zo is direct terug te zien welke setup tot welke trade heeft geleid.");

        AddFeature("🌍", "Marktregime vastgelegd bij aanmaken setup",
            "Bij het toevoegen van een setup aan de tracker wordt het BTC-marktregime op dat moment opgeslagen " +
            "(RiskOn / Neutral / RiskOff). De Setup Strategie-statistieken gebruiken dit om te laten zien " +
            "in welk marktklimaat jouw setups het beste presteren.");

        AddFeature("✅", "TP2 tracking in Setup Tracker",
            "De Setup Tracker detecteert nu ook wanneer TP2 wordt bereikt. " +
            "In de statistieken zie je hoeveel procent van de gewonnen setups ook TP2 heeft gehaald, " +
            "zodat je kunt beoordelen of het lonen om een tweede doelkoers in te stellen.");

        // ── v1.25 ────────────────────────────────────────────────────────────
        AddVersionHeader("v1.25", "Pattern Trading — Volledig vernieuwd en uitgebreid");

        AddFeature("⏱", "15-minuten timeframe toegevoegd",
            "De multi-timeframe architectuur is uitgebreid met een vierde timeframe: 15M. " +
            "Elke coin wordt nu geanalyseerd op 1D, 4H, 1H én 15M. " +
            "Op elke card staan vier biasbadges (kleurgecodeerd) en patronen worden ook op 15M gedetecteerd. " +
            "In de interactieve grafiek staat een nieuwe '15M'-knop. " +
            "Wanneer je op een 15M-patroonbadge klikt opent de grafiek direct in het 15M-timeframe.");

        AddFeature("👁", "Watchlijst volledig vernieuwd",
            "De watchlijst heeft een eigen uitklapbaar Expander-paneel gekregen bovenaan de pagina. " +
            "Het paneel toont alle coins op je watchlijst als chips; elke chip heeft een ✕-knop om direct te verwijderen. " +
            "Zoeken werkt met het zoekvak in het paneel — begin te typen en selecteer een coin uit de suggesties. " +
            "Een voortgangsring geeft aan dat er gezocht wordt. " +
            "Foutmeldingen (bijv. CoinGecko rate-limit) worden direct in de statusbalk getoond. " +
            "De dubbele-uitvoering bij suggestie-klik is verholpen.");

        AddFeature("🔗", "TF-conflict waarschuwing",
            "Wanneer 1D en 4H een tegengestelde bias hebben (bijv. 1D Bullish maar 4H Bearish) " +
            "verschijnt automatisch een oranje '⚠ TF-conflict 1D/4H' label op de coin-kaart. " +
            "Dit waarschuwt voor tegengestelde signalen op verschillende timeframes.");

        AddFeature("📊", "Sorteer- en filteropties",
            "Drie sorteeropties: op score (standaard), op 24u verandering, of op afstand tot de dichtste weerstand. " +
            "Tijdframe-filter: toon alleen coins met patronen op een specifiek TF (1D / 4H / 1H / 15M). " +
            "In-lijst zoeken: zoek direct in de geladen resultaten op naam of ticker-symbol.");

        AddFeature("✨", "UX-verbeteringen",
            "Bull flag en bear flag tonen nu annotaties in de grafiek: pijlen voor pool-start/top en hlines voor breakout/breakdown. " +
            "Typo 'Ascendng. Driehoek' hersteld naar 'Oplopende Driehoek'. " +
            "Overflow-chip '+N' verschijnt wanneer een coin meer dan 6 sterke patronen heeft. " +
            "Stalenessmelding toont hoe lang geleden de analyse is uitgevoerd (oranje na 1 uur). " +
            "Leegstaatmelding wanneer geen coins aan het filter voldoen. " +
            "Lijstweergave omgezet naar ListView voor UI-virtualisatie bij grote portfolio's.");

        // ── v1.24 ────────────────────────────────────────────────────────────
        AddVersionHeader("v1.24", "Pattern Trading — Pattern Handboek venster");

        AddFeature("📖", "Pattern Handboek inzien",
            "De knop '📖 Handboek' in de filterrij opent een apart, niet-modaal venster met het volledige " +
            "Pattern Handboek. Het handboek toont per patroon: de exacte detectiecriteria, drempelwaarden, " +
            "bevestigingsregels en veelgemaakte detectiefouten (F1–F10). " +
            "Het venster kan naast de pattern-lijst openblijven zodat je een patroon direct kunt opzoeken " +
            "terwijl je de analyse bekijkt. " +
            "De markdown wordt offline omgezet naar gestylede HTML — geen internetverbinding vereist.");

        // ── v1.23 ────────────────────────────────────────────────────────────
        AddVersionHeader("v1.23", "Pattern Trading — Adam & Eve + Ascending/Descending Channel");

        AddFeature("🌊", "Adam & Eve patroon",
            "Een geavanceerde variant op de dubbele bodem waarbij één bodem scherp (V-vormig, 'Adam') is " +
            "en de andere afgerond ('Eve'). De combinatie van beide bodemvormen wijst op volledige " +
            "uitputting van verkopers en is statistisch sterker dan een klassieke dubbele bodem. " +
            "Het patroon wordt gedetecteerd op 1D, 4H en 1H. " +
            "In de grafiek worden de twee bodems gelabeld als 'A' en 'E' met een neklijn.");

        AddFeature("📏", "Oplopend & dalend kanaal",
            "Twee nieuwe kanaalpatronen: een oplopend kanaal (beide trendlijnen stijgen parallel — bullish) " +
            "en een dalend kanaal (beide trendlijnen dalen parallel — bearish). " +
            "Het kanaal is duidelijk onderscheiden van wiggen (te sterke convergentie) en driehoeken (vlakke zijde). " +
            "Wanneer de prijs de kanaalbodem nadert krijgt het signaal een hogere sterkte (potentiële koopzone). " +
            "Beide trendlijnen worden als annotaties ingetekend in de grafiek.");

        // ── v1.22 ────────────────────────────────────────────────────────────
        AddVersionHeader("v1.22", "Patroondectie — handboek-gekalibreerde drempelwaarden");

        AddFeature("📖", "Nauwkeurigere patroonherkenning",
            "Alle patroondetectoren zijn opnieuw gekalibreerd aan de hand van PATTERN_HANDBOOK.md. " +
            "Dubbele bodem/top: minimale scheiding verhoogd naar 8 bars, gelijkheidsmarge aangescherpt naar 3%, " +
            "én een vereiste valleydiepte van 5% toegevoegd — valse signalen door ondiepe 'W-vormen' worden gefilterd. " +
            "Bull/Bear flag: minimale polhoogte verhoogd naar 8% (was 5%); vlagrange aangescherpt naar 5% (was 6%). " +
            "Driehoeken: hellingsdrempel verlaagd naar 0.0008 (was 0.001) voor preciezere 'vlak'-herkenning. " +
            "H&S / Inv H&S: schouderssymmetrie aangescherpt naar 15% (was 20%), minimum breedte 12 bars, " +
            "neklijn berekend als max van de twee afzonderlijke trogminima (niet langer globaal minimum). " +
            "Wedge: convergentiefactor verhoogd naar 1.20 (was 1.15), minimum span 10 bars, bereikcheck 3–30%. " +
            "Cup & Handle: flexibel cupvenster 30–65 bars (was vast 45), rimsymmetrie aangescherpt naar 6% (was 8%), " +
            "handle-retrace gemaximeerd op 45% (was 50%).");

        // ── v1.21 ────────────────────────────────────────────────────────────
        AddVersionHeader("v1.21", "Pattern Trading — Interactieve candlestick grafiek");

        AddFeature("📈", "Interactieve candlestick grafiek per coin",
            "Elke coin-kaart op de Pattern Trading pagina heeft nu een '📈 Grafiek'-knop. " +
            "Een apart venster opent met een TradingView Lightweight Charts-grafiek (donker thema). " +
            "Schakel tussen 1D, 4H en 1H via de knoppen bovenaan. " +
            "Groene stippellijnen tonen support-niveaus, rode resistance-niveaus — " +
            "beide berekend uit de patroonanalyse.");

        // ── v1.20 ────────────────────────────────────────────────────────────
        AddVersionHeader("v1.20", "Pattern Trading — Watchlijst, Level-3 patronen");

        AddFeature("👁", "Watchlijst — coins buiten je portfolio volgen",
            "Voeg elke coin toe aan je watchlijst via de zoekbalk op de Pattern Trading pagina. " +
            "Typ een naam of symbool (bijv. 'solana', 'pepe'), kies een resultaat en klik '+ Toevoegen'. " +
            "Bij de volgende analyse worden watchlijst-coins meegenomen en gemarkeerd met een goud badge. " +
            "Verwijder een coin via de 🗑-knop onderaan de kaart.");

        AddFeature("🔍", "CoinGecko zoekfunctie",
            "De zoekbalk gebruikt de gratis CoinGecko search-API om snel het juiste coin te vinden, " +
            "gesorteerd op marktkapitalisatie-rang. Geen API-sleutel vereist.");

        AddFeature("📐", "Level-3 patronen — klassieke grafiekpatronen",
            "Vier nieuwe complexe patronen worden nu herkend op 1D, 4H en 1H (vereist 50+ bars): " +
            "Head & Shoulders (bearish reversal), Inverse H&S (bullish), " +
            "Rising Wedge (bearish), Falling Wedge (bullish) en Cup & Handle (bullish continuatie). " +
            "Patronen tonen of ze bevestigd zijn (neklijn/wedge-grens doorbroken) of in formatie.");

        AddFeature("🏷", "Portfolio- en watchlijst-badge per kaart",
            "Elke coin-kaart toont nu een blauw 'Portfolio'-badge (als je een positie hebt) " +
            "of een goud 'Watchlijst'-badge zodat je in één oogopslag ziet waar de coin vandaan komt.");

        // ── v1.19 ────────────────────────────────────────────────────────────
        AddVersionHeader("v1.19", "Pattern Trading — geautomatiseerde patroonherkenning");

        AddFeature("📐", "Pattern Trading — nieuwe analysepagina",
            "Nieuwe pagina in het menu: Pattern Trading. Analyseert automatisch alle coins in je portfolio " +
            "op technische patronen en geeft per coin een tradebaarheid-score van 0–100. " +
            "Klik op 'Analyseer portfolio' om een volledige scan te starten.");

        AddFeature("🔍", "Level-1 patronen — indicatorgebaseerd",
            "RSI oversold/overbought, MACD bullish/bearish cross, EMA9/21-cross, Bollinger Squeeze, " +
            "ADX trending-markt en prijs vs. EMA50 worden automatisch gedetecteerd op 1D, 4H en 1H.");

        AddFeature("📊", "Level-2 patronen — OHLCV patroonherkenning",
            "Opwaartse/neerwaartse trend (HH+HL / LH+LL), bull flag, bear flag, dubbele bodem, dubbele top, " +
            "oplopende/dalende/symmetrische driehoek, consolidatie, support-bounce, resistance-rejection, " +
            "breakout boven weerstand en breakdown onder steun worden herkend via swing-point analyse.");

        AddFeature("⭐", "Tradebaarheid-score en setup-kaart",
            "Elke coin krijgt een score 0–100 (Niet interessant / In de gaten houden / Mogelijke setup / Sterke setup) " +
            "en een volledige setup-kaart met entry, stop-loss, TP1, TP2 en R/R-verhouding.");

        AddFeature("⚡", "Filters: bijna breakout, bullish, bearish",
            "Filter snel op hoogste score, coins die bijna een breakout geven, bullish-setups of bearish-risico's.");

        AddFeature("📋", "Deel setup via klembord",
            "Kopieer de volledige setup-samenvatting (patroon, entry/SL/TP, richting, disclaimer) " +
            "naar het klembord voor delen via Discord, X of andere kanalen.");

        // ── v1.18 ────────────────────────────────────────────────────────────
        AddVersionHeader("v1.18", "Fear & Greed Index op het dashboard");

        AddFeature("😨", "Fear & Greed Index — marktsentiment in één getal",
            "Het dashboard toont nu onderaan de Fear & Greed Index van de cryptomarkt (0 = Extreme Fear, " +
            "100 = Extreme Greed). De waarde wordt automatisch opgehaald van de gratis alternative.me-API " +
            "en wordt gekleurd weergegeven: rood bij angst, groen bij hebzucht. " +
            "De index wordt elke uur ververd; de tijdstempel toont wanneer de laatste meting is opgeslagen.");

        // ── v1.17 ────────────────────────────────────────────────────────────
        AddVersionHeader("v1.17", "Lopende trades aanpassen");

        AddFeature("✏️", "SL / TP aanpassen vanuit het Trade Journal",
            "Via het goudkleurige potlood-icoon naast elke open papierpositie opent u het aanpas-venster. " +
            "Daarin ziet u in één oogopslag het symbool, de richting (Long/Short), de instapprijs, " +
            "de huidige koers en de ongerealiseerde winst/verlies. " +
            "Gebruik de snelknoppen Breakeven, ½R vrij en +1R om de stop-loss met één klik naar een " +
            "risicoveilig niveau te verplaatsen. " +
            "De samenvattingsbalk toont live de nieuwe R/R-ratio, het maximale risico in USDT en de " +
            "procentuele afstand van de stop-loss ten opzichte van de instapprijs. " +
            "Bevestig met 'Wijzigingen opslaan' — de journaalrij wordt direct bijgewerkt.");

        // ── v1.16 ────────────────────────────────────────────────────────────
        AddVersionHeader("v1.16", "Automatisch sluiten bij TP / SL");

        AddFeature("⚡", "Auto-close bij TP en SL bereikt",
            "Het Trade Journal controleert bij elke vernieuwopdracht of de huidige koers " +
            "een ingesteld TP- of SL-niveau heeft geraakt. " +
            "Wordt een niveau overschreden, dan sluit de trade automatisch op de exacte TP/SL-prijs " +
            "en verschijnt er een melding bovenaan de lijst. " +
            "Stop-loss heeft prioriteit boven take-profit. " +
            "Als TP2 én TP1 beide zijn geraakt, sluit de trade op TP2 (het betere resultaat). " +
            "De reden wordt automatisch als notitie bij de trade opgeslagen.");

        // ── v1.15 ────────────────────────────────────────────────────────────
        AddVersionHeader("v1.15", "Gedeeltelijk sluiten bij TP");

        AddFeature("📐", "TP-sluitpercentage instellen (25 / 50 / 75 / 100 %)",
            "Bij het plaatsen van een paper trade kunt u per take-profit niveau aangeven welk " +
            "percentage van de positie u daar wilt sluiten. " +
            "Gebruik de snelknoppen 25% / 50% / 75% / 100% of stel een exacte waarde in via de slider. " +
            "Het gekozen percentage wordt opgeslagen met de order en zichtbaar bij het beheren van de trade.");

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
