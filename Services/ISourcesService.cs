using CryptoPortfolioTracker.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CryptoPortfolioTracker.Services;

public interface ISourcesService
{
    Task<List<BronSource>> GetAllAsync();
    Task AddAsync(BronSource source);
    Task UpdateAsync(BronSource source);
    Task DeleteAsync(BronSource source);
    Task ToggleActiveAsync(BronSource source);
    Task SeedDefaultsIfEmptyAsync();
}
