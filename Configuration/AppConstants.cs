using System.Reflection;

namespace CryptoPortfolioTracker.Configuration;

public static class AppConstants
{
    // Static constants
    public const string Url = "https://marcel-osft.github.io/CryptoPortfolioTracker/";
    public const string CoinGeckoApiKey = "";
    public const string ApiPath = "https://api.coingecko.com/api/v3/";
    public const string VersionUrl = "https://marcel-osft.github.io/CryptoPortfolioTracker/current_version.txt";
    public const string DefaultPortfolioGuid = "f52ee1a8-ea8d-4f21-849c-6e6429f88256";
    public const string DefaultDuressPortfolioGuid = "08c1ac97-27e0-4922-93da-320c8a5e08ba";
    public const string ScheduledTaskName = "CryptoPortfolioTrackerPlus MarketCharts Update Task";
    public const string DbName = "sqlCPT.db";
    public const string PrefFileName = "prefs.xml";
    public const string BackupFolder = "Backup";
    public const string PrefixBackupName = "RestorePoint";
    public const string ExtentionBackup = "cpt";
    public const string PortfoliosFileName = "portfolios.json";

    // Runtime-initialized paths (set from startup code / GetAppEnvironmentals)
    public static string AppPath { get; set; } = string.Empty;
    public static string AppDataPath { get; set; } = string.Empty;
    public static string ProductVersion { get; set; } = string.Empty;
    public static string PortfoliosPath { get; set; } = string.Empty;
    public static string IconsPath { get; set; } = string.Empty;
    public static string ChartsFolder { get; set; } = string.Empty;
    public static string ScheduledTaskExe { get; set; } = string.Empty;
    public static string PowerShellScriptPs1 { get; set; } = string.Empty;
    public static string AuthStateFile { get; set; } = string.Empty;


    public static void GetAppEnvironmentals()
    {
        AppConstants.AppPath = System.IO.Path.GetDirectoryName(System.AppContext.BaseDirectory) ?? string.Empty;

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        AppConstants.AppDataPath = Path.Combine(localAppData, "CryptoPortfolioTrackerPlus");

        // One-time migration: copy data from original CryptoPortfolioTracker folder if Plus folder is new
        MigrateDataFolderIfNeeded(Path.Combine(localAppData, "CryptoPortfolioTracker"), AppConstants.AppDataPath);

        if (!Directory.Exists(AppConstants.AppDataPath))
        {
            Directory.CreateDirectory(AppConstants.AppDataPath);
        }

        AppDomain.CurrentDomain.SetData("DataDirectory", AppConstants.AppDataPath);
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        AppConstants.ProductVersion = version is not null ? version.ToString() : string.Empty;

        AppConstants.PortfoliosPath = Path.Combine(AppConstants.AppDataPath, "Portfolios");
        AppConstants.ChartsFolder = Path.Combine(AppConstants.AppDataPath, "MarketCharts");
        AppConstants.PowerShellScriptPs1 = Path.Combine(AppConstants.AppPath, "RegisterScheduledTask.ps1");
        AppConstants.IconsPath = Path.Combine(AppConstants.AppDataPath, "LibraryIcons");
        AppConstants.AuthStateFile = Path.Combine(AppConstants.AppDataPath, "authstate.json");

        if (Debugger.IsAttached)
        {
            // Development mode (running from IDE)
            AppConstants.ScheduledTaskExe = "C:\\Program Files\\Crypto Portfolio Tracker Plus\\MarketChartsUpdateService.exe";
        }
        else
        {
            // Production mode
            AppConstants.ScheduledTaskExe = Path.Combine(AppConstants.AppPath, "MarketChartsUpdateService.exe");
        }
    }

    /// <summary>
    /// Copies all files and subfolders from <paramref name="source"/> to <paramref name="target"/>
    /// the first time the Plus folder does not yet exist. Runs only once — subsequent launches skip this.
    /// </summary>
    private static void MigrateDataFolderIfNeeded(string source, string target)
    {
        // Skip if target already exists (migration already done or fresh install)
        if (Directory.Exists(target)) return;
        // Skip if source doesn't exist either (clean install of Plus)
        if (!Directory.Exists(source)) return;

        try
        {
            CopyDirectory(source, target);
        }
        catch
        {
            // Migration failure is non-fatal — the app will start with an empty data folder
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);

        // Copy all files
        foreach (var file in Directory.GetFiles(source))
        {
            var dest = Path.Combine(target, Path.GetFileName(file));
            File.Copy(file, dest, overwrite: true);
        }

        // Recurse into subdirectories
        foreach (var dir in Directory.GetDirectories(source))
        {
            var destDir = Path.Combine(target, Path.GetFileName(dir));
            CopyDirectory(dir, destDir);
        }
    }

}