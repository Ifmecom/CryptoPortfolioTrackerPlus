using System;
using System.Threading;
using System.Threading.Tasks;
using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

public interface IPortfolioCorrelationService
{
    /// <summary>
    /// Analyseert de correlatie van elke holding met BTC (60 dagrendementen) en aggregeert
    /// dit waarde-gewogen tot een portfolio-diversificatie-oordeel.
    /// </summary>
    Task<PortfolioCorrelationResult> AnalyzeAsync(
        IProgress<(int done, int total, string status)>? progress = null,
        CancellationToken ct = default);
}
