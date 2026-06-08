using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

public interface IFundamentalsService
{
    /// <summary>Haalt de fundamentals van één coin op bij CoinGecko, scoort en slaat ze op.</summary>
    Task<CoinFundamentals?> RefreshAsync(string apiId, string symbol, string name, CancellationToken ct = default);

    /// <summary>Ververst de fundamentals van alle coins in de bibliotheek (rate-limited).</summary>
    Task<int> RefreshAllAsync(IProgress<(int done, int total, string status)>? progress = null, CancellationToken ct = default);

    /// <summary>Alle opgeslagen fundamentals (voor de overzichtspagina).</summary>
    Task<List<CoinFundamentals>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Opgeslagen fundamentals voor één coin, of null.</summary>
    Task<CoinFundamentals?> GetAsync(string apiId, CancellationToken ct = default);

    /// <summary>Slaat de handmatige due-diligence-velden op en herberekent de totaalscore.</summary>
    Task SaveDueDiligenceAsync(CoinFundamentals fundamentals, CancellationToken ct = default);
}
