using System.Threading;
using System.Threading.Tasks;
using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

public interface IRiskDashboardService
{
    /// <summary>
    /// Bouwt het portfolio-brede risico-overzicht uit de open (Filled) orders, de gerealiseerde
    /// dag-P&amp;L en de risk-guardrail-instellingen.
    /// </summary>
    Task<RiskDashboard> BuildAsync(CancellationToken ct = default);
}
