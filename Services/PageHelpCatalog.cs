using System;
using System.Collections.Generic;
using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Pagina-uitleg per menu-optie (v1.48), getoond via de ⓘ-knop rechtsboven in de header.
/// Puur en getest: de sleutel is de naam van de View-klasse (gelijk aan de <c>Tag</c> in MainPage.xaml).
/// Elke pagina volgt dezelfde opbouw: wat zie je · hoe lees je het · hoe ga je ermee om · let op.
/// <para>Wijzig je een pagina zichtbaar? Werk dan ook de tekst hier bij.</para>
/// </summary>
public static class PageHelpCatalog
{
    private const string See   = "👀";
    private const string Read  = "🧭";
    private const string Act   = "🛠️";
    private const string Watch = "⚠️";

    private static PageHelpSection S(string icon, string heading, params string[] points)
        => new(icon, heading, points);

    private static readonly Dictionary<string, PageHelp> Pages = new(StringComparer.OrdinalIgnoreCase)
    {
        // ── Algemeen ────────────────────────────────────────────────────────────────────
        ["DashboardView"] = new(
            "Dashboard",
            "Je startpunt: in één oogopslag hoe je portfolio ervoor staat en hoe de markt erbij ligt.",
            new[]
            {
                S(See, "Wat zie je hier",
                    "Totale portfoliowaarde en de verandering van vandaag.",
                    "Grafiek van het verloop van je portfoliowaarde, plus een heatmap en taartdiagrammen (per coin, account en narratief).",
                    "Top 5 stijgers/dalers en de waardewinst per periode.",
                    "📊 Vandaag's signalen: de coins met de hoogste signaalscore, met een Long/Short/Flat-label.",
                    "📈 Marktregime: of de markt (gemeten aan BTC) RiskOn, Neutral of RiskOff is, met de redenen.",
                    "😨 Fear & Greed: de stemming in de markt van 0 (extreme angst) tot 100 (extreme hebzucht)."),
                S(Read, "Hoe lees je het",
                    "Groen = winst/stijging, rood = verlies/daling.",
                    "RiskOn: BTC staat boven zijn 50- en 200-daags gemiddelde → long-kansen hebben de wind mee. RiskOff: de markt is zwak → wees terughoudend met nieuwe longs.",
                    "Fear & Greed werkt vaak tegendraads: extreme angst (< 25) valt vaak samen met koopkansen, extreme hebzucht (> 75) met oververhitting.",
                    "De signalen hier zijn een samenvatting. De onderbouwing per coin staat op de pagina Analyse."),
                S(Act, "Hoe ga je ermee om",
                    "Begin je dag hier: hoe staat het regime, hoe is de stemming, valt er een coin op?",
                    "Zie je een interessant signaal, open dan Analyse of Pattern Trading voor de details voordat je iets doet.",
                    "Met het oog-icoon (Toggle privacy) verberg je bedragen, handig bij het delen van je scherm."),
                S(Watch, "Let op",
                    "Het dashboard is een overzicht, geen koopadvies. Neem nooit een beslissing op alleen één getal of label."),
            }),

        // ── Portfolio ───────────────────────────────────────────────────────────────────
        ["AssetsView"] = new(
            "Assets",
            "Alle munten die je bezit, met actuele koers, waarde en winst/verlies.",
            new[]
            {
                S(See, "Wat zie je hier",
                    "Per coin: hoeveelheid, gemiddelde aankoopprijs, huidige koers, waarde en winst/verlies (in USDT en %).",
                    "Onder een geselecteerde coin: de accounts waar hij staat en alle transacties.",
                    "Een knop voor de portfolio-correlatie: hoe sterk je munten met BTC meebewegen."),
                S(Read, "Hoe lees je het",
                    "Winst/verlies is ongerealiseerd: pas bij verkopen wordt het echt.",
                    "De gemiddelde aankoopprijs is je break-even. Onder die koers sta je op verlies.",
                    "Correlatie: Hoog betekent dat de coin vrijwel hetzelfde doet als BTC. Een portfolio met alleen hoge correlaties is minder gespreid dan het lijkt."),
                S(Act, "Hoe ga je ermee om",
                    "Voer elke aankoop, verkoop, storting en opname in als transactie, anders kloppen je kostprijs en winst/verlies niet.",
                    "Klik op een kolomkop om te sorteren, bijvoorbeeld op waarde om je grootste posities te zien.",
                    "Weegt één coin zwaar (bijv. > 20–25% van je portfolio)? Overweeg dan of dat risico bewust is."),
                S(Watch, "Let op",
                    "Een coin verwijderen verwijdert ook de bijbehorende transacties. Maak eerst een back-up als je twijfelt."),
            }),

        ["AccountsView"] = new(
            "Accounts",
            "Waar je munten staan: exchanges en wallets als aparte deel-portfolio's.",
            new[]
            {
                S(See, "Wat zie je hier",
                    "Je accounts (bijv. Bybit, Ledger, MEXC) met de waarde per account.",
                    "Per account welke coins erop staan en hoeveel."),
                S(Read, "Hoe lees je het",
                    "De som van alle accounts is je totale portfolio.",
                    "Veel waarde op één exchange is een tegenpartijrisico: gaat die exchange onderuit, dan ben je het kwijt."),
                S(Act, "Hoe ga je ermee om",
                    "Maak een account aan per plek waar je crypto bewaart en koppel transacties aan het juiste account.",
                    "Vergelijk af en toe met het saldo op de exchange zelf. Wijkt het af, dan mist er een transactie.",
                    "Bewaar wat je niet actief verhandelt liefst in een eigen wallet."),
                S(Watch, "Let op",
                    "Een account verwijderen kan alleen als er geen assets meer aan hangen."),
            }),

        ["NarrativesView"] = new(
            "Narratieven",
            "Groepeer je munten per thema (AI, DeFi, Layer 1, Memes …) om te zien waar je geld écht zit.",
            new[]
            {
                S(See, "Wat zie je hier",
                    "Je narratieven met per thema de waarde en de coins die erbij horen."),
                S(Read, "Hoe lees je het",
                    "Crypto beweegt in golven per thema. Zit het grootste deel in één narratief, dan hangt je resultaat sterk af van dat ene verhaal.",
                    "Vergelijk de prestatie van thema's: welk verhaal loopt, welk blijft achter?"),
                S(Act, "Hoe ga je ermee om",
                    "Geef elke coin een narratief, zodat de verdeling op het Dashboard en in de heatmap klopt.",
                    "Gebruik het als spreidingscheck: wil je bewust zwaar in één thema zitten?"),
                S(Watch, "Let op",
                    "Een narratief is jouw eigen indeling, geen marktdata. Houd hem actueel als een coin van verhaal wisselt."),
            }),

        // ── Analyse & handel ────────────────────────────────────────────────────────────
        ["SignalsView"] = new(
            "Analyse (signalen)",
            "Technische indicatoren en een gecombineerde signaalscore per coin: welke munten staan technisch sterk of zwak?",
            new[]
            {
                S(See, "Wat zie je hier",
                    "Per coin indicatoren: RSI, MACD, EMA-cross, Bollinger (%B en squeeze), ATR, StochRSI, ADX, afstand tot MA50 en tot de 52-weekstop.",
                    "Sentiment (−1 tot +1) uit Reddit/RSS/nieuws en het marktregime.",
                    "Score (0–100) met daaronder de gemeten trefkans van vergelijkbare signalen na 7 dagen.",
                    "Richting: Long, Short of Flat."),
                S(Read, "Hoe lees je het",
                    "Score ≥ 60 = Long, ≤ 40 = Short, daartussen = Flat (geen duidelijke richting).",
                    "De score is 60% techniek, 30% sentiment en 10% marktregime. In een RiskOff-markt wordt een Long-signaal sterk afgezwakt.",
                    "De trefkans (bijv. '58% · 43') zegt hoe vaak zo'n signaal in het verleden goed uitpakte, over 43 metingen. Onder de 20 metingen ('n=…') is dat nog niet betrouwbaar.",
                    "RSI < 30 = oversold (mogelijk te ver gedaald), > 70 = overbought. ADX > 25 = er is een echte trend.",
                    "Squeeze = de koers zit in een smalle band; vaak volgt daarna een grote beweging (richting onbekend)."),
                S(Act, "Hoe ga je ermee om",
                    "Klik 'Refresh Analysis' voor actuele indicatoren en daarna 'Evaluate Signals' voor nieuwe scores.",
                    "Filter op Richting om alleen Long- of Short-kandidaten te zien en sorteer op Score.",
                    "Gebruik een sterk signaal als startpunt: check de coin daarna in Trade Advies of Pattern Trading voor concrete instap/stop/doel.",
                    "Wil je oefenen? Klik 'Paper Trade' in de rij voor een order met nepgeld.",
                    "🏆 Top X (balk boven de lijst): markeert de setups die het eerst het beoordelen waard zijn, op kansscore (kwaliteit × R/R × gemeten trefkans × waarschuwingen). Kies het aantal, zet \"Alleen top\" aan om alleen die te zien, en beweeg over 🏆 voor de opbouw. Het is een volgorde om te beoordelen, geen advies.",
                    "📊 TradingView: rechtsklik op een rij → 'Open op TradingView' opent de grafiek in je browser. De knop 📊 TradingView exporteert de getoonde munten als watchlist (secties Top, Long, Short) die je in TradingView importeert. Deze pagina heeft geen entry/stop, dus geen Pine Script."),
                S(Watch, "Let op",
                    "Een hoge score is een waarschijnlijkheid, geen garantie. Kijk naar de trefkans: een score van 70 met 45% trefkans is zwakker dan hij lijkt."),
            }),

        ["TradeAnalysisView"] = new(
            "Trade Advies",
            "Een concreet handelsplan per coin: richting, instapprijs, stop-loss en twee koersdoelen.",
            new[]
            {
                S(See, "Wat zie je hier",
                    "Kies een coin of klik 'Analyseer alles'. Je krijgt een analyse op week, dag, 4 uur en 1 uur.",
                    "Een trade-setup: entry, stop-loss (SL), take-profit 1 en 2 (TP1/TP2), risk/reward (R/R) en een confidence.",
                    "Sleutelniveaus (steun en weerstand), marktcontext (liquiditeit, funding, long/short-verhouding) en komende macro-events.",
                    "Een Fundamental-badge (Ⓕ score · oordeel) voor de kwaliteit van het project zelf."),
                S(Read, "Hoe lees je het",
                    "Stop-loss ≈ 1,5 × ATR van je entry, TP1 ≈ 2 × ATR, TP2 ≈ 3,5 × ATR of de eerste weerstand.",
                    "R/R 2:1 betekent: je kunt twee keer zoveel winnen als je riskeert. Onder 1,5:1 is de setup krap (waarschuwing).",
                    "Confidence Hoog bij R/R ≥ 2,5, Medium bij 1,5–2,5, Laag daaronder.",
                    "Deze pagina kijkt alleen naar trend en momentum, niet naar grafiekpatronen. Daardoor kan Pattern Trading voor dezelfde coin iets anders zeggen. Dat is geen fout."),
                S(Act, "Hoe ga je ermee om",
                    "Gebruik het advies als plan: weet vooraf waar je instapt, waar je eruit gaat bij verlies (SL) en waar je winst neemt (TP).",
                    "Geef de voorkeur aan setups in de richting van de daily-trend en met R/R ≥ 2.",
                    "Klik 'Paper Trade' om het plan met nepgeld te volgen, of kies daar Bybit Demo voor een echte demo-order.",
                    "Een rode melding (ongeldige setup) betekent: niet handelen op deze niveaus.",
                    "📊 TradingView (knop in de werkbalk): open de grafiek van de huidige analyse, maak een Pine Script dat entry, stop, TP1/TP2 en steun/weerstand op de TradingView-grafiek tekent (met alerts), of maak één script/watchlist met de top-setups uit 'Analyseer alles'. Het venster toont de stappen: kopiëren → Pine Editor in TradingView → plakken → 'Toevoegen aan grafiek'.",
                    "🏆 Top X (balk boven de lijst): markeert de setups die het eerst het beoordelen waard zijn, op kansscore (kwaliteit × R/R × gemeten trefkans × waarschuwingen). Kies het aantal, zet \"Alleen top\" aan om alleen die te zien, en beweeg over 🏆 voor de opbouw. Het is een volgorde om te beoordelen, geen advies."),
                S(Watch, "Let op",
                    "Staat er een macro-event (FOMC, CPI) in de komende uren? Dan kan de koers wild bewegen en je stop raken. Overweeg te wachten."),
            }),

        ["TradeJournalView"] = new(
            "Trade Journal",
            "Al je orders op één plek: paper trades (nepgeld) en Bybit Demo-orders, open en gesloten.",
            new[]
            {
                S(See, "Wat zie je hier",
                    "Per order: symbool, richting, hoeveelheid, instap, huidige of sluitkoers, winst/verlies, R-multiple, SL/TP en status.",
                    "Filters: Alles · Open · Gesloten · Paper · Live (Bybit Demo valt onder Live).",
                    "Knoppen 'Risico' (risico-dashboard), 'Vernieuwen' en 'Kill All'."),
                S(Read, "Hoe lees je het",
                    "Status: Pending = wacht op vulling, Filled = positie loopt, Closed = gesloten, Cancelled = geannuleerd.",
                    "R-multiple = winst/verlies uitgedrukt in je risico. +2R betekent dat je twee keer je ingezette risico won, −1R dat je stop geraakt werd.",
                    "Ongerealiseerde winst/verlies tel je alleen bij lopende (Filled) posities.",
                    "Demo-orders worden bij elke vernieuwing gesynchroniseerd met Bybit. Een TP- of SL-sluiting op Bybit zie je hier dus terug."),
                S(Act, "Hoe ga je ermee om",
                    "Klik 'Vernieuwen' om koersen bij te werken. Paper-orders vullen of sluiten dan automatisch bij hun niveaus.",
                    "✓ sluit een positie tegen de marktprijs (bij Bybit Demo: TP/SL eraf en verkopen op Bybit), ✕ annuleert een openstaande order, 🔧 verplaatst SL/TP (alleen paper).",
                    "Schrijf bij elke trade een notitie (✏️): waarom stapte je in, wat leerde je? Dat maakt je journaal waardevol.",
                    "Open 'Risico' om te zien hoeveel je totaal riskeert en of je je limieten nadert."),
                S(Watch, "Let op",
                    "'Kill All' sluit alle open paper-posities in één keer. Gebruik hem alleen als noodrem.",
                    "Trek je stop pas naar break-even als de koers echt de goede kant op is gegaan. Te vroeg verplaatsen leidt vaak tot onnodig uitgestopt worden."),
            }),

        ["PatternTradingView"] = new(
            "Pattern Trading",
            "Automatische herkenning van grafiekpatronen op 1D, 4H, 1H en 15M, met een TradabilityScore en een concreet setup-advies per coin.",
            new[]
            {
                S(See, "Wat zie je hier",
                    "Bovenaan de marktcontext: BTC-regime, Fear & Greed en het eerstvolgende macro-event.",
                    "Per coin een kaart met trend per timeframe, RSI, de TradabilityScore (0–100), patroon-badges en, bij score ≥ 40, een setup: entry, stop, TP1/TP2, R/R en confidence.",
                    "Badges zoals ⚡ Bijna Breakout, ⚠ Tegen daily-trend en een Ⓕ fundamental-score.",
                    "Filters (Hoog gescoord, Bijna Breakout, Bullish, Bearish), timeframe- en patroonfilter, en de flyouts 'Score-uitleg' en 'Patroon-prestaties'."),
                S(Read, "Hoe lees je het",
                    "Score ≥ 80 = sterke setup, 60–79 = mogelijke setup, 40–59 = in de gaten houden, < 40 = niet interessant.",
                    "Groene badges zijn bullish, rode bearish. Een patroon is pas betrouwbaar als het 'bevestigd' is (slotkoers voorbij het niveau, liefst met extra volume).",
                    "'N× gezien' betekent dat het patroon over meerdere scans standhoudt. Hoe vaker, hoe steviger.",
                    "⚠ Tegen daily-trend: de setup gaat tegen de grote trend in. Dat is riskanter.",
                    "Patroon-prestaties laat per patroontype zien hoe vaak het in jouw historie uitkwam."),
                S(Act, "Hoe ga je ermee om",
                    "Klik 'Analyseer', filter op 'Hoog gescoord' en bekijk de beste kaarten.",
                    "Open de grafiek (grafiek-icoon of patroon-badge) en controleer of het patroon er voor jou ook echt zo uitziet.",
                    "Check met 'Check liquiditeit' of er genoeg handel is. Bij 'Dun' riskeer je slechte vullingen.",
                    "Volg een setup met 'Volg trade setup' (Setup Tracker) of open direct een paper/demo-order.",
                    "Automatisch handelen (Instellingen) gebruikt deze scan: alleen Long-setups boven je minimale score worden op Bybit Demo geplaatst.",
                    "📊 TradingView op een kaart: open de grafiek op TradingView of maak een Pine Script voor die setup (entry, stop, doelen, steun/weerstand en drie alerts). De knop 📊 TradingView rechtsboven maakt één Pine Script met alle top-setups, of een watchlist om te importeren.",
                    "Pine Script gebruiken: klik 'Kopiëren' → open TradingView → Pine Editor (onderin) → alles vervangen door plakken (Ctrl+V) → 'Toevoegen aan grafiek'. Het top-setups-script tekent automatisch de niveaus van de munt die op je grafiek staat; blader door de geïmporteerde watchlist. Alerts zet je via 🔔 → Voorwaarde: het CPT-script → 'Entry geraakt' / 'Stop-loss geraakt' / 'TP1 geraakt'.",
                    "🏆 Top X (balk boven de lijst): markeert de setups die het eerst het beoordelen waard zijn, op kansscore (kwaliteit × R/R × gemeten trefkans × waarschuwingen). Kies het aantal, zet \"Alleen top\" aan om alleen die te zien, en beweeg over 🏆 voor de opbouw. Het is een volgorde om te beoordelen, geen advies."),
                S(Watch, "Let op",
                    "Patronen zijn indicatief, geen garantie. Ze falen regelmatig, vooral zonder bevestiging of tegen de trend in.",
                    "Stablecoins en munten met te weinig beweging krijgen bewust geen setup."),
            }),

        ["SetupTrackerView"] = new(
            "Setup Tracker",
            "Volgt de setups die je uit Pattern Trading hebt bewaard en meet automatisch of ze uitkwamen.",
            new[]
            {
                S(See, "Wat zie je hier",
                    "Bovenaan: totaal, In Trade, Watching, gewonnen, verloren en je win rate, plus een kalibratie per scoreklasse.",
                    "Per setup: coin, richting, entry/SL/TP1, R/R, de huidige koers met afstand tot de entry, status en leeftijd."),
                S(Read, "Hoe lees je het",
                    "🔵 Watching = entry nog niet geraakt · 🟡 In Trade = entry geraakt · 🟢 Gewonnen = TP1 geraakt · 🔴 Verloren = stop geraakt · ⚫ Verlopen = handmatig afgesloten.",
                    "De kalibratie laat zien of een hogere TradabilityScore in jóuw praktijk ook echt vaker won. Pas vanaf zo'n 10 afgesloten setups per klasse zegt dat iets.",
                    "De status wordt automatisch bijgewerkt bij elke koersupdate."),
                S(Act, "Hoe ga je ermee om",
                    "Bewaar hier setups die je interessant vindt, ook als je ze niet handelt. Zo bouw je bewijs op welke setups werken.",
                    "Zet een setup op ⏹ Verlopen als het patroon niet meer geldig is, zodat hij je statistiek niet vertekent.",
                    "Kijk na een paar weken welke scoreklasse en welk marktregime het best presteerden, en focus daarop.",
                    "📊 TradingView op een setup: grafiek openen of een Pine Script met entry/stop/TP1/TP2 en alerts, zodat TradingView je waarschuwt als de entry geraakt wordt. Rechtsboven: één script of een watchlist met al je lopende (top-)setups.",
                    "Met webhook-alerts aan (Instellingen → TradingView) komen die alerts in de app en via Telegram binnen, met de setup erbij. Zet je 'automatisch Bybit Demo-order' aan, dan plaatst een 'Entry geraakt'-alert op een Long-setup die hier op Watching staat een demo-order met de SL en TP1 van de setup.",
                    "🏆 Top X (balk boven de lijst): markeert de setups die het eerst het beoordelen waard zijn, op kansscore (kwaliteit × R/R × gemeten trefkans × waarschuwingen). Kies het aantal, zet \"Alleen top\" aan om alleen die te zien, en beweeg over 🏆 voor de opbouw. Het is een volgorde om te beoordelen, geen advies."),
                S(Watch, "Let op",
                    "Weinig afgesloten setups = toeval speelt een grote rol. Trek pas conclusies bij voldoende aantallen."),
            }),

        ["ThreePctView"] = new(
            "3% Trading",
            "Een strategie met één vast doel: +3% netto (na kosten) per trade, met kansen die gemeten zijn in een backtest.",
            new[]
            {
                S(See, "Wat zie je hier",
                    "Tabblad Kalibratie: een backtest over historische koersen die per scoreklasse meet hoe vaak +3% eerder werd gehaald dan de stop.",
                    "Tabblad Live Scan: je coins nu gescoord op 7 factoren (trend, momentum, volume, volatiliteit, steun/weerstand, liquiditeit, positionering), gekoppeld aan de gemeten trefkans.",
                    "Bij Richting \"Both\" wordt elke coin Long én Short gescoord, maar je ziet per coin alleen de sterkste richting (de hoogste score die niet door liquiditeit/positionering is afgevallen).",
                    "Tabblad Paper Trades: de trades die je vanuit deze strategie met nepgeld volgt."),
                S(Read, "Hoe lees je het",
                    "Hitrate = hoe vaak het doel gehaald werd. Expectancy = gemiddelde opbrengst per trade in R. Positief = de strategie verdiende historisch geld.",
                    "Een scoreklasse met minder dan 30 trades is onbetrouwbaar (gemarkeerd).",
                    "Liquiditeit en positionering zijn poortwachters: scoort een coin daar te laag, dan valt hij af, hoe goed de rest ook is.",
                    "De shortlist kiest de beste setups die niet sterk met elkaar meebewegen (standaard max. 5, correlatie < 0,80), zodat je niet vijf keer dezelfde gok neemt. Beide grenzen stel je in bij Instellingen; op 1,00 staat het correlatiefilter uit en kan een heel thema met momentum samen in de shortlist."),
                S(Act, "Hoe ga je ermee om",
                    "Draai eerst de kalibratie. Zonder kalibratie zegt de score niets.",
                    "Handel alleen in scoreklassen met een positieve expectancy en voldoende trades.",
                    "Test het eerst met paper trades. Klik 'Vernieuwen' om ze te laten vullen en sluiten. Pas na een overtuigende reeks heeft het zin om verder te gaan.",
                    "📊 in een rij: open de grafiek op TradingView (Binance, op het gekozen tijdvak) of maak een Pine Script met entry, stop en het +3%-doel. De knop 📊 TradingView boven de tabel maakt één script of een watchlist met de top-setups.",
                    "🏆 Top X (balk boven de lijst): markeert de setups die het eerst het beoordelen waard zijn, op kansscore (kwaliteit × R/R × gemeten trefkans × waarschuwingen). Kies het aantal, zet \"Alleen top\" aan om alleen die te zien, en beweeg over 🏆 voor de opbouw. Het is een volgorde om te beoordelen, geen advies."),
                S(Watch, "Let op",
                    "Een backtest is het verleden. Markten veranderen. Blijf meten met paper trades.",
                    "Zet je het correlatiefilter uit, dan gedragen sterk samenbewegende posities zich als één grote positie: breekt het momentum, dan raken ze vaak tegelijk hun stop-loss. Je risico per trade geldt dan per munt.",
                    "Hoeveel trades je echt open kunt hebben, bepaalt 'Max. open posities' bij de risicobewakers in Instellingen — los van de grootte van de shortlist."),
            }),

        ["FundamentalsView"] = new(
            "Fundamentals",
            "Hoe goed is het project áchter de munt? Een Fundamental Score van 0 tot 100 per coin.",
            new[]
            {
                S(See, "Wat zie je hier",
                    "Per coin de Fundamental Score met een oordeel (Exceptional, Strong, Promising, Speculative, High Risk, Avoid) en een betrouwbaarheid.",
                    "Knoppen 'Analyseer' (cijfers ophalen) en 'Detail' (subscores, ruwe cijfers, eigen beoordeling en een SWOT-rapport).",
                    "Zoeken, favorieten, en verversen van verouderde of favoriete coins."),
                S(Read, "Hoe lees je het",
                    "De score combineert tokenomics, liquiditeit, waardering, community, development, projectvolledigheid en (voor DeFi) TVL.",
                    "≥ 80 = sterk project, 60–70 = speculatief, < 50 = hoog risico of vermijden.",
                    "Betrouwbaarheid laat zien hoeveel van het oordeel onderbouwd is. Zonder eigen due-diligence blijft die lager.",
                    "Een grote kloof tussen FDV en marktwaarde betekent dat er nog veel tokens bijkomen (verwateringsrisico)."),
                S(Act, "Hoe ga je ermee om",
                    "Klik 'Analyseer' bij coins die je overweegt. Dit haalt actuele cijfers op.",
                    "Vul in 'Detail' je eigen beoordeling in (team, product, adoptie, omzet, unlocks). Dat maakt de score scherper.",
                    "Combineer: een sterke technische setup (Pattern Trading) op een fundamenteel zwak project is een korte gok, geen belegging."),
                S(Watch, "Let op",
                    "Team, omzet en unlock-schema's zijn niet automatisch te meten. Die moet je zelf onderzoeken."),
            }),

        ["StatisticsView"] = new(
            "Statistieken",
            "Hoe goed handel je echt? Je resultaten in cijfers, plus een meting van hoe betrouwbaar de signalen zijn.",
            new[]
            {
                S(See, "Wat zie je hier",
                    "Tabblad Trade Journal: totale winst/verlies, win rate, gemiddelde winst en verlies, open posities, volume, verdelingen en je beste/slechtste coins.",
                    "Tabblad Setup Strategie: win rate op TP1/TP2, profit factor, expectancy en houdtijd, opgesplitst naar richting, score en marktregime.",
                    "Tabblad Signaal-kalibratie: hoe vaak signalen na 1, 3, 7 of 14 dagen de goede kant op gingen."),
                S(Read, "Hoe lees je het",
                    "Win rate alleen zegt weinig: 40% winst kan prima zijn als je winsten veel groter zijn dan je verliezen.",
                    "Profit factor = totale winst ÷ totaal verlies. Boven 1 verdien je geld, boven 1,5 is goed.",
                    "Expectancy = wat je gemiddeld per trade verdient (in R). Dit is het belangrijkste getal.",
                    "Groepen met < 20 metingen zijn gemarkeerd met ⚠: nog te weinig data."),
                S(Act, "Hoe ga je ermee om",
                    "Kies bovenaan een periode en filter op Paper of Live.",
                    "Zoek je sterke kant (welke richting, score of regime werkt voor jou?) en doe daar meer van, en minder van wat verliest.",
                    "Gebruik de signaal-kalibratie om te bepalen bij welke score je signalen serieus neemt."),
                S(Watch, "Let op",
                    "Paper-resultaten zijn vaak optimistischer dan echt handelen (geen emotie, perfecte vullingen)."),
            }),

        ["SourcesView"] = new(
            "Bronnen",
            "De bronnen waaruit de app het sentiment (de stemming) rond coins haalt.",
            new[]
            {
                S(See, "Wat zie je hier",
                    "Een lijst met Reddit-, RSS-, Telegram- en CryptoPanic-bronnen, met een betrouwbaarheidsscore en een aan/uit-schakelaar.",
                    "Hoeveel metingen er in totaal en de afgelopen 24 uur zijn verzameld."),
                S(Read, "Hoe lees je het",
                    "Een hogere betrouwbaarheidsscore (0–1) geeft een bron meer gewicht in het sentiment.",
                    "Sentiment telt voor 30% mee in de signaalscore op de pagina Analyse."),
                S(Act, "Hoe ga je ermee om",
                    "Zet bronnen die veel ruis of spam geven uit in plaats van ze te verwijderen.",
                    "Voeg bronnen toe die over jouw munten gaan. Klik '▶ Ophalen' voor een directe verzamelronde (gebeurt anders elke 15 minuten)."),
                S(Watch, "Let op",
                    "Sentiment is luidruchtig en makkelijk te beïnvloeden (hypes, bots). Gebruik het als aanvulling, nooit als enige reden."),
            }),

        // ── Bibliotheek ─────────────────────────────────────────────────────────────────
        ["CoinLibraryView"] = new(
            "Coin Library",
            "Alle munten die de app kent en volgt, ook als je ze (nog) niet bezit.",
            new[]
            {
                S(See, "Wat zie je hier",
                    "Alle coins in je bibliotheek met koers, marktwaarde en rang."),
                S(Read, "Hoe lees je het",
                    "Alleen coins in de bibliotheek kunnen een asset, prijsniveau, analyse of fundamentals krijgen."),
                S(Act, "Hoe ga je ermee om",
                    "Voeg een coin toe via de zoekfunctie (CoinGecko) om hem te volgen of later te kopen.",
                    "Ruim munten op die je niet meer volgt. Dat houdt scans en lijsten snel."),
                S(Watch, "Let op",
                    "Een coin verwijderen verwijdert ook zijn assets en transacties.",
                    "Op deze pagina pauzeren de automatische koers-updates tijdelijk."),
            }),

        ["PriceLevelsView"] = new(
            "Prijsniveaus",
            "Je eigen koersniveaus per coin (kopen, stop, winst nemen) en hoe dicht de koers erbij zit.",
            new[]
            {
                S(See, "Wat zie je hier",
                    "Een heatmap van je portfolio, accounts of narratieven.",
                    "Per coin je ingestelde niveaus (Buy, Stop, TakeProfit, EMA) met hun status."),
                S(Read, "Hoe lees je het",
                    "De status geeft aan hoe ver de koers van je niveau is: buiten bereik, binnen bereik, dichtbij of geraakt.",
                    "In de heatmap betekent groter = meer waarde, kleur = stijging of daling."),
                S(Act, "Hoe ga je ermee om",
                    "Leg je plan vast vóórdat de markt beweegt: bij welke koers koop je bij, waar stop je, waar neem je winst?",
                    "Komt een niveau 'dichtbij', bekijk de coin dan opnieuw in Analyse voordat je handelt."),
                S(Watch, "Let op",
                    "Deze niveaus zijn een geheugensteun. Er worden geen echte orders geplaatst."),
            }),

        ["SwitchPortfolioView"] = new(
            "Switch Portfolio",
            "Wissel tussen je portfolio's (bijv. 'Privé' en 'Test').",
            new[]
            {
                S(See, "Wat zie je hier",
                    "Je portfolio's. Het actieve portfolio is gemarkeerd."),
                S(Act, "Hoe ga je ermee om",
                    "Kies een ander portfolio om ermee te werken. Alle pagina's tonen daarna de gegevens van dat portfolio.",
                    "Gebruik een apart portfolio om te experimenteren zonder je echte administratie te vervuilen."),
                S(Watch, "Let op",
                    "Elk portfolio heeft zijn eigen database. Coins, transacties en trades worden niet gedeeld."),
            }),

        ["AdminView"] = new(
            "Portfolio Admin",
            "Beheer je portfolio's: aanmaken, hernoemen en verwijderen.",
            new[]
            {
                S(See, "Wat zie je hier",
                    "De lijst met portfolio's en knoppen om er een toe te voegen, te hernoemen of te verwijderen."),
                S(Act, "Hoe ga je ermee om",
                    "Maak een back-up voordat je een portfolio verwijdert of grote wijzigingen doet.",
                    "Kunnen anderen bij je pc? Zet dan bij Instellingen een wachtwoord op de app."),
                S(Watch, "Let op",
                    "Een verwijderd portfolio is weg, inclusief alle transacties en trades. Dit kun je niet ongedaan maken."),
            }),

        // ── Footer ──────────────────────────────────────────────────────────────────────
        ["SettingsView"] = new(
            "Instellingen",
            "Hoe de app werkt: weergave, meldingen, exchanges, risicolimieten en automatisch handelen.",
            new[]
            {
                S(See, "Wat zie je hier",
                    "Tabblad Instellingen: thema, taal, getalnotatie, Telegram-meldingen, signaaldrempels, TradingView (standaardbeurs, interval, exportmap), exchange-sleutels (Bybit, Bybit Demo, MEXC), risicolimieten en automatisch handelen.",
                    "Tabblad Databronnen: alle externe diensten en lokale bestanden die de app gebruikt.",
                    "Tabblad Belasting: een Box 3-berekening van je crypto."),
                S(Read, "Hoe lees je het",
                    "Risicolimieten: max. % per trade, max. aantal open posities, dagelijkse verlieslimiet en de kill-switch. Ze blokkeren nieuwe orders als je ze bereikt.",
                    "Bybit Demo handelt met nepgeld. Echt geld staat in deze versie op slot.",
                    "Automatisch handelen plaatst na elke Pattern Trading-scan zelf Long-orders op Bybit Demo, binnen je minimale score, dagmaximum en risico per trade."),
                S(Act, "Hoe ga je ermee om",
                    "Stel je risicolimieten in vóórdat je gaat handelen: 1–2% risico per trade is een veelgebruikte bovengrens.",
                    "Gebruik voor exchanges een API-sleutel met alleen handelsrechten, nooit opnamerechten. Klik daarna 'Verbinding testen'.",
                    "Zet automatisch handelen eerst streng af (hoge minimale score, 1–2 orders per dag) en kijk een paar dagen mee via Telegram en het Trade Journal.",
                    "De kill-switch stopt direct alle nieuwe orders, handig als het even te hard gaat.",
                    "TradingView: kies de beurs waarop je in TradingView kijkt (bijv. BYBIT als je op Bybit EU handelt) en het interval waarmee grafieken openen. Exports (.pine en watchlists) staan in de exportmap.",
                    "Webhook-alerts (betaald TradingView-plan): zet 'Webhook-alerts ontvangen' aan, klik 'Kopiëren' en plak de URL in TradingView bij de alert → Meldingen → Webhook-URL. Klik 'Test' om te zien of het werkt. De app haalt alerts elke 20 s op zolang hij draait en stuurt ze door naar Telegram. Optioneel plaatst hij bij 'Entry geraakt' een Bybit Demo-order — alleen voor een Long-setup die je volgt in de Setup Tracker."),
                S(Watch, "Let op",
                    "Sleutels worden versleuteld opgeslagen op deze pc. Deel ze nooit met anderen.",
                    "Houd de webhook-URL geheim: wie hem kent, kan meelezen en nep-alerts sturen. Uitgelekt? Klik 'Nieuwe URL' en pas hem aan in je TradingView-alerts."),
            }),

        ["HelpView"] = new(
            "Help",
            "De uitgebreide handleiding: uitleg, formules en veelgestelde vragen.",
            new[]
            {
                S(See, "Wat zie je hier",
                    "Uitklapbare onderwerpen per categorie: aan de slag, portfolio, trade journal, trade advies, signalen, statistieken, belasting, instellingen, databronnen en FAQ."),
                S(Act, "Hoe ga je ermee om",
                    "Gebruik de ⓘ-knop op een pagina voor een snelle uitleg van díe pagina, en deze Help voor de achtergrond en berekeningen."),
            }),

        ["WhatsNewView"] = new(
            "What's New",
            "Wat er per versie nieuw of veranderd is.",
            new[]
            {
                S(See, "Wat zie je hier",
                    "Alle versies, de nieuwste bovenaan, met per versie de nieuwe functies."),
                S(Act, "Hoe ga je ermee om",
                    "Lees na een update wat er nieuw is. Vaak verandert er iets aan hoe je een pagina gebruikt of leest."),
            }),
    };

    /// <summary>De uitleg voor een pagina (View-klassenaam), of <c>null</c> als er geen is.</summary>
    public static PageHelp? For(string? viewName)
        => viewName is not null && Pages.TryGetValue(viewName, out var help) ? help : null;

    /// <summary>Alle View-namen met uitleg (voor tests).</summary>
    public static IReadOnlyCollection<string> PageNames => Pages.Keys;
}
