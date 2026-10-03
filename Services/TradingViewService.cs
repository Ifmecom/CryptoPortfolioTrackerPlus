using System.Text;
using Windows.ApplicationModel.DataTransfer;

namespace CryptoPortfolioTracker.Services;

/// <inheritdoc cref="ITradingViewService"/>
public sealed class TradingViewService : ITradingViewService
{
    private static readonly ILogger Logger = Log.Logger.ForContext(Constants.SourceContextPropertyName, nameof(TradingViewService).PadRight(22));

    private readonly Settings _settings;

    public TradingViewService(Settings settings) => _settings = settings;

    public string TickerFor(string? symbol, string? dataSource = null)
        => TradingViewSymbol.For(symbol, dataSource, _settings.TradingViewDefaultExchange,
            // Bybit EU noteert tegen USDC (MiCA); de andere beurzen tegen USDT.
            _settings.TradingViewDefaultExchange == "BYBIT" ? _settings.BybitQuoteCoin : "USDT");

    public async Task<bool> OpenChartAsync(string ticker, string? timeframe = null)
    {
        if (string.IsNullOrWhiteSpace(ticker)) return false;
        var url = TradingViewSymbol.ChartUrl(ticker, timeframe ?? _settings.TradingViewInterval);
        try
        {
            return await Windows.System.Launcher.LaunchUriAsync(new Uri(url));
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "TradingView: grafiek openen mislukt voor {Ticker}", ticker);
            return false;
        }
    }

    public bool CopyToClipboard(string text)
    {
        try
        {
            var dp = new DataPackage();
            dp.SetText(text);
            Clipboard.SetContent(dp);
            Clipboard.Flush();   // blijft beschikbaar na sluiten van de app
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "TradingView: kopiëren naar klembord mislukt");
            return false;
        }
    }

    public async Task<string> SaveAsync(string fileName, string content)
    {
        Directory.CreateDirectory(AppConstants.TradingViewFolder);
        var path = Path.Combine(AppConstants.TradingViewFolder, fileName);
        await File.WriteAllTextAsync(path, content, new UTF8Encoding(false));
        Logger.Information("TradingView: export opgeslagen {Path}", path);
        return path;
    }

    public void RevealInExplorer(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "TradingView: Verkenner openen mislukt");
        }
    }
}
