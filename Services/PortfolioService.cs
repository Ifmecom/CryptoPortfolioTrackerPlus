using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Helpers;
using CryptoPortfolioTracker.Infrastructure;
using CryptoPortfolioTracker.Models;
using CryptoPortfolioTracker.ViewModels;
using LanguageExt;
using LanguageExt.ClassInstances.Const;
using LanguageExt.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace CryptoPortfolioTracker.Services
{
    /// <summary>
    /// Invoked when the application is launched.
    /// Needs to call InitializeAsync to get the portfolios and connect to the database.
    /// </summary>
    public partial class PortfolioService : ObservableObject
    {
        private static ILogger Logger { get; set; } = Log.Logger.ForContext(Constants.SourceContextPropertyName, typeof(PortfolioService).Name.PadRight(22));
        private IPriceUpdateService _priceUpdateService;
        private IGraphUpdateService _graphUpdateService;
        private readonly Settings _appSettings;
        public readonly IPortfolioContextFactory _contextFactory;
        public readonly IUpdateContextFactory _updateContextFactory;
        public PortfolioContext Context { get; set; }
        public UpdateContext UpdateContext { get; set; }
        private IMessenger _messenger;

        [ObservableProperty] private Portfolio? currentPortfolio;
        [ObservableProperty] private ObservableCollection<Portfolio> portfolios = new();

        partial void OnCurrentPortfolioChanged(Portfolio value)
        {
            CurrentPortfolio.LastAccess = DateTime.Now; ;
        }

        public bool IsInitialPortfolioLoaded { get; private set; } = false;

        public PortfolioService(IPortfolioContextFactory contextFactory, IUpdateContextFactory updateContextFactory, IMessenger messenger, Settings appSettings)
        {
            _appSettings =  appSettings;
            _contextFactory = contextFactory;
            _updateContextFactory = updateContextFactory;
            _messenger = messenger;
        }

        /// <summary>
        /// Gets the available Portfolios and connects to the Database
        /// </summary>
        public async Task InitializeAsync()
        {
            if (!App.IsDuressMode)
            {
                await GetPortfolios();
                IsInitialPortfolioLoaded = await LoadInitialPortfolio();
            }
            else
            {
                Portfolios = new ObservableCollection<Portfolio>();
                IsInitialPortfolioLoaded = await LoadInitialPortfolio();
            }
            _graphUpdateService = App.Container.GetService<IGraphUpdateService>();

            _priceUpdateService = App.Container.GetService<IPriceUpdateService>();
        }

        


        public async Task<Result<bool>> SwitchPortfolio(Portfolio portfolio)
        {
            var result = await ConnectPortfolioDatabase(portfolio);
            return result.Match(
                Succ: succ =>
                {
                    Logger?.Information($"Switched to Database {portfolio.Signature}");
                    return new Result<bool>(true);
                },
                Fail: err =>
                {
                    Logger?.Error(err, $"Failed to switch to Database {portfolio.Signature}");
                    return new Result<bool>(err);
                });
        }

        private async Task GetPortfolios()
        {
            var loadResult = await LoadPortfoliosFromJson();
            loadResult.Match(
                Right: succes =>
                {
                    return;
                },
                Left: async error => {
                    if (await MigrateFolderStructureIfNeeded())
                    {
                        Portfolios = GetPortfoliosFromFolders();
                        await SavePortfoliosToJson();
                    }
                });

        }

        private async Task<bool> LoadInitialPortfolio()
        {
            try
            {
                var portfolio = new Portfolio();    
                if (!App.IsDuressMode)
                {
                    portfolio = _appSettings.LastPortfolio ?? Portfolios.FirstOrDefault();
                }
                else
                {
                    portfolio = await GetDuressPortfolio();
                }
                if (portfolio == null)
                {
                    Logger?.Error("No portfolio found to load.");
                    return false;
                }

                var result = await ConnectPortfolioDatabase(portfolio);
                if (result.IsSuccess)
                {
                    Logger?.Information($"Connected to Database {portfolio.Signature}");
                    return true;
                }
                else
                {
                    Logger?.Error($"Failed to connect to Database {portfolio.Signature}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Failed to connect to Database");
                return false;
            }
        }

        public async Task<Portfolio> GetDuressPortfolio()
        {
            var duressPortfolio = new Portfolio
            {
                Name = "Default Portfolio",
                Signature = AppConstants.DefaultDuressPortfolioGuid
            };
            try
            {
                // Build the expected duress portfolio path
                var duressPath = Path.Combine(AppConstants.PortfoliosPath, AppConstants.DefaultDuressPortfolioGuid);

                // Check if the directory exists (i.e., the duress portfolio exists on disk)
                if (!Directory.Exists(duressPath))
                { 
                    _ = await AddDuressPortfolio();
                }
                return duressPortfolio;
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Failed to get or create duress portfolio.");
                // Return a default Portfolio object in case of error
                return duressPortfolio;
            }
            finally
            {
                Portfolios.Add(duressPortfolio);
            }
        }

        private async Task<Result<bool>> ConnectPortfolioDatabase(Portfolio portfolio)
        {
            var previousContext = Context;
            try
            {
                var connectResult = Connect(portfolio);
                if (connectResult.IsFaulted) return new Result<bool>(connectResult.Exception);

                CurrentPortfolio = Portfolios.Where(x => x.Signature == portfolio.Signature).FirstOrDefault();
                if (!App.IsDuressMode)
                {
                    _appSettings.LastPortfolio = CurrentPortfolio;
                    _messenger.Send(new PortfolioConnectionChangedMessage());


                    string portfoliosFile = Path.Combine(AppConstants.PortfoliosPath, AppConstants.PortfoliosFileName);
                    await SavePortfoliosAsync(portfoliosFile, async stream =>
                    {
                        await JsonSerializer.SerializeAsync(stream, Portfolios);
                        Logger.Information("Portfolios data serialized successfully. {0} portfolios)", Portfolios?.Count);
                    });
                }

            }
            catch (Exception ex)
            {
                Context = previousContext;
                return new Result<bool>(ex);
            }
            return true;
        }

        /// <summary>Full file-system path to the currently active SQLite database.</summary>
        public string ActivePortfolioPath { get; private set; } = string.Empty;

        public async Task<Either<Error,bool>> Connect(Portfolio portfolio)
        {
            try
            {
                var relativePath = Path.GetRelativePath(AppConstants.AppDataPath, Path.Combine(AppConstants.PortfoliosPath, portfolio.Signature, AppConstants.DbName));
                ActivePortfolioPath = Path.Combine(AppConstants.PortfoliosPath, portfolio.Signature, AppConstants.DbName);
                Context = _contextFactory.Create($"Data Source=|DataDirectory|{relativePath}");
                UpdateContext = _updateContextFactory.Create($"Data Source=|DataDirectory|{relativePath}");
                
                await CheckDatabase(portfolio.Signature);
                AddSuffixToMarketChartsIfNeeded();
                return true;
            }
            catch (Exception ex)
            {
                return Error.New(ex);
            }
        }

        private void AddSuffixToMarketChartsIfNeeded()
        {
            var context = Context;
            List<string> prelistingApiIds = context.Coins
                .AsNoTracking()
                .Where(c => c.Name.Contains("_pre-listing"))
                .Select(c => c.ApiId)
                .ToList();

            foreach(var apiId in prelistingApiIds)
            {
                var file = Directory.GetFiles(AppConstants.ChartsFolder, "MarketChart_" + apiId + ".json").FirstOrDefault();
                if (string.IsNullOrWhiteSpace(file)) continue;

                var newFile = file.Replace(apiId, apiId + "-prelisting");
                File.Move(file, newFile);
            }
        }

        public Either<Error,bool> Disconnect()
        {
            try
            {
                Context?.Database.CloseConnection();
                Context?.Dispose();
                Context = null;

                UpdateContext?.Database.CloseConnection();
                UpdateContext?.Dispose();
                UpdateContext = null;

                // Force garbage collection to ensure all objects are released
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Logger.Information("Disconnected from the database.");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to disconnect from the database.");
                return Error.New(ex);
            }
        }

        private async Task CheckDatabase(string portfolioSignature)
        {
            Logger?.Information($"Checking Database for portfolio {portfolioSignature}");

            if (Context == null)
            {
                Logger?.Error("Failed to retrieve PortfolioContext from the service container.");
                return;
            }

            CreateRestorePoint(portfolioSignature);

            var pendingMigrations = await Context.Database.GetPendingMigrationsAsync();
            var initPriceLevelsEntity = pendingMigrations.Any(m => m.Contains("AddPriceLevelsEntity"));
            var initNarrativesEntity = pendingMigrations.Any(m => m.Contains("AddNarrativesEntity"));

            pendingMigrations = null;

            await Context.Database.MigrateAsync();
            await ApplyPlusSchemaAsync();

            var appliedMigrations = await Context.Database.GetAppliedMigrationsAsync();
            foreach (var migration in appliedMigrations)
            {
                Logger?.Information("Applied Migrations {0}", migration);
            }

            //*** location to implement a kind of HealthCheck/Cleanup for the database
            //*** The IsAsset status is not used properly for a while causing faulty settings. For the Narratives Overview it is important to be correct.
            await RepairCoinIsAssetStatus();
           // await AddPriceLevelsEmaToDatabase();

        }

        private async Task AddPriceLevelsEmaToDatabase()
        {
            var context = Context;
            if (context == null) return;
            context.ChangeTracker.Clear();

            try
            {
                var coinsWithoutEma = await context.Coins
                    .Where(coin => !coin.PriceLevels.Any(pl => pl.Type == PriceLevelType.Ema))
                    .ToListAsync();
                
                if (!coinsWithoutEma.Any()) return;

                foreach (var coin in coinsWithoutEma)
                {
                    var priceLevel = new PriceLevel
                    {
                        Coin = coin,
                        Type = PriceLevelType.Ema,
                        Value = 0,
                        Note = string.Empty,
                        Status = PriceLevelStatus.NotWithinRange
                    };
                    coin.PriceLevels.Add(priceLevel);
                    context.Coins.Update(coin);
                }
                await Context.SaveChangesAsync();
            }
            finally
            {
                context.ChangeTracker.Clear();
            }
        }

        private async Task ApplyPlusSchemaAsync()
        {
            var db = Context.Database;
            try
            {
                // Add new Coin columns — each call is idempotent via try/catch on duplicate column error
                await TryAddColumnAsync(db, "Coins", "Macd",                 "REAL",    "0");
                await TryAddColumnAsync(db, "Coins", "MacdSignal",           "REAL",    "0");
                await TryAddColumnAsync(db, "Coins", "BollingerUpper",       "REAL",    "0");
                await TryAddColumnAsync(db, "Coins", "BollingerLower",       "REAL",    "0");
                await TryAddColumnAsync(db, "Coins", "Atr",                  "REAL",    "0");
                await TryAddColumnAsync(db, "Coins", "StochRsi",             "REAL",    "0");
                await TryAddColumnAsync(db, "Coins", "LatestSentimentScore", "REAL",    "0");
                await TryAddColumnAsync(db, "Coins", "LatestSignalScore",    "REAL",    "0");
                await TryAddColumnAsync(db, "Coins", "MarketRegime",         "TEXT",    "'Neutral'");
                // Extended indicator columns (Sprint 1.2+)
                await TryAddColumnAsync(db, "Coins", "Rsi",                  "REAL",    "0");
                await TryAddColumnAsync(db, "Coins", "EmaCross",             "TEXT",    "'-'");
                await TryAddColumnAsync(db, "Coins", "EmaCrossBarsAgo",      "INTEGER", "0");
                await TryAddColumnAsync(db, "Coins", "BollingerPctB",        "REAL",    "0");
                await TryAddColumnAsync(db, "Coins", "Ma50DistPerc",         "REAL",    "0");
                await TryAddColumnAsync(db, "Coins", "Adx",                  "REAL",    "0");
                await TryAddColumnAsync(db, "Coins", "IsSqueeze",            "INTEGER", "0");
                await TryAddColumnAsync(db, "Coins", "High52wPerc",          "REAL",    "0");

                // Create new PLUS tables if they don't exist
                await db.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS BronSources (
                        Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                        Type TEXT NOT NULL,
                        Url TEXT NOT NULL,
                        Handle TEXT NOT NULL,
                        ReliabilityScore REAL NOT NULL DEFAULT 1.0,
                        IsActive INTEGER NOT NULL DEFAULT 1
                    )");

                await db.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS ExchangeAccounts (
                        Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                        Exchange TEXT NOT NULL,
                        ApiKeyEncrypted TEXT NOT NULL DEFAULT '',
                        ApiSecretEncrypted TEXT NOT NULL DEFAULT '',
                        Permissions TEXT NOT NULL DEFAULT '',
                        IsActive INTEGER NOT NULL DEFAULT 0
                    )");
                // RSA support columns (added in v1.5) — idempotent via TryAddColumnAsync
                await TryAddColumnAsync(db, "ExchangeAccounts", "AuthMethod",    "TEXT", "'HMAC'");
                await TryAddColumnAsync(db, "ExchangeAccounts", "PublicKeyPem",  "TEXT", "''");

                // ExchangeOrders — close-position support (v1.11)
                await TryAddColumnAsync(db,         "ExchangeOrders", "ClosePrice", "REAL", "0");
                await TryAddNullableColumnAsync(db, "ExchangeOrders", "ClosedAt",   "TEXT");
                // ExchangeOrders — notes support (v1.x)
                await TryAddColumnAsync(db,         "ExchangeOrders", "Notes",       "TEXT",    "''");
                // ExchangeOrders — exchange-style paper trade (v1.14)
                await TryAddColumnAsync(db,         "ExchangeOrders", "TakeProfit2",  "REAL",    "0");
                await TryAddColumnAsync(db,         "ExchangeOrders", "Leverage",     "INTEGER", "1");
                await TryAddColumnAsync(db,         "ExchangeOrders", "MarketType",   "INTEGER", "0");
                // ExchangeOrders — TP partial close % (v1.15)
                await TryAddColumnAsync(db,         "ExchangeOrders", "Tp1ClosePct",  "REAL",    "100");
                await TryAddColumnAsync(db,         "ExchangeOrders", "Tp2ClosePct",  "REAL",    "100");

                // WatchedSetups — instapcandle timestamp (v1.31)
                await TryAddNullableColumnAsync(db, "WatchedSetups", "EntryAt", "TEXT");

                // Backfill EntryAt voor bestaande Won/Lost/Open setups die geen EntryAt hebben.
                // Beste benadering: gebruik AddedAt (aanmaaktijdstip setup) als instaptijdstip.
                // Status: Won=1, Lost=2, Open=4  |  Watching=0 en Expired=3 NIET backfillen.
                await db.ExecuteSqlRawAsync(@"
                    UPDATE WatchedSetups
                    SET    EntryAt = AddedAt
                    WHERE  EntryAt IS NULL
                    AND    Status  IN (1, 2, 4)");

                await db.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS SentimentReadings (
                        Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                        CoinId INTEGER NOT NULL,
                        Source TEXT NOT NULL,
                        SentimentScore REAL NOT NULL DEFAULT 0,
                        Confidence REAL NOT NULL DEFAULT 0,
                        MentionCount INTEGER NOT NULL DEFAULT 0,
                        Timestamp TEXT NOT NULL,
                        RawSnippet TEXT NOT NULL DEFAULT '',
                        FOREIGN KEY (CoinId) REFERENCES Coins(Id) ON DELETE CASCADE
                    )");

                await db.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS Signals (
                        Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                        CoinId INTEGER NOT NULL,
                        NarrativeId INTEGER,
                        Timeframe TEXT NOT NULL DEFAULT 'OneDay',
                        TaScore REAL NOT NULL DEFAULT 0,
                        SentimentScore REAL NOT NULL DEFAULT 0,
                        MarketRegimeMultiplier REAL NOT NULL DEFAULT 1,
                        CombinedScore REAL NOT NULL DEFAULT 0,
                        Direction TEXT NOT NULL DEFAULT 'Flat',
                        Reasoning TEXT NOT NULL DEFAULT '',
                        CreatedAt TEXT NOT NULL,
                        Acknowledged INTEGER NOT NULL DEFAULT 0,
                        ActedOn INTEGER NOT NULL DEFAULT 0,
                        FOREIGN KEY (CoinId) REFERENCES Coins(Id) ON DELETE CASCADE,
                        FOREIGN KEY (NarrativeId) REFERENCES Narratives(Id) ON DELETE SET NULL
                    )");

                await db.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS SignalRules (
                        Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                        NarrativeId INTEGER,
                        Name TEXT NOT NULL DEFAULT '',
                        IndicatorConditionsJson TEXT NOT NULL DEFAULT '{{}}',
                        SentimentThreshold REAL NOT NULL DEFAULT 0,
                        ScoreThreshold REAL NOT NULL DEFAULT 60,
                        IsActive INTEGER NOT NULL DEFAULT 1,
                        FOREIGN KEY (NarrativeId) REFERENCES Narratives(Id) ON DELETE SET NULL
                    )");

                await db.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS ExchangeOrders (
                        Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                        SignalId INTEGER,
                        Exchange TEXT NOT NULL,
                        Symbol TEXT NOT NULL DEFAULT '',
                        Side TEXT NOT NULL DEFAULT 'Buy',
                        Type TEXT NOT NULL DEFAULT 'Market',
                        Qty REAL NOT NULL DEFAULT 0,
                        Entry REAL NOT NULL DEFAULT 0,
                        StopLoss REAL NOT NULL DEFAULT 0,
                        TakeProfit REAL NOT NULL DEFAULT 0,
                        Status TEXT NOT NULL DEFAULT 'Pending',
                        ExternalOrderId TEXT NOT NULL DEFAULT '',
                        IsPaper INTEGER NOT NULL DEFAULT 1,
                        CreatedAt TEXT NOT NULL,
                        FilledAt TEXT,
                        FOREIGN KEY (SignalId) REFERENCES Signals(Id) ON DELETE SET NULL
                    )");

                await db.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS FearGreedReadings (
                        Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                        Value INTEGER NOT NULL DEFAULT 0,
                        Classification TEXT NOT NULL DEFAULT '' CHECK(length(Classification) <= 50),
                        Timestamp TEXT NOT NULL
                    )");

                await db.ExecuteSqlRawAsync(@"
                    CREATE INDEX IF NOT EXISTS IX_FearGreedReadings_Timestamp
                    ON FearGreedReadings(Timestamp)");

                await db.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS WatchlistCoins (
                        Id       INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                        ApiId    TEXT    NOT NULL UNIQUE,
                        Name     TEXT    NOT NULL DEFAULT '',
                        Symbol   TEXT    NOT NULL DEFAULT '',
                        ImageUri TEXT    NOT NULL DEFAULT '',
                        AddedAt  TEXT    NOT NULL
                    )");

                // Setup Tracker table (v1.30)
                await db.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS WatchedSetups (
                        Id             INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                        CoinApiId      TEXT    NOT NULL DEFAULT '',
                        CoinName       TEXT    NOT NULL DEFAULT '',
                        CoinSymbol     TEXT    NOT NULL DEFAULT '',
                        ImageUri       TEXT    NOT NULL DEFAULT '',
                        Direction      TEXT    NOT NULL DEFAULT '',
                        EntryPrice     REAL    NOT NULL DEFAULT 0,
                        StopLoss       REAL    NOT NULL DEFAULT 0,
                        Target1        REAL    NOT NULL DEFAULT 0,
                        Target2        REAL    NOT NULL DEFAULT 0,
                        Score          INTEGER NOT NULL DEFAULT 0,
                        PatternSummary TEXT    NOT NULL DEFAULT '',
                        Bias1D         TEXT    NOT NULL DEFAULT '',
                        Bias4H         TEXT    NOT NULL DEFAULT '',
                        AddedAt        TEXT    NOT NULL,
                        Status         INTEGER NOT NULL DEFAULT 0,
                        ClosePrice     REAL,
                        ClosedAt       TEXT
                    )");
                await db.ExecuteSqlRawAsync(@"
                    CREATE INDEX IF NOT EXISTS IX_WatchedSetups_Status
                    ON WatchedSetups(Status)");
                await db.ExecuteSqlRawAsync(@"
                    CREATE INDEX IF NOT EXISTS IX_WatchedSetups_CoinApiId
                    ON WatchedSetups(CoinApiId)");

                Logger?.Information("PLUS schema applied successfully");
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "ApplyPlusSchemaAsync failed");
            }
        }

        private async Task TryAddColumnAsync(Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade db, string table, string column, string type, string defaultVal)
        {
            try
            {
                await db.ExecuteSqlRawAsync($"ALTER TABLE {table} ADD COLUMN \"{column}\" {type} NOT NULL DEFAULT {defaultVal}");
                Logger?.Information("PLUS schema: added {Table}.{Col}", table, column);
            }
            catch (Exception ex) when (ex.Message.Contains("duplicate column name") || ex.Message.Contains("already exists"))
            {
                // Column already present — idempotent, safe to ignore
            }
        }

        /// <summary>Add a nullable column (no NOT NULL constraint, defaults to NULL). Idempotent.</summary>
        private async Task TryAddNullableColumnAsync(Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade db, string table, string column, string type)
        {
            try
            {
                await db.ExecuteSqlRawAsync($"ALTER TABLE {table} ADD COLUMN \"{column}\" {type}");
                Logger?.Information("PLUS schema: added nullable {Table}.{Col}", table, column);
            }
            catch (Exception ex) when (ex.Message.Contains("duplicate column name") || ex.Message.Contains("already exists"))
            {
                // Column already present — idempotent, safe to ignore
            }
        }

        private async Task RepairCoinIsAssetStatus()
        {
            var context = Context;
            if (context == null) return;
            context.ChangeTracker.Clear();

            try
            {
                var coins = await context.Coins.ToListAsync();

                foreach (var coin in coins)
                {
                    var isAsset = Context.Assets.Any(a => a.Coin.ApiId == coin.ApiId);
                    if (coin.IsAsset != isAsset)
                    {
                        coin.IsAsset = isAsset;
                        Context.Update(coin);
                    }
                }
                await Context.SaveChangesAsync();
            }
            finally
            {
                context.ChangeTracker.Clear();
            }

        }

        private static void CreateRestorePoint(string portfolioSignature)
        {
            string backupName;

            var backupFolder = Path.Combine(AppConstants.PortfoliosPath, portfolioSignature, AppConstants.BackupFolder);

            var searchPatternVersion = $"*{AppConstants.ProductVersion.Replace(".", "-")}*";

            try
            {
                //Check if RestorePoint for this version already exists
                Directory.CreateDirectory(backupFolder); //ensure directory exists

                var files = Directory.GetFiles(backupFolder, searchPatternVersion);

                if (files.Any())
                {
                    var standardBackupFiles = Directory.GetFiles(backupFolder, "*_s_*");

                    // Sort the files by LastWriteTime
                    var sortedFiles = standardBackupFiles
                        .Select(file => new FileInfo(file))
                        .OrderBy(fileInfo => fileInfo.LastWriteTime)
                        .ToArray();

                    if (sortedFiles.Length > 0)
                    {
                        var timeSpan = DateTime.Now - sortedFiles[sortedFiles.Length - 1].LastWriteTime;

                        // do not create a new restore point if the last one was created less than 2 days ago
                        if (timeSpan.Days < 2) return;

                        if (sortedFiles.Length > 5)
                        {
                            //delete the oldest one, to keep no more then 5 regular restore files
                            File.Delete(sortedFiles[0].FullName);
                        }
                    }
                    backupName = $"{AppConstants.PrefixBackupName}_s_{DateTime.Now:yyyyMMdd-HHmmss}.{AppConstants.ExtentionBackup}";
                }
                else
                {
                    backupName = $"{AppConstants.PrefixBackupName}_{AppConstants.ProductVersion.Replace(".", "-")}_{DateTime.Now:yyyyMMdd-HHmmss}.{AppConstants.ExtentionBackup}";
                }

                var saveResult = SaveRestorePoint(portfolioSignature, backupName);
                saveResult.Match(
                    Right: succ =>
                    {
                        Logger.Information("Restore Point created successfully for {0}", portfolioSignature);
                    },
                    Left: err =>
                    {
                        Logger.Error("Failed to create Restore Point for {0}: {1}", portfolioSignature, err.Message);
                    });
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "BackUp CPT files failed!");
            }
        }

        /// <summary>
        /// Creates a restore point of the specified portfolio by copying its database and related files to a temporary folder,
        /// then compressing the folder into a zip file.
        /// </summary>
        /// <param name="portfolioSignature">The unique identifier of the portfolio to back up.</param>
        /// <param name="backupName">The name of the backup file to create, without the path.</param>
        /// <returns>
        /// An Either type containing an Error if the operation fails, or a boolean value indicating success.
        /// </returns>
        public static Either<Error, bool> SaveRestorePoint(string portfolioSignature, string backupName)
        {
            try
            {
                var tempWithSignatureFolder = Path.Combine(AppConstants.PortfoliosPath, "Temp", portfolioSignature);
                var tempFolder = Path.Combine(AppConstants.PortfoliosPath, "Temp");
                var dbFile = Path.Combine(AppConstants.PortfoliosPath, portfolioSignature, AppConstants.DbName);
               // var pidFile = Path.Combine(AppConstants.PortfoliosPath, portfolioSignature, "pid.json");
               // var preferencesFile = Path.Combine(AppConstants.AppDataPath, AppConstants.PrefFileName);
                var graphFile = Path.Combine(AppConstants.PortfoliosPath, portfolioSignature, "graph.json");
                var backupFolder = Path.Combine(AppConstants.PortfoliosPath, portfolioSignature, AppConstants.BackupFolder);

                MkOsft.DirectoryCreate(tempFolder, true);
                MkOsft.DirectoryCreate(tempWithSignatureFolder, true);

                MkOsft.FileCopy(dbFile, tempWithSignatureFolder);
               // MkOsft.FileCopy(preferencesFile, tempWithSignatureFolder);
                MkOsft.FileCopy(graphFile, tempWithSignatureFolder);
                //MkOsft.FileCopy(pidFile, tempWithSignatureFolder);

                Directory.CreateDirectory(AppConstants.ChartsFolder); //ensure folder is present
                MkOsft.DirectoryCopy(AppConstants.ChartsFolder, Path.Combine(tempFolder, "MarketCharts"), true);

                //first create tempZipFile to avoid the exception of the file already exists
                //then move it
                string tempZipPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".cpt");
                ZipFile.CreateFromDirectory(tempFolder, tempZipPath);
                File.Move(tempZipPath, Path.Combine(backupFolder, backupName), true);

                Directory.Delete(tempFolder, true);
            }
            catch (Exception ex)
            {
                return Error.New(ex);
            }
            return true;
        }


        /// <summary>
        /// Creates a backup of the specified portfolio by copying its database and related files to a temporary folder,
        /// then compressing the folder into a zip file.
        /// </summary>
        /// <param name="portfolioSignature">The unique identifier of the portfolio to back up.</param>
        /// <param name="fileName">Full path and name of the backup file to create.</param>
        /// <returns>
        /// An Either type containing an Error if the operation fails, or a boolean value indicating success.
        /// </returns>
        public static Either<Error, bool> SaveBackup(string portfolioSignature, string fileName)
        {
            try
            {
                var tempWithSignatureFolder = Path.Combine(AppConstants.PortfoliosPath, "Temp", portfolioSignature);
                var tempFolder = Path.Combine(AppConstants.PortfoliosPath, "Temp");
                var dbFile = Path.Combine(AppConstants.PortfoliosPath, portfolioSignature, AppConstants.DbName);
                var pidFile = Path.Combine(AppConstants.PortfoliosPath, portfolioSignature, "pid.json");
                // var preferencesFile = Path.Combine(AppConstants.AppDataPath, AppConstants.PrefFileName);
                var graphFile = Path.Combine(AppConstants.PortfoliosPath, portfolioSignature, "graph.json");
                var backupFolder = Path.Combine(AppConstants.PortfoliosPath, portfolioSignature, AppConstants.BackupFolder);

                MkOsft.DirectoryCreate(tempFolder, true);
                MkOsft.DirectoryCreate(tempWithSignatureFolder, true);

                MkOsft.FileCopy(dbFile, tempWithSignatureFolder);
                // MkOsft.FileCopy(preferencesFile, tempWithSignatureFolder);
                MkOsft.FileCopy(graphFile, tempWithSignatureFolder);
                MkOsft.FileCopy(pidFile, tempWithSignatureFolder);

                Directory.CreateDirectory(AppConstants.ChartsFolder); //ensure folder is present
                MkOsft.DirectoryCopy(AppConstants.ChartsFolder, Path.Combine(tempFolder, "MarketCharts"), true);

                //first create tempZipFile to avoid the exception of the file already exists
                //then move it
                string tempZipPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".bak");
                ZipFile.CreateFromDirectory(tempFolder, tempZipPath);
                File.Move(tempZipPath, fileName, true);
                
                Directory.Delete(tempFolder, true);
            }
            catch (Exception ex)
            {
                return Error.New(ex);
            }
            return true;
        }


        private static async Task LoadPortfoliosAsync(string fileName, Func<FileStream, Task> processStream)
        {
            if (File.Exists(fileName))
            {
                try
                {
                    using FileStream openStream = File.OpenRead(fileName);
                    await processStream(openStream);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to de-serialize data from {0}", fileName);
                }
            }
        }

        private static async Task SavePortfoliosAsync(string fileName, Func<FileStream, Task> processStream)
        {
            try
            {
                using FileStream createStream = File.Create(fileName);
                await processStream(createStream);
                Logger.Information("Portfolios data serialized successfully.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to serialize Portfolio data to {0}", fileName);
            }
        }

        private static ObservableCollection<Portfolio> GetPortfoliosFromFolders()
        {
            var dirs = Directory.GetDirectories(AppConstants.PortfoliosPath);
            var portfolios = new ObservableCollection<Portfolio>();

            foreach (var dir in dirs)
            {
                var pidFilePath = Path.Combine(dir, "pid.json");
                string portfolioId = GetPidFromJson(pidFilePath); ;
                string signature = Path.GetRelativePath(AppConstants.PortfoliosPath, dir);

                if (signature == AppConstants.DefaultDuressPortfolioGuid) continue; //don't add the duress portfolio
                portfolios.Add(new Portfolio
                {
                    Name = portfolioId,
                    Signature = signature
                });
            }
            return portfolios;
        }

        private async Task<bool> MigrateFolderStructureIfNeeded()
        {
            bool alreadyMigrated = true;

            if (!Directory.Exists(AppConstants.PortfoliosPath))
            {
                string initialPortfolioFolder = AppConstants.DefaultPortfolioGuid;
                string fullPortfolioPath = Path.Combine(AppConstants.PortfoliosPath, initialPortfolioFolder);
                string oldBackupPath = Path.Combine(AppConstants.AppDataPath, AppConstants.BackupFolder);
                string newBackupPath = Path.Combine(fullPortfolioPath, "Backup");

                alreadyMigrated = false;

                Directory.CreateDirectory(fullPortfolioPath);
                Directory.CreateDirectory(AppConstants.ChartsFolder);
                Directory.CreateDirectory(newBackupPath);

                if (!IsBlankInstall())
                {
                    MkOsft.FileMove(Path.Combine(AppConstants.AppDataPath ,"sqlCPT.db"), fullPortfolioPath);
                    MkOsft.FileMove(Path.Combine(AppConstants.ChartsFolder, "graph.json"), fullPortfolioPath);
                    MkOsft.FileMove(Path.Combine(AppConstants.ChartsFolder, "graph.json.bak"), fullPortfolioPath);
                    MkOsft.DirectoryMove(oldBackupPath, newBackupPath, true);
                    MkOsft.FilesDelete("*_backup_*", AppConstants.AppDataPath);
                    RenameBackupFilesToRestorePoints(newBackupPath);
                }

                var portfolio = new Portfolio
                {
                    Name = "Default Portfolio",
                    Signature = initialPortfolioFolder
                };
                Portfolios.Add(portfolio);

                SavePidToJson(portfolio, true);

                string portfoliosFile = Path.Combine(AppConstants.PortfoliosPath, AppConstants.PortfoliosFileName);
                await SavePortfoliosAsync(portfoliosFile, async stream =>
                {
                    await JsonSerializer.SerializeAsync(stream, Portfolios);
                });
            }
            return alreadyMigrated;
        }

        private static void RenameBackupFilesToRestorePoints(string newBackupPath)
        {
            var files = Directory.GetFiles(newBackupPath, $"*.{AppConstants.ExtentionBackup}");
            foreach (var file in files)
            {
                var newFileName = file.Replace("CPTbackup", "RestorePoint");
                File.Move(file, newFileName);
            }
        }

        private static void SavePidToJson(Portfolio portfolio, bool isHidden = false)
        {
            var portfolioNameObject = new { PortfolioName = portfolio.Name };

            var path = Path.Combine(AppConstants.PortfoliosPath, portfolio.Signature);

            string jsonString = JsonSerializer.Serialize(portfolioNameObject, new JsonSerializerOptions { WriteIndented = true });
            string filePath = Path.Combine(path, "pid.json");

            try
            {
                // ensure that the file is not hidden
                MkOsft.MakeFileHidden(filePath, true);
                File.WriteAllText(filePath, jsonString);
                if (isHidden) MkOsft.MakeFileHidden(filePath);
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Failed to save pid.json file at {0}", filePath);
            }
        }

        private static bool IsBlankInstall()
        {
            var dbFilePath = Path.Combine(AppConstants.AppDataPath, AppConstants.DbName);
            return !Directory.Exists(AppConstants.PortfoliosPath) && !File.Exists(dbFilePath);
        }

        public static ObservableCollection<Backup> GetBackups(string portfolioSignature)
        {
            List<Backup> backups = new();
            var backupPath = Path.Combine(AppConstants.PortfoliosPath, portfolioSignature, AppConstants.BackupFolder);
            if (Directory.Exists(backupPath))
            {
                var files = Directory.GetFiles(backupPath, $"*.{AppConstants.ExtentionBackup}");
                foreach (var file in files)
                {
                    var backup = new Backup { FileName = Path.GetFileName(file), BackupDate = File.GetCreationTime(file) };
                    backups.Add(backup);
                }
                return new ObservableCollection<Backup>(backups.OrderByDescending(x => x.BackupDate).ToList());
            }
            else return new();
        }

        public bool DoesPortfolioNameExist(string name)
        {
            try
            {
                string normalizedName = MkOsft.NormalizeName(name);
                return Portfolios.Any(x => x.Name.ToLower() == normalizedName.ToLower());
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<Either<Error,bool>> RenamePortfolio(Portfolio oldPortfolio, Portfolio newPortfolio)
        {
            try
            {
                var portfolio = Portfolios.FirstOrDefault(p => p.Name.ToLower() == oldPortfolio.Name.ToLower());
                if (portfolio != null)
                {
                    portfolio.Name = newPortfolio.Name;
                    await SavePortfoliosToJson();
                    SavePidToJson(portfolio, true);

                    // Notify the UI that the collection has changed
                    var index = Portfolios.IndexOf(portfolio);
                    Portfolios.RemoveAt(index);
                    Portfolios.Insert(index, portfolio);

                    //check if the name of the current portfolio was changed
                    if (CurrentPortfolio.Name == newPortfolio.Name)
                    {
                        OnPropertyChanged("CurrentPortfolio");
                        _appSettings.LastPortfolio = CurrentPortfolio;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                return Error.New(ex);
            }
        }

        public async Task<Either<Error, bool>> AddPortfolio(Portfolio portfolio, bool needNewSignature = true)
        {
            try
            {
                if (portfolio != null)
                {
                    if (portfolio.Signature == string.Empty || needNewSignature)
                    {
                        portfolio.Signature = Guid.NewGuid().ToString();
                    }
                    var path = Path.Combine(AppConstants.PortfoliosPath, portfolio.Signature);
                    var backupPath = Path.Combine(path, AppConstants.BackupFolder);

                    MkOsft.DirectoryCreate(backupPath);
                    SavePidToJson(portfolio, true);

                    Portfolios.Add(portfolio);
                    await SavePortfoliosToJson();
                }
                return true;
            }
            catch (Exception ex)
            {
                return Error.New(ex);
            }
        }

        public async Task<Either<Error, bool>> DeletePortfolio(Portfolio portfolio)
        {
            try
            {
                if (portfolio != null)
                {
                    var path = Path.Combine(AppConstants.PortfoliosPath, portfolio.Signature);
                    MkOsft.DirectoryDelete(path, true);
                    Portfolios.Remove(portfolio);
                    await SavePortfoliosToJson();
                    
                }
                return true;
            }
            catch (Exception ex)
            {
                return Error.New(ex);
            }
        }
        private async Task SavePortfoliosToJson()
        {
            string portfoliosFile = Path.Combine(AppConstants.PortfoliosPath, AppConstants.PortfoliosFileName);
            await SavePortfoliosAsync(portfoliosFile, async stream =>
            {
                await JsonSerializer.SerializeAsync(stream, Portfolios);
                Logger.Information("Portfolios data serialized successfully. {0} portfolios)", Portfolios?.Count);
            });
        }

        private async Task<Either<Error,bool>> LoadPortfoliosFromJson()
        {
            try
            {
                string portfoliosFile = Path.Combine(AppConstants.PortfoliosPath, AppConstants.PortfoliosFileName);

                if (!File.Exists(portfoliosFile)) return Error.New("File Not Exists");

                await LoadPortfoliosAsync(portfoliosFile, async stream =>
                {
                    Portfolios = await JsonSerializer.DeserializeAsync<ObservableCollection<Portfolio>>(stream);
                    Logger.Information("Portfolios data de-serialized successfully. {0} portfolios)", Portfolios?.Count);
                });
                return true;
            }
            catch (Exception ex)
            {
                return Error.New(ex);
            }
        }

        internal async Task PauseUpdateServices(bool isDisconnecting = false)
        {
            _priceUpdateService.Pause(isDisconnecting);
            _graphUpdateService.Pause(isDisconnecting);

            while (_priceUpdateService.IsUpdating || _graphUpdateService.IsUpdating)
            {
                await Task.Delay(100);
            }
        }

        internal void ResumeUpdateServices()
        {
            _priceUpdateService.Resume();
            _graphUpdateService.Resume();
        }

        public Result<bool> DeleteRestorePoint(string portfolioSignature, string fileName)
        {
            if (portfolioSignature == string.Empty || fileName == string.Empty) return new Result<bool>(false);
            try
            {
                var backupPath = Path.Combine(AppConstants.PortfoliosPath, portfolioSignature, AppConstants.BackupFolder);
                var filePath = Path.Combine(backupPath, fileName);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to remove restore point {fileName} from portfolio {portfolioSignature}");
                return new Result<bool>(ex);
            }
            
        }

        internal async Task<Either<Error, bool>> CopyPortfolio(Portfolio portfolio, Portfolio newPortfolio, bool needPartialCopy)
        {
            newPortfolio.Signature = Guid.NewGuid().ToString();
            var destSignaturePath = Path.Combine(AppConstants.PortfoliosPath, newPortfolio.Signature);
            var sourceSignaturePath = Path.Combine(AppConstants.PortfoliosPath, portfolio.Signature);

            try
            {
                MkOsft.DirectoryCopy(sourceSignaturePath, destSignaturePath, false);
                SavePidToJson(newPortfolio, true);
                Portfolios.Add(newPortfolio);
                await SavePortfoliosToJson();

                if (needPartialCopy)
                {
                    // use UpdateContext to temporarely connect with the new portfolio
                    // for that pause both UpdateServices
                    await PauseUpdateServices();
                    DisconnectUpdateContext();
                    await ConnectUpdateContext(newPortfolio);

                    await RemoveAssetsFromPortfolio(UpdateContext);
                    RemoveGraphJson(destSignaturePath);

                    DisconnectUpdateContext();
                    await ConnectUpdateContext(portfolio);
                    ResumeUpdateServices();
                }
                return true;
            }
            catch (Exception ex)
            {
                return Error.New(ex);
            }
        }

        public async Task<Either<Error, Portfolio>> AddDuressPortfolio()
        {
            try
            {
                var portfolio = new Portfolio
                {
                    Name = "Default Portfolio",
                    Signature = AppConstants.DefaultDuressPortfolioGuid
                };

                var path = Path.Combine(AppConstants.PortfoliosPath, portfolio.Signature);
                var backupPath = Path.Combine(path, AppConstants.BackupFolder);

                MkOsft.DirectoryCreate(backupPath);
                SavePidToJson(portfolio, true);
                return portfolio;
            }
            catch (Exception ex)
            {
                return Error.New(ex);
            }
        }

        private void RemoveGraphJson(string destSignaturePath)
        {
            var graphJsonPath = Path.Combine(destSignaturePath, "graph.json");
            var graphJsonBakPath = Path.Combine(destSignaturePath, "graph.json.bak");

            if (File.Exists(graphJsonPath))
            {
                File.Delete(graphJsonPath);
            }
            if (File.Exists(graphJsonBakPath))
            {
                File.Delete(graphJsonBakPath);
            }
        }

        private async Task RemoveAssetsFromPortfolio(UpdateContext context)
        {
            if (context == null) { return; };

            try
            {
                context.ChangeTracker?.Clear();

                context.Mutations.RemoveRange(context.Mutations);
                context.Transactions.RemoveRange(context.Transactions);
                context.Assets.RemoveRange(context.Assets);
                foreach(var coin in context.Coins)
                {
                    coin.IsAsset = false;
                }
                await context.SaveChangesAsync();
            }
            finally
            {
                context.ChangeTracker?.Clear();
            }
        }

        public Task<Either<Error, bool>> ConnectUpdateContext(Portfolio portfolio)
        {
            try
            {
                var relativePath = Path.GetRelativePath(AppConstants.AppDataPath, Path.Combine(AppConstants.PortfoliosPath, portfolio.Signature, AppConstants.DbName));
                UpdateContext = _updateContextFactory.Create($"Data Source=|DataDirectory|{relativePath}");
                Logger.Information($"UpdateContext Connected to {portfolio.Signature}");
                return Task.FromResult<Either<Error, bool>>(true);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to connect UpdateContext to {portfolio.Signature}");
                return Task.FromResult<Either<Error, bool>>(Error.New(ex));
            }
        }

        public Either<Error, bool> DisconnectUpdateContext()
        {
            try
            {
                UpdateContext?.Database.CloseConnection();
                UpdateContext?.Dispose();
                UpdateContext = null;

                GC.Collect();
                GC.WaitForPendingFinalizers();
                Logger.Information($"UpdateContext Disconnected");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to disconnect UpdateContext.");
                return Error.New(ex);
            }
        }

        internal static string GetPidFromJson(string? pidFile)
        {
            string pid = string.Empty;
            try
            {
                if (File.Exists(pidFile))
                {
                    var jsonString = File.ReadAllText(pidFile);
                    var jsonDoc = JsonDocument.Parse(jsonString);
                    if (jsonDoc.RootElement.TryGetProperty("PortfolioName", out var nameElement))
                    {
                        pid = nameElement.GetString() ?? pid;
                    }
                }
                return pid;
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Failed to read or parse pid.json in file {0}", pidFile);
                return string.Empty;
            }
        }

        internal Portfolio GetPortfolioBySignature(string signature)
        {
           return Portfolios.Where(x => x.Signature == signature).FirstOrDefault();
        }
    }
}