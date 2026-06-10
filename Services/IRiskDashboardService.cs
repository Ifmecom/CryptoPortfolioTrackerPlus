using System.Threading;
using System.Threading.Tasks;
using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

public interface IRiskDashboardService
{
    /// <summary>
    /// Bouwt het risico-overzicht voor het gegeven bereik (paper óf live) uit de open (Filled)
    /// orders, de gerealiseerde dag-P&amp;L en de risk-guardrail-instellingen.
    /// Paper rekent tegen de gekozen kapitaalbasis; live altijd tegen de echte portfoliowaarde.
    /// </summary>
    Task<RiskDashboard> BuildAsync(RiskScope scope = RiskScope.Paper, CancellationToken ct = default);
}
