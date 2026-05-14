using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

public interface ITradeService
{
    /// <summary>Place an instant paper trade (no real exchange call).</summary>
    Task<ExchangeOrder> PlacePaperAsync(Coin coin, Signal signal, OrderRequest req);

    /// <summary>Place a real live order on the configured exchange. (Sprint 2)</summary>
    Task<ExchangeOrder> PlaceLiveAsync(Coin coin, Signal signal, OrderRequest req);

    /// <summary>Cancel an open order (paper or live).</summary>
    Task<bool> CancelAsync(ExchangeOrder order);

    /// <summary>Close a paper position at the current market price and record realised P&amp;L.</summary>
    Task<bool> ClosePaperAsync(ExchangeOrder order, double closePrice);

    /// <summary>Close all open paper positions at their current market prices.</summary>
    Task<int> CloseAllPaperAsync(Dictionary<string, double> priceMap);

    /// <summary>Sync live fill statuses from exchange. (Sprint 2)</summary>
    Task SyncFillsAsync();

    /// <summary>Persist a free-text note on an existing order.</summary>
    Task UpdateNotesAsync(int orderId, string notes);
}
