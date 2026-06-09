using System.Threading;
using System.Threading.Tasks;
using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

public interface IMarketContextService
{
    /// <summary>
    /// Levert de gedeelde marktcontext (regime + Fear &amp; Greed + eerstvolgende macro-event).
    /// Resultaat wordt ~5 minuten gecached zodat meerdere tabs het delen zonder extra calls.
    /// </summary>
    Task<MarketContext> GetAsync(CancellationToken ct = default);
}
