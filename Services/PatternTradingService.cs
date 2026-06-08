using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Core;
using Skender.Stock.Indicators;
using System.Text;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Orchestrates multi-timeframe pattern analysis for portfolio coins.
///
/// Data flow per coin:
///   1. Fetch 1D (200), 4H (200) and 1H (100) OHLCV bars from Binance
///      with KuCoin / Gate.io / MEXC fallback.
///   2. Compute TimeframeAnalysis (RSI, EMA, MACD, Bollinger, ADX, ATR)
///      using Skender.Stock.Indicators.
///   3. Run Level-1 indicator patterns + Level-2 OHLCV patterns.
///   4. Calculate tradability score (0–100) via IPatternDetectionService.
///   5. Build TradeSetupAdvice from ATR + key levels.
///   6. Build PatternCoinAnalysis result.
/// </summary>
public class PatternTradingService : IPatternTradingService
{
    private static readonly ILogger Logger = Log.Logger.ForContext(
        Constants.SourceContextPropertyName, nameof(PatternTradingService).PadRight(22));

    private readonly PortfolioService        _portfolio;
    private readonly IPatternDetectionService _detector;
    private readonly IWatchlistService       _watchlist;
    private readonly IBinanceDataService     _binance;
    private readonly IKuCoinDataService      _kuCoin;
    private readonly IGateIoDataService      _gateIo;
    private readonly IMexcDataService        _mexc;

    // Max simultaneous OHLCV fetches to respect exchange rate-limits
    private static readonly SemaphoreSlim _sem = new(3, 3);

    public PatternTradingService(
        PortfolioService        portfolio,
        IPatternDetectionService detector,
        IWatchlistService       watchlist,
        IBinanceDataService     binance,
        IKuCoinDataService      kuCoin,
        IGateIoDataService      gateIo,
        IMexcDataService        mexc)
    {
        _portfolio = portfolio;
        _detector  = detector;
        _watchlist = watchlist;
        _binance   = binance;
        _kuCoin    = kuCoin;
        _gateIo    = gateIo;
        _mexc      = mexc;
    }

    // =========================================================================
    // Public API
    // =========================================================================

    public async Task<List<PatternCoinAnalysis>> AnalyzePortfolioAsync(
        IProgress<int>?   progress = null,
        CancellationToken ct       = default)
    {
        var ctx = _portfolio.Context;
        if (ctx is null)
        {
            Logger.Warning("PatternTrading: portfolio context is null");
            return new List<PatternCoinAnalysis>();
        }

        // ── Portfolio coins (holdings > 0) ──────────────────────────────────
        var coinsWithHoldings = await ctx.Coins
            .AsNoTracking()
            .Include(c => c.Assets)
            .Where(c => c.IsAsset && c.Assets.Any(a => a.Qty > 0))
            .OrderByDescending(c => c.MarketCap)
            .ToListAsync(ct);

        // ── Watchlist coins not already in holdings ──────────────────────────
        var watchlistItems = await _watchlist.GetAllAsync();
        var holdingApiIds  = coinsWithHoldings.Select(c => c.ApiId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Convert watchlist items to lightweight Coin objects for analysis
        var watchlistCoins = watchlistItems
            .Where(w => !holdingApiIds.Contains(w.ApiId))
            .Select(w => new Coin
            {
                ApiId    = w.ApiId,
                Name     = w.Name,
                Symbol   = w.Symbol,
                ImageUri = w.ImageUri,
                IsAsset  = false,
            })
            .ToList();

        // Try to enrich with live price from DB coins library (if tracked)
        var watchlistApiIds = watchlistCoins.Select(w => w.ApiId).ToList();
        var libraryByApiId  = watchlistApiIds.Count > 0
            ? await ctx.Coins.AsNoTracking()
                .Where(c => watchlistApiIds.Contains(c.ApiId))
                .ToDictionaryAsync(c => c.ApiId, ct)
            : new Dictionary<string, Coin>();

        foreach (var wc in watchlistCoins)
        {
            if (libraryByApiId.TryGetValue(wc.ApiId, out var dbCoin))
            {
                wc.Price      = dbCoin.Price;
                wc.Change24Hr = dbCoin.Change24Hr;
                wc.MarketCap  = dbCoin.MarketCap;
                wc.ImageUri   = dbCoin.ImageUri;
            }
        }

        int total = coinsWithHoldings.Count + watchlistCoins.Count;
        Logger.Information(
            "PatternTrading: {Holdings} holding coins + {Watchlist} watchlist coins",
            coinsWithHoldings.Count, watchlistCoins.Count);

        int done = 0;

        // Analyse all coins in parallel (holdings first, then watchlist)
        var holdingTasks = coinsWithHoldings.Select(async coin =>
        {
            await _sem.WaitAsync(ct);
            try
            {
                var result = await AnalyzeCoinAsync(coin);
                result.HasHolding = true;
                progress?.Report((int)Math.Round(Interlocked.Increment(ref done) * 100.0 / total));
                return result;
            }
            finally { _sem.Release(); }
        });

        var watchlistTasks = watchlistCoins.Select(async coin =>
        {
            await _sem.WaitAsync(ct);
            try
            {
                var result = await AnalyzeCoinAsync(coin);
                result.HasHolding  = false;
                result.IsWatchlist = true;
                progress?.Report((int)Math.Round(Interlocked.Increment(ref done) * 100.0 / total));
                return result;
            }
            finally { _sem.Release(); }
        });

        var allTasks = holdingTasks.Concat(watchlistTasks);
        var results  = await Task.WhenAll(allTasks);

        // Sort: highest score first, then isNearBreakout
        return results
            .OrderByDescending(r => r.TradabilityScore)
            .ThenByDescending(r => r.IsNearBreakout)
            .ToList();
    }

    public async Task<PatternCoinAnalysis> AnalyzeCoinAsync(Coin coin)
    {
        var result = new PatternCoinAnalysis
        {
            Coin        = coin,
            AnalyzedAt  = DateTime.Now,
        };

        try
        {
            // ── 1. Fetch OHLCV bars ──────────────────────────────────────────
            var (dailyBars, h4Bars, h1Bars, m15Bars, source) = await FetchBarsAsync(coin);

            result.DataSource = source;
            result.HasData    = dailyBars.Any() || h4Bars.Any();

            // Store bars for chart rendering
            result.DailyBars = dailyBars;
            result.H4Bars    = h4Bars;
            result.H1Bars    = h1Bars;
            result.M15Bars   = m15Bars;

            if (!result.HasData)
            {
                Logger.Warning("PatternTrading: no data for {Coin}", coin.Name);
                return result;
            }

            // ── 2. Compute indicators per timeframe ──────────────────────────
            var tfDaily = ComputeIndicators("1D",  dailyBars, coin.Price);
            var tfH4    = ComputeIndicators("4H",  h4Bars,    coin.Price);
            var tfH1    = ComputeIndicators("1H",  h1Bars,    coin.Price);
            var tfM15   = ComputeIndicators("15M", m15Bars,   coin.Price);

            result.DailyBias = tfDaily.TrendBias;
            result.H4Bias    = tfH4.TrendBias;
            result.H1Bias    = tfH1.TrendBias;
            result.M15Bias   = tfM15.TrendBias;
            result.DailyRsi  = tfDaily.Rsi;
            result.H4Rsi     = tfH4.Rsi;
            result.H1Rsi     = tfH1.Rsi;
            result.M15Rsi    = tfM15.Rsi;

            // ── 3. Pattern detection ─────────────────────────────────────────
            var patterns = new List<PatternResult>();

            // Level 1 — indicators
            patterns.AddRange(_detector.DetectFromIndicators(tfDaily, "1D",  coin.Price));
            patterns.AddRange(_detector.DetectFromIndicators(tfH4,    "4H",  coin.Price));
            patterns.AddRange(_detector.DetectFromIndicators(tfH1,    "1H",  coin.Price));
            patterns.AddRange(_detector.DetectFromIndicators(tfM15,   "15M", coin.Price));

            // Level 2 — OHLCV bars
            if (dailyBars.Count >= 20)
                patterns.AddRange(_detector.DetectFromBars(dailyBars, "1D",  coin.Price));
            if (h4Bars.Count >= 20)
                patterns.AddRange(_detector.DetectFromBars(h4Bars,    "4H",  coin.Price));
            if (h1Bars.Count >= 20)
                patterns.AddRange(_detector.DetectFromBars(h1Bars,    "1H",  coin.Price));
            if (m15Bars.Count >= 20)
                patterns.AddRange(_detector.DetectFromBars(m15Bars,   "15M", coin.Price));

            result.Patterns = patterns;

            // ── 4. Key levels ────────────────────────────────────────────────
            // Use 4H bars when available (more granular pivots); fall back to daily.
            bool use4H  = h4Bars.Count >= 50;
            var  (resistance, support) = FindKeyLevels(
                use4H ? h4Bars : dailyBars,
                coin.Price,
                use4H ? "4H" : "1D");
            result.ResistanceLevels = resistance;
            result.SupportLevels    = support;

            // Mark near-breakout
            result.IsNearBreakout = resistance.Any(r =>
                r.Price > coin.Price && (r.Price - coin.Price) / coin.Price < 0.03);

            // ── 5. Tradability score ─────────────────────────────────────────
            (result.TradabilityScore, result.PrimaryDirection) =
                _detector.CalculateTradabilityScore(patterns, tfDaily, tfH4);

            // ── 6. Trade setup advice ────────────────────────────────────────
            double atr = tfDaily.Atr > 0 ? tfDaily.Atr
                       : tfH4.Atr   > 0 ? tfH4.Atr * 6
                       : coin.Price * 0.025;

            result.Setup = BuildSetupAdvice(coin, result, tfDaily, atr);

            // ── 7. Share text ────────────────────────────────────────────────
            result.ShareText = BuildShareText(result, coin);

            Logger.Information(
                "PatternTrading: {Coin} → score {Score} ({Dir}), {PCount} patterns",
                coin.Name, result.TradabilityScore, result.PrimaryDirection, patterns.Count);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "PatternTrading: analysis failed for {Coin}", coin.Name);
        }

        return result;
    }

    // =========================================================================
    // Data fetching — Binance → KuCoin → Gate.io → MEXC chain
    // =========================================================================

    private async Task<(List<OhlcvBar> daily, List<OhlcvBar> h4, List<OhlcvBar> h1, List<OhlcvBar> m15, string source)>
        FetchBarsAsync(Coin coin)
    {
        // Try Binance first (no API key, very fast)
        var symbol = _binance.ResolveBinanceSymbol(coin.ApiId, coin.Symbol);
        var (d, h4, h1, m15) = await FetchFourTimeframesAsync(
            (s, i, l) => _binance.GetKlinesAsync(s, i, l), symbol);

        if (d.Any()) return (d, h4, h1, m15, $"Binance ({symbol})");

        // KuCoin fallback
        var ksym = _kuCoin.ResolveKuCoinSymbol(coin.Symbol);
        (d, h4, h1, m15) = await FetchFourTimeframesAsync(
            (s, i, l) => _kuCoin.GetKlinesAsync(s, i, l), ksym);
        if (d.Any()) return (d, h4, h1, m15, $"KuCoin ({ksym})");

        // Gate.io fallback
        var gsym = _gateIo.ResolveSymbol(coin.Symbol);
        (d, h4, h1, m15) = await FetchFourTimeframesAsync(
            (s, i, l) => _gateIo.GetKlinesAsync(s, i, l), gsym);
        if (d.Any()) return (d, h4, h1, m15, $"Gate.io ({gsym})");

        // MEXC fallback
        var msym = _mexc.ResolveSymbol(coin.Symbol);
        (d, h4, h1, m15) = await FetchFourTimeframesAsync(
            (s, i, l) => _mexc.GetKlinesAsync(s, i, l), msym);
        if (d.Any()) return (d, h4, h1, m15, $"MEXC ({msym})");

        return (new(), new(), new(), new(), "geen data");
    }

    private static async Task<(List<OhlcvBar> daily, List<OhlcvBar> h4, List<OhlcvBar> h1, List<OhlcvBar> m15)>
        FetchFourTimeframesAsync(
            Func<string, string, int, Task<List<OhlcvBar>>> getter,
            string symbol)
    {
        try
        {
            var t1  = getter(symbol, "1d",  200);
            var t2  = getter(symbol, "4h",  200);
            var t3  = getter(symbol, "1h",  100);
            var t4  = getter(symbol, "15m", 200);   // 200 × 15 min ≈ 50 h
            await Task.WhenAll(t1, t2, t3, t4);
            return (t1.Result, t2.Result, t3.Result, t4.Result);
        }
        catch
        {
            return (new(), new(), new(), new());
        }
    }

    // =========================================================================
    // Indicator computation (mirrors TradeAnalysisService.AnalyzeTimeframe)
    // =========================================================================

    private static TimeframeAnalysis ComputeIndicators(
        string label, List<OhlcvBar> bars, double currentPrice)
    {
        var tf = new TimeframeAnalysis { Label = label };
        if (bars.Count < 15) return tf;

        tf.HasData = true;
        var q = ToQuotes(bars);

        if (q.Count >= 15)
            tf.Rsi = (double)(q.GetRsi(14).LastOrDefault()?.Rsi ?? 0);

        if (q.Count >= 10)  tf.Ema9   = Ema(q, 9);
        if (q.Count >= 22)  tf.Ema21  = Ema(q, 21);
        if (q.Count >= 51)  tf.Ema50  = Ema(q, 50);
        if (q.Count >= 201) tf.Ema200 = Ema(q, 200);

        if (q.Count >= 35)
        {
            var m = q.GetMacd(12, 26, 9).LastOrDefault();
            if (m is not null)
            {
                tf.Macd       = (double)(m.Macd   ?? 0);
                tf.MacdSignal = (double)(m.Signal ?? 0);
            }
        }

        if (q.Count >= 21)
        {
            var bb = q.GetBollingerBands(20, 2).LastOrDefault();
            if (bb is not null && q.Count >= 31)
            {
                double upper = (double)(bb.UpperBand ?? 0);
                double lower = (double)(bb.LowerBand ?? 0);
                tf.PctB = (upper > lower)
                    ? (currentPrice - lower) / (upper - lower) * 100
                    : 50;

                var kc = q.GetKeltner(20, 1.5, 10).LastOrDefault();
                if (kc is not null)
                {
                    double bbW = upper - lower;
                    double kcW = (double)((kc.UpperBand ?? 0) - (kc.LowerBand ?? 0));
                    tf.IsSqueeze = bbW < kcW && bbW > 0;
                }
            }
        }

        if (q.Count >= 28) tf.Adx = (double)(q.GetAdx(14).LastOrDefault()?.Adx ?? 0);
        if (q.Count >= 15) tf.Atr = (double)(q.GetAtr(14).LastOrDefault()?.Atr ?? 0);

        // EMA9/21 cross state
        if (tf.Ema9 > 0 && tf.Ema21 > 0 && q.Count >= 23)
        {
            var e9  = q.GetEma(9).ToList();
            var e21 = q.GetEma(21).ToList();
            int n   = Math.Min(e9.Count, e21.Count);
            if (n >= 2)
            {
                double last9  = (double)(e9[n - 1].Ema  ?? 0);
                double prev9  = (double)(e9[n - 2].Ema  ?? 0);
                double last21 = (double)(e21[n - 1].Ema ?? 0);
                double prev21 = (double)(e21[n - 2].Ema ?? 0);

                if      (prev9 < prev21 && last9 > last21) tf.EmaCrossState = "Bullish kruis";
                else if (prev9 > prev21 && last9 < last21) tf.EmaCrossState = "Bearish kruis";
                else    tf.EmaCrossState = last9 >= last21 ? "EMA9 boven EMA21" : "EMA9 onder EMA21";
            }
        }

        tf.TrendBias = DetermineTrendBias(tf, currentPrice);
        return tf;
    }

    private static double Ema(List<Quote> q, int period)
        => (double)(q.GetEma(period).LastOrDefault()?.Ema ?? 0);

    private static List<Quote> ToQuotes(List<OhlcvBar> bars)
        => bars.Select(b => new Quote
        {
            Date   = b.Date,
            Open   = (decimal)b.Open,
            High   = (decimal)b.High,
            Low    = (decimal)b.Low,
            Close  = (decimal)b.Close,
            Volume = (decimal)b.Volume,
        }).ToList();

    private static string DetermineTrendBias(TimeframeAnalysis tf, double price)
    {
        int bull = 0, bear = 0;
        if (tf.Ema21  > 0) { if (price > tf.Ema21)  bull++; else bear++; }
        if (tf.Ema50  > 0) { if (price > tf.Ema50)  bull++; else bear++; }
        if (tf.Ema200 > 0) { if (price > tf.Ema200) bull++; else bear++; }
        if (tf.Rsi    > 0) { if (tf.Rsi > 50) bull++; else bear++; }
        if (tf.Macd  != 0) { if (tf.Macd > tf.MacdSignal) bull++; else bear++; }
        return bull > bear ? "Bullish" : bear > bull ? "Bearish" : "Neutraal";
    }

    // =========================================================================
    // Key levels (pivot clustering)
    // =========================================================================

    private static (List<PatternLevel> resistance, List<PatternLevel> support)
        FindKeyLevels(List<OhlcvBar> bars, double price, string timeframe)
    {
        if (bars.Count < 20) return (new(), new());

        var data = bars.TakeLast(200).ToList();
        const int lb = 5;
        var highs = new List<double>();
        var lows  = new List<double>();

        for (int i = lb; i < data.Count - lb; i++)
        {
            double h = data[i].High, l = data[i].Low;
            bool isHigh = true, isLow = true;
            for (int j = i - lb; j <= i + lb; j++)
            {
                if (j == i) continue;
                if (data[j].High >= h) isHigh = false;
                if (data[j].Low  <= l) isLow  = false;
            }
            if (isHigh) highs.Add(h);
            if (isLow)  lows.Add(l);
        }

        var resistance = Cluster(highs.Where(p => p > price * 1.003).ToList())
            .OrderBy(p => p).Take(4)
            .Select(p => new PatternLevel(p, timeframe)).ToList();
        var support = Cluster(lows.Where(p => p < price * 0.997).ToList())
            .OrderByDescending(p => p).Take(4)
            .Select(p => new PatternLevel(p, timeframe)).ToList();

        return (resistance, support);
    }

    private static List<double> Cluster(List<double> levels)
    {
        var result = new List<double>();
        foreach (var level in levels.OrderBy(p => p))
        {
            bool merged = false;
            for (int i = 0; i < result.Count; i++)
            {
                if (Math.Abs(level - result[i]) / result[i] < 0.015)
                {
                    result[i] = (result[i] + level) / 2;
                    merged = true;
                    break;
                }
            }
            if (!merged) result.Add(level);
        }
        return result;
    }

    // =========================================================================
    // Trade setup (simplified ATR-based, augmented by key levels)
    // =========================================================================

    private static TradeSetupAdvice BuildSetupAdvice(
        Coin coin, PatternCoinAnalysis res, TimeframeAnalysis daily, double atr)
    {
        var setup = new TradeSetupAdvice();
        double price = coin.Price;
        string dir   = res.PrimaryDirection;

        if (dir == "Neutraal" || res.TradabilityScore < 40)
        {
            setup.Direction  = "Geen signaal";
            setup.Confidence = "–";
            setup.Reasoning.Add($"Score {res.TradabilityScore}/100 — neutraal. Wacht op een duidelijker richting (>60 = Long, <40 = Short).");
            return setup;
        }

        setup.Direction = dir;

        if (dir == "Long")
        {
            double ema21 = daily.Ema21 > 0 ? daily.Ema21 : price;
            setup.EntryPrice = (ema21 > 0 && price > ema21 * 1.04)
                ? ema21 * 1.005
                : price;
            setup.EntryNote = (ema21 > 0 && price > ema21 * 1.04)
                ? $"Wacht op pullback naar EMA21 daily ({Fp(ema21)})."
                : "Huidig prijsniveau — of stel een limietorder net onder de koers.";

            (setup.StopLoss, setup.Target1, setup.Target2) =
                TradeLevelCalculator.FromAtr("Long", setup.EntryPrice, atr);

            var nearRes = res.ResistanceLevels
                .FirstOrDefault(r => r.Price > setup.EntryPrice && r.Price < setup.EntryPrice * 1.20);
            if (nearRes is not null) setup.Target2 = nearRes.Price;
        }
        else // Short
        {
            double ema21 = daily.Ema21 > 0 ? daily.Ema21 : price;
            setup.EntryPrice = (ema21 > 0 && price < ema21 * 0.96)
                ? ema21 * 0.995
                : price;
            setup.EntryNote = (ema21 > 0 && price < ema21 * 0.96)
                ? $"Wacht op bounce naar EMA21 daily ({Fp(ema21)})."
                : "Huidig prijsniveau — of stel een limietorder net boven de koers.";

            (setup.StopLoss, setup.Target1, setup.Target2) =
                TradeLevelCalculator.FromAtr("Short", setup.EntryPrice, atr);

            var nearSup = res.SupportLevels
                .FirstOrDefault(s => s.Price < setup.EntryPrice && s.Price > setup.EntryPrice * 0.80);
            if (nearSup is not null) setup.Target2 = nearSup.Price;
        }

        // Garandeer dat Target1 het dichtstbijzijnde (eerst geraakte) doel is.
        (setup.Target1, setup.Target2) =
            TradeLevelCalculator.OrderTargets(dir, setup.EntryPrice, setup.Target1, setup.Target2);

        (setup.StopLossPct, setup.Target1Pct, setup.Target2Pct, setup.RiskReward1, setup.RiskReward2) =
            TradeLevelCalculator.Percentages(setup.EntryPrice, setup.StopLoss, setup.Target1, setup.Target2);

        // ── Validatie van de gegenereerde niveaus (degenerate ATR=0, richting, R/R) ──
        var advCheck = TradeSetupValidator.CheckAdvice(
            setup.Direction, setup.EntryPrice, setup.StopLoss,
            setup.Target1, setup.Target2, setup.RiskReward1);
        setup.IsValid           = advCheck.IsValid;
        setup.ValidationWarning = advCheck.Warning;

        int score = res.TradabilityScore;
        setup.Confidence = (score >= 80 && daily.Adx >= 25) ? "Hoog"
                         : score >= 60                       ? "Gemiddeld"
                         :                                     "Laag";

        // Key reasoning bullets from detected patterns
        foreach (var p in res.Patterns
            .Where(p => p.Strength >= 65)
            .OrderByDescending(p => p.Strength)
            .Take(4))
        {
            setup.Reasoning.Add($"• [{p.Timeframe}] {p.DisplayName}: {p.Description}");
        }

        if (daily.IsSqueeze)
            setup.Reasoning.Add("• Bollinger Squeeze daily actief — uitbraak verwacht. Let op het volume direct na de opening van de squeeze.");

        if (!setup.Reasoning.Any())
            setup.Reasoning.Add($"Score {score}/100 ({dir}) op basis van gecombineerde indicator- en patroonanalyse.");

        return setup;
    }

    // =========================================================================
    // Share text builder
    // =========================================================================

    private static string BuildShareText(PatternCoinAnalysis a, Coin coin)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"📊 Pattern Trading — {coin.Name} ({coin.Symbol?.ToUpper()})");
        sb.AppendLine($"💰 Prijs: {Fp(coin.Price)}  |  24h: {coin.Change24Hr:+0.00;-0.00}%");
        sb.AppendLine($"⭐ Setup score: {a.TradabilityScore}/100 — {a.ScoreLabel}");
        sb.AppendLine($"📈 Richting: {a.PrimaryDirection}");

        if (a.KeyPatterns.Any())
        {
            sb.AppendLine("🔍 Patronen:");
            foreach (var p in a.KeyPatterns.Take(4))
                sb.AppendLine($"  [{p.Timeframe}] {p.DisplayName}" + (p.IsConfirmed ? " ✅" : " ⏳"));
        }

        if (a.Setup is not null && a.TradabilityScore >= 40 && a.PrimaryDirection != "Neutraal")
        {
            sb.AppendLine($"🎯 Entry: {Fp(a.Setup.EntryPrice)}");
            sb.AppendLine($"🛑 Stop:  {Fp(a.Setup.StopLoss)} (-{a.Setup.StopLossPct:F1}%)");
            sb.AppendLine($"✅ TP1:   {Fp(a.Setup.Target1)}  (+{a.Setup.Target1Pct:F1}%)");
            sb.AppendLine($"🚀 TP2:   {Fp(a.Setup.Target2)}  (+{a.Setup.Target2Pct:F1}%)");
            sb.AppendLine($"⚖️ R/R:   1:{a.Setup.RiskReward1:F1}");
        }

        sb.AppendLine();
        sb.AppendLine("⚠️ Geen financieel advies. DYOR.");
        sb.AppendLine("#crypto #patterntrading #technicalanalysis");
        return sb.ToString().TrimEnd();
    }

    private static string Fp(double price) => price switch
    {
        >= 10_000 => $"${price:N0}",
        >= 1      => $"${price:N2}",
        >= 0.01   => $"${price:N4}",
        _         => $"${price:N6}",
    };
}
