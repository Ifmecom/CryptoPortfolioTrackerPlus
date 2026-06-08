using System.Text;
using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Infrastructure;
using CryptoPortfolioTracker.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Core;

namespace CryptoPortfolioTracker.Services;

public class SignalEngine : ISignalEngine
{
    private static readonly ILogger Logger = Log.Logger.ForContext(Constants.SourceContextPropertyName, nameof(SignalEngine).PadRight(22));

    private readonly PortfolioService     _portfolioService;
    private readonly IIndicatorService    _indicatorService;
    private readonly IMarketRegimeService _marketRegimeService;
    private readonly INotifierService     _notifierService;

    // Combination weights (must sum to 1.0)
    private const double WTa        = 0.6;
    private const double WSentiment = 0.3;
    private const double WRegime    = 0.1;

    public SignalEngine(
        PortfolioService portfolioService,
        IIndicatorService indicatorService,
        IMarketRegimeService marketRegimeService,
        INotifierService notifierService)
    {
        _portfolioService    = portfolioService;
        _indicatorService    = indicatorService;
        _marketRegimeService = marketRegimeService;
        _notifierService     = notifierService;
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    public async Task<List<Signal>> EvaluateAsync(Narrative? watchlist = null, CancellationToken ct = default)
    {
        var context = _portfolioService.Context;
        if (context is null) return new();

        var query = context.Coins.Where(c => c.IsAsset);
        if (watchlist is not null)
            query = query.Where(c => c.Narrative != null && c.Narrative.Id == watchlist.Id);

        var coins  = await query.ToListAsync(ct);
        // Rijker regime: EMA50/200-crossover + BTC-dominantie (i.p.v. enkel-EMA op MarketChart-JSON)
        var regimeCtx = await _marketRegimeService.GetRegimeContextAsync(ct);
        var regime    = regimeCtx.Regime;

        var resultPairs = new List<(Signal Signal, Coin Coin)>();

        foreach (var coin in coins)
        {
            if (ct.IsCancellationRequested) break;
            var signal = await EvaluateCoinCoreAsync(coin, Timeframe.OneDay, regime, context);
            if (signal is not null) resultPairs.Add((signal, coin));
        }

        var results = resultPairs.Select(p => p.Signal).ToList();

        if (results.Count > 0)
            await context.SaveChangesAsync(ct);

        Logger.Information("SignalEngine: {Count} signals evaluated, regime={Regime} ({Summary})",
            results.Count, regime, regimeCtx.Summary);

        // Fire-and-forget notifications (don't block the UI thread)
        _ = _notifierService.NotifySignalsAsync(
            resultPairs.Select(p => (p.Signal, p.Coin.Name, p.Coin.Symbol ?? p.Coin.Name)),
            ct);

        return results;
    }

    public async Task<Signal?> EvaluateCoinAsync(Coin coin, Timeframe tf = Timeframe.OneDay)
    {
        var context = _portfolioService.Context;
        if (context is null) return null;

        var regime = (await _marketRegimeService.GetRegimeContextAsync()).Regime;
        var signal = await EvaluateCoinCoreAsync(coin, tf, regime, context);
        if (signal is not null)
            await context.SaveChangesAsync();
        return signal;
    }

    // -----------------------------------------------------------------------
    // Core evaluation
    // -----------------------------------------------------------------------

    private async Task<Signal?> EvaluateCoinCoreAsync(Coin coin, Timeframe tf, MarketRegime regime, PortfolioContext context)
    {
        try
        {
            var taScore = await _indicatorService.CalculateTaScoreAsync(coin, tf);

            // Normalise sentiment from [-1..+1] → [0..100]
            var sentimentNorm = (coin.LatestSentimentScore + 1.0) * 50.0;

            // Regime reference score
            var regimeScore = regime switch
            {
                MarketRegime.RiskOn  => 75.0,
                MarketRegime.RiskOff => 25.0,
                _                   => 50.0,
            };

            // Weighted combination
            var combined = (taScore.Score * WTa) + (sentimentNorm * WSentiment) + (regimeScore * WRegime);
            combined = Math.Clamp(combined, 0, 100);

            // Determine pre-multiplier direction
            var direction = ScoreToDirection(combined);

            // Apply regime multiplier (scales distance from 50)
            var multiplier = RegimeMultiplier(direction, regime);
            combined = Math.Round(50.0 + ((combined - 50.0) * multiplier), 2);
            combined = Math.Clamp(combined, 0, 100);

            // Re-evaluate direction after multiplier
            direction = ScoreToDirection(combined);

            var signal = new Signal
            {
                CoinId                 = coin.Id,
                Timeframe              = tf,
                TaScore                = taScore.Score,
                SentimentScore         = coin.LatestSentimentScore,
                MarketRegimeMultiplier = multiplier,
                CombinedScore          = combined,
                Direction              = direction,
                Reasoning              = BuildReasoning(taScore, coin.LatestSentimentScore, regime, combined),
                CreatedAt              = DateTime.UtcNow,
                Acknowledged           = false,
                ActedOn                = false,
            };

            context.Signals.Add(signal);

            // Persist score and regime back to Coin row
            coin.LatestSignalScore = combined;
            coin.MarketRegime      = regime;
            context.Coins.Update(coin);

            return signal;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "SignalEngine: failed to evaluate {CoinName}", coin.Name);
            return null;
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static SignalDirection ScoreToDirection(double score)
        => score >= 60 ? SignalDirection.Long
         : score <= 40 ? SignalDirection.Short
         : SignalDirection.Flat;

    private static double RegimeMultiplier(SignalDirection direction, MarketRegime regime)
        => (direction, regime) switch
        {
            (SignalDirection.Long,  MarketRegime.RiskOn)  => 1.0,
            (SignalDirection.Long,  MarketRegime.Neutral) => 0.7,
            (SignalDirection.Long,  MarketRegime.RiskOff) => 0.3,
            (SignalDirection.Short, MarketRegime.RiskOn)  => 0.7,
            (SignalDirection.Short, MarketRegime.Neutral) => 1.0,
            (SignalDirection.Short, MarketRegime.RiskOff) => 1.3,
            _                                             => 1.0,
        };

    private static string BuildReasoning(TaScore ta, double sentiment, MarketRegime regime, double combined)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"TA: {ta.Score:F1}  |  Sentiment: {sentiment:+0.000;-0.000;0.000}  |  Regime: {regime}");
        sb.AppendLine($"Combined score: {combined:F1}");
        if (ta.TriggeredRules.Count > 0)
        {
            sb.AppendLine("Triggered TA rules:");
            foreach (var rule in ta.TriggeredRules)
                sb.AppendLine($"  • {rule}");
        }
        return sb.ToString().TrimEnd();
    }
}
