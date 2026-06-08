using System.Threading;
using System.Threading.Tasks;

namespace CryptoPortfolioTracker.Services;

/// <summary>Total Value Locked + categorie van een DeFi-protocol (DefiLlama).</summary>
public record DefiLlamaInfo(string Name, string Symbol, string GeckoId, string Category, double Tvl, double? Mcap);

public interface IDefiLlamaService
{
    /// <summary>
    /// Zoekt het DefiLlama-protocol dat hoort bij een coin (op CoinGecko-id, met symbool als fallback).
    /// Retourneert null voor coins die geen DeFi-protocol met TVL zijn (bv. BTC, de meeste L1's).
    /// </summary>
    Task<DefiLlamaInfo?> GetInfoAsync(string geckoId, string symbol, CancellationToken ct = default);
}
