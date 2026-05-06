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

## Wat niet te doen

- Geen `dotnet build` gebruiken (zie Build hierboven)
- Geen breaking changes in bestaande service-interfaces
- Geen business-logica in Views of code-behind
- Geen handmatige wijzigingen in `PortfolioContextModelSnapshot.cs`
- Geen twee features in één EF-migratie
- Geen hardcoded paden — gebruik `AppConstants.*`
