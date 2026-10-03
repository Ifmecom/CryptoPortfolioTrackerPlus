# CLAUDE.md — CryptoPortfolioTracker Plus

Werkmap: `C:\Users\Remko Piepers\Documents\Claude\Projects\Crypto\CryptoPortfolioTrackerPlus-main\`
Plan: zie `C:\Users\Remko Piepers\Documents\Claude\Projects\Crypto\Plan_Crypto_Analyse_Tool.md`

---

## Build

**Altijd via Visual Studio MSBuild — nooit `dotnet build` CLI.**
`dotnet build` geeft een vals-positieve exit code 1 via XamlCompiler.exe bij WinUI 3 + .NET 10 CLI.

```
C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe CryptoPortfolioTracker.sln /p:Configuration=Debug /p:Platform=x64
```

Of: Ctrl+Shift+B in Visual Studio. F5 om te runnen (Unpackaged profiel).

**Build-output lezen:**
- `error CS####` = echte compileerfout → fixen.
- `error MSB3027`/`MSB3021` ("file is locked by … CryptoFolioTrackerPlus") = de app draait nog; **compilatie is geslaagd**, alleen de copy-stap faalt. Geen codefout — negeren of de app sluiten.
- `token recognition error at: '!'` = onschadelijke ruis, geen fout.
- Command-line MSBuild **verifieert** compilatie maar levert geen runnende build (ontbrekende `.pri`/WebView2Loader/e_sqlite3); een runnende deploy maak je in Visual Studio. **Nooit `bin`/`obj` verwijderen** — dat brak de build eerder.

**Opstartcrash `0xc0000374` (heapcorruptie) — opgelost in v1.48, oorzaak bekend:** heap-bufferoverloop in Windows App SDK **MRM** (`MrmGetFilePathFromName`, upstream microsoft/WindowsAppSDK#4873) zodra `MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY` gezet is. De meegeleverde `MRM.dll` van **1.5.x én 1.6.x** bevat de fout nog (machinecode geverifieerd) — een SDK-upgrade lost het dus niet op. Workaround: eigen **`Program.cs`** (`DISABLE_XAML_GENERATED_MAIN`) wist die variabele vóór `Application.Start`.
- **Niet verwijderen** zonder vervanging; bij PublishSingleFile is de variabele wél nodig (zie commentaar in `Program.cs`).
- Komt `0xc0000374` toch terug: onderzoek met WinDbg (`cdbX64.exe`, Store-alias) + page heap (IFEO `GlobalFlag=0x02000000`, `PageHeapFlags=3`, admin); met page heap faalt de foute schrijfactie direct met de schuldige in de stack.
- Ander gedrag **alleen onder F5**? Dat is de debug-heap → `_NO_DEBUG_HEAP=1` staat in `launchSettings.json`.

---

## Testen

Testproject: `…\Crypto\CryptoPortfolioTracker.Tests\` — een **sibling-map BUITEN deze git-repo** (source-includes via `..\CryptoPortfolioTrackerPlus-main\…`). xUnit + FluentAssertions.

```
dotnet test "…\CryptoPortfolioTracker.Tests\CryptoPortfolioTracker.Tests.csproj" --nologo -v minimal
```

- Tests worden **niet meegecommit** (staan buiten de repo); alleen productiecode zit in `main`. Draai ze lokaal voor verificatie.
- Een nieuwe **pure** service die je wilt testen: voeg het bronbestand toe aan de `<Compile Include>`-lijst in `CryptoPortfolioTracker.Tests.csproj` (geen ProjectReference). `Models/*` en `Enums/*` worden al automatisch meegecompileerd.

---

## Commits

- Werk op `main`, push per feature.
- PowerShell-commitmessage via single-quoted here-string `@'…'@` **zonder dubbele aanhalingstekens** (PowerShell 5.1 splitst de message anders).
- Trailer: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.

---

## Architectuur

### MVVM-patroon

- **Models** (`Models/`) — pure data, EF-entiteiten, geen UI-logica
- **ViewModels** (`ViewModels/`) — erven van `BaseViewModel` (via `ObservableRecipient`), gebruiken `[ObservableProperty]` en `[RelayCommand]`
- **Views** (`Views/`, `Dialogs/`, `Controls/`) — alleen XAML + code-behind voor UI-events, geen business-logica
- **Services** (`Services/`) — alle business-logica, altijd via interface

### Services-regels

1. Elke nieuwe service heeft een interface: `IXxxService` in `Services/`
2. Implementatie in dezelfde map: `XxxService.cs`
3. Registreer in `App.xaml.cs` in de bestaande DI-container (zie §DI hieronder)
4. Singleton als de service state bijhoudt over de hele app-lifetime; Scoped anders
5. **Nooit breaking changes** aan bestaande publieke methodes van:
   - `IIndicatorService` (`CalculateRsiAsync`, `CalculateMaAsync`, `EvaluatePriceLevels`)
   - `IPriceUpdateService`, `ILibraryService`, `IAssetService`

### DI-registratie (`App.xaml.cs` ~regel 160-235)

```csharp
// Patroon voor nieuwe service:
services.AddSingleton<ISignalEngine, SignalEngine>();
services.AddScoped<ISentimentService, SentimentService>();

// Patroon voor nieuwe ViewModel:
services.AddScoped<SignalsViewModel>();

// Patroon voor nieuwe View:
services.AddScoped<SignalsView>();
```

---

## Database

### DbContext

- **Hoofd-context**: `Infrastructure/PortfolioContext.cs` — voor lees/schrijf vanuit de UI
- **Update-context**: `Infrastructure/UpdateContext.cs` — alleen voor `PriceUpdateService` (aparte thread)
- Beide gebruiken `EntityTypeConfiguration`-klassen, aangemaakt in `Infrastructure/EntityConfigurations/`

### Nieuwe entiteit toevoegen

1. Model aanmaken in `Models/XxxModel.cs`
2. `EntityTypeConfiguration` aanmaken in `Infrastructure/EntityConfigurations/`
3. `DbSet<Xxx>` toevoegen aan `PortfolioContext.cs`
4. **Tabel aanmaken — gebruik het PLUS-patroon, NIET `dotnet ef` (onbetrouwbaar bij WinUI-CLI):**
   - `CREATE TABLE IF NOT EXISTS Xxx (…)` + indices toevoegen in `PortfolioService.ApplyPlusSchemaAsync` (draait idempotent bij startup, ná `MigrateAsync`).
   - Hand-geschreven migratie `Migrations/2026MMDDHHMMSS_AddXxx.cs` als **documentatie** (mirror `AddCoinFundamentals`/`AddPatternState`). Géén `[Migration]`-attribuut/Designer → `MigrateAsync` negeert hem, dus geen "table already exists"-clash. `PortfolioContextModelSnapshot.cs` niet bewerken.
5. Startup doet `MigrateAsync()` (alleen attribuut-dragende migraties) + `ApplyPlusSchemaAsync()` (de echte, idempotente aanmaak voor PLUS-tabellen).

### Migratie-conventies

- Één migratie per sprint/feature-branch: `AddPlusFeatures`, `AddSentimentSchema`, etc.
- Nooit twee features in één migratie mengen
- `PortfolioContextModelSnapshot.cs` **niet handmatig bewerken** — wordt automatisch gegenereerd

---

## Achtergrondservice-patroon

Aparte console-exe's die via Windows Task Scheduler worden uitgevoerd:
- `MarketChartsUpdateService.exe` → schrijft `MarketChart_{ApiId}.json` naar `AppConstants.ChartsFolder`
- Nieuw: `SentimentCollectorService.exe` (Sprint 1.3), `SignalEngineService.exe` (Sprint 1.4)

Patroon: kopieer `MarketChartsUpdateService`-project als sjabloon. Registreer de taak via `ScheduledTaskService`.

JSON-bestanden worden gelezen door `IndicatorService` via `AppConstants.ChartsFolder`.

---

## Indicator-extensie (Sprint 1.2)

Nieuwe publieke methodes toegevoegd aan `IIndicatorService` en `IndicatorService`:

```csharp
Task<MacdResult>      CalculateMacdAsync(Coin coin);
Task<BollingerResult> CalculateBollingerAsync(Coin coin);
Task<double>          CalculateAtrAsync(Coin coin);
Task<double>          CalculateStochRsiAsync(Coin coin);
Task<TaScore>         CalculateTaScoreAsync(Coin coin, Timeframe tf);
```

Gebruikt `Skender.Stock.Indicators` NuGet (MIT, gratis). Bestaande methodes blijven ongewijzigd.

---

## Nieuwe entiteiten (Sprint 1.1)

| Entiteit | Tabel | Doel |
|---|---|---|
| `SentimentReading` | SentimentReadings | Ruwe sentiment-scores per bron |
| `Signal` | Signals | Gegenereerde handelssignalen |
| `SignalRule` | SignalRules | Configureerbare signaalregels |
| `ExchangeOrder` | ExchangeOrders | Paper- en live-orders |
| `ExchangeAccount` | ExchangeAccounts | Versleutelde exchange API-keys |
| `BronSource` | BronSources | Sentiment-bronnen (Reddit, RSS etc.) |

`Coin` uitbreiden met: `Macd`, `MacdSignal`, `BollingerUpper`, `BollingerLower`, `Atr`, `StochRsi`, `LatestSentimentScore`, `LatestSignalScore`, `MarketRegime`.

---

## Takken (branches)

| Branch | Sprint | Status |
|---|---|---|
| `main` | — | Stabiele basis |
| `feature/db-plus-schema` | 1.1 | In uitvoering |
| `feature/ta-extended-indicators` | 1.2 | Gepland |
| `feature/sentiment-collector` | 1.3 | Gepland |
| `feature/signal-engine` | 1.4 | Gepland |
| `feature/ui-mvp` | 1.5 | Gepland |

---

## Naamgevingsconventies

- Interfaces: `IXxxService`
- Implementaties: `XxxService`
- ViewModels: `XxxViewModel`
- Views: `XxxView.xaml` + `XxxView.xaml.cs`
- Entiteiten: PascalCase, enkelvoud (`Signal`, niet `Signals`)
- EF-tabellen: meervoud (`Signals`, `ExchangeOrders`)
- Branches: `feature/beschrijvende-naam` (kebab-case)

---

## What's New pagina bijhouden

De pagina `Views/WhatsNewView.xaml.cs` (methode `BuildContent()`) bevat het volledige versie-overzicht van de app.

**Verplichte regel: voeg altijd een nieuw feature-item toe bij elke wijziging die zichtbaar is voor de gebruiker.**

Richtlijnen:
- Nieuwe functies gaan bovenaan, in het bestaande `AddVersionHeader`-blok van de huidige sprint/versie
- Begin een nieuw versieblok (`AddVersionHeader("v1.x", "subtitel")`) bij een nieuwe release
- Gebruik een passend emoji-icoon, een korte Nederlandse titel en een heldere beschrijving
- Technische refactors en bugfixes die de gebruiker niet merkt hoeven niet vermeld te worden
- Meest recente versie staat altijd bovenaan — oudere versies blijven staan

De startup-dialog (`Dialogs/WhatsNewDialog.xaml.cs`) toont ook een samenvatting bij de eerste opstart na een versie-update. **Werk deze ook bij** zodat de popup-samenvatting overeenkomt met de volledige What's New pagina.

---

## Databronnen-pagina bijhouden

De tab **Databronnen** in `Views/SettingsView.xaml` (tweede `PivotItem`) is een handmatig bijgehouden overzicht van alle externe en lokale bronnen die de app gebruikt.

**Verplichte regel: voeg altijd een kaart toe aan deze tab bij elke wijziging die een nieuwe databron introduceert.**

Dit geldt voor:
- Een nieuwe externe API of service (REST, WebSocket, RSS, etc.)
- Een nieuwe lokale opslaglocatie (extra database, extra JSON-cache, configuratiebestand)
- Een nieuwe achtergrondservice die data ophaalt of wegschrijft
- Een nieuwe NuGet-library die zelf een extern endpoint aanspreekt (bijv. Telegram.Bot, Reddit-client)

De kaart hoort in de logisch passende `ct:SettingsExpander`-sectie:
| Type bron | Sectie |
|---|---|
| Externe prijs-/marktdata API | Koers- en marktdata |
| Lokale bestanden of databases | Lokale opslag |
| Nieuws, social media, sentiment | Sentiment & nieuws |
| Push- of e-mailnotificaties | Notificaties |
| Iets anders | Voeg een nieuwe sectie toe |

---

## Pagina-uitleg bijhouden (ⓘ-knop, v1.48)

Elke pagina heeft rechtsboven een ⓘ-knop (`PageInfoButton` in `MainPage.xaml`) die de uitleg uit **`Services/PageHelpCatalog.cs`** toont (sleutel = View-klassenaam = `Tag`).

**Verplichte regel: werk de uitleg van een pagina bij als je zichtbaar iets wijzigt** (nieuwe kolom, knop, drempel, score-band, betekenis van een kleur). Een **nieuwe menu-optie** heeft een nieuw catalogus-item nodig — `PageHelpCatalogTests` leest alle `Tag="…View"` uit `MainPage.xaml` en faalt anders.

- Vaste blokken: `Wat zie je hier` · `Hoe lees je het` · `Hoe ga je ermee om` · `Let op` (handelspagina's: alle vier verplicht).
- Noem echte drempels uit de code (bijv. `SignalEngine.ScoreToDirection`: Long ≥ 60, Short ≤ 40), geen geschatte.
- Nieuwe pagina met eigen knoppen rechtsboven in de header? Houd rechts ~44 px vrij, anders valt de ⓘ-knop eroverheen.

---

## PRD bijhouden (`PRD.md`)

De `PRD.md` in de projectroot is de centrale ontwikkelaarsdocumentatie. **Werk deze altijd bij na elke wijziging die je doorvoert.**

Wat bijgewerkt moet worden:

| Soort wijziging | Sectie in PRD.md |
|---|---|
| Nieuwe entiteit of property | § Data Model |
| Nieuwe service of interface | § Architecture / § Services |
| Nieuwe berekening of formule | § Calculations & Formulas |
| Nieuwe externe API / integratie | § External Integrations |
| Nieuwe pagina of ViewModel | § Pages & Views |
| Nieuwe configuratie-instelling | § Configuration |
| Bugfix met architecturele impact | § Known Limitations / § Architecture |
| Nieuwe belasting-calculator | § Tax Module |
| Versienummer / sprint | § Version History (bovenaan toevoegen) |

**Verplichte werkwijze:**
1. Voer de wijziging door in de code
2. Pas `PRD.md` aan op de relevante secties
3. Commit code én PRD in dezelfde commit (of direct daarna als aparte `docs:`-commit)

---

## Wat niet te doen

- Geen `dotnet build` gebruiken (zie Build hierboven)
- Geen breaking changes in bestaande service-interfaces
- Geen business-logica in Views of code-behind
- Geen handmatige wijzigingen in `PortfolioContextModelSnapshot.cs`
- Geen twee features in één EF-migratie
- Geen hardcoded paden — gebruik `AppConstants.*`
- **Geen nieuwe databron toevoegen zonder de Databronnen-tab in `SettingsView.xaml` bij te werken**
- **Geen zichtbare gebruikersfunctie toevoegen zonder `WhatsNewView.xaml.cs` (`BuildContent`) bij te werken**
- **Geen pagina zichtbaar wijzigen of toevoegen zonder de uitleg in `PageHelpCatalog.cs` bij te werken**
- **Nooit `context.Coins.Update(coin)` op een `AsNoTracking`-coin die zonder `.Include(x => x.Narrative)` is geladen.** De `Coin`-constructor zet `Narrative = new()`; `Update()` voegt dat lege narratief dan in en koppelt het aan de coin (v1.48-incident: 196.000 lege narratieven, 66 coins kwijt aan hun narratief). Schrijf losse velden met `ExecuteUpdateAsync` (zie `PriceUpdateService.UpdatePriceCoin`) of laad met tracking.
- **Geen netwerk-wachttijd in het eerste laadpad van een pagina.** Toon eerst wat in de database staat (snapshot) en werk op de achtergrond bij (patroon: `StatisticsViewModel.UpdateOutcomesAsync`, `TradeJournalViewModel.SyncLiveInBackgroundAsync`). Laadtijden staan in het log als `Perf: …` (`Helpers/PerfLog`).
- **`PRD.md` nooit verouderd laten — altijd bijwerken na elke wijziging (zie § PRD bijhouden)**

---

## Patroondetectie (subsysteem)

- **`Services/PatternDetectionService.cs`** — puur/stateless. `DetectFromBars`: swings op wicks, R²≥0,70 + ≥2 aanrakingen, ATR+%-groottebanden, drie-staten-bevestiging via `ApplyStatus`/`BreakoutMarginPct`. Niet breken; uitbreiden = nieuwe `Detect*`-methode toevoegen.
- **`Services/TradeSetupGate.cs`** — pure poort: geen trade-setup op stablecoins of coins met ATR < `MinAtrPctForSetup` (1,5%). Toegepast in `PatternTradingService.BuildSetupAdvice` én `TradeAnalysisService.BuildTradeSetup`.
- **Patroon-geheugen (P7):** `PatternStateRecord` (tabel `PatternStates`) + pure `PatternFingerprint`/`PatternReconciler` + EF `PatternStateStore`. Reconciliatie draait **sequentieel ná** de parallelle per-coin scan in `PatternTradingService` (detector blijft puur; gedeelde DB-context niet vanuit de parallelle tak schrijven).
- **Spec/docs:** `PATTERN_HANDBOOK.md` (v2.1, autoritatief), `PATTERN_SPEC_STATUS.md` (werklijst P1–P7, allemaal ✅). Werk deze bij bij detectie-wijzigingen.

---

## Signaal-kalibratie (subsysteem, v1.46)

- **`Models/SignalOutcome.cs`** (tabel `SignalOutcomes`, uniek op `Source, CoinApiId, SignalDay`) — gemeten uitkomst per signaal: rendement na 1/3/7/14 dagen in de richting van het signaal + MFE/MAE.
- **Puur + getest:** `SignalOutcomeEvaluator` (meting op gesloten daily candles, instap = slotkoers signaaldag, geen lookahead; ontdubbeling één per coin per dag; regime terugrekenen uit de multiplier) en `SignalCalibrationCalculator` (trefkans per bron/richting/scoreklasse/regime). Tests: `SignalOutcomeEvaluatorTests`, `SignalCalibrationCalculatorTests`.
- **`SignalOutcomeService`** — EF-lijm: neemt `Signals` op (met terugwerkende kracht), `PatternTradingService` roept `RecordPatternScanAsync` sequentieel ná de reconciliatie aan, `UpdateAsync` haalt per coin één keer daily klines op. Houd DB-werk kort en binnen de semafoor; netwerk erbuiten.
- **Wijzig je de SignalEngine-multipliers of de Long/Short-drempels?** Pas dan ook `SignalOutcomeEvaluator.RegimeFromMultiplier` en `SignalCalibrationCalculator.BucketsFor/BucketFor` aan.

---

## Order-uitvoering Bybit EU Demo (subsysteem, v1.47)

- **Echt geld staat op slot:** `TradeService.PlaceLiveAsync` accepteert alleen `ExchangeKind.BybitDemo`. Niet versoepelen zonder expliciete opdracht van Remko.
- **Puur + getest:** `BybitApi` (sign/parse), `BybitOrderPlanner` (afronden, minima, SL/TP, JSON), `LiveOrderReconciler` (fills → status), `AutoTradeSelector`. Tests: `BybitTradingTests.cs`.
- **`BybitDemoExecutor`** (`ILiveOrderExecutor`) — alleen HTTP + EF. Instap altijd Limit+GTC met `takeProfit`/`stopLoss`; "market" = ask × 1,005. Eén open order per paar. Sync via `orderLinkId` (= `ExchangeOrder.ExternalOrderId`) + `/v5/execution/list`.
- **`AutoTraderService`** draait sequentieel ná de Pattern-scan (net als de outcome-tracker) en alleen als `Settings.IsAutoTradeEnabled`. Markeert orders met `[AUTO]` in `Notes` (telt voor het dagmaximum).
- Demo-domein en sleutel: `Settings.BybitDemoBaseUrl` wordt gezet door 'Verbinding testen' (`ExchangeAccountService.TestBybitDemoAsync`, valideert via `/v5/account/info`).
- **`api-demo.bybit.eu` kent `/v5/account/wallet-balance` niet (lege HTTP 404).** Saldo komt uit `/v5/order/spot-borrow-check` (`BybitApi.ParseSpotAvailable`). Lege 401/404-antwoorden = sleutel onbekend / endpoint niet gerouteerd.

---

## Top X — kansscore (subsysteem, v1.48)

- **Puur + getest:** `Services/OpportunityRanker.cs` (`OpportunityRankerTests`). Kansscore = kwaliteit × R/R-factor (R/R÷2, 0,25–1,4) × bewijsfactor (alleen bij een *betrouwbare* gemeten trefkans: 1 + E/2, E = p·R/R − (1−p)) × waarschuwingen (tegen trend 0,85 · TF-conflict 0,90 · dunne liquiditeit 0,80 · bijna breakout 1,05). `HowItWorks` is de uitlegtekst achter de ?-knop.
- **Gezondheid van de munt:** pure `Services/CoinHealth.cs` (`CoinHealthTests`, incl. de praktijkgevallen WMOXY/NEIRO/NIBI). Doet niet mee: geen koers/marktwaarde/rang (≥ 999999), marktwaarde < $1 mln, ATR 0 of onder de setup-drempel (alleen waar ATR bekend is: Analyse; elders doet `TradeSetupGate` dat al). Factor: ingestort (≤ −70% in een maand of ≤ −50% onder MA50) ×0,60, microcap (< $10 mln) ×0,80 — stapelbaar. Reden: de signaalengine beloont extremen, en bij dode/instortende munten zijn die een data-artefact of vallend mes.
- **UI:** gedeelde code-only `Controls/TopPicksBar` (TopCount/OnlyTop/Summary). Rijen implementeren `ITopPickRow`; VM: `OpportunityRanker.Apply(rows, ToOpportunity, TopPickCount)` → `Visible(rows, …, OnlyTopPicks)`. Keuze per pagina in `Settings.Get/SetTopPickCount(page)` en `Get/SetTopPicksOnly(page)`.
- **Pagina's:** Analyse (sterkte in eigen richting + signaal-kalibratie, geen R/R, stablecoins uit), Trade Advies (score in eigen richting — Short = 100 − score — + R/R, rangschikking in de VM, view tekent), Pattern Trading (+ pattern-kalibratie, liquiditeit, TF-conflict, breakout), Setup Tracker (alleen *Watching* + eigen win-rate per scoreklasse), 3% Trading (score + backtest-hitrate, F6/F7-gefilterd en stablecoins tellen niet mee).
- **Models blijven WinUI-vrij** (tests compileren `Models/**`): in modellen alleen `bool IsTopPick`; XAML bindt `Visibility="{x:Bind IsTopPick}"`.
- Nieuwe pagina met setups? Implementeer `ITopPickRow`, voeg de `TopPicksBar` toe en noem de factoren in `PageHelpCatalog`.

---

## TradingView-koppeling (subsysteem, v1.48)

- **Geen API:** alles loopt via deeplinks (`TradingViewSymbol.ChartUrl`), Pine Script (v6) dat de gebruiker zelf plakt, en watchlist-bestanden (`###Sectie,BEURS:PAAR,…`). Geen scraping, geen webhooks (betaald plan + publieke URL) — zie PRD §3.6.
- **Puur + getest** (`TradingViewTests`): `TradingViewSymbol` (DataSource → `BEURS:PAAR`, interval-mapping), `PineScriptGenerator` (`ForSetup`, `ForWatchlist` met `switch syminfo.ticker`, max. 40), `TradingViewWatchlist`.
- **`ITradingViewService`** (singleton): `TickerFor` (standaardbeurs uit Settings; BYBIT → `BybitQuoteCoin`), `OpenChartAsync`, `CopyToClipboard`, `SaveAsync` (naar `AppConstants.TradingViewFolder`), `RevealInExplorer`. VM's krijgen hem als **optionele laatste ctor-parameter** (`ITradingViewService? tradingView = null`).
- **UI:** `Dialogs/PineScriptDialog` (`ShowAsync`, `ExportWatchlistAsync`). Per pagina in de VM: `PineSetupFor(row)`, `TopPineSetups()`, `BuildTradingViewWatchlist()`, `OpenTradingViewCommand`; code-behind toont alleen de vensters.
- **Pine-regels:** alles in het script via `PineScriptGenerator.P()` (invariant, geen exponent) en `Safe()` (geen quotes/regeleinden in strings). Wijzig je de gegenereerde Pine, controleer dan dat hij in de Pine Editor compileert — de tests dekken alleen de structuur.
- Nieuwe pagina met setups? Voeg een 📊 TradingView-knop toe volgens hetzelfde patroon en noem hem in `PageHelpCatalog`.

---

## Wishlist — toekomstige indicatoren

Indicatoren die nog niet zijn geïmplementeerd vanwege data- of scope-beperkingen:

| Indicator | Reden |
|---|---|
| **Ichimoku Cloud** | Te complex voor een tabelkolom; vereist een aparte grafiekweergave |
| **Funding Rate / Open Interest** | Vereist koppeling met een exchange-API (Binance/Bybit etc.) |
| **VWAP** | Zinvol op intraday/uurdata; de app werkt met dagelijkse slotkoersen |
| **Volume vs Gemiddeld** | Volume-data is niet beschikbaar in de lokale MarketChart JSON |
