using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;
using Serilog;
using Serilog.Core;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Pattern detection engine — pure computation, no I/O.
///
/// LEVEL 1 patterns are derived from pre-computed indicator values (RSI, MACD, EMA, ADX, %B).
/// LEVEL 2 patterns are derived from raw OHLCV bars via swing-high/low detection.
///
/// Adding a new pattern:
///  1. Add an entry to PatternType enum (PatternEnums.cs).
///  2. Add a DisplayName mapping in PatternResult.DisplayName.
///  3. Add a private Detect* method here and call it from DetectFromBars or DetectFromIndicators.
/// </summary>
public class PatternDetectionService : IPatternDetectionService
{
    private static readonly ILogger Logger = Log.Logger.ForContext(
        Constants.SourceContextPropertyName, nameof(PatternDetectionService).PadRight(22));

    // =========================================================================
    // IPatternDetectionService — public interface
    // =========================================================================

    public List<PatternResult> DetectFromIndicators(
        TimeframeAnalysis tf, string timeframeLabel, double currentPrice)
    {
        var results = new List<PatternResult>();
        if (!tf.HasData || currentPrice <= 0) return results;

        TryAdd(results, DetectRsiOversold(tf, timeframeLabel));
        TryAdd(results, DetectRsiOverbought(tf, timeframeLabel));
        TryAdd(results, DetectMacdSignal(tf, timeframeLabel));
        TryAdd(results, DetectEmaCross(tf, timeframeLabel));
        TryAdd(results, DetectPriceVsEma50(tf, timeframeLabel, currentPrice));
        TryAdd(results, DetectBollingerSqueeze(tf, timeframeLabel));
        TryAdd(results, DetectTrendingMarket(tf, timeframeLabel));

        return results;
    }

    public List<PatternResult> DetectFromBars(
        List<OhlcvBar> bars, string timeframeLabel, double currentPrice)
    {
        var results = new List<PatternResult>();
        if (bars.Count < 20 || currentPrice <= 0) return results;

        // Use last 120 candles max for all algorithms to keep things fast
        var data = bars.TakeLast(120).ToList();

        // Swing significance scales with the coin's own volatility (≈0.4 ATR) so a pivot must
        // be a real structural peak/trough, not noise. lookback 5 = fractal-style 5-bar pivot.
        double atr    = AverageTrueRange(data, 14);
        double minSig = atr * 0.40;
        var swingHighs = FindSwingHighs(data, lookback: 5, minSig);
        var swingLows  = FindSwingLows(data,  lookback: 5, minSig);

        TryAdd(results, DetectVolumeSpike(data, timeframeLabel));
        TryAdd(results, DetectTrend(data, swingHighs, swingLows, timeframeLabel, currentPrice));
        TryAdd(results, DetectDoubleBottom(data, swingLows, timeframeLabel, currentPrice));
        TryAdd(results, DetectDoubleTop(data, swingHighs, timeframeLabel, currentPrice));
        TryAdd(results, DetectBullFlag(data, timeframeLabel, currentPrice));
        TryAdd(results, DetectBearFlag(data, timeframeLabel, currentPrice));
        TryAdd(results, DetectTriangle(data, swingHighs, swingLows, timeframeLabel, currentPrice));
        TryAdd(results, DetectConsolidation(data, timeframeLabel, currentPrice));
        TryAdd(results, DetectBreakoutBreakdown(data, swingHighs, swingLows, timeframeLabel, currentPrice));
        TryAdd(results, DetectAdamAndEve(data, swingLows, timeframeLabel, currentPrice));
        TryAdd(results, DetectChannel(data, swingHighs, swingLows, timeframeLabel, currentPrice));

        // Level 3 — complex classical patterns (require more data)
        if (data.Count >= 50)
        {
            TryAdd(results, DetectHeadAndShoulders(data, swingHighs, swingLows, timeframeLabel, currentPrice));
            TryAdd(results, DetectInverseHeadAndShoulders(data, swingHighs, swingLows, timeframeLabel, currentPrice));
            TryAdd(results, DetectWedge(data, swingHighs, swingLows, timeframeLabel, currentPrice));
            TryAdd(results, DetectCupAndHandle(data, timeframeLabel, currentPrice));
        }

        return results;
    }

    public (int score, string direction) CalculateTradabilityScore(
        List<PatternResult> patterns,
        TimeframeAnalysis   daily,
        TimeframeAnalysis   h4)
    {
        // Each direction accumulates weighted points separately.
        // Final score = max(bull, bear); direction follows the winner.

        double bull = 0, bear = 0;

        // ── Pattern contribution (max ~40 pts combined) ──────────────────────
        foreach (var p in patterns)
        {
            double contrib = p.Strength * 0.25; // 100-strength pattern → 25 pts raw
            if (p.Category == PatternCategory.Bullish) bull += contrib;
            else if (p.Category == PatternCategory.Bearish) bear += contrib;
            else
            {
                // Neutral (squeeze, consolidation, ADX) adds to BOTH — amplifier
                bull += contrib * 0.4;
                bear += contrib * 0.4;
            }
        }

        // Cap pattern contribution at 40 pts per direction
        bull = Math.Min(bull, 40);
        bear = Math.Min(bear, 40);

        // ── Daily trend bias (max 15 pts) ────────────────────────────────────
        if (daily.HasData)
        {
            if      (daily.TrendBias == "Bullish") bull += 15;
            else if (daily.TrendBias == "Bearish") bear += 15;
            else { bull += 5; bear += 5; } // neutral adds small amount to both
        }

        // ── 4H trend alignment bonus (max 8 pts) ─────────────────────────────
        if (h4.HasData)
        {
            if      (h4.TrendBias == "Bullish") bull += 8;
            else if (h4.TrendBias == "Bearish") bear += 8;
        }

        // ── Daily RSI momentum (max 10 pts) ──────────────────────────────────
        if (daily.HasData && daily.Rsi > 0)
        {
            if      (daily.Rsi < 30) bull += 10;   // deeply oversold → potential reversal
            else if (daily.Rsi < 40) bull += 5;
            else if (daily.Rsi > 70) bear += 10;   // overbought → caution
            else if (daily.Rsi > 60) bear += 4;
        }

        // ── MACD signal (max 6 pts) ───────────────────────────────────────────
        if (daily.HasData && (daily.Macd != 0 || daily.MacdSignal != 0))
        {
            if      (daily.Macd > daily.MacdSignal) bull += 6;
            else if (daily.Macd < daily.MacdSignal) bear += 6;
        }

        // ── ADX bonus for trending market (+4 pts to dominant direction) ─────
        if (daily.HasData && daily.Adx >= 25)
        {
            if (bull > bear) bull += 4;
            else if (bear > bull) bear += 4;
        }

        // ── Breakout proximity adds urgency (max 8 pts to bull) ──────────────
        bool nearBreakout = patterns.Any(p =>
            p.Type == PatternType.BreakoutAboveResistance ||
            p.Type == PatternType.PotentialBreakout);
        bool nearBreakdown = patterns.Any(p => p.Type == PatternType.BreakdownBelowSupport);
        if (nearBreakout)  bull += 8;
        if (nearBreakdown) bear += 8;

        // ── Normalise to 0-100 ────────────────────────────────────────────────
        // At full score, bull or bear tops out at ~91 pts.
        // Map that range [0, 91] → [0, 100].
        const double maxPossible = 91.0;
        int bullScore = (int)Math.Min(100, Math.Round(bull / maxPossible * 100));
        int bearScore = (int)Math.Min(100, Math.Round(bear / maxPossible * 100));

        int finalScore;
        string direction;

        if (bullScore > bearScore + 5)
        {
            finalScore = bullScore;
            direction  = "Long";
        }
        else if (bearScore > bullScore + 5)
        {
            finalScore = bearScore;
            direction  = "Short";
        }
        else
        {
            finalScore = Math.Max(bullScore, bearScore);
            direction  = "Neutraal";
        }

        return (finalScore, direction);
    }

    // =========================================================================
    // LEVEL 1 — Indicator-based pattern detectors
    // =========================================================================

    private static PatternResult? DetectRsiOversold(TimeframeAnalysis tf, string tfl)
    {
        if (tf.Rsi <= 0 || tf.Rsi >= 30) return null;
        int strength = (int)Math.Min(85, 50 + (30 - tf.Rsi) * 3.5);
        return new PatternResult
        {
            Type        = PatternType.RsiOversold,
            Category    = PatternCategory.Bullish,
            Timeframe   = tfl,
            IsConfirmed = true,
            Strength    = strength,
            Description = $"RSI {tf.Rsi:F0} — oververkocht (<30). Historisch verhoogde kans op technisch herstel. Combineer met EMA- of MACD-bevestiging.",
        };
    }

    private static PatternResult? DetectRsiOverbought(TimeframeAnalysis tf, string tfl)
    {
        if (tf.Rsi <= 70) return null;
        int strength = (int)Math.Min(85, 50 + (tf.Rsi - 70) * 3.5);
        return new PatternResult
        {
            Type        = PatternType.RsiOverbought,
            Category    = PatternCategory.Bearish,
            Timeframe   = tfl,
            IsConfirmed = true,
            Strength    = strength,
            Description = $"RSI {tf.Rsi:F0} — overbought (>70). Verhoogde kans op winstneming of correctie. Wacht op bevestiging via bearish candle of MACD-divergentie.",
        };
    }

    private static PatternResult? DetectMacdSignal(TimeframeAnalysis tf, string tfl)
    {
        bool hasMacd = tf.Macd != 0 || tf.MacdSignal != 0;
        if (!hasMacd) return null;

        if (tf.Macd > tf.MacdSignal)
            return new PatternResult
            {
                Type        = PatternType.MacdBullishCross,
                Category    = PatternCategory.Bullish,
                Timeframe   = tfl,
                IsConfirmed = true,
                Strength    = 58,
                Description = "MACD boven signaallijn — bullish momentum. Hoe groter de afstand, hoe sterker het signaal.",
            };

        return new PatternResult
        {
            Type        = PatternType.MacdBearishCross,
            Category    = PatternCategory.Bearish,
            Timeframe   = tfl,
            IsConfirmed = true,
            Strength    = 58,
            Description = "MACD onder signaallijn — bearish momentum. Pas op voor verdere daling.",
        };
    }

    private static PatternResult? DetectEmaCross(TimeframeAnalysis tf, string tfl)
    {
        return tf.EmaCrossState switch
        {
            "Bullish kruis" => new PatternResult
            {
                Type        = PatternType.EmaBullishCross,
                Category    = PatternCategory.Bullish,
                Timeframe   = tfl,
                IsConfirmed = true,
                Strength    = 75,
                Description = "EMA9 heeft EMA21 opwaarts gekruist — vers bullish momentum-signaal. Hogere betrouwbaarheid wanneer ADX > 25.",
            },
            "Bearish kruis" => new PatternResult
            {
                Type        = PatternType.EmaBearishCross,
                Category    = PatternCategory.Bearish,
                Timeframe   = tfl,
                IsConfirmed = true,
                Strength    = 75,
                Description = "EMA9 heeft EMA21 neerwaarts gekruist — vers bearish signaal. Longs worden riskanter totdat herstel boven EMA21.",
            },
            "EMA9 boven EMA21" => new PatternResult
            {
                Type        = PatternType.EmaBullishCross,
                Category    = PatternCategory.Bullish,
                Timeframe   = tfl,
                IsConfirmed = true,
                Strength    = 55,
                Description = "EMA9 boven EMA21 — aanhoudend bullish korte-termijn momentum.",
            },
            "EMA9 onder EMA21" => new PatternResult
            {
                Type        = PatternType.EmaBearishCross,
                Category    = PatternCategory.Bearish,
                Timeframe   = tfl,
                IsConfirmed = true,
                Strength    = 55,
                Description = "EMA9 onder EMA21 — aanhoudend bearish korte-termijn momentum.",
            },
            _ => null,
        };
    }

    private static PatternResult? DetectPriceVsEma50(
        TimeframeAnalysis tf, string tfl, double price)
    {
        if (tf.Ema50 <= 0) return null;

        if (price > tf.Ema50 * 1.005)
            return new PatternResult
            {
                Type        = PatternType.PriceAboveEma50,
                Category    = PatternCategory.Bullish,
                Timeframe   = tfl,
                IsConfirmed = true,
                Strength    = 55,
                Description = $"Prijs boven EMA50 (${tf.Ema50:G5}) — structureel bullish context. EMA50 is key steun.",
                KeyLevel    = tf.Ema50,
            };

        if (price < tf.Ema50 * 0.995)
            return new PatternResult
            {
                Type        = PatternType.PriceBelowEma50,
                Category    = PatternCategory.Bearish,
                Timeframe   = tfl,
                IsConfirmed = true,
                Strength    = 55,
                Description = $"Prijs onder EMA50 (${tf.Ema50:G5}) — structureel bearish context. EMA50 is key weerstand.",
                KeyLevel    = tf.Ema50,
            };

        return null;
    }

    private static PatternResult? DetectBollingerSqueeze(TimeframeAnalysis tf, string tfl)
    {
        if (!tf.IsSqueeze) return null;
        return new PatternResult
        {
            Type        = PatternType.BollingerSqueeze,
            Category    = PatternCategory.Neutral,
            Timeframe   = tfl,
            IsConfirmed = true,
            Strength    = 70,
            Description = "Bollinger Squeeze actief — extreem lage volatiliteit. Na een squeeze volgt statistisch gezien een krachtige uitbraak. Let op de richting van de eerste doorbraak.",
        };
    }

    private static PatternResult? DetectTrendingMarket(TimeframeAnalysis tf, string tfl)
    {
        if (tf.Adx < 25) return null;
        int strength = (int)Math.Min(90, tf.Adx * 2);
        return new PatternResult
        {
            Type        = PatternType.TrendingMarket,
            Category    = PatternCategory.Neutral,
            Timeframe   = tfl,
            IsConfirmed = true,
            Strength    = strength,
            Description = $"ADX {tf.Adx:F0} — trending markt (ADX >25). Trendvolgende setups zijn nu betrouwbaarder; range-strategieën werken minder goed.",
        };
    }

    // =========================================================================
    // LEVEL 2 — OHLCV swing-point pattern detectors
    // =========================================================================

    // ── Volume spike ──────────────────────────────────────────────────────────
    private static PatternResult? DetectVolumeSpike(List<OhlcvBar> bars, string tfl)
    {
        if (bars.Count < 22) return null;

        double lastVol = bars[^1].Volume;
        if (lastVol <= 0) return null; // no volume data (local cache)

        double avgVol = bars.TakeLast(21).SkipLast(1).Average(b => b.Volume);
        if (avgVol <= 0) return null;

        double ratio = lastVol / avgVol;
        if (ratio < 1.8) return null;

        int strength = (int)Math.Min(90, 55 + (ratio - 1.8) * 20);
        return new PatternResult
        {
            Type        = PatternType.VolumeSpike,
            Category    = PatternCategory.Neutral,
            Timeframe   = tfl,
            IsConfirmed = true,
            Strength    = strength,
            Description = $"Volume spike: {ratio:F1}× het 20-periods gemiddelde — sterke marktparticipatie. Volume bevestigt de beweging; hoge kans op verdere koersontwikkeling in de huidige richting.",
        };
    }

    // ── Trend (HH+HL vs LH+LL) ────────────────────────────────────────────────
    private static PatternResult? DetectTrend(
        List<OhlcvBar> bars,
        List<(int idx, double value)> swingHighs,
        List<(int idx, double value)> swingLows,
        string tfl, double currentPrice)
    {
        // Need at least 3 recent swing highs and lows, and both must be current
        if (!HasRecentSwing(swingHighs, bars.Count) || !HasRecentSwing(swingLows, bars.Count)) return null;

        var recentHighs = swingHighs.TakeLast(4).ToList();
        var recentLows  = swingLows .TakeLast(4).ToList();

        if (recentHighs.Count < 3 || recentLows.Count < 3) return null;

        // Check for HH + HL (uptrend)
        bool hh = recentHighs[^1].value > recentHighs[^2].value &&
                  recentHighs[^2].value > recentHighs[^3].value;
        bool hl = recentLows[^1].value  > recentLows[^2].value  &&
                  recentLows[^2].value  > recentLows[^3].value;

        if (hh && hl)
            return new PatternResult
            {
                Type        = PatternType.Uptrend,
                Category    = PatternCategory.Bullish,
                Timeframe   = tfl,
                IsConfirmed = true,
                Strength    = 70,
                Description = "Hogere toppen én hogere bodems — bevestigde opwaartse trend. Pullbacks naar voorgaande hogere bodem zijn instapkansen.",
            };

        // Check for LH + LL (downtrend)
        bool lh = recentHighs[^1].value < recentHighs[^2].value &&
                  recentHighs[^2].value < recentHighs[^3].value;
        bool ll = recentLows[^1].value  < recentLows[^2].value  &&
                  recentLows[^2].value  < recentLows[^3].value;

        if (lh && ll)
            return new PatternResult
            {
                Type        = PatternType.Downtrend,
                Category    = PatternCategory.Bearish,
                Timeframe   = tfl,
                IsConfirmed = true,
                Strength    = 70,
                Description = "Lagere toppen én lagere bodems — bevestigde neerwaartse trend. Bounces zijn verkoopkansen; wacht op structuurwijziging vóór long-entries.",
            };

        return null;
    }

    // ── Double bottom ─────────────────────────────────────────────────────────
    private static PatternResult? DetectDoubleBottom(
        List<OhlcvBar> bars,
        List<(int idx, double value)> swingLows,
        string tfl, double currentPrice)
    {
        if (swingLows.Count < 2) return null;

        // Recency guard: the most recent low must be within the last 20 bars (current candle involved).
        if (!HasRecentSwing(swingLows, bars.Count)) return null;

        // Context: double bottom is only a reversal signal after a meaningful downtrend.
        // Require the price 50 bars ago to be ≥ 15% above the current swing low area (handbook §10.4).
        if (bars.Count >= 50)
        {
            double priceBack50 = bars[^50].Close;
            double recentLow   = swingLows.TakeLast(3).Min(s => s.value);
            double priorDrop   = (priceBack50 - recentLow) / priceBack50;
            if (priorDrop < 0.15) return null; // no meaningful downtrend before the double bottom
        }

        // Look for two recent lows within 3 % of each other (handbook: F2 — 3%, not 2.5%)
        var last = swingLows[^1];
        for (int i = swingLows.Count - 2; i >= Math.Max(0, swingLows.Count - 6); i--)
        {
            var prev = swingLows[i];
            if (last.idx - prev.idx < 8) continue;  // must be separated by at least 8 bars (handbook: F1)

            double diff = Math.Abs(last.value - prev.value) / prev.value;
            if (diff > 0.03) continue;

            // Minimum 5% depth: the valley between the two lows must dip at least 5% below the higher low
            double higherLow = Math.Max(prev.value, last.value);
            double midMin    = bars.Skip(prev.idx).Take(last.idx - prev.idx + 1).Min(b => b.Open);  // body bottom
            double depth     = (higherLow - midMin) / higherLow;
            if (depth < 0.05) continue;

            // Confirm price has moved up from the double bottom
            double recovery = (currentPrice - last.value) / last.value;
            if (recovery < 0.01) continue; // price hasn't recovered yet

            double distPct = recovery * 100;
            bool confirmed = recovery > 0.04; // >4 % recovery = confirmed

            double neckline = Math.Max(
                bars.Skip(prev.idx).Take(last.idx - prev.idx + 1).Max(b => b.Close),  // body top
                currentPrice);

            // Don't report if price has already moved >8% past the neckline (pattern is stale)
            if (IsPatternStale(currentPrice, neckline, PatternCategory.Bullish)) continue;

            return new PatternResult
            {
                Type        = PatternType.DoubleBottom,
                Category    = PatternCategory.Bullish,
                Timeframe   = tfl,
                IsConfirmed = confirmed,
                Strength    = confirmed ? 78 : 60,
                Description = $"Dubbele bodem gevonden (lows: {FormatP(prev.value)} / {FormatP(last.value)}). "
                            + (confirmed
                               ? "Patroon bevestigd — bullish reversal signaal."
                               : "Nog niet bevestigd — wacht op sloting boven de neklijn."),
                KeyLevel    = neckline,
                Annotation  = new PatternAnnotation
                {
                    Markers = new()
                    {
                        new PatternPoint { Time = bars[prev.idx].Date, Price = prev.value, Label = "B1", AboveBar = false },
                        new PatternPoint { Time = bars[last.idx].Date, Price = last.value, Label = "B2", AboveBar = false },
                    },
                    // Neklijn als begrensd segment over het patroon (eerste bodem → huidige candle).
                    Trendlines = new()
                    {
                        new PatternTrendline { StartTime = bars[prev.idx].Date, StartPrice = neckline, EndTime = bars[^1].Date, EndPrice = neckline, Color = "#26a69a" },
                    },
                },
            };
        }
        return null;
    }

    // ── Double top ────────────────────────────────────────────────────────────
    private static PatternResult? DetectDoubleTop(
        List<OhlcvBar> bars,
        List<(int idx, double value)> swingHighs,
        string tfl, double currentPrice)
    {
        if (swingHighs.Count < 2) return null;

        // Recency guard: the most recent high must be within the last 20 bars (current candle involved).
        if (!HasRecentSwing(swingHighs, bars.Count)) return null;

        // Context: double top is only a reversal signal after a meaningful uptrend.
        // Require the price 50 bars ago to be ≥ 15% below the current swing high area (handbook §10.5).
        if (bars.Count >= 50)
        {
            double priceBack50 = bars[^50].Close;
            double recentHigh  = swingHighs.TakeLast(3).Max(s => s.value);
            double priorRise   = (recentHigh - priceBack50) / priceBack50;
            if (priorRise < 0.15) return null; // no meaningful uptrend before the double top
        }

        var last = swingHighs[^1];
        for (int i = swingHighs.Count - 2; i >= Math.Max(0, swingHighs.Count - 6); i--)
        {
            var prev = swingHighs[i];
            if (last.idx - prev.idx < 8) continue;  // must be separated by at least 8 bars (handbook: F1)

            double diff = Math.Abs(last.value - prev.value) / prev.value;
            if (diff > 0.03) continue;  // handbook: 3%, not 2.5% (F2)

            // Minimum 5% depth: the valley between the two highs must be at least 5% below the lower high
            double lowerHigh = Math.Min(prev.value, last.value);
            double midMin    = bars.Skip(prev.idx).Take(last.idx - prev.idx + 1).Min(b => b.Open);  // body bottom
            double depth     = (lowerHigh - midMin) / lowerHigh;
            if (depth < 0.05) continue;

            double decline = (last.value - currentPrice) / last.value;
            if (decline < 0.01) continue;

            bool confirmed = decline > 0.04;
            double neckline = Math.Min(
                bars.Skip(prev.idx).Take(last.idx - prev.idx + 1).Min(b => b.Open),  // body bottom
                currentPrice);

            // Don't report if price has already moved >8% past the neckline (pattern is stale)
            if (IsPatternStale(currentPrice, neckline, PatternCategory.Bearish)) continue;

            return new PatternResult
            {
                Type        = PatternType.DoubleTop,
                Category    = PatternCategory.Bearish,
                Timeframe   = tfl,
                IsConfirmed = confirmed,
                Strength    = confirmed ? 78 : 60,
                Description = $"Dubbele top gevonden (highs: {FormatP(prev.value)} / {FormatP(last.value)}). "
                            + (confirmed
                               ? "Patroon bevestigd — bearish reversal signaal."
                               : "Nog niet bevestigd — wacht op breakdown onder de neklijn."),
                KeyLevel    = neckline,
                Annotation  = new PatternAnnotation
                {
                    Markers = new()
                    {
                        new PatternPoint { Time = bars[prev.idx].Date, Price = prev.value, Label = "T1", AboveBar = true },
                        new PatternPoint { Time = bars[last.idx].Date, Price = last.value, Label = "T2", AboveBar = true },
                    },
                    // Neklijn als begrensd segment over het patroon (eerste top → huidige candle).
                    Trendlines = new()
                    {
                        new PatternTrendline { StartTime = bars[prev.idx].Date, StartPrice = neckline, EndTime = bars[^1].Date, EndPrice = neckline, Color = "#ef5350" },
                    },
                },
            };
        }
        return null;
    }

    // ── Bull flag ─────────────────────────────────────────────────────────────
    private static PatternResult? DetectBullFlag(
        List<OhlcvBar> bars, string tfl, double currentPrice)
    {
        if (bars.Count < 15) return null;

        // Pole = the run before the flag (bars [-14 … -6]); flag = last 5 bars.
        var poleBars = bars.Skip(Math.Max(0, bars.Count - 14)).Take(9).ToList();
        var flag     = bars.TakeLast(5).ToList();
        if (poleBars.Count < 5) return null;

        // Pole measured on the WICKS (visible extremes), from its low to its high.
        double poleLow  = poleBars.Min(b => b.Low);
        double poleHigh = poleBars.Max(b => b.High);
        double poleGain = poleLow > 0 ? (poleHigh - poleLow) / poleLow : 0;
        if (poleGain < 0.08) return null;             // need ≥ 8% up move for a pole

        // The pole must actually rise: its high has to come AFTER its low.
        int loIdx = poleBars.FindIndex(b => b.Low  <= poleLow  + 1e-12);
        int hiIdx = poleBars.FindLastIndex(b => b.High >= poleHigh - 1e-12);
        if (hiIdx <= loIdx) return null;

        // Flag on wicks — tight consolidation.
        double flagHigh  = flag.Max(b => b.High);
        double flagLow   = flag.Min(b => b.Low);
        double flagRange = flagHigh > 0 ? (flagHigh - flagLow) / flagHigh : 1;
        if (flagRange > 0.06) return null;            // flag must be tight (< 6% range, wick-based)

        // A flag CONSOLIDATES — it drifts sideways/down. A 5-bar stretch that keeps ripping up
        // is still part of the pole, not a flag. Reject a strongly rising consolidation.
        double flagSlope = LinearSlope(flag.Select(b => b.Close).ToList());
        if (flagSlope > 0.004) return null;

        // Flag should not retrace more than 50 % of the pole.
        double retrace = (poleHigh - flagLow) / (poleHigh - poleLow);
        if (retrace > 0.5) return null;

        return new PatternResult
        {
            Type        = PatternType.BullFlag,
            Category    = PatternCategory.Bullish,
            Timeframe   = tfl,
            IsConfirmed = false,  // waiting for breakout of flag top
            Strength    = 72,
            Description = $"Bull flag: sterke opwaartse pool (+{poleGain * 100:F1}%) gevolgd door consolidatie. "
                        + $"Breakout boven {FormatP(flagHigh)} activeert het patroon met koersdoel paal-hoogte erboven.",
            KeyLevel    = flagHigh,
            DistancePct = (flagHigh - currentPrice) / currentPrice * 100,
            Annotation  = new PatternAnnotation
            {
                Trendlines = new()
                {
                    // Pole: diagonale lijn van wick-low naar wick-high.
                    new PatternTrendline
                    {
                        StartTime  = poleBars[loIdx].Date, StartPrice = poleLow,
                        EndTime    = poleBars[hiIdx].Date, EndPrice   = poleHigh,
                        Color      = "#26a69a",
                    },
                    // Vlag-vak: korte boven- en onderlijn over alléén de consolidatie-candles,
                    // zodat het vlaggetje zichtbaar is als een begrensd vak aan het eind van de pool.
                    new PatternTrendline
                    {
                        StartTime = flag.First().Date, StartPrice = flagHigh,
                        EndTime   = flag.Last().Date,  EndPrice   = flagHigh, Color = "#f59e0b",
                    },
                    new PatternTrendline
                    {
                        StartTime = flag.First().Date, StartPrice = flagLow,
                        EndTime   = flag.Last().Date,  EndPrice   = flagLow,  Color = "#f59e0b",
                    },
                },
                // Breakout-trigger als één volle-breedte lijn.
                HLines = new()
                {
                    new PatternHLine { Price = flagHigh, Color = "#26a69a", Title = "Breakout" },
                },
            },
        };
    }

    // ── Bear flag ─────────────────────────────────────────────────────────────
    private static PatternResult? DetectBearFlag(
        List<OhlcvBar> bars, string tfl, double currentPrice)
    {
        if (bars.Count < 15) return null;

        // Pole = the run before the flag (bars [-14 … -6]); flag = last 5 bars.
        var poleBars = bars.Skip(Math.Max(0, bars.Count - 14)).Take(9).ToList();
        var flag     = bars.TakeLast(5).ToList();
        if (poleBars.Count < 5) return null;

        // Pole measured on the WICKS, from its high to its low.
        double poleHigh = poleBars.Max(b => b.High);
        double poleLow  = poleBars.Min(b => b.Low);
        double poleLoss = poleHigh > 0 ? (poleHigh - poleLow) / poleHigh : 0;
        if (poleLoss < 0.08) return null;             // need ≥ 8% down move for a pole

        // The pole must actually fall: its low has to come AFTER its high.
        int hiIdx = poleBars.FindIndex(b => b.High >= poleHigh - 1e-12);
        int loIdx = poleBars.FindLastIndex(b => b.Low <= poleLow + 1e-12);
        if (loIdx <= hiIdx) return null;

        // Flag on wicks — tight consolidation.
        double flagHigh  = flag.Max(b => b.High);
        double flagLow   = flag.Min(b => b.Low);
        double flagRange = flagHigh > 0 ? (flagHigh - flagLow) / flagHigh : 1;
        if (flagRange > 0.06) return null;            // flag must be tight (< 6% range, wick-based)

        // A bear-flag consolidation drifts sideways/up. A 5-bar stretch still falling hard is
        // part of the pole, not a flag → reject a strongly falling consolidation.
        double flagSlope = LinearSlope(flag.Select(b => b.Close).ToList());
        if (flagSlope < -0.004) return null;

        double retrace = (flagHigh - poleLow) / (poleHigh - poleLow);
        if (retrace > 0.5) return null;

        return new PatternResult
        {
            Type        = PatternType.BearFlag,
            Category    = PatternCategory.Bearish,
            Timeframe   = tfl,
            IsConfirmed = false,
            Strength    = 72,
            Description = $"Bear flag: scherpe daling (-{poleLoss * 100:F1}%) gevolgd door consolidatie. "
                        + $"Breakdown onder {FormatP(flagLow)} bevestigt vervolg neerwaarts.",
            KeyLevel    = flagLow,
            DistancePct = (currentPrice - flagLow) / currentPrice * 100,
            Annotation  = new PatternAnnotation
            {
                Trendlines = new()
                {
                    // Pole: diagonale lijn van wick-high naar wick-low.
                    new PatternTrendline
                    {
                        StartTime  = poleBars[hiIdx].Date, StartPrice = poleHigh,
                        EndTime    = poleBars[loIdx].Date, EndPrice   = poleLow,
                        Color      = "#ef5350",
                    },
                    // Vlag-vak: korte boven- en onderlijn over alléén de consolidatie-candles.
                    new PatternTrendline
                    {
                        StartTime = flag.First().Date, StartPrice = flagHigh,
                        EndTime   = flag.Last().Date,  EndPrice   = flagHigh, Color = "#f59e0b",
                    },
                    new PatternTrendline
                    {
                        StartTime = flag.First().Date, StartPrice = flagLow,
                        EndTime   = flag.Last().Date,  EndPrice   = flagLow,  Color = "#f59e0b",
                    },
                },
                // Breakdown-trigger als één volle-breedte lijn.
                HLines = new()
                {
                    new PatternHLine { Price = flagLow, Color = "#ef5350", Title = "Breakdown" },
                },
            },
        };
    }

    // ── Triangle (ascending / descending / symmetrical) ────────────────────────
    private static PatternResult? DetectTriangle(
        List<OhlcvBar> bars,
        List<(int idx, double value)> swingHighs,
        List<(int idx, double value)> swingLows,
        string tfl, double currentPrice)
    {
        // Need at least 3 recent swing highs and lows, and both must be current
        if (!HasRecentSwing(swingHighs, bars.Count) || !HasRecentSwing(swingLows, bars.Count)) return null;

        var rh = swingHighs.TakeLast(4).ToList();
        var rl = swingLows .TakeLast(4).ToList();
        if (rh.Count < 3 || rl.Count < 3) return null;

        // Overlapping window on the real time axis + bar-index regression for both lines
        int winStart = Math.Max(rh.First().idx, rl.First().idx);
        int winEnd   = Math.Min(rh.Last().idx,  rl.Last().idx);
        if (winEnd - winStart < 10) return null;

        var (highSlope, highInt) = LinearRegressionByBarIdx(rh);
        var (lowSlope,  lowInt)  = LinearRegressionByBarIdx(rl);

        // Goodness-of-fit guard: the swings must lie on their trendlines.
        if (RSquaredByBarIdx(rh, highSlope, highInt) < 0.70 ||
            RSquaredByBarIdx(rl, lowSlope,  lowInt)  < 0.70) return null;

        double meanPrice = (rh.Average(s => s.value) + rl.Average(s => s.value)) / 2.0;
        if (meanPrice <= 0) return null;

        // Classify each line by its total fractional move over the window (interpretable).
        int    span        = winEnd - winStart;
        double highMovePct = highSlope * span / meanPrice;
        double lowMovePct  = lowSlope  * span / meanPrice;

        const double flatTol  = 0.02;   // |move| < 2% over the window ⇒ "flat"
        const double trendTol = 0.03;   // |move| ≥ 3% ⇒ clearly rising / falling

        bool flatHighs    = Math.Abs(highMovePct) < flatTol;
        bool flatLows     = Math.Abs(lowMovePct)  < flatTol;
        bool risingLows   = lowMovePct  >  trendTol;
        bool fallingHighs = highMovePct < -trendTol;

        // Projected (fitted) trendlines for drawing — both share the window start/end bars.
        double highAtStart = highInt + highSlope * winStart;
        double highAtEnd   = highInt + highSlope * winEnd;
        double lowAtStart  = lowInt  + lowSlope  * winStart;
        double lowAtEnd    = lowInt  + lowSlope  * winEnd;
        var trendlines = new List<PatternTrendline>
        {
            new() { StartTime = bars[winStart].Date, StartPrice = highAtStart, EndTime = bars[winEnd].Date, EndPrice = highAtEnd, Color = "#ef5350" },
            new() { StartTime = bars[winStart].Date, StartPrice = lowAtStart,  EndTime = bars[winEnd].Date, EndPrice = lowAtEnd,  Color = "#26a69a" },
        };

        // Ascending triangle: flat highs + rising lows → bullish bias
        if (flatHighs && risingLows)
        {
            double resistance = (highAtStart + highAtEnd) / 2.0;
            double distPct = (resistance - currentPrice) / currentPrice * 100;
            return new PatternResult
            {
                Type        = PatternType.AscendingTriangle,
                Category    = PatternCategory.Bullish,
                Timeframe   = tfl,
                IsConfirmed = distPct < 3,
                Strength    = 68,
                Description = $"Oplopende driehoek: vlakke weerstand rond {FormatP(resistance)} + stijgende bodems. "
                            + $"Breakout boven {FormatP(resistance)} geeft statistisch een sterk bullish signaal.",
                KeyLevel    = resistance,
                DistancePct = distPct,
                Annotation  = new PatternAnnotation { Trendlines = trendlines },
            };
        }

        // Descending triangle: falling highs + flat lows → bearish bias
        if (fallingHighs && flatLows)
        {
            double support = (lowAtStart + lowAtEnd) / 2.0;
            double distPct = (currentPrice - support) / currentPrice * 100;
            return new PatternResult
            {
                Type        = PatternType.DescendingTriangle,
                Category    = PatternCategory.Bearish,
                Timeframe   = tfl,
                IsConfirmed = distPct < 3,
                Strength    = 68,
                Description = $"Dalende driehoek: vlakke steun rond {FormatP(support)} + dalende toppen. "
                            + $"Breakdown onder {FormatP(support)} bevestigt bearish continuatie.",
                KeyLevel    = support,
                DistancePct = distPct,
                Annotation  = new PatternAnnotation { Trendlines = trendlines },
            };
        }

        // Symmetrical triangle: converging highs + lows → neutral, watch direction
        if (fallingHighs && risingLows)
            return new PatternResult
            {
                Type        = PatternType.SymmetricalTriangle,
                Category    = PatternCategory.Neutral,
                Timeframe   = tfl,
                IsConfirmed = false,
                Strength    = 60,
                Description = "Symmetrische driehoek: convergende highs en lows. Richting nog onbepaald — wacht op een directional breakout met volume voor een handelssignaal.",
                Annotation  = new PatternAnnotation { Trendlines = trendlines },
            };

        return null;
    }

    // ── Consolidation (tight range) ───────────────────────────────────────────
    private static PatternResult? DetectConsolidation(
        List<OhlcvBar> bars, string tfl, double currentPrice)
    {
        if (bars.Count < 15) return null;
        var window = bars.TakeLast(15).ToList();

        double high = window.Max(b => b.Close);  // top boundary    = close (no wicks)
        double low  = window.Min(b => b.Open);   // bottom boundary = open  (no wicks)
        double rangePct = (high - low) / high * 100;

        if (rangePct > 8) return null; // not tight enough

        return new PatternResult
        {
            Type        = PatternType.Consolidation,
            Category    = PatternCategory.Neutral,
            Timeframe   = tfl,
            IsConfirmed = true,
            Strength    = 62,
            Description = $"Consolidatie: {rangePct:F1}% range in de laatste 15 periodes. "
                        + "De markt verzamelt energie. Een uitbraak in één richting is aanstaande — wacht op volume om de richting te bevestigen.",
        };
    }

    // ── Breakout / breakdown ──────────────────────────────────────────────────
    private static PatternResult? DetectBreakoutBreakdown(
        List<OhlcvBar> bars,
        List<(int idx, double value)> swingHighs,
        List<(int idx, double value)> swingLows,
        string tfl, double currentPrice)
    {
        // Recency guard: at least one swing must be current for a valid breakout/breakdown signal.
        if (!HasRecentSwing(swingHighs, bars.Count) && !HasRecentSwing(swingLows, bars.Count)) return null;

        // Use the 3rd-to-last swing high/low as the key level (avoid current bar)
        var rh = swingHighs.TakeLast(4).ToList();
        var rl = swingLows .TakeLast(4).ToList();

        // Breakout: current price above a prior resistance swing high
        if (rh.Count >= 3)
        {
            double resistance = rh[^2].value;  // second-to-last swing high
            double above = (currentPrice - resistance) / resistance * 100;

            if (above > 0.5 && above <= 4.0)  // just broke out (0.5%–4% above)
                return new PatternResult
                {
                    Type        = PatternType.BreakoutAboveResistance,
                    Category    = PatternCategory.Bullish,
                    Timeframe   = tfl,
                    IsConfirmed = above > 1.5,
                    Strength    = 80,
                    Description = $"Uitbraak boven weerstand {FormatP(resistance)} (+{above:F1}%). "
                                + "Sterke setup: voormalige weerstand wordt nu steun. Volume-bevestiging is cruciaal.",
                    KeyLevel    = resistance,
                };

            // Potential breakout: close to resistance
            if (above > -3.0 && above <= 0.5)
                return new PatternResult
                {
                    Type        = PatternType.PotentialBreakout,
                    Category    = PatternCategory.Bullish,
                    Timeframe   = tfl,
                    IsConfirmed = false,
                    Strength    = 65,
                    Description = $"Bijna breakout: prijs {Math.Abs(above):F1}% onder weerstand {FormatP(resistance)}. "
                                + "Bewaking aanbevolen — uitbraak geeft potentieel sterk bull-signaal.",
                    KeyLevel    = resistance,
                    DistancePct = Math.Abs(above),
                };
        }

        // Breakdown: current price below a prior support swing low
        if (rl.Count >= 3)
        {
            double support = rl[^2].value;
            double below = (support - currentPrice) / support * 100;

            if (below > 0.5 && below <= 4.0)
                return new PatternResult
                {
                    Type        = PatternType.BreakdownBelowSupport,
                    Category    = PatternCategory.Bearish,
                    Timeframe   = tfl,
                    IsConfirmed = below > 1.5,
                    Strength    = 80,
                    Description = $"Breakdown onder steun {FormatP(support)} (-{below:F1}%). "
                                + "Voormalige steun wordt weerstand. Verhoogde kans op verdere daling.",
                    KeyLevel    = support,
                };
        }

        return null;
    }

    // =========================================================================
    // LEVEL 3 — Complex classical pattern detectors (require 50+ bars)
    // =========================================================================

    // ── Head & Shoulders (Bearish) ────────────────────────────────────────────
    // Three swing highs: left shoulder < head > right shoulder.
    // Neckline = average of the two troughs between shoulders/head.
    private static PatternResult? DetectHeadAndShoulders(
        List<OhlcvBar> bars,
        List<(int idx, double value)> swingHighs,
        List<(int idx, double value)> swingLows,
        string tfl, double currentPrice)
    {
        // Need at least 5 recent swing highs to pick out LS, H, RS; the pattern must be current.
        if (!HasRecentSwing(swingHighs, bars.Count)) return null;
        var rh = swingHighs.TakeLast(7).ToList();
        if (rh.Count < 5) return null;

        // Context: H&S is only a reversal signal after a meaningful uptrend (handbook §11.1).
        if (bars.Count >= 50)
        {
            double priceBack50 = bars[^50].Close;
            double recentHigh  = rh.Max(s => s.value);
            double priorRise   = (recentHigh - priceBack50) / priceBack50;
            if (priorRise < 0.15) return null;
        }

        // Scan for a head: a swing high that is higher than its immediate neighbours
        for (int i = 1; i < rh.Count - 1; i++)
        {
            double ls   = rh[i - 1].value;
            double head = rh[i].value;
            double rs   = rh[i + 1].value;

            if (head <= ls || head <= rs) continue;

            // Shoulders must be within 15% of each other (handbook: F6 — 15%, not 20%)
            double shoulderDiff = Math.Abs(ls - rs) / Math.Max(ls, rs);
            if (shoulderDiff > 0.15) continue;

            // Head must be at least 3 % above both shoulders
            if ((head - ls) / ls < 0.03 || (head - rs) / rs < 0.03) continue;

            // Width check: at least 12 bars between left shoulder and right shoulder (handbook: F7)
            if (rh[i + 1].idx - rh[i - 1].idx < 12) continue;

            // Neckline = max of the two trough minimums: T1 (LS→H) and T2 (H→RS)
            // Using max so both troughs are above the line — more conservative (handbook: F8)
            // Body bottom = Open (no wicks).
            double t1 = swingLows
                .Where(l => l.idx > rh[i - 1].idx && l.idx < rh[i].idx)
                .Select(l => l.value)
                .DefaultIfEmpty(bars.Skip(rh[i - 1].idx).Take(rh[i].idx - rh[i - 1].idx + 1).Min(b => b.Open))
                .Min();
            double t2 = swingLows
                .Where(l => l.idx > rh[i].idx && l.idx < rh[i + 1].idx)
                .Select(l => l.value)
                .DefaultIfEmpty(bars.Skip(rh[i].idx).Take(rh[i + 1].idx - rh[i].idx + 1).Min(b => b.Open))
                .Min();
            double neckline = Math.Max(t1, t2);

            bool confirmed = currentPrice < neckline * 0.995; // broke neckline
            double distPct = (currentPrice - neckline) / neckline * 100;

            return new PatternResult
            {
                Type        = PatternType.HeadAndShoulders,
                Category    = PatternCategory.Bearish,
                Timeframe   = tfl,
                IsConfirmed = confirmed,
                Strength    = confirmed ? 84 : 70,
                Description = $"Head & Shoulders: schouders ~{FormatP(ls)}/{FormatP(rs)}, hoofd {FormatP(head)}, neklijn {FormatP(neckline)}. "
                            + (confirmed
                               ? "Neklijn gebroken — bearish reversal bevestigd."
                               : "Rechter schouder gevormd; afwachten of neklijn breekt voor bevestiging."),
                KeyLevel    = neckline,
                DistancePct = distPct,
                Annotation  = new PatternAnnotation
                {
                    Markers = new()
                    {
                        new PatternPoint { Time = bars[rh[i - 1].idx].Date, Price = rh[i - 1].value, Label = "LS", AboveBar = true },
                        new PatternPoint { Time = bars[rh[i].idx].Date,     Price = rh[i].value,     Label = "H",  AboveBar = true },
                        new PatternPoint { Time = bars[rh[i + 1].idx].Date, Price = rh[i + 1].value, Label = "RS", AboveBar = true },
                    },
                    // Neklijn als begrensd segment over het patroon (linkerschouder → huidige candle).
                    Trendlines = new()
                    {
                        new PatternTrendline { StartTime = bars[rh[i - 1].idx].Date, StartPrice = neckline, EndTime = bars[^1].Date, EndPrice = neckline, Color = "#ef5350" },
                    },
                },
            };
        }
        return null;
    }

    // ── Inverse Head & Shoulders (Bullish) ───────────────────────────────────
    private static PatternResult? DetectInverseHeadAndShoulders(
        List<OhlcvBar> bars,
        List<(int idx, double value)> swingHighs,
        List<(int idx, double value)> swingLows,
        string tfl, double currentPrice)
    {
        // The pattern must be current — right shoulder must be within the last 20 bars.
        if (!HasRecentSwing(swingLows, bars.Count)) return null;
        var rl = swingLows.TakeLast(7).ToList();
        if (rl.Count < 5) return null;

        // Context: Inv. H&S is only a reversal signal after a meaningful downtrend (handbook §11.2).
        if (bars.Count >= 50)
        {
            double priceBack50 = bars[^50].Close;
            double recentLow   = rl.Min(s => s.value);
            double priorDrop   = (priceBack50 - recentLow) / priceBack50;
            if (priorDrop < 0.15) return null;
        }

        for (int i = 1; i < rl.Count - 1; i++)
        {
            double ls   = rl[i - 1].value;
            double head = rl[i].value;
            double rs   = rl[i + 1].value;

            if (head >= ls || head >= rs) continue;

            double shoulderDiff = Math.Abs(ls - rs) / Math.Max(ls, rs);
            if (shoulderDiff > 0.15) continue;  // handbook: F6 — 15%, not 20%

            if ((ls - head) / head < 0.03 || (rs - head) / head < 0.03) continue;

            // Width check: at least 12 bars between left shoulder and right shoulder (handbook: F7)
            if (rl[i + 1].idx - rl[i - 1].idx < 12) continue;

            // Neckline = min of the two peak maximums: P1 (LS→H) and P2 (H→RS) (handbook: F8)
            // Body top = Close (no wicks).
            double p1 = swingHighs
                .Where(h => h.idx > rl[i - 1].idx && h.idx < rl[i].idx)
                .Select(h => h.value)
                .DefaultIfEmpty(bars.Skip(rl[i - 1].idx).Take(rl[i].idx - rl[i - 1].idx + 1).Max(b => b.Close))
                .Max();
            double p2 = swingHighs
                .Where(h => h.idx > rl[i].idx && h.idx < rl[i + 1].idx)
                .Select(h => h.value)
                .DefaultIfEmpty(bars.Skip(rl[i].idx).Take(rl[i + 1].idx - rl[i].idx + 1).Max(b => b.Close))
                .Max();
            double neckline = Math.Min(p1, p2);

            bool confirmed = currentPrice > neckline * 1.005;
            double distPct = (neckline - currentPrice) / currentPrice * 100;

            return new PatternResult
            {
                Type        = PatternType.InverseHeadAndShoulders,
                Category    = PatternCategory.Bullish,
                Timeframe   = tfl,
                IsConfirmed = confirmed,
                Strength    = confirmed ? 84 : 70,
                Description = $"Inverse H&S: schouders ~{FormatP(ls)}/{FormatP(rs)}, hoofd {FormatP(head)}, neklijn {FormatP(neckline)}. "
                            + (confirmed
                               ? "Neklijn gebroken — bullish reversal bevestigd."
                               : "Rechter schouder gevormd; uitbraak boven neklijn is het signaal."),
                KeyLevel    = neckline,
                DistancePct = distPct,
                Annotation  = new PatternAnnotation
                {
                    Markers = new()
                    {
                        new PatternPoint { Time = bars[rl[i - 1].idx].Date, Price = rl[i - 1].value, Label = "LS", AboveBar = false },
                        new PatternPoint { Time = bars[rl[i].idx].Date,     Price = rl[i].value,     Label = "H",  AboveBar = false },
                        new PatternPoint { Time = bars[rl[i + 1].idx].Date, Price = rl[i + 1].value, Label = "RS", AboveBar = false },
                    },
                    // Neklijn als begrensd segment over het patroon (linkerschouder → huidige candle).
                    Trendlines = new()
                    {
                        new PatternTrendline { StartTime = bars[rl[i - 1].idx].Date, StartPrice = neckline, EndTime = bars[^1].Date, EndPrice = neckline, Color = "#26a69a" },
                    },
                },
            };
        }
        return null;
    }

    // ── Rising Wedge (Bearish) / Falling Wedge (Bullish) ─────────────────────
    // Both trend lines converge:
    //   Rising wedge:  both lines rise, lower line rises FASTER  → bearish
    //   Falling wedge: both lines fall, upper line falls FASTER  → bullish
    //
    // Validation uses geometric gap-convergence (gap must narrow ≥ 30%) so that:
    //   - Near-parallel channels are never classified as wedges
    //   - The drawn trendlines always visually match the detected shape
    //   - Both lines start at the same bar index (clear wedge silhouette)
    private static PatternResult? DetectWedge(
        List<OhlcvBar> bars,
        List<(int idx, double value)> swingHighs,
        List<(int idx, double value)> swingLows,
        string tfl, double currentPrice)
    {
        if (!HasRecentSwing(swingHighs, bars.Count) || !HasRecentSwing(swingLows, bars.Count)) return null;

        var rh = swingHighs.TakeLast(6).ToList();
        var rl = swingLows .TakeLast(6).ToList();
        if (rh.Count < 3 || rl.Count < 3) return null;

        // ── Overlapping bar-index window ──────────────────────────────────────
        int winStart = Math.Max(rh.First().idx, rl.First().idx);
        int winEnd   = Math.Min(rh.Last().idx,  rl.Last().idx);
        if (winEnd - winStart < 15) return null;

        var rhW = rh.Where(p => p.idx >= winStart && p.idx <= winEnd).ToList();
        var rlW = rl.Where(p => p.idx >= winStart && p.idx <= winEnd).ToList();
        if (rhW.Count < 2 || rlW.Count < 2) return null;

        // ── Regression lines using actual bar indices ─────────────────────────
        var (highSlope, highIntercept) = LinearRegressionByBarIdx(rhW);
        var (lowSlope,  lowIntercept)  = LinearRegressionByBarIdx(rlW);

        // Goodness-of-fit guard (wedges are choppier than channels → slightly looser 0.55).
        if (RSquaredByBarIdx(rhW, highSlope, highIntercept) < 0.55 ||
            RSquaredByBarIdx(rlW, lowSlope,  lowIntercept)  < 0.55) return null;

        // Project each regression line to the window edges
        double highAtStart = highIntercept + highSlope * winStart;
        double highAtEnd   = highIntercept + highSlope * winEnd;
        double lowAtStart  = lowIntercept  + lowSlope  * winStart;
        double lowAtEnd    = lowIntercept  + lowSlope  * winEnd;

        // Upper trendline must remain above the lower throughout
        if (highAtStart <= lowAtStart || highAtEnd <= lowAtEnd) return null;

        double gapStart = highAtStart - lowAtStart;
        double gapEnd   = highAtEnd   - lowAtEnd;
        if (gapStart <= 0) return null;

        // ── Geometric convergence: gap must narrow by at least 30% ───────────
        if (gapEnd >= gapStart * 0.70) return null;

        // ── Both lines must slope in the same direction ───────────────────────
        bool bothFalling = highSlope < 0 && lowSlope < 0;
        bool bothRising  = highSlope > 0 && lowSlope > 0;
        if (!bothFalling && !bothRising) return null;

        // Falling wedge: upper line must fall FASTER (more negative slope)
        // Rising  wedge: lower line must rise FASTER (more positive slope)
        if (bothFalling && highSlope >= lowSlope) return null;
        if (bothRising  && lowSlope  <= highSlope) return null;

        // ── Minimum slope: exclude near-horizontal lines ──────────────────────
        double meanPrice     = (rhW.Average(p => p.value) + rlW.Average(p => p.value)) / 2.0;
        if (meanPrice <= 0) return null;
        double steepestNorm  = Math.Max(Math.Abs(highSlope), Math.Abs(lowSlope)) / meanPrice;
        if (steepestNorm < 0.0003) return null;

        // ── Sanity: total wedge height 3–35% of price ────────────────────────
        double wedgeRangePct = gapStart / ((highAtStart + lowAtStart) / 2.0);
        if (wedgeRangePct < 0.03 || wedgeRangePct > 0.35) return null;

        // ── Drawing: both lines share the same start bar and end bar ─────────
        // Using the projected regression prices (not raw swing-point values)
        // ensures the drawn lines visually match the geometric shape.
        int drawStart = Math.Max(0, Math.Min(winStart, bars.Count - 1));
        int drawEnd   = Math.Max(0, Math.Min(winEnd,   bars.Count - 1));

        double convergencePct = (1.0 - gapEnd / gapStart) * 100;

        if (bothFalling)
        {
            double breakout = highAtEnd;
            return new PatternResult
            {
                Type        = PatternType.FallingWedge,
                Category    = PatternCategory.Bullish,
                Timeframe   = tfl,
                IsConfirmed = currentPrice > breakout * 0.995,
                Strength    = 72,
                Description = $"Falling wedge: beide lijnen dalen, bovenlijn daalt sneller ({convergencePct:F0}% versmalling). "
                            + $"Breakout boven {FormatP(breakout)} bevestigt bullish reversal.",
                KeyLevel    = breakout,
                DistancePct = (breakout - currentPrice) / currentPrice * 100,
                Annotation  = new PatternAnnotation
                {
                    Trendlines = new()
                    {
                        new PatternTrendline { StartTime = bars[drawStart].Date, StartPrice = highAtStart, EndTime = bars[drawEnd].Date, EndPrice = highAtEnd, Color = "#26a69a" },
                        new PatternTrendline { StartTime = bars[drawStart].Date, StartPrice = lowAtStart,  EndTime = bars[drawEnd].Date, EndPrice = lowAtEnd,  Color = "#26a69a" },
                    },
                },
            };
        }

        // Rising wedge
        double breakdown = lowAtEnd;
        return new PatternResult
        {
            Type        = PatternType.RisingWedge,
            Category    = PatternCategory.Bearish,
            Timeframe   = tfl,
            IsConfirmed = currentPrice < breakdown * 1.005,
            Strength    = 72,
            Description = $"Rising wedge: beide lijnen stijgen, onderlijn stijgt sneller ({convergencePct:F0}% versmalling). "
                        + $"Breakdown onder {FormatP(breakdown)} bevestigt bearish signaal.",
            KeyLevel    = breakdown,
            DistancePct = (currentPrice - breakdown) / currentPrice * 100,
            Annotation  = new PatternAnnotation
            {
                Trendlines = new()
                {
                    new PatternTrendline { StartTime = bars[drawStart].Date, StartPrice = highAtStart, EndTime = bars[drawEnd].Date, EndPrice = highAtEnd, Color = "#ef5350" },
                    new PatternTrendline { StartTime = bars[drawStart].Date, StartPrice = lowAtStart,  EndTime = bars[drawEnd].Date, EndPrice = lowAtEnd,  Color = "#ef5350" },
                },
            },
        };
    }

    // ── Cup & Handle (Bullish continuation) ──────────────────────────────────
    // U-shaped recovery over 30–65 bars (flexible window, handbook: F10),
    // followed by a small tight consolidation in the last 10 bars (the "handle").
    private static PatternResult? DetectCupAndHandle(
        List<OhlcvBar> bars, string tfl, double currentPrice)
    {
        const int handleLen = 10;
        if (bars.Count < 30 + handleLen) return null;

        // Handle: last 10 bars — must be a tight consolidation
        // Top = Close (no wicks), Bottom = Open (no wicks).
        var handle     = bars.TakeLast(handleLen).ToList();
        double handleH = handle.Max(b => b.Close);
        double handleL = handle.Min(b => b.Open);
        double handleRange = (handleH - handleL) / handleH;
        if (handleRange > 0.07) return null; // handle too wide

        // Scan flexible cup window 65→30 bars, take first valid cup (handbook: F10)
        int maxCupLen = Math.Min(65, bars.Count - handleLen);
        for (int cupLen = maxCupLen; cupLen >= 30; cupLen--)
        {
            var cup = bars.SkipLast(handleLen).TakeLast(cupLen).ToList();
            double cupLeft  = cup.First().Close;
            double cupRight = cup.Last().Close;
            double cupMin   = cup.Min(b => b.Open);   // bottom boundary = open (no wicks)
            double cupRim   = Math.Max(cupLeft, cupRight);
            double cupDepth = (cupRim - cupMin) / cupRim;

            // Cup must have meaningful depth (10–40 %)
            if (cupDepth < 0.10 || cupDepth > 0.40) continue;

            // Rim symmetry: left and right close prices within 6% (handbook: F10 — 6%, not 8%)
            double rimDiff = Math.Abs(cupLeft - cupRight) / cupRim;
            if (rimDiff > 0.06) continue;

            // The cup minimum must be in the middle half of the cup (not at edges)
            int minIdx = cup.IndexOf(cup.MinBy(b => b.Open)!);   // body bottom
            if (minIdx < cup.Count / 4 || minIdx > cup.Count * 3 / 4) continue;

            // Handle must not retrace more than 45% of the cup (handbook: F10 — 45%, not 50%)
            double retraceFromRim = cupRight > cupMin
                ? (cupRight - handleL) / (cupRight - cupMin)
                : 1.0;
            if (retraceFromRim > 0.45) continue;

            // Handle must be below the right rim (not pushing above breakout level yet)
            if (handleL > cupRight) continue;

            // Valid cup found — build result
            bool   confirmed  = currentPrice > handleH * 1.005;
            double rimTarget  = cupRim;
            int    n          = bars.Count;
            int    cupStartIdx = Math.Max(0, n - handleLen - cupLen);
            int    cupEndIdx   = Math.Max(0, n - handleLen - 1);
            var    cupSection  = bars.Skip(cupStartIdx).Take(cupEndIdx - cupStartIdx + 1).ToList();
            int    cupMinLocal = cupSection.IndexOf(cupSection.MinBy(b => b.Open)!);  // body bottom
            int    cupMinIdx   = cupStartIdx + cupMinLocal;

            return new PatternResult
            {
                Type        = PatternType.CupAndHandle,
                Category    = PatternCategory.Bullish,
                Timeframe   = tfl,
                IsConfirmed = confirmed,
                Strength    = confirmed ? 82 : 68,
                Description = $"Cup & Handle: U-vormig herstel ({cupDepth * 100:F0}% diepte, {cupLen} bars) + kleine consolidatie (handle {handleRange * 100:F1}% range). "
                            + (confirmed
                               ? $"Uitbraak boven {FormatP(handleH)} — bullish continuatie."
                               : $"Uitbraak boven {FormatP(handleH)} activeert het patroon met koersdoel {FormatP(rimTarget + (rimTarget - cupMin))}."),
                KeyLevel    = handleH,
                DistancePct = (handleH - currentPrice) / currentPrice * 100,
                Annotation  = new PatternAnnotation
                {
                    Markers = new()
                    {
                        new PatternPoint { Time = bars[cupStartIdx].Date, Price = cupLeft,  Label = "L",  AboveBar = false },
                        new PatternPoint { Time = bars[cupMinIdx].Date,   Price = cupMin,   Label = "B",  AboveBar = false },
                        new PatternPoint { Time = bars[cupEndIdx].Date,   Price = cupRight, Label = "R",  AboveBar = false },
                        new PatternPoint { Time = bars[n - 1].Date,       Price = handleH,  Label = "↑",  AboveBar = true  },
                    },
                    // Breakout-niveau (cup-rand) als begrensd segment over de cup → huidige candle.
                    Trendlines = new()
                    {
                        new PatternTrendline { StartTime = bars[cupStartIdx].Date, StartPrice = handleH, EndTime = bars[n - 1].Date, EndPrice = handleH, Color = "#26a69a" },
                    },
                },
            };
        }
        return null;
    }

    // ── Adam & Eve double bottom (Bullish reversal) ───────────────────────────
    // Two lows close in price where one is V-shaped (Adam = sharp spike) and the
    // other is rounded/broad (Eve = gentle curve).  Statistically stronger than a
    // plain double bottom because the different shapes reflect genuine seller exhaustion.
    //
    // Adam:  adjacent bars (±2) all have lows ≥ 2.5 % above the spike low.
    // Eve:   ≥ 3 out of 7 bars centred on the low are within 2 % of the minimum.
    private static PatternResult? DetectAdamAndEve(
        List<OhlcvBar> bars,
        List<(int idx, double value)> swingLows,
        string tfl, double currentPrice)
    {
        if (swingLows.Count < 2) return null;

        // Recency guard: the most recent low must be within the last 20 bars.
        if (!HasRecentSwing(swingLows, bars.Count)) return null;

        var last = swingLows[^1];
        for (int i = swingLows.Count - 2; i >= Math.Max(0, swingLows.Count - 6); i--)
        {
            var prev = swingLows[i];
            if (last.idx - prev.idx < 8) continue;   // minimum 8 bars separation

            double diff = Math.Abs(last.value - prev.value) / prev.value;
            if (diff > 0.03) continue;   // lows within 3 %

            // Minimum depth: valley must be ≥ 5 % below the higher of the two lows
            double higherLow = Math.Max(prev.value, last.value);
            double midMin    = bars.Skip(prev.idx).Take(last.idx - prev.idx + 1).Min(b => b.Open);  // body bottom
            if ((higherLow - midMin) / higherLow < 0.05) continue;

            bool prevIsAdam = IsAdamBottom(bars, prev.idx);
            bool prevIsEve  = IsEveBottom(bars, prev.idx);
            bool lastIsAdam = IsAdamBottom(bars, last.idx);
            bool lastIsEve  = IsEveBottom(bars, last.idx);

            // Require exactly one Adam and one Eve — either order is valid
            bool isAdamEve = (prevIsAdam && lastIsEve) || (prevIsEve && lastIsAdam);
            if (!isAdamEve) continue;

            string firstLabel  = prevIsAdam ? "A" : "E";
            string secondLabel = lastIsAdam ? "A" : "E";
            string firstDesc   = prevIsAdam  ? "scherp" : "afgerond";
            string secondDesc  = lastIsAdam  ? "scherp" : "afgerond";

            double recovery = (currentPrice - last.value) / last.value;
            if (recovery < 0.01) continue;   // price hasn't bounced yet

            bool   confirmed = recovery > 0.04;
            double neckline  = Math.Max(
                bars.Skip(prev.idx).Take(last.idx - prev.idx + 1).Max(b => b.Close),  // body top
                currentPrice);

            return new PatternResult
            {
                Type        = PatternType.AdamAndEve,
                Category    = PatternCategory.Bullish,
                Timeframe   = tfl,
                IsConfirmed = confirmed,
                Strength    = confirmed ? 82 : 64,
                Description = $"Adam & Eve: {firstLabel}-bodem {FormatP(prev.value)} ({firstDesc}) en "
                            + $"{secondLabel}-bodem {FormatP(last.value)} ({secondDesc}). "
                            + (confirmed
                               ? "Patroon bevestigd — sterk bullish reversal signaal. "
                                 + "De combinatie van een scherpe en een ronde bodem wijst op volledige uitputting van verkopers."
                               : "In formatie — uitbraak boven de neklijn bevestigt het signaal. "
                                 + "Statistisch sterker dan een klassieke dubbele bodem."),
                KeyLevel    = neckline,
                Annotation  = new PatternAnnotation
                {
                    Markers = new()
                    {
                        new PatternPoint { Time = bars[prev.idx].Date, Price = prev.value, Label = firstLabel,  AboveBar = false },
                        new PatternPoint { Time = bars[last.idx].Date, Price = last.value, Label = secondLabel, AboveBar = false },
                    },
                    // Neklijn als begrensd segment over het patroon (eerste bodem → huidige candle).
                    Trendlines = new()
                    {
                        new PatternTrendline { StartTime = bars[prev.idx].Date, StartPrice = neckline, EndTime = bars[^1].Date, EndPrice = neckline, Color = "#26a69a" },
                    },
                },
            };
        }
        return null;
    }

    /// <summary>
    /// Adam bottom: single sharp V-spike.  All bars within ±2 bars have lows ≥ 2.5 % above the spike.
    /// </summary>
    private static bool IsAdamBottom(List<OhlcvBar> bars, int idx)
    {
        if (idx < 2 || idx >= bars.Count - 2) return false;
        double low = bars[idx].Open;  // body bottom (no wicks)
        for (int j = idx - 2; j <= idx + 2; j++)
        {
            if (j == idx) continue;
            if (bars[j].Open < low * 1.025) return false;   // adjacent bar too close → not a spike
        }
        return true;
    }

    /// <summary>
    /// Eve bottom: broad rounded base.  At least 3 of the 7 bars centred on the low are within 2 % of it.
    /// </summary>
    private static bool IsEveBottom(List<OhlcvBar> bars, int idx)
    {
        if (idx < 3 || idx >= bars.Count - 3) return false;
        double low   = bars[idx].Open;  // body bottom (no wicks)
        int    count = 0;
        for (int j = idx - 3; j <= idx + 3; j++)
        {
            if (bars[j].Open <= low * 1.02) count++;
        }
        return count >= 3;
    }

    // ── Ascending / Descending channel ────────────────────────────────────────
    // Both high-trendline and low-trendline run in the same direction with roughly
    // parallel (not converging) slopes.
    //
    // Ascending channel  (both slopes > minSlope)  → bullish
    // Descending channel (both slopes < -minSlope) → bearish
    //
    // Deliberately excludes wedges: if slopes converge more than ×1.20, the wedge
    // detector fires instead.
    private static PatternResult? DetectChannel(
        List<OhlcvBar> bars,
        List<(int idx, double value)> swingHighs,
        List<(int idx, double value)> swingLows,
        string tfl, double currentPrice)
    {
        if (!HasRecentSwing(swingHighs, bars.Count) || !HasRecentSwing(swingLows, bars.Count)) return null;

        var rh = swingHighs.TakeLast(4).ToList();
        var rl = swingLows .TakeLast(4).ToList();
        if (rh.Count < 3 || rl.Count < 3) return null;

        // Overlapping window on the real time axis
        int winStart = Math.Max(rh.First().idx, rl.First().idx);
        int winEnd   = Math.Min(rh.Last().idx,  rl.Last().idx);
        if (winEnd - winStart < 10) return null;   // exclude micro-channels

        // Bar-index regression for both trendlines (slope = price/bar)
        var (highSlope, highInt) = LinearRegressionByBarIdx(rh);
        var (lowSlope,  lowInt)  = LinearRegressionByBarIdx(rl);

        // Goodness-of-fit: the swings must genuinely lie on their trendlines, otherwise the
        // "channel" is just noise that happens to drift. This is the key false-positive filter.
        if (RSquaredByBarIdx(rh, highSlope, highInt) < 0.70 ||
            RSquaredByBarIdx(rl, lowSlope,  lowInt)  < 0.70) return null;

        double meanPrice = (rh.Average(s => s.value) + rl.Average(s => s.value)) / 2.0;
        if (meanPrice <= 0) return null;

        // Total fractional move of each line over the window — interpretable threshold (≥3%).
        int    span         = winEnd - winStart;
        double highMovePct  = highSlope * span / meanPrice;
        double lowMovePct   = lowSlope  * span / meanPrice;

        bool ascending  = highMovePct >  0.03 && lowMovePct >  0.03;
        bool descending = highMovePct < -0.03 && lowMovePct < -0.03;
        if (!ascending && !descending) return null;

        // Roughly parallel (slopes within 50 % of each other)
        double slopeDiff = Math.Abs(highSlope - lowSlope);
        double slopeMax  = Math.Max(Math.Abs(highSlope), Math.Abs(lowSlope));
        if (slopeMax <= 0 || slopeDiff / slopeMax > 0.50) return null;

        // Project the fitted lines to the window edges — these are the prices we draw.
        double highAtStart = highInt + highSlope * winStart;
        double highAtEnd   = highInt + highSlope * winEnd;
        double lowAtStart  = lowInt  + lowSlope  * winStart;
        double lowAtEnd    = lowInt  + lowSlope  * winEnd;
        if (highAtStart <= lowAtStart || highAtEnd <= lowAtEnd) return null;

        // Not a wedge: a channel keeps a roughly constant width (gap doesn't narrow ≥30%).
        double gapStart = highAtStart - lowAtStart;
        double gapEnd   = highAtEnd   - lowAtEnd;
        if (gapStart > 0 && gapEnd < gapStart * 0.70) return null;

        // Channel width must be meaningful: 4–30 % of price
        double channelHigh  = highAtEnd;
        double channelLow   = lowAtEnd;
        double channelWidth = (channelHigh - channelLow) / channelHigh;
        if (channelWidth < 0.04 || channelWidth > 0.30) return null;

        double midChannel = (channelHigh + channelLow) / 2;
        bool   nearLower  = currentPrice <= midChannel;
        bool   nearUpper  = currentPrice >  midChannel;

        // Fitted trendlines for drawing (both share the window start/end bars).
        var trendlines = new List<PatternTrendline>
        {
            new() { StartTime = bars[winStart].Date, StartPrice = highAtStart, EndTime = bars[winEnd].Date, EndPrice = highAtEnd, Color = "#ef5350" },
            new() { StartTime = bars[winStart].Date, StartPrice = lowAtStart,  EndTime = bars[winEnd].Date, EndPrice = lowAtEnd,  Color = "#26a69a" },
        };

        if (ascending)
            return new PatternResult
            {
                Type        = PatternType.AscendingChannel,
                Category    = PatternCategory.Bullish,
                Timeframe   = tfl,
                IsConfirmed = true,
                Strength    = nearLower ? 70 : 60,
                Description = $"Oplopend kanaal: parallelle hogere toppen (~{FormatP(channelHigh)}) én hogere bodems (~{FormatP(channelLow)}). "
                            + (nearLower
                               ? $"Prijs nadert kanaalbodem — potentiële koopzone. Wacht op een bullish candle als bevestiging."
                               : $"Prijs in het midden/top van het kanaal. Kanaalbodem ~{FormatP(channelLow)} is het key steunsupport."),
                KeyLevel    = channelLow,
                DistancePct = (currentPrice - channelLow) / currentPrice * 100,
                Annotation  = new PatternAnnotation { Trendlines = trendlines },
            };

        // Descending channel
        return new PatternResult
        {
            Type        = PatternType.DescendingChannel,
            Category    = PatternCategory.Bearish,
            Timeframe   = tfl,
            IsConfirmed = true,
            Strength    = nearUpper ? 70 : 60,
            Description = $"Dalend kanaal: parallelle lagere toppen (~{FormatP(channelHigh)}) én lagere bodems (~{FormatP(channelLow)}). "
                        + (nearUpper
                           ? $"Prijs nadert kanaalplafond ~{FormatP(channelHigh)} — potentiële verkoopzone. "
                             + "Breakout boven het plafond met volume zou het kanaal breken."
                           : $"Prijs in het midden/bodem van het kanaal. Kanaalplafond ~{FormatP(channelHigh)} is de key weerstand."),
            KeyLevel    = channelHigh,
            DistancePct = (channelHigh - currentPrice) / currentPrice * 100,
            Annotation  = new PatternAnnotation { Trendlines = trendlines },
        };
    }

    // =========================================================================
    // Swing point helpers
    // =========================================================================

    private static List<(int idx, double value)> FindSwingHighs(
        List<OhlcvBar> bars, int lookback, double minSignificance)
    {
        var result = new List<(int, double)>();
        // Extend loop to bars.Count so the current (last) candle can qualify as a swing.
        // For bars near the end we use a one-sided right-window (afterCount ≤ lookback).
        // Pivot price = candle WICK high (bars[i].High) so swing points land exactly on the
        // peaks the user sees on the chart — not on the body top, which sits a wick away.
        for (int i = lookback; i < bars.Count; i++)
        {
            double h           = bars[i].High;
            bool   isHigh      = true;
            double maxNeighbor = 0;
            int    afterCount  = Math.Min(lookback, bars.Count - 1 - i); // 0 for the last bar
            for (int j = i - lookback; j <= i + afterCount; j++)
            {
                if (j == i) continue;
                double neighborTop = bars[j].High;
                if (neighborTop >= h) { isHigh = false; break; }
                maxNeighbor = Math.Max(maxNeighbor, neighborTop);
            }
            // Significance filter (ATR-relative): the peak must clear its highest neighbour by
            // at least ~0.4 ATR. Scaling with the coin's own volatility filters micro-swings
            // far better than a fixed 0.5% would across coins of very different volatility.
            if (isHigh && maxNeighbor > 0 && h - maxNeighbor >= minSignificance)
                result.Add((i, h));
        }
        return result;
    }

    private static List<(int idx, double value)> FindSwingLows(
        List<OhlcvBar> bars, int lookback, double minSignificance)
    {
        var result = new List<(int, double)>();
        // Pivot price = candle WICK low (bars[i].Low) — see FindSwingHighs for the rationale.
        for (int i = lookback; i < bars.Count; i++)
        {
            double l           = bars[i].Low;
            bool   isLow       = true;
            double minNeighbor = double.MaxValue;
            int    afterCount  = Math.Min(lookback, bars.Count - 1 - i); // 0 for the last bar
            for (int j = i - lookback; j <= i + afterCount; j++)
            {
                if (j == i) continue;
                double neighborBottom = bars[j].Low;
                if (neighborBottom <= l) { isLow = false; break; }
                minNeighbor = Math.Min(minNeighbor, neighborBottom);
            }
            // Significance filter (ATR-relative): trough must clear its lowest neighbour by ~0.4 ATR.
            if (isLow && minNeighbor < double.MaxValue && minNeighbor - l >= minSignificance)
                result.Add((i, l));
        }
        return result;
    }

    /// <summary>
    /// Returns true when the last swing in <paramref name="swings"/> falls within the
    /// last <paramref name="windowBars"/> bars of the dataset. Used to ensure that only
    /// patterns that involve the current (or very recent) candle are reported.
    /// </summary>
    private static bool HasRecentSwing(
        List<(int idx, double value)> swings, int barCount, int windowBars = 20)
        => swings.Count > 0 && swings[^1].idx >= barCount - windowBars;

    // =========================================================================
    // Math helpers
    // =========================================================================

    /// <summary>Linear regression slope of a value series (normalised by mean, array-index X axis).</summary>
    private static double LinearSlope(List<double> values)
    {
        if (values.Count < 2) return 0;
        int n = values.Count;
        double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
        for (int i = 0; i < n; i++)
        {
            sumX  += i;
            sumY  += values[i];
            sumXY += i * values[i];
            sumX2 += i * i;
        }
        double denom = n * sumX2 - sumX * sumX;
        if (Math.Abs(denom) < 1e-12) return 0;
        double slope = (n * sumXY - sumX * sumY) / denom;
        // Normalise by mean price so result is scale-independent
        double mean = sumY / n;
        return mean > 0 ? slope / mean : 0;
    }

    /// <summary>
    /// Linear regression using the actual bar indices as X coordinates (not array offsets).
    /// Returns raw slope (price per bar) and intercept (projected price at bar index 0).
    /// Use this wherever the trendline endpoints must be plotted on a real time axis.
    /// </summary>
    private static (double slope, double intercept) LinearRegressionByBarIdx(
        List<(int idx, double value)> points)
    {
        int n = points.Count;
        if (n < 2) return (0, n == 1 ? points[0].value : 0);

        double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
        foreach (var (idx, val) in points)
        {
            sumX  += idx;
            sumY  += val;
            sumXY += (double)idx * val;
            sumX2 += (double)idx * idx;
        }
        double denom = n * sumX2 - sumX * sumX;
        if (Math.Abs(denom) < 1e-12) return (0, sumY / n);

        double slope     = (n * sumXY - sumX * sumY) / denom;
        double intercept = (sumY - slope * sumX) / n;
        return (slope, intercept);
    }

    /// <summary>
    /// Goodness-of-fit (R², 0–1) of a set of swing points around the fitted bar-index line.
    /// A high R² means the swings genuinely lie on the trendline — the core guard against
    /// drawing a line through points that don't actually form a trend.
    /// </summary>
    private static double RSquaredByBarIdx(
        List<(int idx, double value)> points, double slope, double intercept)
    {
        int n = points.Count;
        if (n < 2) return 0;

        double meanY = points.Average(p => p.value);
        double ssTot = 0, ssRes = 0;
        foreach (var (idx, val) in points)
        {
            double predicted = intercept + slope * idx;
            ssRes += (val - predicted) * (val - predicted);
            ssTot += (val - meanY)     * (val - meanY);
        }
        if (ssTot < 1e-12) return 1.0;             // all points equal → a flat line fits perfectly
        return Math.Max(0.0, 1.0 - ssRes / ssTot);
    }

    /// <summary>
    /// Average True Range over the last <paramref name="period"/> bars (absolute price units).
    /// Used to make swing-significance scale with each coin's own volatility.
    /// </summary>
    private static double AverageTrueRange(List<OhlcvBar> bars, int period = 14)
    {
        if (bars.Count < 2) return 0;

        int    start = Math.Max(1, bars.Count - period);
        double sum   = 0;
        int    count = 0;
        for (int i = start; i < bars.Count; i++)
        {
            double prevClose = bars[i - 1].Close;
            double tr = Math.Max(bars[i].High - bars[i].Low,
                        Math.Max(Math.Abs(bars[i].High - prevClose),
                                 Math.Abs(bars[i].Low  - prevClose)));
            sum += tr;
            count++;
        }
        return count > 0 ? sum / count : 0;
    }

    /// <summary>
    /// Returns true when a pattern's key level was already breached by more than
    /// <paramref name="stalePct"/> (default 8%) in the expected direction.
    /// Such a pattern has already played out and should not be reported as active.
    /// Handbook §3.2: "If currentPrice is already >8% past the key level, it's stale."
    /// </summary>
    private static bool IsPatternStale(
        double currentPrice, double keyLevel,
        PatternCategory category, double stalePct = 0.08)
    {
        if (keyLevel <= 0) return false;
        double moved = category == PatternCategory.Bullish
            ? (currentPrice - keyLevel) / keyLevel   // bullish: price above key level
            : (keyLevel - currentPrice) / keyLevel;  // bearish: price below key level
        return moved > stalePct;
    }

    private static string FormatP(double price) => price switch
    {
        >= 10_000 => $"${price:N0}",
        >= 1      => $"${price:N2}",
        >= 0.01   => $"${price:N4}",
        _         => $"${price:N6}",
    };

    private static void TryAdd(List<PatternResult> list, PatternResult? result)
    {
        if (result is not null) list.Add(result);
    }
}
