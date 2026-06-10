using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Core;

namespace CryptoPortfolioTracker.Services;

public class WatchedSetupService : IWatchedSetupService
{
    private readonly PortfolioService?              _portfolioService;
    private readonly Infrastructure.PortfolioContext? _directCtx;
    private readonly INotifierService?              _notifier;
    private readonly ILogger                        _log;

    public WatchedSetupService(PortfolioService portfolioService, INotifierService? notifier = null)
    {
        _portfolioService = portfolioService;
        _notifier         = notifier;
        _log = Log.Logger.ForContext(
            Constants.SourceContextPropertyName,
            nameof(WatchedSetupService).PadRight(22));
    }

    /// <summary>
    /// Constructor voor unit tests — omzeilt PortfolioService en gebruikt
    /// een rechtstreeks doorgegeven (in-memory) PortfolioContext.
    /// </summary>
    internal WatchedSetupService(Infrastructure.PortfolioContext ctx)
    {
        _directCtx = ctx;
        _log = Log.Logger.ForContext(
            Constants.SourceContextPropertyName,
            nameof(WatchedSetupService).PadRight(22));
    }

    private Infrastructure.PortfolioContext Ctx =>
        _directCtx
        ?? _portfolioService?.Context
        ?? throw new InvalidOperationException("No DB context available.");

    // ── Read ─────────────────────────────────────────────────────────────────

    public async Task<List<WatchedSetup>> GetAllAsync()
        => await Ctx.WatchedSetups
               .OrderByDescending(s => s.AddedAt)
               .ToListAsync();

    public async Task<List<WatchedSetup>> GetWatchingAsync()
        => await Ctx.WatchedSetups
               .Where(s => s.Status == WatchedSetupStatus.Watching)
               .ToListAsync();

    // ── Write ────────────────────────────────────────────────────────────────

    public async Task AddAsync(WatchedSetup setup)
    {
        Ctx.WatchedSetups.Add(setup);
        await Ctx.SaveChangesAsync();
        _log.Information("WatchedSetup added: {Coin} {Dir}", setup.CoinName, setup.Direction);
    }

    public async Task ExpireAsync(int id)
    {
        var s = await Ctx.WatchedSetups.FindAsync(id);
        if (s is null) return;
        s.Status   = WatchedSetupStatus.Expired;
        s.ClosedAt = DateTime.UtcNow;
        await Ctx.SaveChangesAsync();
    }

    public async Task RemoveAsync(int id)
    {
        var s = await Ctx.WatchedSetups.FindAsync(id);
        if (s is null) return;
        Ctx.WatchedSetups.Remove(s);
        await Ctx.SaveChangesAsync();
    }

    public async Task<WatchedSetup?> GetActiveSetupForCoinAsync(string coinApiId, string direction)
        => await Ctx.WatchedSetups
               .Where(s => s.CoinApiId == coinApiId
                        && s.Direction  == direction
                        && (s.Status == WatchedSetupStatus.Watching
                         || s.Status == WatchedSetupStatus.Open))
               .OrderByDescending(s => s.AddedAt)
               .FirstOrDefaultAsync();

    public async Task LinkOrderAsync(int setupId, int orderId)
    {
        var s = await Ctx.WatchedSetups.FindAsync(setupId);
        if (s is null) return;
        s.LinkedOrderId = orderId;
        await Ctx.SaveChangesAsync();
        _log.Information("WatchedSetup {SetupId} linked to ExchangeOrder {OrderId}", setupId, orderId);
    }

    public async Task<List<WatchedSetup>> GetClosedAsync(DateTime? from, DateTime? to)
    {
        var query = Ctx.WatchedSetups
            .Where(s => s.Status == WatchedSetupStatus.Won
                     || s.Status == WatchedSetupStatus.Lost)
            .AsQueryable();

        if (from.HasValue)
            query = query.Where(s => s.ClosedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(s => s.ClosedAt < to.Value);

        return await query.OrderByDescending(s => s.ClosedAt).ToListAsync();
    }

    public async Task<List<ScoreBucketCalibration>> GetScoreCalibrationAsync()
    {
        var closed = await Ctx.WatchedSetups
            .Where(s => s.Status == WatchedSetupStatus.Won
                     || s.Status == WatchedSetupStatus.Lost)
            .AsNoTracking()
            .ToListAsync();
        return SetupOutcomeCalibrator.Calibrate(closed);
    }

    public async Task<bool> ExistsAsync(string coinApiId, string direction)
        => await Ctx.WatchedSetups
               .AnyAsync(s => s.CoinApiId == coinApiId
                           && s.Direction  == direction
                           && (s.Status == WatchedSetupStatus.Watching
                            || s.Status == WatchedSetupStatus.Open));

    public async Task CloseManuallyAsync(int id, WatchedSetupStatus outcome, double closePrice)
    {
        if (outcome != WatchedSetupStatus.Won && outcome != WatchedSetupStatus.Lost)
            throw new ArgumentException("Outcome must be Won or Lost.", nameof(outcome));

        var s = await Ctx.WatchedSetups.FindAsync(id);
        if (s is null) return;

        s.Status     = outcome;
        s.ClosePrice = closePrice;
        s.ClosedAt   = DateTime.UtcNow;
        await Ctx.SaveChangesAsync();
        _log.Information("WatchedSetup manually closed: {Coin} → {Outcome} @ {Price}", s.CoinName, outcome, closePrice);
    }

    // ── Auto price check ─────────────────────────────────────────────────────

    public async Task<int> AutoUpdateStatusesAsync(
        IReadOnlyDictionary<string, double> priceByApiId)
    {
        // Process both Watching (→ Open | Won | Lost) and Open (→ Won | Lost) setups
        var active = await Ctx.WatchedSetups
            .Where(s => s.Status == WatchedSetupStatus.Watching
                     || s.Status == WatchedSetupStatus.Open)
            .ToListAsync();

        int updated = 0;
        foreach (var setup in active)
        {
            if (!priceByApiId.TryGetValue(setup.CoinApiId, out double price)) continue;
            if (price <= 0) continue;

            bool isLong  = setup.Direction == "Long";
            bool hitTP2  = setup.Target2 > 0
                            && (isLong ? price >= setup.Target2 : price <= setup.Target2);
            bool hitTP1  = setup.Target1 > 0
                            && (isLong ? price >= setup.Target1 : price <= setup.Target1);
            bool hitSL   = setup.StopLoss > 0
                            && (isLong ? price <= setup.StopLoss : price >= setup.StopLoss);
            // Entry trigger: for a Long limit order the price must have come DOWN to (or below) entry;
            // for a Short the price must have come UP to (or above) entry.
            bool entryHit = isLong ? price <= setup.EntryPrice
                                   : price >= setup.EntryPrice;

            if (hitTP2 && !setup.Tp2Hit)
            {
                // TP2 hit implies TP1 was also hit — mark as Won + Tp2Hit
                setup.EntryAt    ??= DateTime.UtcNow;   // entry may have been missed; record now
                setup.Status     = WatchedSetupStatus.Won;
                setup.ClosePrice = price;
                setup.ClosedAt   = DateTime.UtcNow;
                setup.Tp2Hit     = true;
                updated++;
                _log.Information("WatchedSetup Won (TP2): {Coin} @ {Price}", setup.CoinName, price);
                await AlertAsync($"🏆 <b>Setup gewonnen (TP2)</b> — {setup.CoinSymbol} {setup.Direction}\nKoers {price:#,0.########} bereikte TP2 {setup.Target2:#,0.########}.");
            }
            else if (hitTP1 && setup.Status != WatchedSetupStatus.Won)
            {
                setup.EntryAt    ??= DateTime.UtcNow;   // entry may have been missed; record now
                setup.Status     = WatchedSetupStatus.Won;
                setup.ClosePrice = price;
                setup.ClosedAt   = DateTime.UtcNow;
                updated++;
                _log.Information("WatchedSetup Won (TP1): {Coin} @ {Price}", setup.CoinName, price);
                await AlertAsync($"🎯 <b>Setup gewonnen (TP1)</b> — {setup.CoinSymbol} {setup.Direction}\nKoers {price:#,0.########} bereikte TP1 {setup.Target1:#,0.########}.");
            }
            else if (hitSL)
            {
                setup.EntryAt    ??= DateTime.UtcNow;   // entry may have been missed; record now
                setup.Status     = WatchedSetupStatus.Lost;
                setup.ClosePrice = price;
                setup.ClosedAt   = DateTime.UtcNow;
                updated++;
                _log.Information("WatchedSetup Lost: {Coin} @ {Price}", setup.CoinName, price);
                await AlertAsync($"🛑 <b>Setup verloren (SL)</b> — {setup.CoinSymbol} {setup.Direction}\nKoers {price:#,0.########} raakte stop-loss {setup.StopLoss:#,0.########}.");
            }
            else if (entryHit && setup.Status == WatchedSetupStatus.Watching)
            {
                // Price reached the entry level → trade is now open / in progress
                setup.EntryAt = DateTime.UtcNow;
                setup.Status  = WatchedSetupStatus.Open;
                updated++;
                _log.Information("WatchedSetup Open (entry hit): {Coin} @ {Price}", setup.CoinName, price);
                await AlertAsync($"📥 <b>Entry geraakt</b> — {setup.CoinSymbol} {setup.Direction}\nKoers {price:#,0.########} bereikte de entry {setup.EntryPrice:#,0.########}; setup is nu In Trade.");
            }
        }

        if (updated > 0)
            await Ctx.SaveChangesAsync();

        return updated;
    }

    /// <summary>Best-effort Telegram-alert; statusovergangen zijn eenmalig dus geen dedupe nodig.</summary>
    private async Task AlertAsync(string htmlMessage)
    {
        if (_notifier is null) return;
        try { await _notifier.SendAlertAsync(htmlMessage); }
        catch { /* alerts mogen de statusupdate nooit laten falen */ }
    }

    // ── Stats ────────────────────────────────────────────────────────────────

    public async Task<SetupStats> GetStatsAsync()
    {
        var all      = await Ctx.WatchedSetups.ToListAsync();
        int total    = all.Count;
        int won      = all.Count(s => s.Status == WatchedSetupStatus.Won);
        int lost     = all.Count(s => s.Status == WatchedSetupStatus.Lost);
        int watching = all.Count(s => s.Status == WatchedSetupStatus.Watching);
        int open     = all.Count(s => s.Status == WatchedSetupStatus.Open);
        int closed   = won + lost;
        double wr    = closed > 0 ? (double)won / closed * 100 : 0;

        return new SetupStats(total, won, lost, watching, open, wr);
    }
}
