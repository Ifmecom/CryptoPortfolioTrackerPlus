using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CryptoPortfolioTracker.Configuration;
using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Core;

namespace CryptoPortfolioTracker.Services;

/// <summary>Automatisch handelen op basis van Pattern Trading-scans (v1.47, alleen Bybit Demo).</summary>
public interface IAutoTraderService
{
    /// <summary>
    /// Beoordeelt de scan en plaatst — als automatisch handelen aan staat — de beste Long-setups op
    /// Bybit Demo. Retourneert alle besluiten (ook de afgewezen kandidaten). Faalt nooit hard.
    /// </summary>
    Task<IReadOnlyList<AutoTradeDecision>> ProcessPatternScanAsync(
        IEnumerable<PatternCoinAnalysis> results, CancellationToken ct = default);
}

/// <summary>
/// Plaatst automatisch orders op Bybit EU Demo na een Pattern Trading-scan (v1.47). De keuze is puur
/// (<see cref="AutoTradeSelector"/>); de positiegrootte volgt uit het risico per trade
/// (<see cref="PositionSizeCalculator"/>) met een maximum per positie; de risk-guardrails gelden
/// via <see cref="ITradeService.PlaceLiveAsync"/>. Live geld is uitgesloten: deze service gebruikt
/// uitsluitend <see cref="ExchangeKind.BybitDemo"/>.
/// </summary>
public class AutoTraderService : IAutoTraderService
{
    private static readonly ILogger Logger = Log.Logger.ForContext(
        Constants.SourceContextPropertyName, nameof(AutoTraderService).PadRight(22));

    public const string AutoTag = "[AUTO]";

    private readonly Settings           _settings;
    private readonly PortfolioService   _portfolio;
    private readonly ITradeService      _trade;
    private readonly ILiveOrderExecutor _live;
    private readonly INotifierService?  _notifier;

    public AutoTraderService(
        Settings           settings,
        PortfolioService   portfolio,
        ITradeService      trade,
        ILiveOrderExecutor live,
        INotifierService?  notifier = null)
    {
        _settings  = settings;
        _portfolio = portfolio;
        _trade     = trade;
        _live      = live;
        _notifier  = notifier;
    }

    public async Task<IReadOnlyList<AutoTradeDecision>> ProcessPatternScanAsync(
        IEnumerable<PatternCoinAnalysis> results, CancellationToken ct = default)
    {
        if (!_settings.IsAutoTradeEnabled) return Array.Empty<AutoTradeDecision>();

        var ctx = _portfolio.Context;
        if (ctx is null) return Array.Empty<AutoTradeDecision>();

        try
        {
            var byApiId = results
                .Where(r => r?.Coin is not null && r.Setup is not null && !string.IsNullOrWhiteSpace(r.Coin.Symbol))
                .GroupBy(r => r.Coin.ApiId)
                .ToDictionary(g => g.Key, g => g.First());

            var candidates = byApiId.Values.Select(r => new AutoTradeCandidate(
                CoinApiId:  r.Coin.ApiId,
                Symbol:     r.Coin.Symbol.Trim().TrimStart('$').ToUpperInvariant(),
                Direction:  r.PrimaryDirection,
                Score:      r.TradabilityScore,
                Price:      r.Coin.Price,
                Entry:      r.Setup!.EntryPrice,
                StopLoss:   r.Setup.StopLoss,
                Target1:    r.Setup.Target1,
                SetupValid: r.Setup.IsValid)).ToList();

            // Huidige demo-posities en wat er vandaag al automatisch geplaatst is.
            var demoOrders = await ctx.ExchangeOrders.AsNoTracking()
                .Where(o => !o.IsPaper && o.Exchange == ExchangeKind.BybitDemo)
                .Select(o => new { o.Symbol, o.Status, o.CreatedAt, o.Notes })
                .ToListAsync(ct);

            string quote = _settings.BybitQuoteCoin;
            var openSymbols = demoOrders
                .Where(o => o.Status is OrderStatus.Pending or OrderStatus.PartiallyFilled or OrderStatus.Filled)
                .Select(o => o.Symbol.EndsWith(quote, StringComparison.OrdinalIgnoreCase) ? o.Symbol[..^quote.Length] : o.Symbol)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            int placedToday = demoOrders.Count(o => o.CreatedAt >= DateTime.UtcNow.Date && (o.Notes ?? "").Contains(AutoTag));

            var limits = new AutoTradeLimits(true, _settings.AutoTradeMinScore, _settings.AutoTradeMaxPerDay, _settings.MaxOpenPositions);
            var decisions = AutoTradeSelector.Select(candidates, limits, openSymbols, placedToday, openSymbols.Count);

            var picks = decisions.Where(d => d.Place).ToList();
            if (picks.Count == 0)
            {
                Logger.Information("AutoTrader: geen kandidaten ({Count} beoordeeld)", decisions.Count);
                return decisions;
            }

            var lines = new List<string>();
            var final = new List<AutoTradeDecision>(decisions.Where(d => !d.Place));

            foreach (var d in picks)
            {
                if (ct.IsCancellationRequested) break;
                var (ok, text) = await PlaceAsync(d.Candidate, byApiId[d.Candidate.CoinApiId], ct);
                final.Add(d with { Place = ok, Reason = ok ? $"{d.Reason} — {text}" : text });
                lines.Add(ok ? $"✅ {d.Candidate.Symbol}: {text}" : $"❌ {d.Candidate.Symbol}: {text}");
            }

            if (_notifier is not null && lines.Count > 0)
            {
                try { await _notifier.SendAlertAsync("<b>🤖 Automatische demo-orders</b>\n" + string.Join("\n", lines), ct); }
                catch (Exception ex) { Logger.Debug(ex, "AutoTrader: Telegram mislukt"); }
            }
            return final;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "AutoTrader: verwerken van de scan mislukt");
            return Array.Empty<AutoTradeDecision>();
        }
    }

    private async Task<(bool Ok, string Text)> PlaceAsync(
        AutoTradeCandidate c, PatternCoinAnalysis analysis, CancellationToken ct)
    {
        try
        {
            double limit    = AutoTradeSelector.LimitPriceFor(c);
            double entryRef = limit > 0 ? limit : c.Price;

            double capital = (double)await _live.GetAvailableQuoteAsync(ct);
            var size = PositionSizeCalculator.Suggest(capital, _settings.AutoTradeRiskPct, entryRef, c.StopLoss);
            double amount = Math.Min(size.Amount, capital * _settings.AutoTradeMaxPositionPct / 100.0);
            if (!size.IsValid || amount <= 0)
                return (false, "geen geldige positiegrootte (saldo of stop-loss)");

            var req = new OrderRequest(
                ExchangeKind.BybitDemo, OrderSide.Buy, MarketType.Spot,
                limit > 0 ? OrderType.Limit : OrderType.Market,
                Math.Round(amount, 2), limit, c.StopLoss, c.Target1, 0, 1,
                Notes: $"{AutoTag} Pattern Trading · score {c.Score} · risico {_settings.AutoTradeRiskPct:0.#}%");

            var signal = new Signal
            {
                CoinId    = analysis.Coin.Id,
                CreatedAt = DateTime.UtcNow,
                Direction = SignalDirection.Long,
                Reasoning = $"Automatisch — Pattern Trading score {c.Score}",
            };

            var order = await _trade.PlaceLiveAsync(analysis.Coin, signal, req);
            string how = limit > 0 ? $"limit @ {limit:0.########}" : "direct";
            return (true, $"{order.Qty:0.########} {c.Symbol} ({amount:0} {_settings.BybitQuoteCoin}, {how}) · SL {c.StopLoss:0.########} · TP {c.Target1:0.########}");
        }
        catch (Exception ex)
        {
            Logger.Information("AutoTrader: {Symbol} niet geplaatst — {Msg}", c.Symbol, ex.Message);
            return (false, ex.Message);
        }
    }
}
