using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Voert orders echt uit op een exchange (v1.47). De enige implementatie is nu
/// <see cref="BybitDemoExecutor"/> (Bybit EU Demo Trading, alleen spot). <see cref="TradeService"/>
/// delegeert hiernaartoe voor alle orders die niet paper zijn.
/// </summary>
public interface ILiveOrderExecutor
{
    /// <summary>De exchange waarop deze uitvoerder handelt.</summary>
    ExchangeKind Exchange { get; }

    /// <summary>Plaatst de order op de exchange en slaat hem op als <see cref="ExchangeOrder"/> (IsPaper = false).</summary>
    Task<ExchangeOrder> PlaceAsync(Coin coin, Signal signal, OrderRequest req, CancellationToken ct = default);

    /// <summary>Annuleert een openstaande (nog niet gevulde) order op de exchange.</summary>
    Task<bool> CancelAsync(ExchangeOrder order, CancellationToken ct = default);

    /// <summary>Sluit een open positie: haalt TP/SL weg en verkoopt tegen marktprijs.</summary>
    Task CloseAsync(ExchangeOrder order, CancellationToken ct = default);

    /// <summary>
    /// Synchroniseert alle open orders van deze exchange (vullingen, TP/SL-sluitingen, annuleringen).
    /// Retourneert mensgerichte regels over wat er veranderde.
    /// </summary>
    Task<IReadOnlyList<string>> SyncAsync(CancellationToken ct = default);

    /// <summary>Beschikbaar saldo in de quote-munt (bv. USDC) op de exchange.</summary>
    Task<decimal> GetAvailableQuoteAsync(CancellationToken ct = default);
}
