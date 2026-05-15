# Product Requirements Document  
## CryptoPortfolioTracker Plus — v1.17

| | |
|---|---|
| **Versie** | 1.17 |
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
- **Signaaalgeneratie** — gecombineerde TA + sentiment + marktregime-score per coin
- **Trade Journal** — paper trading én live trades, met P&L en R-multiple
- **Trade Advies** — multi-timeframe analyse per coin met entry/SL/TP-berekening
- **Statistieken** — geaggregeerde handelsprestaties over meerdere periodes
- **Belasting** — Box 3-berekening (NL) met uitbreidbare architectuur voor andere landen

### 1.2 Primaire gebruiker

Eén persoon: de eigenaar van het portfolio. Er is geen multi-user functionaliteit.

### 1.3 Niet in scope

- Geautomatiseerd handelen (market orders via API) — gepland voor Sprint 2
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
ViewModels/     → erven van BaseViewModel (ObservableRecipient)
                   [ObservableProperty] voor databinding
                   [RelayCommand] voor commando's
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
```

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

---

### 4.6 Trade Advies

**Doel:** Multi-timeframe analyse per coin met concreet handelsadvies.

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

**Paper Trade vanuit advies:**
- Bij Long/Short-signaal: knop "Paper Trade" opent de `PaperTradeDialog` met exchange-stijl interface
- Pre-fills: entry (limit), SL/TP/TP2 als absolute USDT-prijzen, richting (Long/Short)
- Direct opgeslagen in Trade Journal

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

**Zie §6.1 voor P&L- en R-multiple-berekeningen.**

---

#### 4.7.1 PaperTradeDialog

Exchange-stijl orderformulier (540 px breed, `ContentDialog`).

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

**Service-methode:** `ITradeService.UpdateOrderLevelsAsync(orderId, sl, tp1, tp2)` — past `StopLoss`, `TakeProfit` en `TakeProfit2` aan in de database.

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

**Zie §6.2 voor alle statistiek-berekeningen.**

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

### 4.13 What's New

Versiehistorie van de app. Bij eerste opstart na een update toont een dialog een samenvatting.

---

### 4.14 Help

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

### 7.7 Reddit JSON API

| | |
|---|---|
| **Endpoint** | `https://www.reddit.com/r/{subreddit}/new.json` |
| **Authenticatie** | Geen (publieke JSON-feed) |
| **Actieve subreddits** | r/CryptoCurrency · r/Altstreetbets · r/Bitcoin · r/ethereum · r/CryptoMarkets |
| **Gebruik** | Sentimentcollectie via NLP |

### 7.8 RSS-nieuwsfeeds

| Bron | Feed URL |
|------|----------|
| CoinDesk | `https://www.coindesk.com/arc/outboundfeeds/rss/` |
| Cointelegraph | `https://cointelegraph.com/rss` |
| Decrypt | `https://decrypt.co/feed` |
| The Block | `https://www.theblock.co/rss.xml` |
| CryptoSlate | `https://cryptoslate.com/feed/` |

### 7.9 CryptoPanic API

| | |
|---|---|
| **Endpoint** | `https://cryptopanic.com/api/free/v1/posts/` |
| **Authenticatie** | Geen (gratis publieke API) |
| **Gebruik** | 50 meest recente nieuwsartikelen met bullish/bearish labels |
| **Verrijking** | Valutacodes worden toegevoegd aan de tekst voor coin-matching |

### 7.10 Telegram Bot API

| | |
|---|---|
| **Endpoint** | `https://api.telegram.org/bot{token}/sendMessage` |
| **Configuratie** | Bot Token + Chat ID in Instellingen |
| **Gebruik** | Push-notificaties bij signalen boven drempelwaarde |
| **Richting** | Uitsluitend uitgaand (de app leest geen Telegram-berichten) |

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

---

*Dit document beschrijft de toestand van de applicatie per versie 1.17 (mei 2026).*  
*Broncode: `CryptoPortfolioTrackerPlus-main/` · Database: `sqlCPT.db` · Platform: Windows 11 x64*
