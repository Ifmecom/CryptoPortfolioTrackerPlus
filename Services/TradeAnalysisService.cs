using CryptoPortfolioTracker.Infrastructure.Response.Coins;
using CryptoPortfolioTracker.Models;
using Serilog;
using Serilog.Core;
using Skender.Stock.Indicators;
using System.Text;

namespace CryptoPortfolioTracker.Services;

public class TradeAnalysisService : ITradeAnalysisService
{
    private static readonly ILogger Logger = Log.Logger.ForContext(
        Constants.SourceContextPropertyName, nameof(TradeAnalysisService).PadRight(22));

    private readonly IBinanceDataService      _binance;
    private readonly IKuCoinDataService       _kuCoin;
    private readonly IGateIoDataService       _gateIo;
    private readonly IMexcDataService         _mexc;
    private readonly IPatternDetectionService _patternDetection;

    // Sprint B-services hergebruikt voor verrijking (liquiditeit/positionering/events)
    private readonly IOrderBookService?          _orderBook;
    private readonly IBinanceFuturesDataService? _futures;
    private readonly IMacroEventService?         _macroEvents;

    public TradeAnalysisService(
        IBinanceDataService      binance,
        IKuCoinDataService       kuCoin,
        IGateIoDataService       gateIo,
        IMexcDataService         mexc,
        IPatternDetectionService patternDetection,
        IOrderBookService?          orderBook   = null,
        IBinanceFuturesDataService? futures     = null,
        IMacroEventService?         macroEvents = null)
    {
        _binance          = binance          ?? throw new ArgumentNullException(nameof(binance));
        _kuCoin           = kuCoin           ?? throw new ArgumentNullException(nameof(kuCoin));
        _gateIo           = gateIo           ?? throw new ArgumentNullException(nameof(gateIo));
        _mexc             = mexc             ?? throw new ArgumentNullException(nameof(mexc));
        _patternDetection = patternDetection ?? throw new ArgumentNullException(nameof(patternDetection));
        _orderBook        = orderBook;
        _futures          = futures;
        _macroEvents      = macroEvents;
    }

    // -----------------------------------------------------------------------
    // Public entry point
    // -----------------------------------------------------------------------

    public async Task<TradeAnalysisResult> GenerateAsync(Coin coin)
    {
        if (coin is null) throw new ArgumentNullException(nameof(coin));

        Logger.Information("TradeAnalysis: generating for {Coin}", coin.Name);

        var result = new TradeAnalysisResult
        {
            CoinName      = coin.Name,
            Symbol        = coin.Symbol.ToUpper(),
            ImageUri      = coin.ImageUri,
            CurrentPrice  = coin.Price,
            Change24h     = coin.Change24Hr,
            CombinedScore = (int)Math.Round(coin.LatestSignalScore),
            Direction     = DirectionFromScore(coin.LatestSignalScore),
            Regime        = coin.MarketRegime.ToString(),
            GeneratedAt   = DateTime.Now,
        };

        // ── Fetch Binance data ────────────────────────────────────────────
        var binanceSymbol = _binance.ResolveBinanceSymbol(coin.ApiId, coin.Symbol);
        result.BinanceSymbol = binanceSymbol;

        var (weeklyBars, dailyBars, h4Bars, h1Bars, m15Bars) = await FetchAllTimeframesAsync(binanceSymbol);
        result.BinanceDataAvailable = weeklyBars.Any() || dailyBars.Any();

        if (dailyBars.Any())
        {
            result.DataSource = $"Binance ({binanceSymbol})";
        }

        // ── Fallback 1: KuCoin ────────────────────────────────────────────
        if (!dailyBars.Any())
        {
            var sym = _kuCoin.ResolveKuCoinSymbol(coin.Symbol);
            Logger.Information("TradeAnalysis: Binance miss — trying KuCoin ({Symbol})", sym);
            var (w, d, h4, h1, m15) = await FetchAllTimeframesKuCoinAsync(sym);
            if (d.Any()) { (weeklyBars, dailyBars, h4Bars, h1Bars, m15Bars) = (w, d, h4, h1, m15); result.DataSource = $"KuCoin ({sym})"; }
        }

        // ── Fallback 2: Gate.io ───────────────────────────────────────────
        if (!dailyBars.Any())
        {
            var sym = _gateIo.ResolveSymbol(coin.Symbol);
            Logger.Information("TradeAnalysis: KuCoin miss — trying Gate.io ({Symbol})", sym);
            var (w, d, h4, h1, m15) = await FetchAllTimeframesGateIoAsync(sym);
            if (d.Any()) { (weeklyBars, dailyBars, h4Bars, h1Bars, m15Bars) = (w, d, h4, h1, m15); result.DataSource = $"Gate.io ({sym})"; }
        }

        // ── Fallback 3: MEXC ──────────────────────────────────────────────
        if (!dailyBars.Any())
        {
            var sym = _mexc.ResolveSymbol(coin.Symbol);
            Logger.Information("TradeAnalysis: Gate.io miss — trying MEXC ({Symbol})", sym);
            var (w, d, h4, h1, m15) = await FetchAllTimeframesMexcAsync(sym);
            if (d.Any()) { (weeklyBars, dailyBars, h4Bars, h1Bars, m15Bars) = (w, d, h4, h1, m15); result.DataSource = $"MEXC ({sym})"; }
        }

        // ── Fallback 4: local JSON cache ──────────────────────────────────
        if (!dailyBars.Any())
        {
            dailyBars = await LoadDailyFromCacheAsync(coin);
            if (dailyBars.Any())
                result.DataSource = "lokale cache (coin niet op Binance, KuCoin, Gate.io of MEXC)";
        }

        if (string.IsNullOrEmpty(result.DataSource))
            result.DataSource = "geen data beschikbaar";

        // ── Per-timeframe analysis ────────────────────────────────────────
        result.Weekly      = AnalyzeTimeframe("Weekly",  weeklyBars, coin.Price);
        result.Daily       = AnalyzeTimeframe("Daily",   dailyBars,  coin.Price);
        result.FourHour    = AnalyzeTimeframe("4H",      h4Bars,     coin.Price);
        result.OneHour     = AnalyzeTimeframe("1H",      h1Bars,     coin.Price);
        result.FifteenMin  = AnalyzeTimeframe("15m",     m15Bars,    coin.Price);

        // ── Live score — gebruik hetzelfde engine als Pattern Trading ────
        // Vervang de verouderde coin.LatestSignalScore (DB-waarde van SignalEngine)
        // door een real-time score op basis van de vers opgehaalde OHLCV-data.
        // Zo zijn Trade Advies en Pattern Trading altijd consistent.
        if (result.Daily.HasData || result.FourHour.HasData)
        {
            var (freshScore, freshDir) = _patternDetection.CalculateTradabilityScore(
                new List<PatternResult>(), result.Daily, result.FourHour);
            result.CombinedScore = freshScore;
            result.Direction     = freshDir == "Long"  ? "Long"
                                 : freshDir == "Short" ? "Short"
                                 :                       "Flat";
        }

        // ── Key levels (prefer 4H for granularity, fall back to daily) ───
        var levelsSource = h4Bars.Count >= 50 ? h4Bars : dailyBars;
        (result.ResistanceLevels, result.SupportLevels) = FindKeyLevels(levelsSource, coin.Price);

        // ── Trade setup ───────────────────────────────────────────────────
        double atr = result.Daily.Atr > 0 ? result.Daily.Atr
                   : result.FourHour.Atr > 0 ? result.FourHour.Atr * 6   // 6×4H ≈ 1 day
                   : coin.Price * 0.03;                                    // 3% fallback

        result.Setup = BuildTradeSetup(coin, result, atr);

        // ── Verrijking: liquiditeit (F6), positionering (F7) en macro-events ──
        // Alleen zinvol als we een geldig setup-signaal hebben en op Binance zitten.
        if (result.Setup.Direction is "Long" or "Short" && result.DataSource.StartsWith("Binance"))
        {
            if (_orderBook is not null)
            {
                try { result.OrderBook = await _orderBook.GetSnapshotAsync(binanceSymbol); }
                catch (Exception ex) { Logger.Debug(ex, "TradeAnalysis: orderbook fetch failed"); }
            }
            if (_futures is not null)
            {
                try { result.Positioning = await _futures.GetPositioningAsync(binanceSymbol); }
                catch (Exception ex) { Logger.Debug(ex, "TradeAnalysis: futures fetch failed"); }
            }
        }

        // Macro-events binnen ~15 dagen (timeframe-onafhankelijk risico)
        if (_macroEvents is not null)
            result.MacroEvents = _macroEvents.GetUpcoming(15).ToList();

        return result;
    }

    // -----------------------------------------------------------------------
    // Data fetching
    // -----------------------------------------------------------------------

    private async Task<(List<OhlcvBar> weekly, List<OhlcvBar> daily, List<OhlcvBar> h4, List<OhlcvBar> h1, List<OhlcvBar> m15)>
        FetchAllTimeframesAsync(string symbol)
    {
        var t1 = _binance.GetKlinesAsync(symbol, "1w",  104);  // 2 years weekly
        var t2 = _binance.GetKlinesAsync(symbol, "1d",  300);  // ~10 months daily
        var t3 = _binance.GetKlinesAsync(symbol, "4h",  500);  // ~83 days 4H (enough for EMA200)
        var t4 = _binance.GetKlinesAsync(symbol, "1h",  200);  // ~8 days hourly
        var t5 = _binance.GetKlinesAsync(symbol, "15m", 300);  // ~3 days 15m

        await Task.WhenAll(t1, t2, t3, t4, t5);
        return (t1.Result, t2.Result, t3.Result, t4.Result, t5.Result);
    }

    private async Task<(List<OhlcvBar> weekly, List<OhlcvBar> daily, List<OhlcvBar> h4, List<OhlcvBar> h1, List<OhlcvBar> m15)>
        FetchAllTimeframesKuCoinAsync(string symbol)
    {
        var t1 = _kuCoin.GetKlinesAsync(symbol, "1w",  104);
        var t2 = _kuCoin.GetKlinesAsync(symbol, "1d",  300);
        var t3 = _kuCoin.GetKlinesAsync(symbol, "4h",  500);
        var t4 = _kuCoin.GetKlinesAsync(symbol, "1h",  200);
        var t5 = _kuCoin.GetKlinesAsync(symbol, "15m", 300);
        await Task.WhenAll(t1, t2, t3, t4, t5);
        return (t1.Result, t2.Result, t3.Result, t4.Result, t5.Result);
    }

    private async Task<(List<OhlcvBar> weekly, List<OhlcvBar> daily, List<OhlcvBar> h4, List<OhlcvBar> h1, List<OhlcvBar> m15)>
        FetchAllTimeframesGateIoAsync(string symbol)
    {
        var t1 = _gateIo.GetKlinesAsync(symbol, "1w",  104);
        var t2 = _gateIo.GetKlinesAsync(symbol, "1d",  300);
        var t3 = _gateIo.GetKlinesAsync(symbol, "4h",  500);
        var t4 = _gateIo.GetKlinesAsync(symbol, "1h",  200);
        var t5 = _gateIo.GetKlinesAsync(symbol, "15m", 300);
        await Task.WhenAll(t1, t2, t3, t4, t5);
        return (t1.Result, t2.Result, t3.Result, t4.Result, t5.Result);
    }

    private async Task<(List<OhlcvBar> weekly, List<OhlcvBar> daily, List<OhlcvBar> h4, List<OhlcvBar> h1, List<OhlcvBar> m15)>
        FetchAllTimeframesMexcAsync(string symbol)
    {
        var t1 = _mexc.GetKlinesAsync(symbol, "1w",  104);
        var t2 = _mexc.GetKlinesAsync(symbol, "1d",  300);
        var t3 = _mexc.GetKlinesAsync(symbol, "4h",  500);
        var t4 = _mexc.GetKlinesAsync(symbol, "1h",  200);
        var t5 = _mexc.GetKlinesAsync(symbol, "15m", 300);
        await Task.WhenAll(t1, t2, t3, t4, t5);
        return (t1.Result, t2.Result, t3.Result, t4.Result, t5.Result);
    }

    private async Task<List<OhlcvBar>> LoadDailyFromCacheAsync(Coin coin)
    {
        try
        {
            var suffix = coin.Name.Contains("_pre-listing") ? "-prelisting" : "";
            var chart = new MarketChartById();
            await chart.LoadMarketChartJson(coin.ApiId + suffix);

            if (chart.Prices?.Length > 0)
            {
                return chart.Prices
                    .TakeLast(300)
                    .Select(p => new OhlcvBar
                    {
                        Date   = DateTimeOffset.FromUnixTimeMilliseconds((long)p[0]!.Value).UtcDateTime,
                        Open   = (double)p[1]!.Value,
                        High   = (double)p[1]!.Value,
                        Low    = (double)p[1]!.Value,
                        Close  = (double)p[1]!.Value,
                        Volume = 0,
                    })
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to load daily cache for {Coin}", coin.ApiId);
        }
        return new List<OhlcvBar>();
    }

    // -----------------------------------------------------------------------
    // Per-timeframe analysis
    // -----------------------------------------------------------------------

    private TimeframeAnalysis AnalyzeTimeframe(string label, List<OhlcvBar> bars, double currentPrice)
    {
        var tf = new TimeframeAnalysis { Label = label };
        if (bars.Count < 15) return tf;

        tf.HasData = true;
        var quotes = ToQuotes(bars);

        // RSI 14
        if (quotes.Count >= 15)
            tf.Rsi = (double)(quotes.GetRsi(14).LastOrDefault()?.Rsi ?? 0);

        // EMAs
        if (quotes.Count >= 10)
            tf.Ema9 = Ema(quotes, 9);
        if (quotes.Count >= 22)
            tf.Ema21 = Ema(quotes, 21);
        if (quotes.Count >= 51)
            tf.Ema50 = Ema(quotes, 50);
        if (quotes.Count >= 201)
            tf.Ema200 = Ema(quotes, 200);

        // MACD (12,26,9)
        if (quotes.Count >= 35)
        {
            var m = quotes.GetMacd(12, 26, 9).LastOrDefault();
            if (m != null)
            {
                tf.Macd       = (double)(m.Macd   ?? 0);
                tf.MacdSignal = (double)(m.Signal ?? 0);
            }
        }

        // Bollinger %B (20,2)
        if (quotes.Count >= 21)
        {
            var bb = quotes.GetBollingerBands(20, 2).LastOrDefault();
            if (bb != null)
            {
                double upper = (double)(bb.UpperBand ?? 0);
                double lower = (double)(bb.LowerBand ?? 0);
                tf.PctB = (upper > lower) ? (currentPrice - lower) / (upper - lower) * 100 : 50;

                // Squeeze: BB narrower than Keltner Channel
                if (quotes.Count >= 31)
                {
                    var kc = quotes.GetKeltner(20, 1.5, 10).LastOrDefault();
                    if (kc != null)
                    {
                        double bbW = upper - lower;
                        double kcW = (double)((kc.UpperBand ?? 0) - (kc.LowerBand ?? 0));
                        tf.IsSqueeze = bbW < kcW && bbW > 0;
                    }
                }
            }
        }

        // ADX 14
        if (quotes.Count >= 28)
            tf.Adx = (double)(quotes.GetAdx(14).LastOrDefault()?.Adx ?? 0);

        // ATR 14
        if (quotes.Count >= 15)
            tf.Atr = (double)(quotes.GetAtr(14).LastOrDefault()?.Atr ?? 0);

        // EMA9/EMA21 cross state
        if (tf.Ema9 > 0 && tf.Ema21 > 0 && quotes.Count >= 23)
        {
            var e9  = quotes.GetEma(9).ToList();
            var e21 = quotes.GetEma(21).ToList();
            int n = Math.Min(e9.Count, e21.Count);
            if (n >= 2)
            {
                double last9  = (double)(e9[n - 1].Ema  ?? 0);
                double prev9  = (double)(e9[n - 2].Ema  ?? 0);
                double last21 = (double)(e21[n - 1].Ema ?? 0);
                double prev21 = (double)(e21[n - 2].Ema ?? 0);

                if (prev9 < prev21 && last9 > last21)
                    tf.EmaCrossState = "Bullish kruis";
                else if (prev9 > prev21 && last9 < last21)
                    tf.EmaCrossState = "Bearish kruis";
                else
                    tf.EmaCrossState = last9 >= last21 ? "EMA9 boven EMA21" : "EMA9 onder EMA21";
            }
        }

        // Trend bias from multiple signals
        tf.TrendBias = DetermineTrendBias(tf, currentPrice);

        // Dutch narrative bullets
        tf.Bullets = GenerateBullets(tf, currentPrice);

        return tf;
    }

    // -----------------------------------------------------------------------
    // Key levels (pivot detection)
    // -----------------------------------------------------------------------

    private (List<double> resistance, List<double> support) FindKeyLevels(List<OhlcvBar> bars, double price)
    {
        if (bars.Count < 20) return (new(), new());

        var data = bars.TakeLast(200).ToList();
        const int lookback = 5;

        var highs = new List<double>();
        var lows  = new List<double>();

        for (int i = lookback; i < data.Count - lookback; i++)
        {
            double h = data[i].High;
            double l = data[i].Low;
            bool isHigh = true, isLow = true;

            for (int j = i - lookback; j <= i + lookback; j++)
            {
                if (j == i) continue;
                if (data[j].High >= h) isHigh = false;
                if (data[j].Low  <= l) isLow  = false;
            }

            if (isHigh) highs.Add(h);
            if (isLow)  lows.Add(l);
        }

        var resistance = Cluster(highs.Where(p => p > price * 1.003).ToList())
                            .OrderBy(p => p).Take(4).ToList();
        var support    = Cluster(lows.Where(p => p < price * 0.997).ToList())
                            .OrderByDescending(p => p).Take(4).ToList();

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
                if (Math.Abs(level - result[i]) / result[i] < 0.015) // within 1.5%
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

    // -----------------------------------------------------------------------
    // Trade setup
    // -----------------------------------------------------------------------

    private TradeSetupAdvice BuildTradeSetup(Coin coin, TradeAnalysisResult res, double atr)
    {
        var setup = new TradeSetupAdvice();
        double score = res.CombinedScore;   // live score (zelfde engine als Pattern Trading)
        double price = coin.Price;

        // Direction from combined score
        if (score >= 60)
            setup.Direction = "Long";
        else if (score <= 40)
            setup.Direction = "Short";
        else
        {
            setup.Direction = "Geen signaal";
            setup.Confidence = "–";
            setup.Reasoning.Add($"Score {score:F0}/100 — neutraal (40–60). Wacht op een duidelijker richting.");
            setup.Reasoning.Add("Score > 60 → Long-signaal  |  Score < 40 → Short-signaal.");
            if (res.Daily.HasData)
                setup.Reasoning.Add($"Houd {Fp(res.Daily.Ema21)} (EMA21 daily) als scharnierpunt in de gaten.");
            return setup;
        }

        double ema21 = res.Daily.Ema21 > 0 ? res.Daily.Ema21 : price;

        // ── Entry ────────────────────────────────────────────────────────
        if (setup.Direction == "Long")
        {
            if (ema21 > 0 && price > ema21 * 1.04)
            {
                setup.EntryPrice = ema21 * 1.005;  // just above EMA21
                setup.EntryNote = $"Wacht op pullback naar EMA21 ({Fp(ema21)}). " +
                                  $"Huidige prijs ligt {(price / ema21 - 1) * 100:F1}% boven EMA21.";
            }
            else
            {
                setup.EntryPrice = price;
                setup.EntryNote = "Marktprijs — of stel een limietorder net onder de actuele koers.";
            }

            (setup.StopLoss, setup.Target1, setup.Target2) =
                TradeLevelCalculator.FromAtr("Long", setup.EntryPrice, atr);

            // Override TP2 with nearest resistance if within 20%
            var nearRes = res.ResistanceLevels.FirstOrDefault(r => r > setup.EntryPrice && r < setup.EntryPrice * 1.20);
            if (nearRes > 0) setup.Target2 = nearRes;
        }
        else // Short
        {
            if (ema21 > 0 && price < ema21 * 0.96)
            {
                setup.EntryPrice = ema21 * 0.995;  // just below EMA21
                setup.EntryNote = $"Wacht op bounce naar EMA21 ({Fp(ema21)}). " +
                                  $"Huidige prijs ligt {(1 - price / ema21) * 100:F1}% onder EMA21.";
            }
            else
            {
                setup.EntryPrice = price;
                setup.EntryNote = "Marktprijs — of stel een limietorder net boven de actuele koers.";
            }

            (setup.StopLoss, setup.Target1, setup.Target2) =
                TradeLevelCalculator.FromAtr("Short", setup.EntryPrice, atr);

            // Override TP2 with nearest support if within 20%
            var nearSup = res.SupportLevels.FirstOrDefault(s => s < setup.EntryPrice && s > setup.EntryPrice * 0.80);
            if (nearSup > 0) setup.Target2 = nearSup;
        }

        // Garandeer dat Target1 het dichtstbijzijnde (eerst geraakte) doel is.
        (setup.Target1, setup.Target2) =
            TradeLevelCalculator.OrderTargets(setup.Direction, setup.EntryPrice, setup.Target1, setup.Target2);

        // Percentages & R:R
        (setup.StopLossPct, setup.Target1Pct, setup.Target2Pct, setup.RiskReward1, setup.RiskReward2) =
            TradeLevelCalculator.Percentages(setup.EntryPrice, setup.StopLoss, setup.Target1, setup.Target2);

        // ── Validatie van de gegenereerde niveaus (degenerate ATR=0, richting, R/R) ──
        var advCheck = TradeSetupValidator.CheckAdvice(
            setup.Direction, setup.EntryPrice, setup.StopLoss,
            setup.Target1, setup.Target2, setup.RiskReward1);
        setup.IsValid           = advCheck.IsValid;
        setup.ValidationWarning = advCheck.Warning;
        if (!string.IsNullOrEmpty(advCheck.Warning))
            setup.Reasoning.Add($"⚠ {advCheck.Warning}");

        // Confidence
        double dist = Math.Abs(score - 50);
        bool trending = res.Daily.Adx >= 25;
        setup.Confidence = dist >= 25 && trending ? "Hoog"
                         : dist >= 15             ? "Gemiddeld"
                         :                          "Laag";

        // Reasoning — uitgebreid met het hoe en waarom
        setup.Reasoning.Add($"Gecombineerde score {score:F0}/100: live berekening op basis van vers opgehaalde OHLCV-data — zelfde engine als Pattern Trading (EMA-cross, RSI, MACD, ADX, %B, Squeeze). Boven 60 = Long-bias, onder 40 = Short-bias.");

        if (setup.Direction != "Geen signaal")
        {
            setup.Reasoning.Add($"Stop-loss op 1,5× ATR14 daily ({Fp(atr * 1.5)}) biedt de trade ruimte voor normale dagelijkse volatiliteit zonder voortijdig geraakt te worden. Enger dan 1× ATR is statistisch onhoudbaar.");
            setup.Reasoning.Add($"Target 1 op 2× ATR ({Fp(setup.Target1)}): ideaal punt voor partiële winstneming. R/R 1:{setup.RiskReward1:F1} — minimaal aanvaardbaar niveau.");
            bool t2IsResOrSup = res.ResistanceLevels.Any(r2 => Math.Abs(r2 - setup.Target2) / setup.Target2 < 0.01) ||
                                res.SupportLevels.Any(s2 => Math.Abs(s2 - setup.Target2) / setup.Target2 < 0.01);
            string t2Source = t2IsResOrSup ? "naaste pivot-niveau" : "3,5× ATR";
            setup.Reasoning.Add($"Target 2 op {t2Source} ({Fp(setup.Target2)}): volledig uitrijden van de move. R/R 1:{setup.RiskReward2:F1}.");
        }

        if (res.Daily.Adx >= 25)
            setup.Reasoning.Add($"ADX daily {res.Daily.Adx:F0} (>25): de markt is trending, niet zijwaarts. Trendvolgende signalen zijn nu betrouwbaarder en breekouts hebben meer kans van slagen.");
        else if (res.Daily.Adx > 0 && res.Daily.Adx < 20)
            setup.Reasoning.Add($"ADX daily {res.Daily.Adx:F0} (<20): zijwaartse range-markt. Lagere betrouwbaarheid voor trendsignalen; overweeg smallere targets of wacht op ADX >25.");

        if (res.Daily.IsSqueeze)
            setup.Reasoning.Add("Bollinger Squeeze actief op Daily: extreme volatiliteitscompressie — de markt adem in. Uitbraken na een squeeze zijn historisch krachtig en snel; let op het momentum direct na de opening van de squeeze.");
        if (res.FourHour.IsSqueeze)
            setup.Reasoning.Add("Squeeze ook actief op 4H: dubbele bevestiging op twee timeframes. Dit verhoogt de kans op een nabije, scherpe koersbeweging.");

        if (res.Daily.Rsi > 0 && res.Daily.Rsi < 35 && setup.Direction == "Long")
            setup.Reasoning.Add($"RSI daily {res.Daily.Rsi:F0} — diep oversold. De markt is te ver gedaald t.o.v. recente prijzen; statistische kans op technisch herstel neemt toe. Bevestigt Long-richting.");
        if (res.Daily.Rsi > 65 && setup.Direction == "Short")
            setup.Reasoning.Add($"RSI daily {res.Daily.Rsi:F0} — overbought territory. Stijging gaat sneller dan historisch gemiddeld; verhoogde kans op winstneming. Bevestigt Short-richting.");

        if (res.Weekly.TrendBias == "Bullish" && setup.Direction == "Long")
            setup.Reasoning.Add("Weekly trend bullish: je handelt MET de grote trend. Statistisch hebben long-setups in een bullish weekly trend een hogere trefkans dan counter-trend trades.");
        if (res.Weekly.TrendBias == "Bearish" && setup.Direction == "Short")
            setup.Reasoning.Add("Weekly trend bearish: short-setup loopt mee met de dominante trend. Macro-momentum werkt in je voordeel.");
        if (res.Weekly.TrendBias == "Bullish" && setup.Direction == "Short")
            setup.Reasoning.Add("⚠️  Counter-trend waarschuwing: Short tegen een bullish weekly trend. Hoger risico — zorg voor strakker risicobeheer en overweeg kleinere positiegrootte.");
        if (res.Weekly.TrendBias == "Bearish" && setup.Direction == "Long")
            setup.Reasoning.Add("⚠️  Counter-trend waarschuwing: Long in een bearish weekly trend. Hogere kans op false breakouts — wacht op extra bevestiging vóór instap en gebruik kleinere positie.");

        return setup;
    }

    // -----------------------------------------------------------------------
    // Trend bias
    // -----------------------------------------------------------------------

    private static string DetermineTrendBias(TimeframeAnalysis tf, double price)
    {
        int bullish = 0, bearish = 0;

        if (tf.Ema21 > 0)  { if (price > tf.Ema21)  bullish++; else bearish++; }
        if (tf.Ema50 > 0)  { if (price > tf.Ema50)  bullish++; else bearish++; }
        if (tf.Ema200 > 0) { if (price > tf.Ema200) bullish++; else bearish++; }
        if (tf.Rsi > 0)    { if (tf.Rsi > 50) bullish++; else bearish++; }
        if (tf.Macd != 0)  { if (tf.Macd > tf.MacdSignal) bullish++; else bearish++; }

        if (bullish > bearish) return "Bullish";
        if (bearish > bullish) return "Bearish";
        return "Neutraal";
    }

    // -----------------------------------------------------------------------
    // Dutch narrative bullets
    // -----------------------------------------------------------------------

    private static List<string> GenerateBullets(TimeframeAnalysis tf, double price)
    {
        var b = new List<string>();
        if (!tf.HasData) return b;

        // ── EMA9/21 cross ───────────────────────────────────────────────
        if (!string.IsNullOrEmpty(tf.EmaCrossState) && tf.EmaCrossState != "–")
        {
            switch (tf.EmaCrossState)
            {
                case "Bullish kruis":
                    b.Add($"📈 EMA-kruis: EMA9 heeft EMA21 opwaarts gekruist — bullish momentum-signaal. " +
                          $"Dit geeft aan dat de kortetermijntrend omslaat. " +
                          $"Hoe recenter de kruis, hoe relevanter het signaal; bevestig met RSI >50 en positieve MACD.");
                    break;
                case "Bearish kruis":
                    b.Add($"📉 EMA-kruis: EMA9 heeft EMA21 neerwaarts gekruist — bearish momentum-signaal. " +
                          $"De kortetermijntrend draait negatief. " +
                          $"Longs worden riskanter; wacht op herstel boven EMA21 vóór herinstap.");
                    break;
                case "EMA9 boven EMA21":
                    b.Add($"↗️ EMA9 blijft boven EMA21 — aanhoudend opwaarts momentum. " +
                          $"Zolang deze volgorde intact is fungeert de EMA21 als dynamische steun voor de kortetermijntrend. " +
                          $"EMA21 op {Fp(tf.Ema21)} is het eerste cruciale niveau om in de gaten te houden.");
                    break;
                case "EMA9 onder EMA21":
                    b.Add($"↘️ EMA9 blijft onder EMA21 — aanhoudend neerwaarts momentum. " +
                          $"De EMA21 ({Fp(tf.Ema21)}) fungeert als weerstand; bounces ernaar toe zijn potentieel short-entry's. " +
                          $"Trend herstel vereist een sloting boven EMA21.");
                    break;
            }
        }

        // ── Price vs EMA21 ──────────────────────────────────────────────
        if (tf.Ema21 > 0)
        {
            double pct = (price - tf.Ema21) / tf.Ema21 * 100;
            if (pct >= 2)
                b.Add($"Prijs {pct:+0.1}% boven EMA21 ({Fp(tf.Ema21)}). " +
                      $"De EMA21 is een dynamische steunzone; zolang dagelijkse slotingen hierboven plaatsvinden blijft de kortetermijntrend intact. " +
                      $"Terugval naar dit niveau biedt doorgaans een instapkans voor longs.");
            else if (pct < 0)
                b.Add($"Prijs {pct:0.1}% onder EMA21 ({Fp(tf.Ema21)}). " +
                      $"De EMA21 fungeert nu als weerstand — elke bounce ernaar toe is een potentieel short-moment. " +
                      $"Terugkeer boven {Fp(tf.Ema21)} is vereist om de bullish bias te hervatten.");
        }

        // ── Price vs EMA200 ─────────────────────────────────────────────
        if (tf.Ema200 > 0)
        {
            double pct = (price - tf.Ema200) / tf.Ema200 * 100;
            if (pct >= 0)
                b.Add($"✅ EMA200: {Fp(tf.Ema200)} — prijs {pct:+0.1}% erboven. " +
                      $"Dit is het meest gevolgde langetermijn-gemiddelde: erboven signaleert een structurele bull-markt. " +
                      $"Long-trades stromen mee met het macro-klimaat.");
            else
                b.Add($"⚠️ EMA200: {Fp(tf.Ema200)} — prijs {pct:0.1}% eronder. " +
                      $"De langetermijntrend is negatief. Longs in een bear-markt regime vereisen extra bevestiging en strikter risicobeheer; " +
                      $"overweeg een kleinere positiegrootte totdat {Fp(tf.Ema200)} teruggetest wordt.");
        }

        // ── RSI ─────────────────────────────────────────────────────────
        if (tf.Rsi > 0)
        {
            if (tf.Rsi >= 70)
                b.Add($"🔴 RSI {tf.Rsi:F0} — overbought (>70). " +
                      $"De stijging gaat sneller dan historisch gemiddeld; winstneming ligt op de loer. " +
                      $"Geen garantie voor directe daling, maar risico voor nieuwe longs neemt toe. " +
                      $"Overweeg partieel winst vast te leggen of stop dichter te trekken.");
            else if (tf.Rsi <= 30)
                b.Add($"🟢 RSI {tf.Rsi:F0} — oversold (<30). " +
                      $"De indicator signaleert dat de prijs te snel gedaald is t.o.v. recente prijzen. " +
                      $"Dit is geen garantie voor directe bodem, maar verhoogt statistische kans op herstel. " +
                      $"Wacht op bevestiging: een hogere low of bullish candlestick vóór instap.");
            else if (tf.Rsi > 50)
                b.Add($"RSI {tf.Rsi:F0} — positief momentum. " +
                      $"Boven de 50-middenlijn bevestigt de RSI dat bulls de overhand hebben. " +
                      $"Pas bij 70+ ontstaat overbought-risico; tot die tijd ondersteunt de RSI de opwaartse trend.");
            else
                b.Add($"RSI {tf.Rsi:F0} — licht negatief momentum. " +
                      $"Onder de 50-middenlijn drukken bears de koers omlaag. " +
                      $"Herstel is bevestigd zodra RSI de 50 herverovert met een stijgende beweging.");
        }

        // ── MACD ────────────────────────────────────────────────────────
        if (tf.Macd != 0)
        {
            bool macdBull = tf.Macd > tf.MacdSignal;
            bool macdPos  = tf.Macd > 0;
            if (macdBull && macdPos)
                b.Add($"MACD {tf.Macd:+0.00;-0.00} — boven signaallijn en positief. " +
                      $"Bulls hebben zowel op korte als middellange termijn de overhand: het verschil tussen snelle en trage EMA groeit. " +
                      $"Dit is het sterkste MACD-signaal; bevestigt een gunstig momentum-klimaat voor longs.");
            else if (!macdBull && !macdPos)
                b.Add($"MACD {tf.Macd:+0.00;-0.00} — onder signaallijn en negatief. " +
                      $"Verkoopdruk neemt toe op zowel kort als middellange termijn. " +
                      $"Het negatieve histogram duidt op versnellende bearish kracht; longs zijn riskant zolang MACD onder nul blijft.");
            else if (macdBull)
                b.Add($"MACD {tf.Macd:+0.00;-0.00} — bullish kruis (boven signaallijn, maar nog negatief). " +
                      $"Vroeg signaal dat momentum omslaat naar positief. " +
                      $"Bevestigender zodra MACD ook de nul-lijn doorbreekt; houd dit in de gaten voor instap.");
            else
                b.Add($"MACD {tf.Macd:+0.00;-0.00} — bearish kruis (onder signaallijn). " +
                      $"Momentum slaat om naar negatief. " +
                      $"Wees voorzichtig met longs; wacht op herstel van de signaallijn alvorens opnieuw long te gaan.");
        }

        // ── ADX ─────────────────────────────────────────────────────────
        if (tf.Adx > 0)
        {
            if (tf.Adx >= 25)
                b.Add($"ADX {tf.Adx:F0} — krachtige trend aanwezig. " +
                      $"Een ADX boven 25 bevestigt dat de koersbeweging gericht en momentum-gedreven is, niet zijwaarts. " +
                      $"Dit maakt trendvolgende indicatoren (EMA-cross, MACD) betrouwbaarder en breekouts succesvoller.");
            else if (tf.Adx < 20)
                b.Add($"ADX {tf.Adx:F0} — zijwaartse range-markt. " +
                      $"Trendvolgende strategieën zijn minder effectief; de markt beweegt zonder duidelijke richting. " +
                      $"Pas een range-strategie toe (kopen bij steun, verkopen bij weerstand) of wacht op ADX >25 voor een trendbevestiging.");
        }

        // ── Bollinger Squeeze ───────────────────────────────────────────
        if (tf.IsSqueeze)
            b.Add($"⚡ Bollinger Squeeze actief: de Bollinger Bands (BB) zijn smaller dan het Keltner Channel (KC). " +
                  $"BB meet standaarddeviatie; KC meet ATR-gebaseerde range. Als BB < KC zit de markt in extreme volatiliteitscompressie. " +
                  $"Historisch volgt hierop een krachtige, snelle uitbraak — de richting bepaal je met MACD en EMA-positie.");

        // ── %B ──────────────────────────────────────────────────────────
        if (tf.PctB > 0)
        {
            if (tf.PctB > 85)
                b.Add($"%B {tf.PctB:F0} — prijs bij de bovenste Bollinger Band. " +
                      $"De bovenste band ligt 2 standaarddeviaties boven het 20-perioden gemiddelde. " +
                      $"Statistisch 'uitgerekt'; normaal gesproken volgt terugkeer naar de midlijn. " +
                      $"Kan ook extreme kracht signaleren — beoordeel in combinatie met volume en ADX.");
            else if (tf.PctB < 15)
                b.Add($"%B {tf.PctB:F0} — prijs bij de onderste Bollinger Band. " +
                      $"2 standaarddeviaties onder het gemiddelde — statistisch overgedrukt. " +
                      $"Veelal volgt technisch herstel richting de midlijn ({Fp(tf.Ema21 > 0 ? tf.Ema21 : price)}). " +
                      $"In een sterke downtrend kunnen prijzen langs de band 'rijden'; bevestig met RSI <30 voor extra gewicht.");
        }

        // ── ATR ─────────────────────────────────────────────────────────
        if (tf.Atr > 0)
        {
            double atrPct = tf.Atr / price * 100;
            b.Add($"ATR {Fp(tf.Atr)} ({atrPct:F1}% van prijs) — gemiddeld werkelijk koersbereik per candle. " +
                  $"Gebruik als maatstaf voor stop-loss: een stop dichter dan 1× ATR ({Fp(tf.Atr)}) " +
                  $"wordt te snel geraakt door normale marktfluctuaties. Minimale stop: {Fp(tf.Atr * 1.5)} (1,5× ATR).");
        }

        return b;
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static string DirectionFromScore(double score)
        => score >= 60 ? "Long" : score <= 40 ? "Short" : "Flat";

    private static double Ema(IEnumerable<Quote> quotes, int period)
        => (double)(quotes.GetEma(period).LastOrDefault()?.Ema ?? 0);

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

    /// <summary>Formats a price with an appropriate number of decimal places.</summary>
    private static string Fp(double price)
        => price >= 10000 ? $"${price:N0}"
         : price >= 1000  ? $"${price:N1}"
         : price >= 100   ? $"${price:N2}"
         : price >= 10    ? $"${price:N3}"
         : price >= 1     ? $"${price:N4}"
         : price >= 0.01  ? $"${price:N5}"
         :                   $"${price:N6}";
}
