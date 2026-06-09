using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;
using Serilog;
using Serilog.Core;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Aggregeert de bestaande markt-signalen (BTC-regime, Fear &amp; Greed, macro-kalender) tot één
/// gedeelde <see cref="MarketContext"/> voor de context-balk. Gecached om dubbele calls te vermijden.
/// </summary>
public class MarketContextService : IMarketContextService
{
    private static readonly ILogger Logger = Log.Logger.ForContext(
        Constants.SourceContextPropertyName, nameof(MarketContextService).PadRight(22));

    private readonly IMarketRegimeService _regime;
    private readonly IFearGreedService    _fearGreed;
    private readonly IMacroEventService   _macro;

    private const int CacheTtlSec = 300;
    private MarketContext? _cached;
    private DateTime _expiry = DateTime.MinValue;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public MarketContextService(
        IMarketRegimeService regime,
        IFearGreedService    fearGreed,
        IMacroEventService   macro)
    {
        _regime    = regime;
        _fearGreed = fearGreed;
        _macro     = macro;
    }

    public async Task<MarketContext> GetAsync(CancellationToken ct = default)
    {
        if (_cached is not null && DateTime.UtcNow < _expiry) return _cached;

        await _gate.WaitAsync(ct);
        try
        {
            if (_cached is not null && DateTime.UtcNow < _expiry) return _cached;

            MarketRegime regime = MarketRegime.Neutral;
            string regimeSummary = "Regime onbekend";
            try
            {
                var ctx = await _regime.GetRegimeContextAsync(ct);
                regime = ctx.Regime;
                regimeSummary = ctx.Summary;
            }
            catch (Exception ex) { Logger.Warning(ex, "MarketContext: regime laden mislukt"); }

            int fgValue = 0; string fgClass = string.Empty; bool hasFg = false;
            try
            {
                var fg = await _fearGreed.GetCurrentAsync(60);
                if (fg is not null) { fgValue = fg.Value; fgClass = fg.Classification; hasFg = true; }
            }
            catch (Exception ex) { Logger.Warning(ex, "MarketContext: Fear&Greed laden mislukt"); }

            string evType = string.Empty; int evDays = 0; bool hasEv = false;
            try
            {
                var next = _macro.GetUpcoming(14).OrderBy(e => e.Date).FirstOrDefault();
                if (next is not null)
                {
                    evType = next.Type;
                    evDays = Math.Max(0, (int)Math.Round((next.Date.Date - DateTime.UtcNow.Date).TotalDays));
                    hasEv  = true;
                }
            }
            catch (Exception ex) { Logger.Warning(ex, "MarketContext: macro-events laden mislukt"); }

            _cached = new MarketContext(regime, regimeSummary, fgValue, fgClass, hasFg, evType, evDays, hasEv);
            _expiry = DateTime.UtcNow.AddSeconds(CacheTtlSec);
            return _cached;
        }
        finally { _gate.Release(); }
    }
}
