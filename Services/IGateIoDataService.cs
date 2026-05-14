using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

public interface IGateIoDataService
{
    Task<List<OhlcvBar>> GetKlinesAsync(string gateSymbol, string interval, int limit = 200);
    string ResolveSymbol(string coinSymbol);
}
