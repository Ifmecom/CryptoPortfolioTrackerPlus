# Product Requirements Document  
## CryptoPortfolioTracker Plus — v1.38

| | |
|---|---|
| **Versie** | 1.32 |
| **Datum** | Mei 2026 |
| **Platform** | Windows 11 · WinUI 3 · .NET 6 · x64 Unpackaged |
| **Database** | SQLite via Entity Framework Core |
| **Status** | Actief in ontwikkeling |

---

## Inhoudsopgave

1. [Productvisie en doelen](#1-productvisie-en-doelen)
2. [Technische architectuur](#2-technische-architectuur)
3. [Navigatiestructuur](#3-navigatiestructuur)
4. [Pagina's en functionaliteiten](#4-paginas-en-functionaliteiten)
5. [Data-model en relaties](#5-data-model-en-relaties)
6. [Berekeningen](#6-berekeningen)
7. [Externe integraties](#7-externe-integraties)
8. [Achtergrondservices](#8-achtergrondservices)
9. [Configuratie en opslag](#9-configuratie-en-opslag)
10. [Belasting-module](#10-belasting-module)
11. [Beveiliging en encryptie](#11-beveiliging-en-encryptie)
12. [Uitbreidingspunten](#12-uitbreidingspunten)
13. [Bekende beperkingen](#13-bekende-beperkingen)

---

## 1. Productvisie en doelen

### 1.1 Wat is CryptoPortfolioTracker Plus?

CryptoPortfolioTracker Plus is een desktop-applicatie voor Windows waarmee een individuele crypto-belegger zijn volledige portfolio, handelsactiviteiten en belastingpositie op één plek beheert en analyseert. De app combineert:

- **Portfolio-tracking** — realtime koersen, assets per exchange, rendement
- **Technische analyse** — 10+ indicatoren berekend vanuit live OHLCV-data of lokale cache
- **Signaalgeneratie** — gecombineerde TA + sentiment + marktregime-score per coin
- **Pattern Trading** — automatische patroonherkenning (Level 1–3) op 1D/4H/1H/15M met TradabilityScore en setup-advies
- **Setup Tracker** — win-rate monitoring van gevolgde trade-setups met automatische TP/SL-detectie
- **3% Trading** — gekalibreerd 7-factor scoremodel met +3% netto-doel (kalibratie, live scan, paper trades)
- **Fundamentals** — fundamentele analyse met Fundamental Score (0-100), SWOT-rapport en DefiLlama TVL
- **Trade Journal** — paper trading én live trades, met P&L en R-multiple
- **Trade Advies** — multi-timeframe analyse per coin met entry/SL/TP-berekening
- **Statistieken** — geaggregeerde handelsprestaties over meerdere periodes
- **Belasting** — Box 3-berekening (NL) met uitbreidbare architectuur voor andere landen

### 1.2 Primaire gebruiker

Eén persoon: de eigenaar van het portfolio. Er is geen multi-user functionaliteit.

### 1.3 Niet in scope

- Geautomatiseerd handelen (market orders via exchange-API)
- Mobiele app of web-interface
- Belastingaangifte exporteren naar officiële formulieren

---

## 2. Technische architectuur

### 2.1 Stack

| Laag | Technologie |
|------|-------------|
| UI | WinUI 3 (Windows App SDK 1.x), XAML |
| Binding | CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`) |
| Charts | LiveChartsCore 2.0.0-rc4.5 (SkiaSharp) |
| Database | SQLite 3 via Entity Framework Core 7 |
| Dependency Injection | Microsoft.Extensions.DependencyInjection |
| Logging | Serilog |
| Indicators | Skender.Stock.Indicators (MIT) |
| HTTP | HttpClient (CoinGecko, Binance, KuCoin, exchange APIs) |
| Encryptie | Windows Data Protection API (DPAPI) |

### 2.2 MVVM-patroon

```
Views/          → XAML + code-behind (alleen UI-events, geen business-logica)
ViewModels/     → erven van BaseViewModel (ObservableObject)
                   [ObservableProperty] voor databinding
                   [RelayCommand] voor commando's
                   IMessenger via constructor-injectie voor berichten tussen lagen
Models/         → pure data-klassen, EF-entiteiten
Services/       → business-logica, altijd via interface (IXxxService)
Infrastructure/ → DbContext, EntityConfigurations, Factories
Enums/          → alle enumeraties
```

### 2.3 Dependency Injection registratie

Alle Views, ViewModels en Services zijn geregistreerd in `App.xaml.cs → RegisterServices()`.  
Scoped = per navigatie-sessie; Singleton = app-lifetime.

```csharp
// Views & ViewModels: AddScoped
// Settings, IndicatorService, GraphService: AddSingleton
// DbContext: AddDbContext (dummy connection string — echte verbinding via PortfolioContextFactory)

// Pattern Trading (v1.19):
services.AddSingleton<IPatternDetectionService, PatternDetectionService>(); // pure berekening, geen state
services.AddScoped<IPatternTradingService, PatternTradingService>();         // afhankelijk van PortfolioService

// Setup Tracker (v1.30):
services.AddScoped<IWatchedSetupService, WatchedSetupService>(); // CRUD + auto-status + stats
services.AddScoped<SetupTrackerViewModel>();
services.AddScoped<SetupTrackerView>();
```

**Pattern Trading services:**

| Service | Scope | Omschrijving |
|---------|-------|-------------|
| `IPatternDetectionService` | Singleton | Pure berekening: Level 1 + Level 2 patroondetectie, TradabilityScore berekening. Geen I/O, geen state. |
| `IPatternTradingService` | Scoped | Portfolio-analyse: OHLCV ophalen (Binance→KuCoin→Gate.io→MEXC), indicatoren berekenen, detectie aanroepen, setup bouwen. |

### 2.4 Database-toegang

| Context | Gebruik | Thread |
|---------|---------|--------|
| `PortfolioContext` | Lezen/schrijven vanuit UI | UI-thread |
| `UpdateContext` | Alleen `PriceUpdateService` | Achtergrond-thread |

Migraties worden automatisch toegepast bij app-start via `context.Database.MigrateAsync()`.

### 2.5 Bestandslocaties

```
%LOCALAPPDATA%\CryptoPortfolioTrackerPlus\
  sqlCPT.db               → SQLite database (huidig portfolio)
  prefs.xml               → gebruikersvoorkeuren
  portfolios.json         → lijst van portfolio-bestanden
  authstate.json          → authenticatiestatus
  MarketCharts\           → gecachte OHLCV JSON-bestanden per coin
    MarketChart_{ApiId}.json
  Backup\
    RestorePoint_*.cpt    → portfolio-backups
  Icons\                  → coinlogo's (PNG)
```

---

## 3. Navigatiestructuur

De app gebruikt een `NavigationView` (WinUI 3) met een collapsible zijmenu.

### 3.1 Primaire menu-items

| Menu-item | View | Beschrijving |
|-----------|------|--------------|
| Dashboard | `DashboardView` | Overzicht portfolio + signaalwidget + marktregime |
| Assets | `AssetsView` | Alle assets met realtime koers en P&L |
| Accounts | `AccountsView` | Portfolio-accounts (exchanges) |
| Narratieven | `NarrativesView` | Thema-groepen van coins (DeFi, L1, AI, etc.) |
| Signalen | `SignalsView` | TA-indicatoren + signaal-engine per coin |
| Trade Advies | `TradeAnalysisView` | Multi-timeframe analyse, entry/SL/TP |
| Trade Journal | `TradeJournalView` | Overzicht paper + live trades |
| Statistieken | `StatisticsView` | Geaggregeerde handelsprestaties |
| Bronnen | `SourcesView` | Sentimentbronnen (Reddit, RSS, etc.) |
| CoinLibrary | `CoinLibraryView` | Bibliotheek van alle gevolgde coins |
| Prijsniveaus | `PriceLevelsView` | Heatmap + prijsniveaus per coin |
| Pattern Trading | `PatternTradingView` | Automatische patroonherkenning voor portfolio-coins |
| Setup Tracker | `SetupTrackerView` | Win-rate monitoring van gevolgde trade-setups |
| 3% Trading | `ThreePctView` | Gekalibreerd 7-factor scoremodel met +3% netto-doel (kalibratie + live scan) |
| Fundamentals | `FundamentalsView` | Fundamentele analyse: Fundamental Score (0-100), DD-invoer, SWOT-rapport, DefiLlama TVL |

### 3.2 Footer-items

| Footer-item | View / actie | Beschrijving |
|-------------|--------------|--------------|
| Help | `HelpView` | On-page gebruikshandleiding (uitklapbare secties, formules, FAQ) |
| What's New | `WhatsNewView` | Versiegeschiedenis |
| Instellingen | `SettingsView` | App-configuratie |
| Afsluiten | — | `Environment.Exit(0)` |

### 3.3 Conditionele items

`Switch Portfolio` en `Admin` verschijnen alleen in debug of bij specifieke gebruikersinstellingen.

---

## 4. Pagina's en functionaliteiten

### 4.1 Dashboard

**Doel:** Snel overzicht van de meest relevante informatie.

**Componenten:**
- **Portfolio-waarde:** totale waarde in USDT, dagelijkse wijziging
- **Signaal-widget:** de 6 coins met de hoogste CombinedScore, met richtingsbadge (Long/Short/Flat) en kleurcodering
- **Market Regime-kaart:** huidig BTC-regime (RiskOn / Neutral / RiskOff) met top-6 redeneerregels uit het meest recente signaal
- **Sparklines:** lineair regressietrend-visualisatie op 1H, 4H en 1D per coin

---

### 4.2 Assets

**Doel:** Realtime portfolio-overzicht per asset.

**Portfolio-correlatie *(v1.35)*:** een knop in de header opent `PortfolioCorrelationDialog`, dat via
`IPortfolioCorrelationService` per holding de Pearson-correlatie met BTC berekent (60 dagrendementen, Binance-klines
+ `ICorrelationService`) en deze **waarde-gewogen** aggregeert tot een diversificatie-oordeel
(`PortfolioCorrelationCalculator`, puur/getest). Toont per coin een correlatiebalk + label (Hoog/Gemiddeld/Laag)
en een samenvattend verdict. Geen nieuwe databron — hergebruikt de bestaande klines en correlatie-engine.

**Kolommen:** Coin · Symbool · Rang · Hoeveelheid · Gem. kostprijs · Huidige prijs · Waarde · P&L (USDT + %) · Account

**Sortering:** op elke kolom klikbaar (oplopend/aflopend)

**Acties:**
- Transactie toevoegen (koop/verkoop/storting/opname)
- Asset verwijderen
- Volledig scherm: taartdiagram portfolio / accounts / narratieven

---

### 4.3 Accounts

**Doel:** Beheer van exchange-accounts als subportfolio's.

**Functionaliteit:**
- Accounts aanmaken, hernoemen, verwijderen
- Balans-vergelijking met echte exchange (MEXC, Bybit)
- Import van MEXC-handelshistorie (deduplicatie via `SourceId = "MEXC:{tradeId}"`)

---

### 4.4 Narratieven

**Doel:** Coins groeperen per thema of beleggingshypothese.

**Functionaliteit:**
- Narratief aanmaken (naam, beschrijving, kleur)
- Coins koppelen aan narratief
- Signaal-engine per narratief uitvoeren (alleen coins in dat narratief evalueren)

---

### 4.5 Signalen (Analyse-pagina)

**Doel:** Technische analyse en signaaloverzicht voor alle portfolio-coins.

**Kolommen per coin:**

| Kolom | Inhoud | Bron |
|-------|--------|------|
| Rank | CoinGecko marktrangschikking | API |
| Naam | Coin naam + logo | DB |
| MACD | Positief/negatief momentum | `CalculateMacdAsync` |
| Bollinger | %B positie (0–100) | `CalculateBollingerAsync` |
| ATR | Absolute volatiliteit (14-daags) | `CalculateAtrAsync` |
| StochRSI | Oscillator 0–100 | `CalculateStochRsiAsync` |
| Sentiment | Gewogen gem. sentimentscore (–1.0 tot +1.0) | `SentimentService` |
| Regime | Marktregime (RiskOn/Neutral/RiskOff) | `MarketRegimeService` |
| Score | CombinedScore 0–100 | `SignalEngine` |
| Richting | Long / Short / Flat | `SignalEngine` |
| EMA Cross | Bullish/Bearish + dagen geleden | `CalculateExtendedIndicatorsAsync` |
| RSI | Dagelijks, 14-perioden | `CalculateRsiAsync` |
| MA50% | Afstand t.o.v. 50-daags gemiddelde | `CalculateMaAsync` |
| ADX | Trendsterkte (>25 = trend) | `CalculateExtendedIndicatorsAsync` |
| %B | Bollinger Band positionering | Idem |
| Squeeze | Volatiliteitscompressie Bollinger/Keltner | Idem |
| 52w% | % onder 52-weeks hoog (180-daags) | Idem |

**Knoppen:**
- **Refresh Analysis** — herlaadt prices + berekent alle indicatoren (skippt herlaad als recent)
- **Evaluate Signals** — voert volledige signaal-engine uit, slaat signalen op in DB
- **Paper Trade** (per rij) — opent `PaperTradeDialog` voor die coin

**Sortering:** alle kolommen klikbaar met pijl-indicator

**Richtingsfilter *(v1.37)*:** een dropdown "Richting" (Alle/Long/Short/Geen signaal) in de toolbar. De VM houdt
de volledige rijenlijst in `_allRows`; `DirectionFilter` filtert daaruit en `ApplySortToRows` past eerst de filter
en daarna de kolomsortering toe op `Rows`. "Geen signaal" = richting ≠ Long en ≠ Short.

---

### 4.6 Trade Advies

**Doel:** Multi-timeframe analyse per coin met concreet handelsadvies.

**Fundamenteel kwaliteitsoordeel *(v1.35)*:** na het analyseren toont de statusbalk een Fundamental-badge
(`Ⓕ score · verdict`) van de geanalyseerde coin via `IFundamentalsService.GetAsync(ApiId)`, zodat technische
en fundamentele kwaliteit naast elkaar staan.

**Werkwijze:**
1. Gebruiker selecteert coin uit portfolio-lijst
2. App haalt live OHLCV op van Binance (geen API-key vereist)
3. Fallback: KuCoin → lokale JSON-cache
4. Analyse op 4 timeframes: Weekly (104 bars), Daily (300), 4H (500), 1H (200)
5. Resultaat: trade setup met entry, SL, TP1/TP2, R/R-ratio, confidence

**Bulk-analyse:**
- Knop **"Analyseer alles"** → analyseert alle portfolio-coins concurrent (max 3 tegelijk)
- Resultatenlijst gesorteerd: Long signals (sterkste eerste) → Short signals → geen signaal
- Elke rij toont: logo · naam · richting · entry · SL · TP · R/R · databron
- **Richtingsfilter *(v1.37)*:** dropdown "Richting" (Alle/Long/Short/Geen signaal) boven het Overzicht.
  `TradeAnalysisViewModel.OverviewDir` stuurt het filter; de Overzicht-lijst (code-behind `RenderAllResults`)
  filtert `AllResults` op richting en herrendert bij wijziging. De titelregel toont het actieve filter.

**Paper Trade vanuit advies:**
- Bij Long/Short-signaal: knop "Paper Trade" opent de `PaperTradeDialog` met exchange-stijl interface
- Pre-fills: entry (limit), SL/TP/TP2 als absolute USDT-prijzen, richting (Long/Short)
- Direct opgeslagen in Trade Journal

**Validatie van het advies *(v1.33)*:** de gegenereerde SL/TP-niveaus worden bij generatie
gecontroleerd via `TradeSetupValidator.CheckAdvice`. Een degenerate setup (bv. ATR=0 → SL = entry)
wordt als **ongeldig** gemarkeerd (rode InfoBar); een krappe R/R (< 1,5:1) geeft een waarschuwing.
Dezelfde validatie draait in Pattern Trading.

**Markt-context *(v1.33)*:** bij een Binance-signaal toont het advies ook liquiditeit
(orderboek-spread & diepte via `OrderBookService`), positionering (funding & long/short via
`BinanceFuturesDataService`) en event-risico's (FOMC/CPI/NFP/PCE via `MacroEventService`).

**Zie §6.5 voor de gedetailleerde berekeningen.**

---

### 4.7 Trade Journal

**Doel:** Overzicht en beheer van alle orders (paper én live).

**Kolommen per order:**

| Kolom | Beschrijving |
|-------|-------------|
| Symbool | Handelspaar (bijv. BTCUSDT) |
| Exchange | MEXC / Bybit |
| Markt | Spot / Futures / Margin |
| Kant | Long / Short |
| Hefboom | 1× – 100× (alleen Futures/Margin) |
| Hoeveelheid | Aantal coins |
| Instap | Instapprijs (USDT) |
| Huidig/Sluit | Huidige marktprijs of sluitprijs |
| P&L (USDT) | Gerealiseerde of ongerealiseerde winst/verlies |
| P&L % | Procentueel rendement |
| R-Multiple | Rendement uitgedrukt in risico-eenheden |
| SL / TP1 / TP2 | Stop-loss en twee take-profit niveaus (absolute USDT-prijzen) |
| Status | Pending / Filled / Closed / Cancelled |
| Type | Paper / Live |
| Datum | Aanmaakdatum |
| Notities | Vrij tekstveld (bewerkbaar) |

**Filters:** Alles · Open · Gesloten · Paper · Live  
Standaardfilter bij openen: **Open**. De actieve tab wordt gemarkeerd met een gouden onderstreping (2 px).

**Acties per rij:**

| Knop | Icoon | Zichtbaar wanneer | Actie |
|------|-------|-------------------|-------|
| Notitie bewerken | ✏️ (potlood) | Altijd | Opent inline teksteditor |
| Order annuleren | ✕ | Status = Pending / PartiallyFilled | Zet status op Cancelled |
| SL / TP aanpassen | 🔧 (moersleutel) | Paper + Status Filled of Pending | Opent `EditTradeDialog` |
| Positie sluiten | ✓ | Paper + Status Filled + koers bekend | Sluit op huidige marktprijs |

**Kill All:** sluit alle open papierposities in één keer na bevestiging.

**Vernieuwen:** herlaadt orders, actualiseert koersen en voert auto-close check uit.

**Kolom Unrealised P&L:** toont alleen waarden voor orders met status `Filled`. Voor gesloten of geannuleerde orders staat `–`.

**Auto-close bij TP/SL bereikt:**  
Bij elke vernieuwopdracht roept de ViewModel `AutoCloseTriggeredAsync()` aan. Orders worden automatisch gesloten als de huidige koers een ingesteld niveau heeft bereikt (zie §6.1.4).

**Totaalregel:** som van alle gerealiseerde P&L voor de actieve filter.

**Risico-dashboard *(v1.36, scope-splitsing v1.37)*:** de knop 'Risico' opent `RiskDashboardDialog` met een
**Paper/Live-schakelaar** (`RiskScope`). `IRiskDashboardService.BuildAsync(scope)` filtert de open (Filled) orders op
`IsPaper` en bouwt via de pure `RiskDashboardCalculator` per bereik een overzicht: open posities vs `MaxOpenPositions`,
totaal open risico (som van verlies-bij-SL) + % van kapitaal, grootste positie-risico, blootstelling, dag-P&L vs
`DailyLossLimitPerc`, kill-switch-status en guardrail-alerts. **Kapitaalbasis per scope:** paper rekent tegen de
gekozen basis (`IRiskCapitalService`: `Settings.PaperVirtualCapital` of echte portfoliowaarde via
`Settings.UseRealPortfolioForRisk`); live rekent **altijd** tegen de echte portfoliowaarde
(`IRiskCapitalService.GetRealPortfolioValueAsync`). Paper- en live-risico vermengen dus nooit.

**Guardrail-handhaving *(v1.37)*:** `TradeService.PlacePaperAsync` controleert vóór elke order de guardrails via
`IGuardrailService` (pure `GuardrailEvaluator`): een actieve **kill-switch**, het bereikte **max aantal open
paper-posities** of een bereikte **dagelijkse verlieslimiet** (gerealiseerde paper-dag-P&L ≤ −limiet% × kapitaal)
blokkeren de order met een `InvalidOperationException` ("⛔ Geblokkeerd door risk-guardrails: …") die alle
aanroepende views als statusmelding tonen. Een limiet van 0 = niet ingesteld. Een falende check blokkeert nooit
(fail-open, gelogd).

**Trade-alerts via Telegram *(v1.37)*:** `INotifierService.SendAlertAsync` (best-effort, faalt stil) wordt
aangeroepen bij: auto-fill van pending orders, auto-close op TP/SL (incl. P&L), statusovergangen van gevolgde
setups (entry geraakt / TP1 / TP2 / SL) en — eenmaal per dag — het bereiken van de dagelijkse verlieslimiet.
Vereist alleen de bestaande Telegram-configuratie (`IsTelegramEnabled` + token + chat-id).

**Zie §6.1 voor P&L- en R-multiple-berekeningen.**

---

#### 4.7.1 PaperTradeDialog

Exchange-stijl orderformulier (540 px breed, `ContentDialog`).

**Risico-gebaseerde positiegrootte *(v1.36)*:** een 'Risico %'-veld (standaard `Settings.MaxPortfolioPercPerTrade`)
+ knop berekent via de pure `PositionSizeCalculator` het inlegbedrag zodat verlies-bij-SL = risico% van de
**gekozen kapitaalbasis** (via `IRiskCapitalService`: paper-kapitaal of echte portfolio), met hefboom verrekend. Een live indicator toont continu het actuele risico-% en waarschuwt
bij overschrijding van de per-trade-limiet; bij een actieve kill-switch (`Settings.IsKillSwitchActive`) verschijnt
een melding. *(Max-open-posities en dagelijkse-verlieslimiet horen op het toekomstige risico-dashboard.)*

**Rij-indeling:**

| Sectie | Inhoud |
|--------|--------|
| Coin banner | Symbool · naam · actuele prijs · 24h-wijziging |
| Markttype | RadioButtons: Spot / Futures / Margin + Exchange ComboBox |
| Ordertype | RadioButtons: Limit / Market |
| Limietprijs | `NumberBox` (verborgen bij Market-order) |
| Bedrag | `NumberBox` (USDT) + snelknoppen 25 % / 50 % / 75 % / Max op virtueel kapitaal (€ 10 000) |
| Hefboom | ComboBox 1× – 100× (verborgen bij Spot) |
| SL / TP1 / TP2 | `CheckBox` + `NumberBox` (absolute USDT-prijzen) + live %-label |
| TP-sluitpercentage | Per TP-niveau: snelknoppen 25 / 50 / 75 / 100 % + `Slider` (1–100) — welk deel van de positie op dat niveau gesloten wordt |
| Samenvatting | Kostprijs · Hoeveelheid · R/R-ratio · Max risico |
| Signaalreden | `Expander` met reasoning-tekst (uit Trade Advies of Signaalengine) |
| Actieknoppen | 📈 **Open Long** (groen) · 📉 **Open Short** (rood) |

**Gedrag:**
- `Recalculate()` herberekent realtime bij elke invoerwijziging
- `OrderType_Changed` toont/verbergt limietprijs-rij
- `MarketType_Changed` toont/verbergt hefboom-rij
- Actieklant stelt `Confirmed = true` en `SelectedSide` in; caller controleert `dialog.Confirmed`
- `BuildOrderRequest()` levert een `OrderRequest`-record terug aan de ViewModel

**OrderRequest (record):**

```csharp
public record OrderRequest(
    ExchangeKind Exchange,
    OrderSide    Side,
    MarketType   MarketType,
    OrderType    OrderType,
    double       AmountUsdt,
    double       LimitPrice,       // 0 = market order
    double       StopLossPrice,    // 0 = geen stop
    double       TakeProfitPrice,  // 0 = geen take-profit
    double       TakeProfit2Price, // 0 = geen tweede take-profit
    int          Leverage,         // 1 = geen hefboom
    double       Tp1ClosePct = 100, // % van positie te sluiten op TP1 (1–100)
    double       Tp2ClosePct = 100, // % van positie te sluiten op TP2 (1–100)
    string       Notes = "");
```

---

#### 4.7.2 EditTradeDialog

Dialoog voor het aanpassen van SL / TP1 / TP2 van een lopende paper trade (520 px breed, `ContentDialog`).

**Bereikbaar via:** moersleutel-knop (🔧) naast elke `IsEditable`-rij in het Trade Journal.  
`IsEditable = Status is "Filled" or "Pending" && IsPaper`

**Layout:**

| Sectie | Inhoud |
|--------|--------|
| Banner | Symbool · Long ▲ / Short ▼ badge (groen/rood) · instapprijs · huidige koers · ongerealiseerde P&L (USDT + %) |
| Stop Loss | Huidig SL + %-afstand van entry · preset-knoppen · `NumberBox` + live %-label |
| Take Profit 1 | Huidig TP1 + % · `NumberBox` + live %-label |
| Take Profit 2 | Huidig TP2 + % · `NumberBox` + live %-label |
| Samenvatting | Nieuw R/R-ratio · Max risico (USDT) · SL-afstand van entry |
| Actie | Groene knop **Wijzigingen opslaan** |

**Preset-knoppen Stop Loss:**

| Knop | Formule Long | Formule Short |
|------|-------------|---------------|
| ⚡ Breakeven | SL = entry | SL = entry |
| ½R vrij | SL = entry + ½ × initialRisk | SL = entry − ½ × initialRisk |
| +1R | SL = entry + initialRisk | SL = entry − initialRisk |

`initialRisk = |entry − oorspronkelijkeSL|`

Een preset is **uitgeschakeld** (opacity 0.4, `IsEnabled = false`) als het berekende niveau de huidige koers al heeft bereikt — dat zou de auto-close onmiddellijk triggeren:
- Long: preset uitgeschakeld als `presetSL ≥ huidigeKoers`
- Short: preset uitgeschakeld als `presetSL ≤ huidigeKoers`

**Inline waarschuwing:** als de handmatig ingevoerde SL een niveau bereikt dat direct auto-close triggert, verschijnt een oranje waarschuwingsregel. Opslaan is geblokkeerd totdat het niveau gecorrigeerd is.

**Resultaat-properties (na bevestiging):**
```csharp
bool   Confirmed     // true als de gebruiker opgeslagen heeft
double NewStopLoss
double NewTakeProfit
double NewTakeProfit2
```

**Service-methode:** `ITradeService.UpdateOrderLevelsAsync(orderId, sl, tp1, tp2, currentPrice)` — past `StopLoss`, `TakeProfit` en `TakeProfit2` aan in de database.

**Validatie bij bewerken *(bugfix v1.38)*:** een **gevulde (open) positie** wordt gevalideerd via
`TradeSetupValidator.ValidateForOpenPosition` t.o.v. de **huidige koers** (de VM geeft `row.CurrentPrice` mee),
zodat de stop naar winst getrokken mag worden (een short-stop mág onder de entry zakken zolang hij boven de
huidige koers blijft; spiegelbeeld voor long). Een **Pending** order blijft via `Validate` t.o.v. de geplande
entry gecontroleerd. Voorheen werd alles tegen de entry gevalideerd, waardoor het borgen van winst op een
lopende trade ten onrechte werd geweigerd.

**Risicovrij-weergave *(v1.38)*:** `EditTradeDialog.RecalcSummary` toont, zodra de stop op/voorbij break-even
staat (long: SL ≥ entry, short: SL ≤ entry), **'Risicovrij ✓'** i.p.v. een R/R-ratio en herlabelt 'Max risico'
naar **'Geborgde winst'** (+USDT = `|entry − SL| × qty`). Bij exact break-even toont het 'Break-even'. Dit
voorkomt het misleidende positieve risico-getal dat de oude `Math.Abs(entry − SL)`-berekening gaf.

---

### 4.8 Statistieken

**Doel:** Geaggregeerde analyse van handelsprestaties.

**Filters (bovenaan):**

| Filter | Opties |
|--------|--------|
| Type | Alle · Live · Paper |
| Periode | Alles · Deze maand · Afgelopen 3 maanden · Dit jaar · Aangepast |

Bij "Aangepast" verschijnen twee `DatePicker`-controls voor start- en einddatum.

**Samenvattingskaarten:**

| Kaart | Berekening |
|-------|------------|
| Totale P&L | Som P&L gesloten orders |
| Win rate | % winstgevende trades van totaal gesloten |
| Gem. winst / verlies | Gemiddelde P&L van winst- resp. verliesorders |
| Open posities | Aantal orders met status `Filled` |
| Totaal volume | Som (entry × qty) gesloten orders |

**Taartdiagrammen:**
- Winst / Verlies / Neutraal (op basis van P&L-teken)
- Long / Short (op basis van `OrderSide`)
- Paper / Live (op basis van `IsPaper`)

**Toptabel:** top-5 beste + top-5 slechtste symbolen (gededupliceerd), gesorteerd op totaal P&L.

**Pivot-structuur *(v1.31)*:** de pagina is opgedeeld in twee tabbladen via een WinUI 3 `Pivot`:

| Tabblad | Inhoud |
|---------|--------|
| Trade Journal | Bestaande kaarten, taartdiagrammen, toptabel; plus Trade-type (Live/Paper) filter |
| Setup Strategie | Win Rate TP1/TP2, Profit Factor, Expectancy, gem. P&L%, gem. houdtijd, breakdowntabellen per richting/score/marktregime |

De periodefilter (bovenaan de pagina) geldt voor beide tabbladen.

**Zie §6.2 voor trade-statistieken en §6.9 voor setup-strategie-statistieken.**

---

### 4.9 Bronnen (Sources)

**Doel:** Beheer van sentimentbronnen.

**Functionaliteit:**
- Lijst van actieve bronnen: Type · URL/Handle · Betrouwbaarheidsscore · Actief/Inactief
- 17 pre-geladen bronnen bij eerste opstart
- Bron toevoegen, bewerken, verwijderen
- **Ophalen-knop:** start handmatige sentimentcollectieronde
- Statusweergave: laatste uitvoertijd, aantal readings (totaal + afgelopen 24u)

---

### 4.10 CoinLibrary

**Doel:** Bibliotheek van alle gevolgde coins (ook coins zonder asset).

**Functionaliteit:**
- Coin toevoegen vanuit CoinGecko-zoekresultaten
- Coin verwijderen (inclusief bijhorende assets en transacties)
- Coin koppelen/ontkoppelen aan portfolio

---

### 4.11 Prijsniveaus

**Doel:** Visuele heatmap en prijsniveau-beheer.

**Heatmap-modi:**
- Portfolio-heatmap (waarde per coin)
- Accounts-heatmap
- Narratieven-heatmap

**Prijsniveaus per coin:**
- Types: `TakeProfit`, `Stop`, `Buy`, `Ema`
- Status-tracking: `NotWithinRange`, `WithinRange`, `CloseToPrice`, `TaggedPrice` (en combinaties)

---

### 4.12 Instellingen

**Tabblad 1 — Instellingen:**
- App-thema (licht/donker/systeem)
- Getalsnotatie (NL: `1.234,56` / EN: `1,234.56`)
- Taal (NL/EN)
- Lettergrootte
- Update-controle (aan/uit + handmatig controleren)
- Wachtwoordbeheer (encryptie portfolio)
- Telegram Bot Token + Chat ID voor notificaties
- Signaaldrempel (slider 50–85, standaard 60)
- Paper trading toggle
- Exchange API-sleutelbeheer (HMAC / RSA)
- Risicobewakers: max % per trade, max open posities, dagelijkse verliesgrens, kill-switch

**Tabblad 2 — Databronnen:**
Handmatig bijgehouden overzicht van alle externe en lokale databronnen (zie §7).

**Tabblad 3 — Belasting:**
Box 3-calculator (zie §10).

---

### 4.13 Pattern Trading

**Doel:** Automatische technische patroonherkenning voor alle portfolio-coins. Scant op Level 1 (indicator-gebaseerd), Level 2 (koerstructuur) en Level 3 (klassieke grafiekpatronen) op **1D, 4H, 1H en 15M** en genereert een handelsscore + concreet setup-advies.

**Fundamenteel kwaliteitsoordeel *(v1.35)*:** elke coin-kaart toont naast de technische TradabilityScore een
Fundamental-badge (`Ⓕ score · verdict`) via `IFundamentalsService.GetScoreMapAsync` (lookup op `ApiId`, gecachet
bij `ViewLoading`), zodat technische en fundamentele kwaliteit in één oogopslag samenkomen.

**Gedeelde marktcontext-balk *(v1.35)*:** de herbruikbare `Controls/MarketContextBar` toont bovenaan Pattern
Trading, 3% Trading en de Setup Tracker dezelfde context — BTC-regime, Fear & Greed-index en het eerstvolgende
macro-event — geaggregeerd door `IMarketContextService` (combineert `IMarketRegimeService` + `IFearGreedService`
+ `IMacroEventService`, ~5 min gecached). De control laadt zichzelf via `App.Container`; geen per-view bedrading nodig.

**Liquiditeitscheck *(v1.35)*:** de knop 'Check liquiditeit' draait `CheckLiquidityCommand` — haalt voor de
getoonde setups het Binance-orderboek op (`IOrderBookService`) en labelt elke setup via de pure
`LiquidityClassifier` (spread + diepte → Liquide/Matig/Dun). Bewust **on-demand** i.p.v. in de bulk-scan, zodat de
multi-timeframe patroonanalyse snel blijft. Zelfde F6-gatekeeper-gedachte als 3% Trading.

**Werkwijze:**
1. Gebruiker klikt **Analyseer** → app analyseert alle portfolio-coins met holdings concurrent (max 3 tegelijk)
2. Per coin: OHLCV ophalen (Binance → KuCoin → Gate.io → MEXC), indicatoren berekenen, patronen detecteren
3. Resultaten sorteren op TradabilityScore (hoog → laag) en weergeven als kaarten

**Filters:**
| Filter | Omschrijving |
|--------|-------------|
| Alle | Alle geanalyseerde coins |
| Hoog gescoord | TradabilityScore ≥ 60 |
| Bijna Breakout | `IsNearBreakout = true` |
| Bullish | PrimaryDirection = "Long" |
| Bearish | PrimaryDirection = "Short" |

Daarnaast: een **timeframe-filter** (Alle/1D/4H/1H/15M), een **in-lijst zoekveld** (naam/symbool) en een
**patroon-filter** *(v1.37)*. De patroon-filter is een dropdown (`PatternOptions` / `SelectedPattern`,
model `PatternFilterOption`) die zich na elke scan vult met de patroontypes die in de resultaten voorkomen
(key-patronen, Strength ≥ 60), elk met het aantal coins dat dat patroon heeft, gesorteerd op aantal. Selectie
toont alleen coins met een key-patroon van dat type (`r.KeyPatterns.Any(p => p.Type == gekozen)`). De keuze
blijft behouden zolang het patroon in een volgende scan nog voorkomt en is combineerbaar met de overige filters.
`PatternResult.NameFor(PatternType)` levert de Nederlandse patroonnaam zonder instance.

**Coin-kaart per resultaat:**
- Logo · naam · symbool · TF-bias badges (1D/4H: Bullish/Bearish/Neutraal) · RSI-badges
- Actuele prijs · 24u-wijziging (groen/rood) · TradabilityScore (0–100) met kleurcodering
- Patroon-badges (max 6, Strength ≥ 55, bullish voor bearish, groen/rood/oranje); de **"+N"-overflow-chip** toont bij mouseover (`OverflowToolTip`) de overige patronen met timeframe, naam, sterkte en bevestigingsstatus
- ⚡ Bijna Breakout indicator (indien van toepassing)
- Setup-kaart (alleen als Score ≥ 40): Entry · Stop · TP1 · TP2 · R/R · Confidence · Entry-notitie
- Redeneer-bullets (max 4, uit de analyse)
- Databron-indicator + analysetijdstip
- **📋 Kopieer-knop** → Dutch-language setup-tekst naar klembord

**Analyse-voortgang:**
- `ProgressBar` toont 0–100%
- `StatusText` met fase-informatie (`"Analyseren… 42%"`)
- Annuleerbaar via ingebouwde CancellationToken
- Tijdstempel "Laatste analyse: HH:mm"

**Grafiekweergave (`CoinChartWindow`):** candlestick-grafiek (WebView2 + Lightweight Charts) met support/resistance
en patroon-overlays. Te openen via het **grafiek-icoon** (zonder highlight) of via een **patroon-badge** (zet het
start-timeframe op dat patroon). **Sinds v1.37** tekent de grafiek voor het actieve timeframe **alle** geometrische
patronen samengevoegd (`MergeAnnotations` over `_analysis.Patterns` met niet-lege `Annotation`), ongeacht het
entry-point en ook bij het wisselen van timeframe. Alleen Level 2/3-patronen dragen een annotatie; Level 1
indicator-signalen (RSI/MACD/EMA/squeeze) hebben er geen en verschijnen dus niet als overlay.

**Legenda + C&H-doelen *(v1.38)*:** de grafiek toont bovenin een **legenda** (`UpdateLegend`) die per getoond
patroon de symbolen uitlegt (gekleurde markers L/B/R, schouders, bodems; lijnen = neklijn/targets/trendlijnen).
**Cup & Handle** tekent nu expliciet de **neklijn** (breakout = `handleHigh`) plus twee koersdoelen:
**T1** = `handleHigh + handle-diepte` en **Tmax** = `handleHigh + cup-diepte` (measured move).

**Doel-naamgeving (consistent, v1.38):** **Neklijn** waar van toepassing, anders **Breakout/Breakdown**;
tussendoelen **T1/T2**; het maximale (measured-move) doel **Tmax**. Flags hebben één doel → Tmax.

**Reversal-doelen *(v1.38)*:** dubbele top/bodem en (Inv.) H&S tekenen nu een **Tmax**-lijn = patroonhoogte
vanaf de neklijn geprojecteerd (top/H&S omlaag: `neklijn − (top/hoofd − neklijn)`; bodem/Inv. H&S omhoog:
`neklijn + (neklijn − bodem/hoofd)`).

**ViewModel:** `PatternTradingViewModel` (`ViewModels/`) erft van `BaseViewModel`
**View:** `PatternTradingView` (`Views/`)

**Zie §6.7 voor de TradabilityScore-berekening.**

---

### 4.14 Setup Tracker

**Doel:** Win-rate monitoring van gevolgde trade-setups. Elke setup is een snapshot van een patroonanalyse-advies dat de gebruiker actief wil volgen. De tracker detecteert automatisch of entry, TP1 of stop-loss worden geraakt.

**Werkwijze:**
1. Gebruiker klikt **"Volg trade setup"** op een Pattern Trading coin-kaart → `WatchedSetup` wordt aangemaakt
2. Bij elke `RefreshAsync` haalt de ViewModel live koersen op via `BuildPriceMapAsync()`
3. `AutoUpdateStatusesAsync()` evalueert alle Watching + Open setups:
   - Entry geraakt (Long: koers ≤ entry; Short: koers ≥ entry) → status `Open`
   - TP1 geraakt (Long: koers ≥ TP1; Short: koers ≤ TP1) → status `Won`
   - SL geraakt (Long: koers ≤ SL; Short: koers ≥ SL) → status `Lost`
   - Geen trigger → status ongewijzigd
4. `CurrentPrice` wordt ingesteld op de setup (runtime `[NotMapped]`) vóór toevoeging aan `ObservableCollection`

**Statussen (`WatchedSetupStatus` enum):**

| Waarde | Label (UI) | Betekenis |
|--------|-----------|-----------|
| 0 Watching | 🔵 Watching | Setup gevolgd, entry nog niet geraakt |
| 4 Open | 🟡 In Trade | Entry geraakt, trade actief |
| 1 Won | 🟢 Gewonnen | TP1 geraakt |
| 2 Lost | 🔴 Verloren | Stop-loss geraakt |
| 3 Expired | ⚫ Verlopen | Handmatig verlopen verklaard |

**Stats-balk (bovenin pagina):** Totaal · In Trade · Watching · Won · Lost · Win Rate %

**Filteropties:** Alle · 🔵 Watching · 🟡 In Trade · 🟢 Won · 🔴 Lost · ⏹ Verlopen

**Card-indeling (4 kolommen + logo):**

| Kolom | Inhoud |
|-------|--------|
| 0 — Logo | Coin-logo (36 × 36 px) |
| 1 — Coin | Naam · ticker · richting-badge · bias-badges (1D/4H) · patroon-samenvatting · huidige live koers + %-afstand van entry |
| 2 — Levels | Entry · SL (rood) · TP1 (groen) · R/R |
| 3 — Status | Score-badge · status-badge (met tooltip) · P&L % (Won/Lost) of Unreal. P&L % (Open) · leeftijd |
| 4 — Acties | Knoppen (zie hieronder) |

**Acties per card (zichtbaar voor Watching / Open setups):**
- **✅ Gewonnen** — sluit handmatig als Won op de huidige live koers
- **❌ Verloren** — sluit handmatig als Lost op de huidige live koers
- **⏹ Verlopen** — zet status op `Expired` (patroon geïnvalideerd)
- **🗑 Verwijder** — verwijdert setup permanent uit DB (altijd zichtbaar)

**Databron live koersen:** `BuildPriceMapAsync()` bevraagt eerst `GetCoinsFromContext()` (DB-query, altijd beschikbaar), aangevuld met in-memory `ListCoins`. Dit garandeert dat koersen ook zichtbaar zijn als de Library-pagina nooit geladen is in de huidige sessie.

**Auto-refresh:** de ViewModel ontvangt `UpdatePricesMessage` via `IMessenger` na elke koersupdatecyclus — prijzen en statussen worden dan automatisch ververst zonder gebruikersinteractie.

**ViewModel:** `SetupTrackerViewModel` (`ViewModels/`) erft van `BaseViewModel`  
**View:** `SetupTrackerView` (`Views/`)  
**Service-interface:** `IWatchedSetupService` (`Services/`)

**Score-kalibratie / feedback-loop *(v1.35)*:** `IWatchedSetupService.GetScoreCalibrationAsync` voert de
gesloten (Won/Lost) setups door de pure `SetupOutcomeCalibrator` → per scoreklasse (0-40/41-60/61-80/81-100)
de **werkelijk behaalde win-rate en gemiddelde R** (`ScoreBucketCalibration`, ≥10 trades = betrouwbaar).
Een kalibratie-strip in de stats-balk toont dit, zodat je ziet of een hogere TradabilityScore in jóuw praktijk
ook echt beter presteerde — de empirische tegenhanger van de 3%-Trading-backtest.

**Fundamenteel kwaliteitsoordeel *(v1.35)*:** per gevolgde setup wordt via `IFundamentalsService.GetScoreMapAsync`
(lookup op `CoinApiId`) de Fundamental Score + verdict als badge getoond (`Ⓕ 72 · Strong`), zodat technische
setup-kwaliteit en fundamentele kwaliteit naast elkaar staan.

**Marktregime vastleggen *(v1.31)*:** bij aanmaken van een setup zoekt `PatternTradingViewModel.WatchSetup` naar BTC in `_allResults` en slaat `Coin.MarketRegime.ToString()` op als `MarketRegimeAtCreation`.

**Setup ↔ Order koppeling *(v1.31)*:** bij het openen van een paper trade in `PatternTradingView` wordt de actieve setup voor die coin+richting opgezocht via `IWatchedSetupService.GetActiveSetupForCoinAsync`. Het `WatchedSetupId` wordt meegegeven in het `OrderRequest`, zodat `TradeService` het op de `ExchangeOrder` opslaat. Na plaatsing roept de view `LinkOrderAsync` aan om ook `WatchedSetup.LinkedOrderId` bij te werken.

**Zie §6.8 voor de Setup Tracker-berekeningen.**

---

### 4.15 What's New

Versiehistorie van de app. Bij eerste opstart na een update toont een dialog een samenvatting.

---

### 4.16 Help

**Doel:** On-page gebruikshandleiding — vervangt de externe PDF.

**Structuur:** `HelpView` (View) + code-behind (`HelpView.xaml.cs`). Geen ViewModel nodig — statische content.

**Opbouw:**
- Header-sectie identiek aan `WhatsNewView` (hero image, goudkleurige titels)
- `ScrollViewer` met `StackPanel` (`ContentPanel`)
- Categorie-headers (`AddCategoryHeader`) en uitklapbare `Expander`-secties (`AddSection`)
- Vier content-helpers:
  - `AddParagraph` — lopende tekst
  - `AddBullets` — ongeordende lijst met •
  - `AddFormula` — `Consolas`-achtergrondblok voor wiskundige uitdrukkingen
  - `AddNote` — blauwe info-box voor waarschuwingen en aanwijzingen

**Secties:**
| # | Categorie | Onderwerpen |
|---|-----------|-------------|
| 1 | Aan de slag | Portfolio aanmaken, coins toevoegen, transacties invoeren |
| 2 | Portfolio & Assets | P&L-berekening, accounts, narratieven, prijsniveaus |
| 3 | Trade Journal | Trades registreren, R-multiple |
| 4 | Trade Advies | CombinedScore, databronnen, SL/TP/ATR, pivotdetectie |
| 5 | Signalen & TA | 6 indicatoren, signaalregels configureren |
| 6 | Statistieken | Pagina-overzicht, periodefilters |
| 7 | Belasting (Box 3) | Berekenmethode, tarieven per jaar, invoervelden |
| 8 | Instellingen | Thema/taal, Telegram, Exchange API-koppelingen |
| 9 | Databronnen & privacy | Externe API's, lokale opslag |
| 10 | Veelgestelde vragen | Koersen, crash, backup, MEXC-sync |

**Uitbreiden:** voeg een nieuwe `AddSection(...)` toe in de juiste categorie in `HelpView.xaml.cs`.

**Navigatie:** Tag `"HelpView"` in `MainPage.xaml` — laadt via `LoadView(typeof(HelpView))` als standaard-view.

**Verwijderd:** externe PDF-help (`DisplayHelpFile()`), `QuestPdfService`, `IQuestPdfService`, `TestDocument`, QuestPDF NuGet-pakket.

---

### 4.17 3% Trading *(v1.33)*

**Doel:** Een gekalibreerd scoremodel met een vast netto-doel van **+3% na fees en slippage**.
Een "kans" is hier een historisch gemeten waarschijnlijkheid (uit een backtest), niet een aanname.

**Structuur:** `ThreePctView` + `ThreePctViewModel`. Twee Pivot-tabs.

**Fase 1 — Kalibratie (`ThreePctBacktestService`):**
- Haalt tot 1000 candles op (Binance klines) voor het opgegeven symbool/timeframe.
- Scoort elke historische bar met het 5-factor model en simuleert per signaal of eerst de
  netto-TP (+3%) of de structurele SL (1,5×ATR) wordt geraakt binnen de horizon (max. 15 bars).
- Groepeert op scoreklasse (`0-40` / `41-60` / `61-80` / `81-100`) en berekent per klasse:
  aantal trades, netto-hitrate, gemiddelde R en expectancy. Een klasse met < 30 trades is
  "onbetrouwbaar" (overfitting-waarschuwing).
- Resultaat wordt opgeslagen als JSON in `AppDataPath\3pct_calibration.json`.

**Fase 2 — Live Scan:**
- Scoort alle portfolio-coins, koppelt de score aan de gemeten hitrate/expectancy uit Fase 1.
- **7-factor model** (`ThreePctScoringService`): Trend (25%), Momentum (15%), Volume/OBV (15%),
  Volatiliteit (10%), Support/Resistance (15%) — plus **F6 Liquiditeit** en **F7 Positionering**
  als *gatekeepers*: setups onder de drempel (F6 < 4 of F7 < 3) worden gefilterd. De 5-factor
  score blijft ongewijzigd zodat de kalibratie geldig blijft.
- **Correlatie-diversificatie** (`CorrelationService`): bouwt een shortlist van max. 5 setups
  met onderlinge correlatie < 0,80, zodat sterk gecorreleerde alts als één trade tellen.
- **Detailvenster** (`SetupDetailDialog`): indicatoren, S/R, BTC-correlatie, positionering,
  liquiditeit, concreet invalidatieniveau en aankomende macro-events.
- **Fundamenteel kwaliteitsoordeel *(v1.35)*:** per live-scan-rij een Fundamental-badge (`Ⓕ score · verdict`)
  via `IFundamentalsService.GetScoreMapAsync` (lookup op `ApiId`), naast de technische score.

**Fase 3 — Paper Trades (forward-test):**
- Vanuit de Live Scan opent een **Paper**-knop de bestaande `PaperTradeDialog` (entry/SL/TP voorgevuld
  vanuit de rij). Na bevestiging plaatst `ITradeService.PlacePaperAsync` een paper-order die met de
  Notes-tag `[3%]` wordt gemarkeerd.
- Het tabblad **Paper Trades** toont uitsluitend deze `[3%]`-getagde orders (herbruikt `TradeJournalRow`),
  met samenvatting: aantal trades/open, win rate over gesloten trades, en totale P&L (gerealiseerd +
  ongerealiseerd).
- **Vernieuwen** draait `AutoFillPendingAsync` + `AutoCloseTriggeredAsync` op de actuele koersen — dezelfde
  engine als het Trade Journal — zodat pending limit-orders vullen en TP/SL triggeren. Zo kan de strategie
  met live data worden gevalideerd zonder echt kapitaal. Per rij is er een Sluit/Annuleer-actie.
- De trades verschijnen ook in het normale Trade Journal (het zijn gewone paper-orders).

**Marktregime:** `MarketRegimeService.GetRegimeContextAsync` bepaalt het regime via BTC EMA50/200
(golden/death cross) + BTC-dominantie. Ook gebruikt door `SignalEngine`.

**Externe data:** `OrderBookService` (Binance `/depth`), `BinanceFuturesDataService`
(`fapi.binance.com` funding/OI/long-short), `GlobalMarketDataService` (CoinGecko `/global`),
`MacroEventService` (FOMC/CPI/NFP/PCE-kalender). Alle netwerk-calls delen `TtlCache<T>`.

> **Macro-event tijden *(v1.35)*:** `MacroEvent.TimeUtc` bevat de precieze releasetijd (FOMC 14:00 ET,
> data-releases 08:30 ET, ET→UTC met zomertijd via `TimeZoneInfo`). Weergaven (`ShortDisplay`/`LocalDisplay`,
> marktcontext-balk) tonen die in de **lokale tijdzone** van de gebruiker. `Date` blijft de date-only kalenderdag.

---

### 4.18 Fundamentele Analyse *(v1.34)*

**Doel:** Fundamentals per coin inzichtelijk maken en een objectieve **Fundamental Score (0-100)**
toekennen volgens een professioneel due-diligence-raamwerk. **Hybride aanpak:** automatisch wat
meetbaar is uit CoinGecko, handmatig wat dat niet is.

**Datalaag (`FundamentalsService` / `IFundamentalsService`):**
- Haalt via CoinGecko `/coins/{id}` (met `developer_data` + `community_data`) de fundamentals op:
  aanbod (circulating/total/max), FDV, 24u-volume, ATH/ATL (+ % en datum), market-cap rank,
  categorieën, links (homepage/whitepaper/GitHub/Twitter/Reddit), GitHub-activiteit en community-cijfers.
- Mapt naar de persistente entiteit `CoinFundamentals` (één rij per coin, upsert op `ApiId`).
- `RefreshAllAsync` ververst de hele bibliotheek, rate-limited (~2,2s/call, demo-tier).

**Auto-scoring (`FundamentalsScoreCalculator`, puur/testbaar):** subscores (0-100) met transparante
drempels — Tokenomics (22%), Liquiditeit (18%), Waardering (13%), Community (13%), Development (13%),
Projectvolledigheid (9%) en **On-Chain/TVL (12%)** → samengestelde **DataScore**. De On-Chain-factor
(TVL-omvang + market-cap/TVL-efficiëntie) telt **alleen mee voor DeFi-coins met TVL**; voor niet-DeFi-coins
vervalt hij en worden de overige gewichten gehernormaliseerd, zodat ze niet onterecht worden afgestraft.
De Community-factor wordt *(v1.35)* bovendien bescheiden bijgestuurd door het eigen app-sentiment
(`Coin.LatestSentimentScore`, Reddit/RSS) wanneer beschikbaar — geen extra API-call.

**Hybride totaalscore:** handmatige due-diligence (team, product-maturiteit, adoptie, revenue,
unlocks — 0-10 elk) blendt met de DataScore tot de **TotalScore**; het DD-gewicht schaalt met het
aantal ingevulde velden. Een **Confidence** (0-100) geeft aan hoeveel van het raamwerk is onderbouwd.
**Verdict:** Exceptional (≥90) / Strong (≥80) / Promising (≥70) / Speculative (≥60) / High Risk (≥50) / Avoid.

**Databron-grenzen (eerlijk):** team, maturiteit, adoptie (DAU/MAU), revenue en unlock-schema's zijn met de
gratis API's niet betrouwbaar te automatiseren en komen via handmatige DD. On-chain TVL is wél geïntegreerd
(DefiLlama, zie hieronder). *Niet beschikbaar:* token-unlock-schema's — de DefiLlama-emissions-endpoint is
betaald (Pro); unlock-risico wordt benaderd via FDV/MC-overhang + de handmatige DD-factor.

**UI (`FundamentalsView` + `FundamentalsViewModel`):** overzichtspagina met de bibliotheek-coins
(via `ILibraryService.GetCoinsFromContext`), gesorteerd op score, met zoek-/filterbalk. Per coin een
**Analyseer**-knop die `RefreshAsync` on-demand aanroept (handmatig, geen bulk) en een **Detail**-knop
die `FundamentalsDetailDialog` opent: scorekop + verdict + betrouwbaarheid, de zes factor-subscores als
balken, en alle ruwe cijfers (waardering/aanbod, extremen, community, development, project-links/whitepaper,
beschrijving). De `FundamentalRow`-projectie levert kant-en-klare display-helpers en de score-kleur.

**Caching & versheid:** opgehaalde fundamentals worden persistent bewaard met `UpdatedAt`; bij het openen
wordt niets opnieuw afgeroepen (alleen op verzoek). Een instelbare **versheidsdrempel**
(`Settings.FundamentalsFreshnessDays`, standaard 7) bepaalt na hoeveel dagen een coin als "verouderd" geldt;
de knop **Verouderde** ververst alleen ontbrekende/verouderde coins (rate-limited), verse worden overgeslagen.

**Favorieten:** tot 10 coins zijn als favoriet te markeren (`Settings.FundamentalsFavorites`, CSV van ApiId's).
Favorieten staan bovenaan, zijn apart te filteren, en de knop **Favorieten** ververst in één keer alleen die set.

**Handmatige due-diligence + rapport (Sprint C):** in het detailvenster zijn de vijf DD-factoren (team,
product-maturiteit, adoptie, revenue, unlock-risico) bewerkbaar via sliders 0-10 met een "beoordeeld"-vinkje
(uitgevinkt = niet meegerekend) plus een notitieveld. Opslaan persisteert via `SaveDueDiligenceAsync`,
herberekent `TotalScore`/`Confidence` en herordent de lijst. Het venster toont tevens een **rule-based
analyse-rapport** (`FundamentalsReportBuilder`, puur/testbaar): executive summary, SWOT (sterktes/zwaktes/
kansen/bedreigingen), risiconiveau (LOW/MEDIUM/HIGH), een heuristische waarderingsconclusie en de top-risico's.

**On-chain TVL (DefiLlama):** `DefiLlamaService` (`IDefiLlamaService`) haalt éénmalig `api.llama.fi/protocols`
op (gecached 30 min) en matcht per coin op CoinGecko-id (symbool als fallback) → `Tvl` + `TvlCategory` op
`CoinFundamentals` (kolommen via `ApplyPlusSchemaAsync` + `TryAddColumnAsync` voor bestaande DBs). De
**market-cap/TVL-ratio** is een sterk waarderingssignaal voor DeFi-protocollen en voedt het rapport
(sterkte/zwakte/kans + verdict). Coins zonder DeFi-protocol (bv. BTC, de meeste L1's) hebben geen TVL → geen effect.
*Token-unlocks* zijn **niet** opgenomen: de DefiLlama-emissions/unlocks-endpoint geeft op de gratis tier
HTTP 402 (Pro/betaald). Unlock-risico wordt benaderd via de FDV/MC-overhang en de handmatige DD-factor "unlock-risico".

> Tabel wordt aangemaakt via `ApplyPlusSchemaAsync` (`CREATE TABLE IF NOT EXISTS`).

---

## 5. Data-model en relaties

### 5.1 Entity-Relationship diagram (tekstueel)

```
Coin (1) ──< Asset (N)
Coin (1) ──< SentimentReading (N)
Coin (1) ──< Signal (N)
Coin (1) ──< PriceLevel (N)

Asset (N) >── Account (1)
Asset (1) ──< Mutation (N)

Mutation (N) >── Transaction (1)

Signal (N) >── Narrative (1) [optioneel]
Signal (1) ──< ExchangeOrder (N) [optioneel, via SignalId]

SignalRule (N) >── Narrative (1) [optioneel]

BronSource (standalone)
ExchangeAccount (standalone)
FearGreedReading (standalone)
WatchedSetup (standalone — geen FK naar Coin; coin hoeft niet in portfolio te zijn)
```

### 5.2 Entiteiten gedetailleerd

#### Coin
Centrale entiteit — elk gevolgd coin.

| Eigenschap | Type | Omschrijving |
|-----------|------|-------------|
| `Id` | int | Primary key |
| `ApiId` | string | CoinGecko API-id (bijv. "bitcoin") |
| `Name` | string | Volledige naam (bijv. "Bitcoin") |
| `Symbol` | string | Ticker (bijv. "BTC") |
| `Rank` | long | CoinGecko marktrangschikking |
| `Price` | double | Actuele prijs in USDT |
| `Ath` | double | All-time high |
| `Change24Hr` | double | 24u procentuele wijziging |
| `Change1Month` | double | 30-daagse wijziging |
| `Change52Week` | double | 52-weekse wijziging |
| `MarketCap` | double | Marktkapitalisatie |
| `ImageUri` | string | Pad naar lokaal logo |
| `IsAsset` | bool | True als onderdeel van portfolio |
| `Macd` | double | MACD-waarde |
| `MacdSignal` | double | MACD signal-lijn |
| `BollingerUpper` | double | Bollinger bovenband |
| `BollingerLower` | double | Bollinger onderband |
| `Atr` | double | Average True Range (14d) |
| `StochRsi` | double | Stochastische RSI (0–100) |
| `Rsi` | double | RSI (14d, dagelijks) |
| `EmaCross` | string | "Bullish" / "Bearish" / "–" |
| `EmaCrossBarsAgo` | int | Bars geleden dat EMA-crossing plaatsvond |
| `BollingerPctB` | double | %B Bollinger (0–100) |
| `Ma50DistPerc` | double | % afstand t.o.v. MA50 |
| `Adx` | double | Average Directional Index |
| `IsSqueeze` | bool | Bollinger Squeeze actief |
| `High52wPerc` | double | % onder 180-daags hoog |
| `LatestSentimentScore` | double | Meest recente geaggregeerde sentimentscore |
| `LatestSignalScore` | double | Meest recente CombinedScore |
| `MarketRegime` | enum | RiskOn / Neutral / RiskOff |
| `[NotMapped] ClosingPrices` | List\<double\> | Slotkoersen vanuit JSON-cache |
| `[NotMapped] Ema` | double | Berekende EMA (runtime) |

#### Asset
Koppeltabel tussen portfolio-account en coin.

| Eigenschap | Type | Omschrijving |
|-----------|------|-------------|
| `Id` | int | PK |
| `Qty` | double | Hoeveelheid |
| `AverageCostPrice` | double | Gewogen gemiddelde kostprijs |
| `RealizedPnL` | double | Gerealiseerd rendement |
| `Coin` | Coin | Navigatie-eigenschap |
| `Account` | Account | Navigatie-eigenschap |
| `Mutations` | ICollection\<Mutation\> | Transactieregels |

#### Transaction / Mutation
`Transaction` is een financiële gebeurtenis (koop, verkoop, storting, etc.).  
`Mutation` is één regel erin (bijv. +0.1 BTC bij een koop).

| Transaction | |
|---|---|
| `Id` | PK |
| `TimeStamp` | Tijdstip |
| `Note` | Omschrijving |
| `SourceId` | Externe sleutel voor deduplicatie (`"MEXC:{tradeId}"`) |
| `Mutations` | ICollection\<Mutation\> |

#### ExchangeOrder
Handelsorder (paper of live).

| Eigenschap | Type | Omschrijving |
|-----------|------|-------------|
| `Id` | int | PK |
| `SignalId` | int? | Koppeling met Signal (optioneel) |
| `Exchange` | ExchangeKind | MEXC / Bybit |
| `Symbol` | string | Handelspaar (bijv. "BTCUSDT") |
| `Side` | OrderSide | Buy / Sell |
| `Type` | OrderType | Market / Limit / StopLimit |
| `MarketType` | MarketType | Spot / Futures / Margin (default Spot) |
| `Leverage` | int | Hefboomfactor 1–100 (default 1) |
| `Qty` | double | Hoeveelheid (gecorrigeerd voor hefboom) |
| `Entry` | double | Instapprijs (absolute USDT) |
| `StopLoss` | double | Stop-loss prijs (absolute USDT, 0 = geen) |
| `TakeProfit` | double | Take-profit 1 prijs (absolute USDT, 0 = geen) |
| `TakeProfit2` | double | Take-profit 2 prijs (absolute USDT, 0 = geen) |
| `Tp1ClosePct` | double | % van positie te sluiten op TP1 (1–100, default 100) |
| `Tp2ClosePct` | double | % van positie te sluiten op TP2 (1–100, default 100) |
| `Status` | OrderStatus | Pending / Filled / Closed / Cancelled |
| `IsPaper` | bool | Paper trade of live |
| `CreatedAt` | DateTime | Aanmaaktijdstip |
| `FilledAt` | DateTime? | Uitvoeringstijdstip |
| `ClosePrice` | double | Sluitingsprijs |
| `ClosedAt` | DateTime? | Sluittijdstip |
| `Notes` | string | Gebruikersnotities |
| `WatchedSetupId` | int? | Koppeling met `WatchedSetup.Id` — setup waaruit dit order is voortgekomen *(v1.31)* |
| `Signal` | Signal? | Navigatie-eigenschap |

#### Signal
Gegenereerd handelssignaal.

| Eigenschap | Type | Omschrijving |
|-----------|------|-------------|
| `Id` | int | PK |
| `CoinId` | int | FK → Coin |
| `NarrativeId` | int? | FK → Narrative (optioneel) |
| `Timeframe` | Timeframe | OneHour / FourHour / OneDay |
| `TaScore` | double | Technische analyse-score (0–100) |
| `SentimentScore` | double | Genormaliseerde sentimentscore (0–100) |
| `MarketRegimeMultiplier` | double | Regime-multiplier (0.8 / 1.0 / 1.2) |
| `CombinedScore` | double | Eindscore (0–100) |
| `Direction` | SignalDirection | Long / Short / Flat |
| `Reasoning` | string | Uitleg in tekst |
| `CreatedAt` | DateTime | Tijdstip aanmaak |
| `Acknowledged` | bool | Door gebruiker bekeken |
| `ActedOn` | bool | Heeft gebruiker trade geopend |

#### SentimentReading
Één sentimentmeting van één bron.

| Eigenschap | Type | Omschrijving |
|-----------|------|-------------|
| `Id` | int | PK |
| `CoinId` | int | FK → Coin |
| `Source` | SentimentSource | Reddit / Rss / CryptoPanic |
| `SentimentScore` | double | Score –1.0 tot +1.0 |
| `Confidence` | double | Betrouwbaarheid meting (0–1) |
| `MentionCount` | int | Aantal vermeldingen |
| `Timestamp` | DateTime | Tijdstip meting |
| `RawSnippet` | string | Ruwe tekst-snippet |

#### BronSource
Configuratie van een sentimentbron.

| Eigenschap | Type | Omschrijving |
|-----------|------|-------------|
| `Id` | int | PK |
| `Type` | SentimentSource | Reddit / Rss / CryptoPanic |
| `Url` | string | Endpoint-URL |
| `Handle` | string | Subreddit-naam of feed-naam |
| `ReliabilityScore` | double | Gewicht in aggregatie (0–1) |
| `IsActive` | bool | Actief/inactief |

#### ExchangeAccount
Versleutelde API-sleutels per exchange.

| Eigenschap | Type | Omschrijving |
|-----------|------|-------------|
| `Id` | int | PK |
| `Exchange` | ExchangeKind | MEXC / Bybit |
| `ApiKeyEncrypted` | string | DPAPI-versleutelde API-key |
| `ApiSecretEncrypted` | string | DPAPI-versleuteld secret (HMAC) |
| `AuthMethod` | string | "HMAC" of "RSA" |
| `PublicKeyPem` | string | PEM public key (niet gevoelig) |
| `IsActive` | bool | Actieve account |
| `Permissions` | string | Exchange-permissies |

#### FearGreedReading
Tijdreeks van Fear & Greed Index-snapshots (marktbreed sentiment).

| Eigenschap | Type | Omschrijving |
|-----------|------|-------------|
| `Id` | int | PK |
| `Value` | int | 0–100 (0 = Extreme Fear, 100 = Extreme Greed) |
| `Classification` | string | "Extreme Fear" / "Fear" / "Neutral" / "Greed" / "Extreme Greed" |
| `Timestamp` | DateTime | UTC tijdstip van meting |

#### WatchedSetup *(v1.30)*
Snapshot van een trade-setup die de gebruiker actief volgt. Opgeslagen in SQLite; status wordt automatisch bijgewerkt door `AutoUpdateStatusesAsync`.

| Eigenschap | Type | Omschrijving |
|-----------|------|-------------|
| `Id` | int | PK |
| `CoinApiId` | string | CoinGecko API-id (bijv. "sui") — geen FK, coin hoeft niet in portfolio te zijn |
| `CoinName` | string | Naam (bijv. "Sui") |
| `CoinSymbol` | string | Ticker (bijv. "SUI") |
| `ImageUri` | string | Pad lokaal coin-logo |
| `Direction` | string | "Long" / "Short" |
| `EntryPrice` | double | Geplande instapprijs |
| `StopLoss` | double | Stop-loss niveau |
| `Target1` | double | Take-profit 1 |
| `Target2` | double | Take-profit 2 (optioneel) |
| `Score` | int | TradabilityScore op moment van aanmaken (0–100) |
| `PatternSummary` | string | Compacte patroonbeschrijving (bijv. "Bull Flag 1D · Bijna Breakout 4H") |
| `Bias1D` | string | Dagelijkse bias ("Bullish" / "Bearish" / "Neutraal") |
| `Bias4H` | string | 4H bias |
| `AddedAt` | DateTime | UTC tijdstip aanmaken |
| `Status` | WatchedSetupStatus | Watching(0) / Won(1) / Lost(2) / Expired(3) / Open(4) |
| `ClosePrice` | double? | Prijs waarbij status bepaald is (TP1/SL-hit of handmatig) |
| `ClosedAt` | DateTime? | UTC tijdstip sluiten |
| `MarketRegimeAtCreation` | string? | BTC marktregime op moment van aanmaken ("RiskOn" / "Neutral" / "RiskOff") *(v1.31)* |
| `Tp2Hit` | bool | True als TP2 automatisch is geraakt *(v1.31)* |
| `LinkedOrderId` | int? | FK naar `ExchangeOrder.Id` — het paper/live-order dat uit deze setup is voortgekomen *(v1.31)* |
| `EntryAt` | DateTime? | UTC tijdstip waarop de entryprijs werd geraakt (instapcandle) *(v1.32)* |
| `[NotMapped] CurrentPrice` | double | Live marktprijs — wordt ingesteld door ViewModel, niet opgeslagen |

**Computed properties (niet opgeslagen):**

| Property | Formule | Opmerking |
|----------|---------|-----------|
| `RiskReward` | `|TP1 − Entry| / |Entry − SL|` | 0 als niveaus ontbreken |
| `PnlPct` | `(ClosePrice − Entry) / Entry × 100` (Long), gespiegeld voor Short | Null als niet gesloten |
| `UnrealisedPnlPct` | `(CurrentPrice − Entry) / Entry × 100` (Long) | Null als status ≠ Open |
| `EntryDistancePct` | `(CurrentPrice − Entry) / Entry × 100` (Long) | Positief = richting van winst; NaN als prijs onbekend |

#### CoinFundamentals *(v1.34)*
Fundamentele analyse per coin (één rij per coin, upsert op `ApiId`). Tabel aangemaakt via
`ApplyPlusSchemaAsync`. Auto-velden komen uit CoinGecko; `Dd*`-velden zijn handmatige due-diligence.

| Eigenschap | Type | Omschrijving |
|-----------|------|-------------|
| `Id` | int | PK |
| `ApiId` / `Symbol` / `Name` | string | Coin-identificatie (`ApiId` uniek) |
| `Categories` | string | CSV van sectoren/categorieën |
| `GenesisDate` | DateTime? | Lanceerdatum (track record) |
| `HomepageUrl` / `WhitepaperUrl` / `GithubUrl` / `TwitterHandle` / `SubredditUrl` | string | Links |
| `Description` | string | Projectomschrijving (EN) |
| `MarketCapRank` | long? | Market-cap rang |
| `MarketCap` / `Fdv` / `TotalVolume` | double | Waardering & 24u-volume (USD) |
| `Ath` / `AthChangePct` / `AthDate` | double / double / DateTime? | All-time high + afstand |
| `Atl` / `AtlChangePct` / `AtlDate` | double / double / DateTime? | All-time low + herstel |
| `CirculatingSupply` / `TotalSupply` / `MaxSupply` | double | Aanbod & verwatering |
| `Tvl` / `TvlCategory` | double / string | On-chain Total Value Locked + categorie (DefiLlama); 0/leeg voor niet-DeFi-coins |
| `GithubStars` / `GithubForks` / `GithubSubscribers` / `CommitCount4Weeks` / `PullRequestsMerged` / `PullRequestContribs` | long | Development-activiteit |
| `TwitterFollowers` / `RedditSubscribers` / `RedditActive48H` / `SentimentUpPct` | long / double | Community |
| `AppSentiment` | double | Eigen app-sentiment (Reddit/RSS, −1..1); voedt de Community-factor *(v1.35)* |
| `ScoreTokenomics` / `ScoreLiquidity` / `ScoreValuation` / `ScoreCommunity` / `ScoreDevelopment` / `ScoreProject` / `ScoreOnChain` | double | Auto-subscores (0–100); `ScoreOnChain` (TVL) weegt alleen mee bij DeFi-coins |
| `DataScore` | double | Samengestelde auto-score (0–100) |
| `DdTeam` / `DdProductMaturity` / `DdAdoption` / `DdRevenue` / `DdUnlocks` | int? | Handmatige DD (0–10, null = niet beoordeeld) |
| `DdNotes` | string | DD-notities |
| `TotalScore` | double | Volledige score (auto + DD) |
| `Verdict` | string | Exceptional … Avoid |
| `Confidence` | double | Onderbouwing van het raamwerk (0–100) |
| `UpdatedAt` | DateTime | Laatste refresh (UTC) |

#### PatternResult *(Models/ — geen DB-entiteit)*
Één gedetecteerd patroon op een bepaald timeframe.

| Eigenschap | Type | Omschrijving |
|-----------|------|-------------|
| `Type` | PatternType | Enum: welk patroon |
| `Category` | PatternCategory | Bullish / Bearish / Neutral / Warning |
| `Timeframe` | string | "1D" / "4H" / "1H" |
| `IsConfirmed` | bool | True = candle gesloten boven/onder niveau |
| `Strength` | int | 0–100 |
| `Description` | string | Eén-zin uitleg |
| `KeyLevel` | double? | Relevant koersniveau (weerstand/steun) |
| `DistancePct` | double? | % afstand van huidig koers tot KeyLevel |
| `[Computed] DisplayName` | string | Nederlandse naam (bijv. "Dubbele Bodem") |

#### PatternCoinAnalysis *(Models/ — geen DB-entiteit)*
Volledige analyse van één coin; resultaat van `IPatternTradingService`.

| Eigenschap | Type | Omschrijving |
|-----------|------|-------------|
| `Coin` | Coin | Portfolio-coin referentie |
| `HasHolding` | bool | True als asset met qty > 0 |
| `TradabilityScore` | int | 0–100 gecombineerde handelsscore |
| `PrimaryDirection` | string | "Long" / "Short" / "Neutraal" |
| `Patterns` | List\<PatternResult\> | Alle gedetecteerde patronen (1D + 4H + 1H) |
| `Setup` | TradeSetupAdvice? | Entry/SL/TP-setup (null als Score < 40) |
| `SupportLevels` | List\<double\> | Gedetecteerde steunniveaus |
| `ResistanceLevels` | List\<double\> | Gedetecteerde weerstandsniveaus |
| `IsNearBreakout` | bool | True als koers binnen 3% van weerstand (Long) of steun (Short) |
| `DailyBias` | string | "Bullish" / "Bearish" / "Neutraal" (1D) |
| `H4Bias` | string | "Bullish" / "Bearish" / "Neutraal" (4H) |
| `DailyRsi` | double | RSI-waarde op dagbasis |
| `H4Rsi` | double | RSI-waarde op 4H-basis |
| `HasData` | bool | False als OHLCV-ophalen volledig mislukte |
| `DataSource` | string | "Binance" / "KuCoin" / "Gate.io" / "MEXC" |
| `AnalyzedAt` | DateTime | Tijdstip analyse |
| `ShareText` | string | Klembord-tekst (Dutch, met disclaimer + hashtags) |
| `[Computed] KeyPatterns` | IEnumerable | Patronen met Strength ≥ 60 |
| `[Computed] ScoreLabel` | string | "Sterke setup" / "Mogelijke setup" / "In de gaten houden" / "Niet interessant" |

#### PatternCoinRow *(ViewModels/ — display model)*
Display-wrapper over `PatternCoinAnalysis` met XAML-vriendelijke properties voor `x:Bind`.

- Computed string properties: `PriceDisplay`, `Change24h`, `MarketCap`, `ScoreText`, `ScoreLabel`, `DailyRsi`, `H4Rsi`, `EntryDisplay`, `StopDisplay`, `Target1Display`, `Target2Display`, `RRDisplay`, `ConfidenceText`, `EntryNote`, `BreakoutIndicator`
- Computed `SolidColorBrush` properties: `Change24hBrush`, `ScoreBrush`, `DirectionBrush`, `DailyBiasBrush`, `H4BiasBrush`
- Computed `Visibility` properties: `HasDataVis`, `HasSetupVis`, `ShowNoDataVis`
- `PatternBadges` : IReadOnlyList\<PatternBadge\> — max 6, Strength ≥ 55, bullish voor bearish
- `ReasoningBullets` : IReadOnlyList\<string\> — max 4 bullets uit Setup.Reasoning

#### PatternBadge *(ViewModels/ — display model)*
Chip-badge voor één gedetecteerd patroon.

| Eigenschap | Type | Omschrijving |
|-----------|------|-------------|
| `Label` | string | DisplayName van het patroon |
| `Timeframe` | string | "1D" / "4H" / "1H" |
| `Background` | SolidColorBrush | Groen (Bullish) / Rood (Bearish) / Oranje (Warning) / Grijs (Neutraal) |
| `ToolTip` | string | PatternResult.Description |
| `Confirmed` | bool | PatternResult.IsConfirmed |

---

## 6. Berekeningen

### 6.1 P&L en R-multiple (Trade Journal)

#### 6.1.1 Ongerealiseerde P&L (open positie)

```
// Long (Buy)
P&L_USDT = (huidigeKoers - instapKoers) × hoeveelheid

// Short (Sell)
P&L_USDT = (instapKoers - huidigeKoers) × hoeveelheid

P&L_% = P&L_USDT / (instapKoers × hoeveelheid) × 100
```

#### 6.1.2 Gerealiseerde P&L (gesloten positie)

Identieke formule maar met `sluitingsprijs` in plaats van `huidigeKoers`.  
Waarde wordt opgeslagen in `ExchangeOrder.ClosePrice`.

#### 6.1.3 R-multiple

De R-multiple drukt het rendement uit als veelvoud van het initiële risico:

```
risico_per_stuk = |instapKoers - stopLoss|
initieel_risico = risico_per_stuk × hoeveelheid

R_multiple = P&L_USDT / initieel_risico
```

Voorbeeld: instap $100, SL $95, koers sluit op $110, qty = 10  
→ risico = $5 × 10 = $50  
→ P&L = ($110 − $100) × 10 = $100  
→ R = $100 / $50 = **+2R**

#### 6.1.4 Automatisch sluiten bij TP/SL bereikt (`AutoCloseTriggeredAsync`)

Bij elke `LoadRowsAsync()`-aanroep wordt `ITradeService.AutoCloseTriggeredAsync(priceMap)` uitgevoerd. Dit controleert alle open papierposities (`Status = Filled, IsPaper = true`) tegen de meegegeven koersmap.

**Volgorde van controle (prioriteit):**

1. **Stop Loss** — hoogste prioriteit; als prijs SL raakt, wint dit altijd
2. **Take Profit 2** — controle vóór TP1; als beide geraakt zijn, sluit op TP2 (beter resultaat)
3. **Take Profit 1**

**Triggerlogica per richting:**

| Niveau | Long (Buy) | Short (Sell) |
|--------|-----------|--------------|
| SL geraakt | `koers ≤ SL` | `koers ≥ SL` |
| TP1 geraakt | `koers ≥ TP1` | `koers ≤ TP1` |
| TP2 geraakt | `koers ≥ TP2` | `koers ≤ TP2` |

**Bij trigger:**
- `order.Status = Closed`
- `order.ClosePrice = exacte TP/SL-prijs` (niet de huidige marktkoers)
- `order.Notes` wordt voorafgegaan door `[Auto] 🎯 TP1 geraakt @ …` of `[Auto] 🛑 SL geraakt @ …`
- `SaveChangesAsync()` één keer voor alle gesloten orders
- Statusbalk toont: `⚡ Auto-gesloten: {symbool} {reden}`

**Veiligheidscontrole in EditTradeDialog:**  
Een nieuw SL-niveau wordt geblokkeerd als het de huidige koers al heeft bereikt (en dus direct auto-close zou triggeren). Zie §4.7.2.

---

### 6.2 Statistieken

Alle berekeningen gelden alleen voor **gesloten** orders (`Status == Closed && ClosePrice > 0 && Entry > 0`).

#### Filteren
```csharp
// Periode-filter
if (SelectedPeriod == "Aangepast")
    query = query.Where(o => o.CreatedAt >= customStart.Date && o.CreatedAt < customEnd.Date.AddDays(1));
else if (cutoff.HasValue)
    query = query.Where(o => o.CreatedAt >= cutoff);

// Type-filter
if (SelectedTradeKind == "Live")  query = query.Where(o => !o.IsPaper);
if (SelectedTradeKind == "Paper") query = query.Where(o => o.IsPaper);
```

#### P&L per order
```csharp
static double Pnl(ExchangeOrder o) =>
    o.Side == OrderSide.Buy
        ? Math.Round((o.ClosePrice - o.Entry) * o.Qty, 2)
        : Math.Round((o.Entry - o.ClosePrice) * o.Qty, 2);
```

#### Samenvattingskaarten
```
TotaleP&L    = gesloten.Sum(Pnl)
WinRate      = wins.Count / gesloten.Count × 100    [%]
AvgWin       = wins.Average(Pnl)
AvgLoss      = losses.Average(Pnl)
OpenTrades   = orders.Count(o => Status == Filled)
TotaalVolume = gesloten.Sum(o => o.Entry × o.Qty)
```

#### Top-symbols tabel
```
perSymbool = gesloten.GroupBy(o => o.Symbol)
  .Select(g => {
      cnt   = g.Count()
      pnl   = g.Sum(Pnl)
      rate  = g.Count(Pnl > 0) / cnt × 100
      → SymbolStatRow(symbool, cnt, pnl, rate)
  })
  .OrderByDescending(r => r.TotalPnl)

top    = eerste 5
bottom = laatste 5
tabel  = top ∪ bottom (gededupliceerd op Symbol)
```

---

### 6.3 Technische indicatoren

Alle indicatoren worden berekend via `Skender.Stock.Indicators` op de `ClosingPrices`-lijst van een `Coin`, gevuld vanuit de lokale JSON-cache (`MarketChart_{ApiId}.json`).

#### RSI (Relative Strength Index)
- Periode: 14 dagen (dagelijkse slotkoersen)
- Bereik: 0–100
- Interpretatie: < 30 = oversold (groen), > 70 = overbought (rood)

#### MACD (Moving Average Convergence Divergence)
- Parameters: EMA12, EMA26, signal EMA9
- Opgeslagen als `Coin.Macd` (MACD-lijn) en `Coin.MacdSignal`
- Positief (MACD > signal): opwaarts momentum

#### Bollinger Bands
- Parameters: 20-perioden, 2× standaarddeviatie
- Opgeslagen als `Coin.BollingerUpper` en `Coin.BollingerLower`
- **%B** = (koers − lowerBand) / (upperBand − lowerBand) × 100

#### ATR (Average True Range)
- Periode: 14 dagen
- Absolute maat voor volatiliteit
- Gebruikt als basis voor SL/TP-berekening in Trade Advies

#### StochRSI
- RSI (14) → Stochastisch (14, smoothing 3/3)
- Bereik: 0–100
- < 20 = oversold, > 80 = overbought

#### EMA Cross (9/21)
- Berekent EMA9 en EMA21 over dagelijkse slotkoersen
- `EmaCross = "Bullish"` als EMA9 > EMA21 en deze positie recent veranderd is
- `EmaCrossBarsAgo` = aantal bars geleden dat de crossing plaatsvond

#### MA50% (distance)
- 50-daags voortschrijdend gemiddelde
- `Ma50DistPerc = (koers − MA50) / MA50 × 100`
- Positief = boven MA50 (groen), negatief = eronder (rood)

#### ADX (Average Directional Index)
- Periode: 14 dagen
- > 25 = trendige markt (oranje indicator)
- < 20 = zijwaartse markt

#### Bollinger Squeeze
- Vergelijkt bandbreedte Bollinger met Keltner-kanaal
- `IsSqueeze = true` als Bollinger volledig binnen Keltner valt
- Squeeze indiceert ophanden zijnde volatiliteitsuitbraak

#### 52-weeks hoog %
- Gebruikt 180 dagelijkse slotkoersen (≈ 6 maanden)
- `High52wPerc = (max180 − koers) / max180 × 100`
- Hoge waarde = ver van hoog (mogelijke koopkans)

---

### 6.4 Signaal-engine (CombinedScore)

#### 6.4.1 TaScore berekening (0–100)

De TaScore wordt berekend door `IIndicatorService.CalculateTaScoreAsync()` en combineert meerdere indicatoren gewogen tot één score:

```
TaScore = gewogen gemiddelde van:
  - RSI component    (oversold → bullish bijdrage)
  - MACD component   (crossover positie)
  - EMA component    (bullish/bearish cross + recency)
  - Bollinger %B     (positie in band)
  - ADX component    (trendsterkte als versterker)
  - StochRSI         (momentum component)
  
Genormaliseerd naar 0–100
```

#### 6.4.2 Sentiment normalisatie

SentimentReadings worden geaggregeerd over een tijdvenster:
```
latestSentiment ∈ [–1.0, +1.0]
sentimentNorm   = (latestSentiment + 1.0) × 50.0   → [0, 100]
```

#### 6.4.3 Market Regime score

```
RiskOn   → regimeScore = 75
Neutral  → regimeScore = 50  
RiskOff  → regimeScore = 25
```

#### 6.4.4 CombinedScore formule

```
raw = (TaScore × 0.60) + (sentimentNorm × 0.30) + (regimeScore × 0.10)
raw = Clamp(raw, 0, 100)

// Regime-multiplier past de spreiding aan
multiplier = 1.2 (RiskOn) | 1.0 (Neutral) | 0.8 (RiskOff)
combined   = 50.0 + ((raw − 50.0) × multiplier)
combined   = Clamp(combined, 0, 100)
```

**Gewichtverdeling in de praktijk:**
- TA heeft het grootste gewicht (60%)
- Sentiment geeft richting (30%)
- Marktregime stuurt de spreiding, niet het gemiddelde (multiplier)

#### 6.4.5 Richting bepaling

```
combined ≥ 65  → Long
combined ≤ 35  → Short
anders         → Flat
```

Drempel aanpasbaar via Instellingen (slider 50–85, standaard 60).

---

### 6.5 Trade Advies berekeningen

#### 6.5.1 OHLCV-data ophalen

| Timeframe | Bron | Bars | Fallback |
|-----------|------|------|---------|
| Weekly | Binance `/klines?interval=1w` | 104 | KuCoin → lokale cache |
| Daily | Binance `/klines?interval=1d` | 300 | KuCoin → lokale cache |
| 4H | Binance `/klines?interval=4h` | 500 | KuCoin → lokale cache |
| 1H | Binance `/klines?interval=1h` | 200 | KuCoin → lokale cache |

Binance API vereist geen API-key voor publieke OHLCV-data.

#### 6.5.2 Sleutelniveaus (pivot-detectie)

```
lookback = 5 candles

Voor elke bar i (lookback .. n-lookback):
  isPivotHigh = high[i] > max(high[i-5..i-1]) AND high[i] > max(high[i+1..i+5])
  isPivotLow  = low[i]  < min(low[i-5..i-1])  AND low[i]  < min(low[i+1..i+5])

Clustering (samenvoegen nabije niveaus):
  drempel = 1.5% van huidigeKoers
  Als twee pivots ≤ drempel uit elkaar → samenvoegen tot gemiddelde

Resultaat: max 4 resistance + max 4 support niveaus
```

#### 6.5.3 Entry-prijs bepaling

```
optie A (market): entry = huidigeKoers
optie B (pullback): entry = EMA21 van dagelijkse slotkoersen
                   → kies de lagere (Long) of hogere (Short)
```

#### 6.5.4 Stop-loss berekening

```
atr = ATR(14) op dagelijkse data

Long:  stopLoss = entry − (1.5 × atr)
Short: stopLoss = entry + (1.5 × atr)
```

#### 6.5.5 Take-profit berekening

```
Long:
  tp1 = entry + (2.0 × atr)
  tp2 = entry + (3.5 × atr)      ← of dichtstbijzijnde resistance (≤ 20% boven entry)

Short:
  tp1 = entry − (2.0 × atr)
  tp2 = entry − (3.5 × atr)      ← of dichtstbijzijnde support (≤ 20% onder entry)
```

#### 6.5.6 Risk/Reward ratio

```
stopLossPct  = |entry − stopLoss| / entry × 100
target1Pct   = |entry − tp1|      / entry × 100
target2Pct   = |entry − tp2|      / entry × 100

R/R_1 = target1Pct / stopLossPct
R/R_2 = target2Pct / stopLossPct
```

#### 6.5.7 Confidence indicator

```
Low    → R/R_1 < 1.5
Medium → 1.5 ≤ R/R_1 < 2.5
High   → R/R_1 ≥ 2.5
```

---

### 6.6 Market Regime

De marktregime wordt bepaald op basis van BTC.

```
Inputs:
  - BTC trendrichting (berekend via MA50 en MA200 van dagelijkse koersen)
  - BTC dominantie (BTC.d percentage)

Regels (top-6 worden weergegeven op Dashboard):
  RiskOn   = BTC boven MA50 en MA200 AND dominantie stijgend
  RiskOff  = BTC onder MA50 of MA200 AND dominantie dalend of hoog
  Neutral  = overige situaties

Multiplier: zie §6.4.4
```

---

### 6.7 Pattern Trading — TradabilityScore berekening

De TradabilityScore (0–100) wordt berekend door `PatternDetectionService.CalculateTradabilityScore()` op basis van gedetecteerde patronen.

#### 6.7.1 Puntenschema (bullish / bearish)

Per gedetecteerd patroon worden punten opgeteld in een bull- of bear-emmer:

| Patroon | Bull-punten | Bear-punten |
|---------|-------------|-------------|
| RSI Oversold | +8 | — |
| RSI Overbought | — | +8 |
| MACD Bullish Cross | +10 | — |
| MACD Bearish Cross | — | +10 |
| EMA Bullish Cross | +10 | — |
| EMA Bearish Cross | — | +10 |
| Prijs boven EMA50 | +5 | — |
| Prijs onder EMA50 | — | +5 |
| Bollinger Squeeze | +3 | +3 |
| Trending Market (ADX ≥ 25) | +3 | +3 |
| Volume Spike | +6 | +6 |
| Uptrend | +8 | — |
| Downtrend | — | +8 |
| Bull Flag | +9 | — |
| Bear Flag | — | +9 |
| Double Bottom | +10 | — |
| Double Top | — | +10 |
| Ascending Triangle | +7 | — |
| Descending Triangle | — | +7 |
| Symmetrical Triangle | +4 | +4 |
| Support Bounce | +7 | — |
| Resistance Rejection | — | +7 |
| Breakout Above Resistance | +10 | — |
| Breakdown Below Support | — | +10 |
| Potential Breakout | +5 | — |
| Consolidation | +2 | +2 |

*Dubbele detectie over meerdere timeframes telt cumulatief mee.*

#### 6.7.2 Score normalisatie

```
maxMogelijk = 91   // empirisch vastgesteld; kan overschreden worden
rawScore    = max(bullPoints, bearPoints)
score       = min(100, round(rawScore / maxMogelijk × 100))
```

#### 6.7.3 Richtingsbepaling

```
diff = bullPoints − bearPoints
direction = diff > +5 ? "Long"
          : diff < −5 ? "Short"
          :             "Neutraal"
```

#### 6.7.4 Score-labels

| Score | Label |
|-------|-------|
| ≥ 80 | Sterke setup |
| ≥ 60 | Mogelijke setup |
| ≥ 40 | In de gaten houden |
| < 40 | Niet interessant |

#### 6.7.5 Setup-advies berekening (als Score ≥ 40 en richting ≠ Neutraal)

```
Long:
  entry    = (koers > EMA21 × 1.04) ? EMA21 : koers     // pullback of marktprijs
  stopLoss = entry − (1.5 × ATR_1D)
  tp1      = entry + (2.0 × ATR_1D)
  tp2      = min(entry + 3.5 × ATR_1D, dichtstbijzijnde resistance ≤ 20% boven entry)

Short:
  entry    = (koers < EMA21 × 0.96) ? EMA21 : koers
  stopLoss = entry + (1.5 × ATR_1D)
  tp1      = entry − (2.0 × ATR_1D)
  tp2      = max(entry − 3.5 × ATR_1D, dichtstbijzijnde support ≤ 20% onder entry)

R/R_1 = |entry − tp1| / |entry − stopLoss|

Confidence:
  "Hoog"   → score ≥ 80 AND ADX ≥ 25
  "Middel" → score ≥ 60
  "Laag"   → anders
```

#### 6.7.6 Pattern-detectie methodes

**Level 1 — Indicator-gebaseerd** (uit reeds berekende `TimeframeAnalysis`):
- RSI oversold (< 30) / overbought (> 70)
- MACD cross boven/onder signaallijn
- EMA9/EMA21 cross (bullish/bearish, via EmaCrossState string)
- Prijs boven/onder EMA50
- Bollinger Squeeze (Bollinger volledig binnen Keltner)
- Trending Market (ADX ≥ 25)

**Level 2 — OHLCV swing-point analyse** (op ruwe bar-data):
- Volume Spike: laatste volume > 1.8× gemiddelde van 20 vorige bars
- Uptrend / Downtrend: 3+ opeenvolgende HH+HL of LH+LL swing-punten
- Double Bottom / Top: twee lows/highs binnen 3%, ≥ 8 bars uiteen, confirmed als recovery > 4%. **Bugfix v1.38:** de dubbele-bodem-diepte meet de **opleving** tússen de twee bodems (≥ 5% boven de hoogste bodem); Adam & Eve idem.
- **Dominante-pieken-eis *(v1.38)*:** de twee toppen/bodems moeten de dominante extremen zijn. **Dubbele top** wordt afgewezen als er tussen de toppen een **hogere high** zit (`max(High) tussen > top × 1.005`) — dit voorkwam de OP-fout waar een geslaagde uitbraak (Cup & Handle) tussen twee gelijke highs ten onrechte als dubbele top werd gelezen. Spiegelbeeldig voor **dubbele bodem** (geen **lagere low** ertussen) en **(Inv.) H&S** (het hoofd moet de hoogste/laagste piek zijn). De **neklijn** = het werkelijke dal/de piek tussen de extremen (de oude `Math.Min/Max(…, currentPrice)` is verwijderd, zodat de neklijn niet meer met de live koers meeschuift).
- Bull Flag / Bear Flag *(herzien v1.38)*: pool op de **wicks** (≥ 8%) met geverifieerde richting (high ná low voor bull, low ná high voor bear); flag-range < 6% (wicks); flag moet **consolideren** — `LinearSlope` van de flag-closes mag niet sterk mee-trenden (> +0.004 bull / < −0.004 bear → afgewezen); retrace < 50% van de pool. Pool wordt als diagonale trendlijn getekend; het vlaggetje als een begrensd vak (korte boven-/onderlijn over alleen de consolidatie-candles) + een volle-breedte breakout/breakdown-trigger. **Koersdoel *(v1.38)*:** pool-lengte (`poleHigh − poleLow`) geprojecteerd vanaf de vlag-top (bull → `flagHigh + lengte`) resp. de vlag-bodem (bear → `flagLow − lengte`), getekend als 'Doel'-lijn.
- Ascending / Descending / Symmetrical Triangle: bar-index regressie op swinghighs en swinglows
- Consolidation: koersbereik < 8% over laatste 15 bars
- Support Bounce / Resistance Rejection: koers stuitert op steun/weerstand
- Breakout / Breakdown: confirmed (0.5–4% voorbij niveau) of potentieel (−3% tot +0.5%)

**Swing point detectie *(herzien v1.38)*:**
```
lookback = 5
pivotHigh[i] = bars[i].High is strikt hoogste High in [i−5 .. i+5]   (wick, niet body)
pivotLow[i]  = bars[i].Low  is strikt laagste  Low  in [i−5 .. i+5]
significantie = pivot moet de dichtstbijzijnde buur met ≥ 0.40 × ATR(14) overtreffen
```
- Pivots op de **wicks** (`bars[i].High/Low`) i.p.v. de body, zodat markers/lijnen op de
  zichtbare toppen/bodems landen. ATR-relatieve significantie schaalt mee met de volatiliteit
  van de coin (vervangt de vaste 0.5%).

**Drie-staten-bevestiging *(v1.38, spec-review)*:** `PatternStatus` (Forming/Tentative/Confirmed) wordt
centraal in `DetectFromBars` bepaald (`ApplyStatus` → `EvalStatus`) voor alle breakout-patronen:
**Bevestigd** = slotkoers (`bars[^1].Close`) voorbij het sleutelniveau + marge (driehoek/kanaal ≥1%,
neklijn/wedge/flag ≥0,5%, breakout/breakdown ≥1,5%); **Voorlopig** = live koers erbuiten; anders **In formatie**.
`PatternResult.IsConfirmed` is hiervan afgeleid (fixt het F7-probleem: bevestiging op slotkoers i.p.v. live koers).
`StatusLabel` wordt getoond in de badge-tooltip, de "+N"-overflow-tooltip en het grafieklabel.

**Detectie-kwaliteitsgates *(v1.38, spec-review)*:** kanaal/driehoek/wedge vereisen náást de R²-fit nu ook
**≥2 echte aanrakingen** per lijn (`CountTouches`, swing binnen 1% van de regressielijn) en een
**ATR-grootteband** (gap `≥0,5×ATR`, `≤15×ATR`) bovenop de prijs-%-band. De **staleness-check**
(`IsPatternStale`, >8% voorbij sleutelniveau) draait nu breed: double bottom/top, H&S, Inv. H&S, wedge,
kanaal, asc/desc-driehoek, bull/bear-flag en cup&handle. Conform `PATTERN_HANDBOOK.md` v2.1 (§3.1, §3.3, §3.2/F6).

**Trendlijn-fit & valse-patroon-filter *(v1.38)*:** kanaal-, driehoek- én wedge-detectie gebruiken
`LinearRegressionByBarIdx` (helling = prijs/bar over de echte bar-index) en tekenen de **geprojecteerde
regressielijn** naar de vensterranden — niet langer een rechte tussen het eerste en laatste swing-punt.
Een **R²-fitdrempel** (`RSquaredByBarIdx`: kanaal/driehoek ≥ 0.70, wedge ≥ 0.55) verwerpt patronen
waarvan de swings niet daadwerkelijk op de trendlijn liggen. De richting wordt bepaald via de totale
fractionele beweging van elke lijn over het venster (kanaal ≥ 3%; driehoek: vlak < 2%, trend ≥ 3%).
De grafiek (`CoinChartWindow`) tekent nog maar **één** patroon per timeframe: het aangeklikte, anders
het sterkste (confirmed, dan hoogste Strength).

**Begrensde patroon-annotaties *(v1.38)*:** alle patronen tekenen hun structuur als **begrensde
trendlijn-segmenten** over de bijbehorende candles i.p.v. volle-breedte `HLines`. Neklijnen (Double
Bottom/Top, Head & Shoulders, Inverse H&S, Adam & Eve) lopen van het eerste structuurpunt tot de huidige
candle; de cup-rand (Cup & Handle) over de cup; de flag-grenzen als klein vak over de consolidatie. Alleen
de bull/bear-flag houden één volle-breedte `HLine` als breakout/breakdown-trigger.

---

### 6.8 Setup Tracker berekeningen *(v1.31)*

#### 6.8.1 Auto-status evaluatie (`AutoUpdateStatusesAsync`)

Wordt aangeroepen bij elke `RefreshAsync` in `SetupTrackerViewModel`. Evalueert alle setups met status `Watching` of `Open`.

```
Voor elke setup:
  isLong = (Direction == "Long")

  hitTP2 = Target2 > 0 AND (isLong ? price >= Target2 : price <= Target2)
  hitTP1 = Target1 > 0 AND (isLong ? price >= Target1 : price <= Target1)
  hitSL  = StopLoss > 0 AND (isLong ? price <= StopLoss : price >= StopLoss)
  hitEntry = isLong ? price <= EntryPrice : price >= EntryPrice

  if hitTP2 AND NOT Tp2Hit  → Status = Won, ClosePrice = price, ClosedAt = now, Tp2Hit = true
  else if hitTP1            → Status = Won, ClosePrice = price, ClosedAt = now
  else if hitSL             → Status = Lost, ClosePrice = price, ClosedAt = now
  else if hitEntry AND Status == Watching → Status = Open
```

*TP2 controle gaat vóór TP1: als de koers TP2 bereikt, impliceert dat TP1 ook is gepasseerd. `Tp2Hit = true` wordt bewaard voor statistieken.*

#### 6.8.2 Win Rate berekening

```
gesloten = setups met Status == Won OR Status == Lost
WinRatePct = gesloten.Count(s => s.Status == Won) / gesloten.Count × 100

(doel: > 50%)
```

#### 6.8.3 P&L % (gesloten setup)

```csharp
// Long
PnlPct = (ClosePrice - EntryPrice) / EntryPrice * 100

// Short
PnlPct = (EntryPrice - ClosePrice) / EntryPrice * 100
```

#### 6.8.4 Ongerealiseerde P&L % (Open setup)

```csharp
// Long
UnrealisedPnlPct = (CurrentPrice - EntryPrice) / EntryPrice * 100

// Short
UnrealisedPnlPct = (EntryPrice - CurrentPrice) / EntryPrice * 100

// Null als Status ≠ Open of CurrentPrice ≤ 0
```

#### 6.8.5 Entry-afstand % (Watching setup)

```csharp
// Long  (positief = koers boven entry = in richting van winst)
EntryDistancePct = (CurrentPrice - EntryPrice) / EntryPrice * 100

// Short (positief = koers onder entry = in richting van winst)
EntryDistancePct = (EntryPrice - CurrentPrice) / EntryPrice * 100

// double.NaN als CurrentPrice ≤ 0
```

### 6.9 Setup Strategie statistieken *(v1.31)*

Berekend in `StatisticsViewModel.LoadSetupStatsAsync()` op basis van gesloten `WatchedSetup`-records.

| Metriek | Formule |
|---------|---------|
| **Win Rate TP1** | `Won / (Won + Lost) × 100` |
| **Win Rate TP2** | `Tp2Hit / Won × 100` (percentage gewonnen setups dat ook TP2 haalde) |
| **Profit Factor** | `Σ winst-PnlPct / |Σ verlies-PnlPct|` — groter dan 1 is winstgevend |
| **Expectancy** | `(WinRate × gem.winst%) − (LossRate × gem.|verlies|%)` |
| **Gem. P&L** | `Σ PnlPct / gesloten.Count` |
| **Gem. houdtijd** | `Avg(ClosedAt − AddedAt)` in uren of dagen |

**Breakdown-tabellen** worden gegenereerd via `BuildBreakdown()` op drie dimensies:
- Per richting: "Long" / "Short"
- Per score-klasse: `< 50` / `50–64` / `65–79` / `80+`
- Per marktregime: "RiskOn" / "Neutral" / "RiskOff" / "Onbekend"

De periodefilter van het Trade Journal-tabblad (SelectedPeriod) geldt ook voor de setup statistieken.

---

## 7. Externe integraties

### 7.1 CoinGecko API

| | |
|---|---|
| **Endpoint** | `https://api.coingecko.com/api/v3/` |
| **Authenticatie** | Geen (gratis tier) |
| **API-key variabele** | `AppConstants.CoinGeckoApiKey` (leeg) |
| **Gebruik** | Koersen, marktdata, coin-metadata, logo's |
| **Rate limit** | ±30 calls/min (gratis tier) |
| **Kritisch pad** | `PriceUpdateService` — loopt als achtergrondtaak |

### 7.2 Binance REST API (OHLCV)

| | |
|---|---|
| **Endpoint** | `https://api.binance.com/api/v3/klines` |
| **Authenticatie** | Geen (publieke endpoint) |
| **Gebruik** | OHLCV-kaarsen voor Trade Advies |
| **Symboolnotatie** | `{SYMBOL}USDT` (bijv. `BTCUSDT`) |
| **Fallback** | KuCoin → lokale JSON-cache |

### 7.3 KuCoin REST API (OHLCV — fallback)

| | |
|---|---|
| **Endpoint** | `https://api.kucoin.com/api/v1/market/candles` |
| **Authenticatie** | Geen (publieke endpoint) |
| **Gebruik** | Fallback als Binance geen data heeft |
| **Symboolnotatie** | `{SYMBOL}-USDT` (bijv. `BTC-USDT`) |

### 7.4 Binance Private API (live trading — gepland Sprint 2)

| | |
|---|---|
| **Authenticatie** | HMAC-SHA256 of RSA |
| **API-sleutels** | Versleuteld via DPAPI, opgeslagen in `ExchangeAccount` |
| **Huidig gebruik** | Balans-verificatie, orderimport (MEXC) |

### 7.5 MEXC REST API

| | |
|---|---|
| **Gebruik** | Handelshistorie synchroniseren |
| **Deduplicatie** | Via `Transaction.SourceId = "MEXC:{tradeId}"` |

### 7.6 Bybit REST API

| | |
|---|---|
| **Gebruik** | Balans-verificatie en (toekomstig) live orders |
| **Regio** | EU-endpoint instelbaar via `Settings.BybitIsEu` |

### 7.7 alternative.me Fear & Greed API

| | |
|---|---|
| **Endpoint** | `https://api.alternative.me/fng/?limit=1` |
| **Authenticatie** | Geen (gratis, geen API-sleutel) |
| **Gebruik** | Huidige Fear & Greed Index (0–100) ophalen voor dashboard-widget |
| **Frequentie** | Bij elke dashboard-verversing; cache van 60 minuten (geen herhaalverzoek als lezing niet ouder dan 60 min) |
| **Opslag** | `FearGreedReadings`-tabel (SQLite) · service `IFearGreedService` |

### 7.8 Reddit JSON API

| | |
|---|---|
| **Endpoint** | `https://www.reddit.com/r/{subreddit}/new.json` |
| **Authenticatie** | Geen (publieke JSON-feed) |
| **Actieve subreddits** | r/CryptoCurrency · r/Altstreetbets · r/Bitcoin · r/ethereum · r/CryptoMarkets |
| **Gebruik** | Sentimentcollectie via NLP |

### 7.9 RSS-nieuwsfeeds

| Bron | Feed URL |
|------|----------|
| CoinDesk | `https://www.coindesk.com/arc/outboundfeeds/rss/` |
| Cointelegraph | `https://cointelegraph.com/rss` |
| Decrypt | `https://decrypt.co/feed` |
| The Block | `https://www.theblock.co/rss.xml` |
| CryptoSlate | `https://cryptoslate.com/feed/` |

### 7.10 CryptoPanic API

| | |
|---|---|
| **Endpoint** | `https://cryptopanic.com/api/free/v1/posts/` |
| **Authenticatie** | Geen (gratis publieke API) |
| **Gebruik** | 50 meest recente nieuwsartikelen met bullish/bearish labels |
| **Verrijking** | Valutacodes worden toegevoegd aan de tekst voor coin-matching |

### 7.11 Telegram Bot API

| | |
|---|---|
| **Endpoint** | `https://api.telegram.org/bot{token}/sendMessage` |
| **Configuratie** | Bot Token + Chat ID in Instellingen |
| **Gebruik** | Push-notificaties bij signalen boven drempelwaarde |
| **Richting** | Uitsluitend uitgaand (de app leest geen Telegram-berichten) |

### 7.12 Binance Spot Order Book *(v1.33)*

| | |
|---|---|
| **Endpoint** | `https://api.binance.com/api/v3/depth?symbol={SYMBOL}USDT&limit=20` |
| **Authenticatie** | Geen (publiek) |
| **Gebruik** | Liquiditeitsfactor (F6): bid-ask spread + orderboekdiepte in USDT |
| **Service** | `OrderBookService` · `TtlCache` 60s |

### 7.13 Binance Futures API *(v1.33)*

| | |
|---|---|
| **Endpoint** | `https://fapi.binance.com/fapi/v1/fundingRate`, `/fapi/v1/openInterest`, `/futures/data/globalLongShortAccountRatio` |
| **Authenticatie** | Geen (publiek) |
| **Gebruik** | Positioneringsfactor (F7): funding rate, open interest, long/short-ratio |
| **Service** | `BinanceFuturesDataService` · `TtlCache` 5 min · spot-only coins → `IsAvailable = false` |

### 7.14 CoinGecko Global *(v1.33)*

| | |
|---|---|
| **Endpoint** | `https://api.coingecko.com/api/v3/global` |
| **Gebruik** | BTC-dominantie voor het marktregime |
| **Service** | `GlobalMarketDataService` · cache 5 min |

---

## 8. Achtergrondservices

### 8.1 SentimentService (in-process)

Loopt als timer in het hoofdproces.

```
Interval : 15 minuten
Trigger  : automatisch + handmatig via Bronnen-pagina

Cyclus:
  1. Haal actieve BronSources op uit DB (IsActive = true)
  2. Per bron-type:
     - Reddit  : fetch /r/{subreddit}/new.json, parse posts + comments
     - RSS     : fetch feed, parse artikelen
     - CryptoPanic: fetch posts, extraheer valutacodes
  3. In-memory batch coin-matching (symbool/naam → CoinId)
  4. NLP-sentimentanalyse per tekst-snippet → score –1.0 tot +1.0
  5. Sla SentimentReading op per gematchte coin
  6. Update Coin.LatestSentimentScore (gewogen gemiddelde laatste 24u)
  7. Raise StateChanged event → UI bijwerken

Status-properties voor UI:
  IsCollecting : bool
  LastRunAt    : DateTime?
  LastRunStatus: string
```

### 8.2 MarketChartsUpdateService (externe exe)

Aparte console-executable, gerund via Windows Task Scheduler.

```
Bestand  : MarketChartsUpdateService.exe
Uitvoer  : %LOCALAPPDATA%\CryptoPortfolioTrackerPlus\MarketCharts\MarketChart_{ApiId}.json
Inhoud   : array van dagelijkse OHLCV-bars (tot 365 dagen)
Bron     : CoinGecko Market Chart API
Interval : configureerbaar via taakplanner (standaard: dagelijks)
Registratie: via RegisterScheduledTask.ps1 + ScheduledTaskService
```

### 8.3 PriceUpdateService

Loopt als achtergrond-timer in het hoofdproces.

```
Interval : instelbaar (standaard: elke minuut)
Actie    : haalt actuele koersen op van CoinGecko
           schrijft naar UpdateContext (aparte thread-context)
           triggert UI-update via IMessenger
```

---

## 9. Configuratie en opslag

### 9.1 Settings-object (singleton)

Het `Settings`-object is de centrale configuratieklasse, opgeslagen in `prefs.xml` via `PreferencesService`.

| Eigenschap | Omschrijving |
|-----------|-------------|
| `AppTheme` | Licht / Donker / Systeem |
| `FontSize` | Small / Normal / Large |
| `AppCultureLanguage` | "nl" of "en" |
| `NumberFormatIndex` | 0 = NL-notatie, 1 = EN-notatie |
| `IsCheckForUpdate` | Automatisch op updates controleren |
| `SignalThreshold` | CombinedScore-drempel (50–85) |
| `IsPaperTrading` | Paper trading modus actief |
| `TelegramBotToken` | Telegram Bot Token |
| `TelegramChatId` | Telegram Chat ID |
| `MaxRiskPerTrade` | Max % portfolio per trade |
| `MaxOpenPositions` | Max aantal gelijktijdige posities |
| `DailyLossLimit` | Dagelijkse verliesgrens in USDT |
| `KillSwitchEnabled` | Kill-switch actief bij limietoverschrijding |
| `BybitIsEu` | Gebruik EU-endpoint voor Bybit |

### 9.2 Portfoliosysteem

Meerdere portfolio's zijn mogelijk via `portfolios.json`:
```json
[
  { "guid": "f52ee1a8-...", "name": "Hoofd", "path": "sqlCPT.db" },
  { "guid": "08c1ac97-...", "name": "Duress", "path": "sqlCPT_duress.db" }
]
```

De "Duress"-portfolio opent bij invul van het noodwachtwoord (plausible deniability).

### 9.3 Backup-systeem

```
Bestandsnaam : RestorePoint_{timestamp}.cpt
Formaat      : ZIP-archief met sqlCPT.db + prefs.xml
Locatie      : %LOCALAPPDATA%\CryptoPortfolioTrackerPlus\Backup\
Triggers     : handmatig vanuit instellingen, automatisch bij migratie
```

---

## 10. Belasting-module

### 10.1 Architectuurprincipe

De belasting-module is gebouwd op een country-agnostisch patroon. Elk land implementeert `ITaxCalculator`.

```
ITaxCalculator (interface)
  ├── NetherlandsTaxCalculator  ← geïmplementeerd
  ├── GermanyTaxCalculator      ← toekomstig
  ├── BelgiumTaxCalculator      ← toekomstig
  └── UnitedKingdomTaxCalculator← toekomstig
```

Een nieuw land toevoegen:
1. Waarde toevoegen aan `TaxCountry` enum
2. Klasse aanmaken in `Services/Tax/` die `ITaxCalculator` implementeert
3. Instantie toevoegen aan `TaxViewModel._calculators`

### 10.2 ITaxCalculator interface

```csharp
public interface ITaxCalculator
{
    TaxCountry Country         { get; }
    string     CountryName     { get; }
    IReadOnlyList<int> SupportedYears { get; }
    DateOnly   ReferenceDate(int year);          // peildatum
    TaxReport  Calculate(TaxInput input);
}
```

### 10.3 TaxInput (landneutraal invoermodel)

```csharp
public class TaxInput
{
    public int     Year;              // Belastingjaar
    public decimal CryptoValue;       // Cryptowaarde op peildatum (€)
    public decimal BankSavings;       // Banktegoeden op peildatum (€)
    public decimal OtherAssets;       // Overige beleggingen (€)
    public decimal Debts;             // Aftrekbare schulden (€)
    public bool    HasFiscalPartner;  // [NL] Fiscaal partner (verdubbelt HVV)
}
```

### 10.4 Nederland — Box 3 berekening

#### Wettelijk kader
- Grondslag: Wet Inkomstenbelasting 2001, Box 3 (vermogensrendementsheffing)
- Methode: fictief rendement per vermogenscategorie (post-kerstarrest 2022)
- Peildatum: 1 januari van het belastingjaar
- Crypto valt onder: "overige bezittingen" (hoger rendementstarief)

#### Tarieven per jaar

| Jaar | HVV (p.p.) | Spaargeld | Overige bezittingen | Belastingtarief |
|------|-----------|-----------|---------------------|----------------|
| 2022 | € 50.650 | 0,00 % | 5,53 % | 31 % |
| 2023 | € 57.000 | 0,92 % | 6,17 % | 32 % |
| 2024 | € 57.000 | 1,03 % | 6,04 % | 36 % |

*HVV = Heffingsvrij vermogen (per persoon; met fiscaal partner × 2)*

#### Stappenplan berekening

**Stap 1 — Rendementsgrondslag**
```
brutovermogen = cryptoWaarde + banktegoeden + overigeBeleg
grondslag     = max(0, brutovermogen − schulden)
drempel       = HVV × (partner ? 2 : 1)
belastbaarBedrag = max(0, grondslag − drempel)
```

**Stap 2 — Proportionele verdeling over vermogenscategorieën**
```
Als belastbaarBedrag > 0 en brutovermogen > 0:

  spaardeel    = min(belastbaarBedrag, banktegoeden / brutovermogen × belastbaarBedrag)
  cryptodeel   = min(belastbaarBedrag − spaardeel, cryptoWaarde / brutovermogen × belastbaarBedrag)
  overigdeel   = belastbaarBedrag − spaardeel − cryptodeel
```

**Stap 3 — Fictief rendement**
```
fictRend = (spaardeel  × spaarrente)
         + (cryptodeel × overigeRente)
         + (overigdeel × overigeRente)
```

**Stap 4 — Belasting**
```
belasting     = round(fictRend × belastingtarief, 2)
effectiefTarief = belasting / grondslag × 100    [%]
```

#### Voorbeeld (2024, alleen crypto, geen partner)

```
Crypto op 1 januari: € 100.000
Banktegoeden:        € 0
Overige belegg.:     € 0
Schulden:            € 0
Fiscaal partner:     Nee

grondslag        = € 100.000
drempel          = € 57.000
belastbaarBedrag = € 43.000
fictRend         = € 43.000 × 6,04% = € 2.597,20
belasting        = € 2.597,20 × 36% = € 935,00
effectiefTarief  = € 935 / € 100.000 = 0,94%
```

---

## 11. Beveiliging en encryptie

### 11.1 API-sleutels

Exchange API-sleutels worden **nooit** in plaintext opgeslagen.

| Methode | Implementatie | Sleuteltype |
|---------|--------------|-------------|
| HMAC | DPAPI (`ProtectedData.Protect`) | API Key + API Secret |
| RSA | RSA keypair gegenereerd in-app; public key naar exchange; private key DPAPI-versleuteld | API Key + Private Key |

```
Opslag: ExchangeAccount.ApiKeyEncrypted (DPAPI)
         ExchangeAccount.ApiSecretEncrypted (DPAPI)
         ExchangeAccount.PublicKeyPem (plaintext — niet gevoelig)
```

### 11.2 Portfolio-wachtwoord

Het portfolio kan worden vergrendeld met een wachtwoord. Bij juist wachtwoord opent de hoofd-portfolio. Bij het "duress"-wachtwoord opent een alternatieve portfolio (plausible deniability).

### 11.3 Gevoelige instellingen

Telegram Bot Token en Chat ID worden opgeslagen in `prefs.xml`. Bij productie-gebruik wordt aanbevolen dit te versleutelen. (Huidig: plaintext in prefs.xml.)

---

## 12. Uitbreidingspunten

### 12.1 Nieuw land belasting toevoegen

1. `Enums/TaxCountry.cs`: waarde toevoegen (bijv. `Germany`)
2. `Services/Tax/GermanyTaxCalculator.cs` aanmaken:
   ```csharp
   public class GermanyTaxCalculator : ITaxCalculator
   {
       public TaxCountry Country     => TaxCountry.Germany;
       public string     CountryName => "Duitsland";
       public IReadOnlyList<int> SupportedYears => new[] { 2023, 2024 };
       public DateOnly ReferenceDate(int year) => new DateOnly(year, 1, 1);
       public TaxReport Calculate(TaxInput input) { ... }
   }
   ```
3. `ViewModels/TaxViewModel.cs`: instantie toevoegen aan `Calculators`

### 12.2 Nieuwe exchange toevoegen

1. `Enums/PlusEnums.cs`: waarde toevoegen aan `ExchangeKind`
2. `Services/IXxxDataService.cs` + `XxxDataService.cs` aanmaken
3. `App.xaml.cs`: DI-registratie
4. `ExchangeAccountService` uitbreiden met nieuwe exchange-logica

### 12.3 Nieuwe technische indicator toevoegen

1. `IIndicatorService.cs`: methode-signature toevoegen
2. `IndicatorService.cs`: implementatie
3. `Coin.cs`: opslag-eigenschap toevoegen (of `[NotMapped]`)
4. `Infrastructure/EntityConfigurations/CoinEntityTypeConfiguration.cs`: EF-configuratie
5. `ViewModels/SignalsViewModel.cs`: kolom toevoegen aan `Rows`
6. `Views/SignalsView.xaml`: kolom toevoegen

**Regel:** bestaande methodes `CalculateRsiAsync`, `CalculateMaAsync`, `EvaluatePriceLevels` nooit aanpassen.

### 12.4 Nieuwe sentimentbron toevoegen

1. `Enums/PlusEnums.cs`: waarde toevoegen aan `SentimentSource`
2. `Services/Sentiment/`: nieuwe connector aanmaken
3. `SentimentService.cs`: connector inroepen in de verzamelcyclus
4. `Views/SettingsView.xaml` (Databronnen-tab): kaart toevoegen

### 12.5 Live trading (Sprint 2)

`ITradeService.PlaceLiveAsync()` is al gedefinieerd maar niet geïmplementeerd.  
Vereist: exchange API-verbinding, orderbeheer, fill-synchronisatie.

---

## 13. Bekende beperkingen

| Beperking | Details |
|-----------|---------|
| **Geen realtime fill-sync** | Live orders worden niet automatisch gesynct met de exchange |
| **Binance/KuCoin geen API-key** | Publieke endpoints; geen privé accountdata |
| **Sentimentanalyse** | Eenvoudige NLP, geen BERT/LLM; nauwkeurigheid beperkt |
| **WinUI 3 x:Bind beperking** | `{x:Bind}` werkt niet binnen `ct:SettingsExpander.Items` — gebruik altijd `{Binding}` of losse `ct:SettingsCard` elementen |
| **x:Name schaduw-bug** | `x:Name` gelijk aan een Page-klassenaam veroorzaakt heap corruption (0xC0000374); gebruik altijd unieke namen (bijv. `StatisticsNavItem` niet `StatisticsView`) |
| **FaultTolerantHeap (FTH)** | Windows FTH kan na crash activeren en bij volgende start heap corruption veroorzaken; oplossing: `HKCU:\...\AppCompatFlags\Layers` → `~ DISABLEFTH` |
| **Geen multi-portfolio UI** | Switchen gaat via verborgen menu-item; niet zichtbaar in primaire UX |
| **Tarieven belasting handmatig** | `NetherlandsTaxCalculator` tarieven moeten jaarlijks handmatig bijgewerkt worden in `GetRates()` |
| **Box 3 vereenvoudigd** | De schuldenaftrek gebruikt proportionele vereenvoudiging; de exacte Belastingdienst-methode voor schuld-fictief-rendement is niet geïmplementeerd |
| **MarketChart-JSON zonder volume** | De lokale `MarketChart_{id}.json` bevat alleen `[timestamp, prijs]` — geen OHLC en geen volume. `IndicatorService.LoadQuotesAsync` zet daarom `Volume = 0`. Volume-indicatoren (OBV/MFI/VWAP) mogen hier niet op draaien; de Signalen-score gebruikt bewust geen volume. Voor echte volume gebruiken Trade Advies, Pattern Trading en 3% Trading klines via `IBinanceDataService` |
| **Geen kalibratie van Signalen/Pattern-scores** *(v1.33)* | Alleen de 3%-tool en Setup Strategie kalibreren score → historische hitrate, omdat alleen `WatchedSetup`/backtests forward-uitkomsten vastleggen. De `SignalEngine`-CombinedScore en de Pattern-`TradabilityScore` hebben geen opgeslagen uitkomsthistorie en worden daarom (nog) niet naar een gemeten kans vertaald — dat vereist een aparte signal-outcome-tracker |

---

## Appendix A — Enum-overzicht

| Enum | Waarden |
|------|---------|
| `TaxCountry` | Netherlands *(+ toekomstig: Germany, Belgium, UnitedKingdom, UnitedStates)* |
| `SignalDirection` | Long · Short · Flat |
| `Timeframe` | OneHour · FourHour · OneDay |
| `ExchangeKind` | Mexc · Bybit |
| `OrderSide` | Buy · Sell |
| `OrderType` | Market · Limit · StopLimit |
| `OrderStatus` | Pending · Filled · PartiallyFilled · Cancelled · Rejected · Closed |
| `MarketRegime` | RiskOn · Neutral · RiskOff |
| `MarketType` | Spot · Futures · Margin |
| `SentimentSource` | Reddit · Telegram · Rss · CryptoPanic |
| `TransactionKind` | Deposit · Withdraw · Buy · Sell · Convert · Transfer · Fee |
| `MutationDirection` | In · Out · NotSet |
| `PriceLevelType` | TakeProfit · Stop · Buy · Ema |
| `PriceLevelStatus` | NotWithinRange · WithinRange · CloseToPrice · TaggedPrice *(+ combinaties)* |
| `AppFontSize` | Small · Normal · Large |
| `PatternType` | RsiOversold · RsiOverbought · MacdBullishCross · MacdBearishCross · EmaBullishCross · EmaBearishCross · BollingerSqueeze · PriceAboveEma50 · PriceBelowEma50 · TrendingMarket · VolumeSpike · Uptrend · Downtrend · BullFlag · BearFlag · DoubleBottom · DoubleTop · AscendingTriangle · DescendingTriangle · SymmetricalTriangle · SupportBounce · ResistanceRejection · BreakoutAboveResistance · BreakdownBelowSupport · PotentialBreakout · Consolidation · HeadAndShoulders · InverseHeadAndShoulders · RisingWedge · FallingWedge · CupAndHandle |
| `PatternCategory` | Bullish · Bearish · Neutral · Warning |
| `PatternFilter` | All · HighestScore · NearBreakout · BullishOnly · BearishOnly |

---

## Appendix B — Versiehistorie samenvatting

| Versie | Hoogtepunten |
|--------|-------------|
| v1.1 | Basisfunctionaliteit: assets, accounts, library, instellingen |
| v1.2 | Extended TA: MACD, Bollinger, ATR, StochRSI |
| v1.3 | Sentiment Collector: Reddit, RSS, CryptoPanic |
| v1.4 | Signaal Engine, Paper Trading, Trade Journal |
| v1.5 | Sorteerbare kolommen Signalen, tooltip-uitleg |
| v1.6 | Dashboard-widgets, Bronnen-pagina, Telegram, risicobewakers |
| v1.7 | 7 extra indicatoren: EMA Cross, RSI, MA50%, ADX, %B, Squeeze, 52w% |
| v1.8 | Trade Advies: multi-timeframe, live OHLCV, sleutelniveaus, setup |
| v1.9 | Sentiment Collector 15-min cyclus, bronbeheer UI |
| v1.10 | Trade Advies: "Analyseer alles", Paper Trade-knop, KuCoin fallback |
| v1.11 | Statistieken-pagina: P&L, win rate, taartdiagrammen, top-symbols |
| v1.12 | Box 3 belastingcalculator (NL, 2022–2024) · Live/Paper filter · Aangepaste periode |
| v1.13 | On-page Help-module (HelpView) · QuestPDF verwijderd |
| v1.14 | Exchange-stijl PaperTradeDialog: Spot/Futures/Margin · Limit/Market · hefboom · SL/TP2 · R/R-samenvatting |
| v1.15 | TP-sluitpercentage per niveau: snelknoppen 25/50/75/100 % + slider; opgeslagen als `Tp1ClosePct` / `Tp2ClosePct` |
| v1.16 | Auto-close bij TP/SL bereikt: `AutoCloseTriggeredAsync` elke refresh; SL prioriteit boven TP; TP2 vóór TP1 |
| v1.17 | EditTradeDialog: SL/TP aanpassen vanuit Trade Journal · preset-knoppen BE/½R/+1R · veiligheidscheck huidige koers · UX: standaard Open-filter · actieve tab indicator · unrealised P&L leeg voor gesloten orders |
| v1.18 | Fear & Greed Index widget op dashboard · `FearGreedReading`-entiteit · `IFearGreedService` (alternative.me API, 60-min cache) · Databronnen-tab uitgebreid |
| v1.19 | Pattern Trading tab · automatische Level 1 + Level 2 patroonherkenning op 1D/4H/1H · TradabilityScore 0–100 · setup-kaarten (Entry/SL/TP1/TP2/R/R) · 5 filters · klembord-share · `IPatternDetectionService` + `IPatternTradingService` |
| v1.32 | Setup Tracker verbeterd: bevestigingsdialoog bij handmatig sluiten vóór TP1 bereikt · instap-/sluitingstijden (`EntryAt`) op setupkaarten · automatisch ingevuld bij TP/SL-hit · backfill voor bestaande trades · `Functions.Formatters.cs` (partial class, testbaar) · `WatchedSetupService` interne testconstructor · `CryptoPortfolioTracker.Tests` xUnit project (40 tests: TP/SL-detectie, PnlPct, PatternScore, formatters) |
| v1.33 | **3% Trading-tool** (`ThreePctView`): gekalibreerd 7-factor scoremodel met +3% netto-doel · Fase 1 backtest/kalibratie (`ThreePctBacktestService`, JSON-opslag) · Fase 2 live scan met F6 liquiditeit + F7 positionering als gatekeepers · `CorrelationService` (gediversifieerde shortlist) · `MacroEventService` (FOMC/CPI/NFP/PCE) · `SetupDetailDialog`. **Cross-tool:** `TradeSetupValidator.CheckAdvice` markeert ongeldige/krappe setups in Trade Advies & Pattern Trading · `MarketRegimeService.GetRegimeContextAsync` (EMA50/200 + dominantie) ook in `SignalEngine` · markt-context (liquiditeit/funding/events) in Trade Advies · gedeelde `TtlCache<T>` · geëxtraheerde `TradeLevelCalculator` · nieuwe databronnen (Binance depth/futures, CoinGecko global). Tests: 40 → 183 |
| v1.31 | Setup Strategie statistieken: nieuw 'Setup Strategie'-tabblad in `StatisticsView` (Pivot) met Win Rate TP1/TP2, Profit Factor, Expectancy, gem. P&L%, gem. houdtijd, breakdowntabellen per richting/score/marktregime · `SetupBreakdownRow` + `LoadSetupStatsAsync` in `StatisticsViewModel` · TP2-detectie in `AutoUpdateStatusesAsync` (`Tp2Hit` flag) · BTC-marktregime vastgelegd bij aanmaken setup (`MarketRegimeAtCreation`) · bidirectionele Setup↔Order koppeling (`WatchedSetup.LinkedOrderId` + `ExchangeOrder.WatchedSetupId`) · `IWatchedSetupService.GetActiveSetupForCoinAsync` + `LinkOrderAsync` + `GetClosedAsync` · migratie `AddStrategyStatisticsFields` |
| v1.30 | Setup Tracker: `WatchedSetup`-entiteit · `IWatchedSetupService` (CRUD + auto-status + stats) · `SetupTrackerViewModel` + `SetupTrackerView` · automatische entry/TP1/SL-detectie · Open-status bij entry-hit · live koers per kaart (DB-fallback via `GetCoinsFromContext`) · P&L % + Unreal. P&L % per kaart · handmatige Won/Lost/Verlopen knoppen · `ExistsAsync` duplicaatcheck · SL=0 validatie bij aanmaken · auto-refresh via `UpdatePricesMessage` (IMessenger) · prijstijdstempel in UI · tooltips doorvoeren in alle views |
| v1.25 | 15M timeframe: `M15Bars`, `M15Bias`, `M15Rsi` aan `PatternCoinAnalysis` + `PatternCoinRow`; `FetchFourTimeframesAsync` (15m interval); Level 1+2 detectie op 15M; `Btn15M` in `CoinChartWindow`; bias-badge in card-header. Watchlijst UX: `Expander`-paneel met `WatchlistItems` ObservableCollection; `RemoveWatchlistItemCommand`; `LoadWatchlistItemsAsync` bij ViewLoading; User-Agent header + 429-afhandeling in `WatchlistService`. TF-conflict: `HasTfConflict` / `TfConflictText` in `PatternCoinRow`. Sort: `SortMode` (Score/Change24h/Breakout) + `SetSortModeCommand`. TF-filter: `TfFilter` + `SetTfFilterCommand`. In-lijst zoeken: `ListSearchText` + `HandleListSearch`. Overflow-chip: `OverflowCount`/`OverflowVis`. Staleness: `StalenessText`/`StalenessColor` (oranje na 1u). Leeg-state border. Bull/Bear flag chart-annotaties (markers + hlines). Typo fix: Oplopende Driehoek. `ItemsControl` → `ListView` voor virtualisatie. `Functions.EmptyCollectionToVisible` toegevoegd. |
| v1.24 | `PatternHandbookWindow` — niet-modaal venster met WebView2 · inline C# markdown→HTML converter (h1–h4, tabellen, lijsten, blockquotes, bold, code) · `📖 Handboek`-knop in filterrij · `PATTERN_HANDBOOK.md` via .csproj PreserveNewest naar output gekopieerd · fallback naar projectroot als debug-run |
| v1.23 | Nieuwe patronen: Adam & Eve (scherpe + ronde bodem, bullish reversal · IsAdam/IsEveBottom helpers) · AscendingChannel + DescendingChannel (parallelle trendlijnen, grafiek-annotaties · wedge-exclusie via ×1.20 convergentiecheck) · `PatternType` enum + `DisplayName` uitgebreid |
| v1.22 | `PatternDetectionService` handboek-kalibratie: Double Bottom/Top min. scheiding 8 bars, gelijkheidsmarge 3%, diepte 5%; Bull/Bear flag pole 8%, vlagrange 5%; Triangle slopeTol 0.0008; H&S schouders 15%, breedte 12 bars, neklijn max(T1,T2); Inv H&S neklijn min(P1,P2); Wedge convergentie 1.20, span 10 bars, bereik 3–30%; Cup & Handle flexibel 30–65 bars, rim 6%, retrace 45% · `PATTERN_HANDBOOK.md` toegevoegd als authoritatieve spec |
| v1.21 | `CoinChartWindow` — interactieve candlestick grafiek via WebView2 + TradingView Lightweight Charts · 1D/4H/1H timeframe-switcher · support/resistance-lijnen · '📈 Grafiek'-knop per kaart · bars opgeslagen in `PatternCoinAnalysis` |
| v1.20 | Watchlijst (WatchlistCoins SQLite tabel · `IWatchlistService`) · CoinGecko zoekfunctie · Level-3 patronen (H&S, Inv. H&S, Rising/Falling Wedge, Cup & Handle) · Portfolio/Watchlijst badges per kaart |

---

*Dit document beschrijft de toestand van de applicatie per versie 1.33 (juni 2026).*  
*Broncode: `CryptoPortfolioTrackerPlus-main/` · Database: `sqlCPT.db` · Platform: Windows 11 x64*
