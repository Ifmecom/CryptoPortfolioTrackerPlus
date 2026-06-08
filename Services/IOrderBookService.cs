using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Haalt een momentopname van het orderboek op van de Binance spot-markt.
/// Resultaten worden 60 seconden gecached om rate-limits te vermijden.
/// Geeft null terug als het symbool niet beschikbaar is op Binance.
/// </summary>
public interface IOrderBookService
{
    Task<OrderBookSnapshot?> GetSnapshotAsync(string binanceSymbol, CancellationToken ct = default);
}
