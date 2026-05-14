using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

public interface IMexcDataService
{
    Task<List<OhlcvBar>> GetKlinesAsync(string mexcSymbol, string interval, int limit = 200);
    string ResolveSymbol(string coinSymbol);
}
