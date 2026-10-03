namespace CryptoPortfolioTracker.Services;

/// <summary>
/// TradingView-koppeling (v1.48): grafiek openen in de browser, Pine Scripts en watchlists kopiëren of opslaan.
/// TradingView heeft geen openbare API — alles loopt via deeplinks en bestanden die de gebruiker zelf
/// in TradingView plakt of importeert. De inhoud komt uit de pure <see cref="PineScriptGenerator"/>,
/// <see cref="TradingViewWatchlist"/> en <see cref="TradingViewSymbol"/>.
/// </summary>
public interface ITradingViewService
{
    /// <summary>Ticker voor een munt; zonder bekende databron met de standaardbeurs uit Instellingen.</summary>
    string TickerFor(string? symbol, string? dataSource = null);

    /// <summary>Opent de TradingView-grafiek in de standaardbrowser (interval uit Instellingen, of <paramref name="timeframe"/>).</summary>
    Task<bool> OpenChartAsync(string ticker, string? timeframe = null);

    /// <summary>Zet tekst op het klembord. False bij een fout.</summary>
    bool CopyToClipboard(string text);

    /// <summary>Slaat een export op in AppConstants.TradingViewFolder en geeft het volledige pad terug.</summary>
    Task<string> SaveAsync(string fileName, string content);

    /// <summary>Opent Verkenner met het bestand geselecteerd.</summary>
    void RevealInExplorer(string path);
}
