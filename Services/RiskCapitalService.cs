using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CryptoPortfolioTracker.Configuration;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Core;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Bepaalt de kapitaalbasis voor risico: het virtuele paper-kapitaal (instelbaar) of de werkelijke
/// portfoliowaarde (som van holdings × prijs), afhankelijk van <see cref="Settings.UseRealPortfolioForRisk"/>.
/// </summary>
public class RiskCapitalService : IRiskCapitalService
{
    private static readonly ILogger Logger = Log.Logger.ForContext(
        Constants.SourceContextPropertyName, nameof(RiskCapitalService).PadRight(22));

    private readonly PortfolioService _portfolioService;
    private readonly Settings         _settings;

    public RiskCapitalService(PortfolioService portfolioService, Settings settings)
    {
        _portfolioService = portfolioService;
        _settings         = settings;
    }

    public string BasisLabel => _settings.UseRealPortfolioForRisk ? "echte portfoliowaarde" : "virtueel paper-kapitaal";

    public async Task<double> GetCapitalAsync(CancellationToken ct = default)
    {
        double paper = _settings.PaperVirtualCapital > 0 ? _settings.PaperVirtualCapital : 10_000.0;
        if (!_settings.UseRealPortfolioForRisk) return paper;

        double real = await GetRealPortfolioValueAsync(ct);
        return real > 0 ? real : paper;   // lege echte portfolio → val terug op paper
    }

    public async Task<double> GetRealPortfolioValueAsync(CancellationToken ct = default)
    {
        try
        {
            var ctx = _portfolioService.Context;
            if (ctx is null) return 0;

            var coins = await ctx.Coins.Include(c => c.Assets)
                .Where(c => c.IsAsset)
                .AsNoTracking()
                .ToListAsync(ct);

            return coins.Sum(c => (c.Assets?.Sum(a => a.Qty) ?? 0) * c.Price);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "RiskCapitalService: echte portfoliowaarde bepalen mislukt");
            return 0;
        }
    }
}
