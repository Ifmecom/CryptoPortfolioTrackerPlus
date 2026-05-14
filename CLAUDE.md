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
4. Migratie genereren:
   ```
   dotnet ef migrations add MigratieNaam --project CryptoPortfolioTracker.csproj
   ```
5. Migratie toepassen bij app-start gebeurt automatisch via `context.Database.MigrateAsync()`

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
- **`PRD.md` nooit verouderd laten — altijd bijwerken na elke wijziging (zie § PRD bijhouden)**

---

## Wishlist — toekomstige indicatoren

Indicatoren die nog niet zijn geïmplementeerd vanwege data- of scope-beperkingen:

| Indicator | Reden |
|---|---|
| **Ichimoku Cloud** | Te complex voor een tabelkolom; vereist een aparte grafiekweergave |
| **Funding Rate / Open Interest** | Vereist koppeling met een exchange-API (Binance/Bybit etc.) |
| **VWAP** | Zinvol op intraday/uurdata; de app werkt met dagelijkse slotkoersen |
| **Volume vs Gemiddeld** | Volume-data is niet beschikbaar in de lokale MarketChart JSON |
