using CryptoPortfolioTracker.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CryptoPortfolioTracker.Services;

public interface IFearGreedService
{
    /// <summary>
    /// Returns the most-recent reading from the DB, or fetches + stores a fresh
    /// one from the alternative.me API when the cached value is older than <paramref name="maxAgeMinutes"/>.
    /// </summary>
    Task<FearGreedReading?> GetCurrentAsync(int maxAgeMinutes = 60);

    /// <summary>
    /// Always fetches a fresh value from the alternative.me API and persists it.
    /// </summary>
    Task<FearGreedReading?> FetchAndStoreAsync();

    /// <summary>
    /// Returns the last <paramref name="days"/> days of stored readings (newest first).
    /// </summary>
    Task<List<FearGreedReading>> GetHistoryAsync(int days = 30);
}
