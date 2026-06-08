using CryptoPortfolioTracker.Models;
using Skender.Stock.Indicators;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Implementatie van het 5-factor scoremodel voor de 3%-trading-strategie.
///
/// Gewichten (hernormeerd naar 100% zonder factor 6 + 7):
///   Factor 1 — Trend              origineel 25% → hernorm 31,25%
///   Factor 2 — Momentum           origineel 15% → hernorm 18,75%
///   Factor 3 — Volume / OBV       origineel 15% → hernorm 18,75%
///   Factor 4 — Volatiliteit       origineel 10% → hernorm 12,50%
///   Factor 5 — Support/Resistance origineel 15% → hernorm 18,75%
///
/// Elke factor geeft 0-10 terug (5 = neutraal).
/// Totaalscore = Σ(factor × gewicht) × 10  →  0-100.
/// </summary>
public class ThreePctScoringService : IThreePctScoringService
{
    private const double W1 = 0.25 / 0.80;  // 0.3125
    private const double W2 = 0.15 / 0.80;  // 0.1875
    private const double W3 = 0.15 / 0.80;  // 0.1875
    private const double W4 = 0.10 / 0.80;  // 0.125
    private const double W5 = 0.15 / 0.80;  // 0.1875

    // =========================================================================
    // Public entry point
    // =========================================================================

    public ThreePctScoreResult? Score(
        List<OhlcvBar>     bars,
        string             symbol,
        string             coinName,
        string             bias,
        BacktestParameters pars)
    {
        if (bars.Count < 210) return null;

        bool isLong = bias != "Short";
        var  quotes = ToQuotes(bars);
        var  cur    = bars[^1];

        double atr = CalcAtr(quotes);

        double f1 = F1_Trend(quotes, cur, isLong);
        double f2 = F2_Momentum(quotes, isLong);
        double f3 = F3_Volume(bars, isLong);
        double f4 = F4_Volatility(cur, atr, pars);
        double f5 = F5_SR(bars, cur, isLong, pars);

        double total = Math.Clamp(
            (f1 * W1 + f2 * W2 + f3 * W3 + f4 * W4 + f5 * W5) * 10,
            0, 100);

        // Trade levels
        double entry   = cur.Close;
        double slDist  = Math.Max(pars.SLMinPct / 100.0 * entry,
                                   pars.SLAtrMultiple * atr);
        double sl      = isLong ? entry - slDist : entry + slDist;
        double tp      = isLong
            ? entry * (1 + pars.TpGrossPct / 100.0)
            : entry * (1 - pars.TpGrossPct / 100.0);
        double rr      = slDist > 0 ? Math.Abs(tp - entry) / slDist : 0;

        return new ThreePctScoreResult
        {
            Symbol        = symbol,
            CoinName      = coinName,
            Bias          = bias,
            TotalScore    = Math.Round(total, 1),
            F1Trend       = Math.Round(f1, 2),
            F2Momentum    = Math.Round(f2, 2),
            F3Volume      = Math.Round(f3, 2),
            F4Volatility  = Math.Round(f4, 2),
            F5SR          = Math.Round(f5, 2),
            EntryPrice    = entry,
            StopLoss      = sl,
            TakeProfit    = tp,
            RiskReward    = Math.Round(rr, 2),
            Atr           = atr,
            FactorSummary = $"Trend:{f1:0.0}  Mom:{f2:0.0}  Vol:{f3:0.0}  Vola:{f4:0.0}  S/R:{f5:0.0}",
        };
    }

    // =========================================================================
    // Factor 1 — Trend  (EMA50/200 + short-term momentum)
    // =========================================================================

    private static double F1_Trend(List<Quote> quotes, OhlcvBar cur, bool isLong)
    {
        double ema50  = quotes.GetEma(50) .LastOrDefault()?.Ema  ?? 0;
        double ema200 = quotes.GetEma(200).LastOrDefault()?.Ema  ?? 0;
        double price  = cur.Close;
        double score  = 5.0;

        if (ema50 == 0 || ema200 == 0) return score;

        if (isLong)
        {
            // Golden cross environment
            if (ema50 > ema200)             score += 1.5;
            // Price above moving averages
            if (price > ema50)              score += 1.5;
            if (price > ema200)             score += 1.0;
            // Penalty: excessively stretched above EMA50 (risk of pullback)
            double dist50 = (price - ema50) / ema50;
            if (dist50 > 0.15)              score -= 1.0;
            if (dist50 > 0.30)              score -= 1.0;
        }
        else // Short
        {
            if (ema50 < ema200)             score += 1.5;
            if (price < ema50)              score += 1.5;
            if (price < ema200)             score += 1.0;
            double dist50 = (ema50 - price) / ema50;
            if (dist50 > 0.15)              score -= 1.0;
            if (dist50 > 0.30)              score -= 1.0;
        }

        // Short-term slope: avg of last 5 bars vs avg of prev 5 bars
        var closes = quotes.TakeLast(12).Select(q => (double)q.Close).ToList();
        if (closes.Count >= 10)
        {
            double recent = closes.TakeLast(5).Average();
            double prev   = closes.Skip(closes.Count - 10).Take(5).Average();
            double slope  = (recent - prev) / (prev + 1e-10);
            if (isLong  && slope > 0.01) score += 1.0;
            if (!isLong && slope < -0.01) score += 1.0;
        }

        return Math.Clamp(score, 0, 10);
    }

    // =========================================================================
    // Factor 2 — Momentum  (RSI + MACD als één blok)
    // =========================================================================

    private static double F2_Momentum(List<Quote> quotes, bool isLong)
    {
        double score = 5.0;

        // ── RSI ──────────────────────────────────────────────────────────────
        double rsi = (double?)quotes.GetRsi(14).LastOrDefault()?.Rsi ?? 50;

        if (isLong)
        {
            if      (rsi < 30)  score += 2.0;   // oversold: sterke kans op bounce
            else if (rsi < 45)  score += 1.0;
            else if (rsi > 70)  score -= 1.5;   // overbought: riskant instapmoment
            else if (rsi > 60)  score += 0.5;   // licht bullish momentum
        }
        else
        {
            if      (rsi > 70)  score += 2.0;
            else if (rsi > 55)  score += 1.0;
            else if (rsi < 30)  score -= 1.5;
            else if (rsi < 40)  score += 0.5;
        }

        // ── MACD ─────────────────────────────────────────────────────────────
        var macdSeries = quotes.GetMacd(12, 26, 9).ToList();
        var macdLast   = macdSeries.LastOrDefault();

        if (macdLast?.Macd is not null)
        {
            double macd  = macdLast.Macd      ?? 0;
            double sig   = macdLast.Signal    ?? 0;
            double hist  = macdLast.Histogram ?? 0;

            if (isLong)
            {
                if (macd > sig) score += 1.0;   // MACD boven signaal
                if (hist > 0)   score += 0.5;   // histogram positief

                // Acceleratie: histogram neemt de laatste 3 bars toe
                var recentHist = macdSeries.TakeLast(4)
                    .Select(r => r.Histogram ?? 0).ToList();
                if (recentHist.Count == 4
                    && recentHist[^1] > recentHist[^2]
                    && recentHist[^2] > recentHist[^3])
                    score += 0.5;
            }
            else
            {
                if (macd < sig) score += 1.0;
                if (hist < 0)   score += 0.5;

                var recentHist = macdSeries.TakeLast(4)
                    .Select(r => r.Histogram ?? 0).ToList();
                if (recentHist.Count == 4
                    && recentHist[^1] < recentHist[^2]
                    && recentHist[^2] < recentHist[^3])
                    score += 0.5;
            }
        }

        return Math.Clamp(score, 0, 10);
    }

    // =========================================================================
    // Factor 3 — Volume / OBV
    // =========================================================================

    private static double F3_Volume(List<OhlcvBar> bars, bool isLong)
    {
        if (bars.Count < 21) return 5.0;

        double curVol  = bars[^1].Volume;
        double avgVol  = bars.TakeLast(21).SkipLast(1).Average(b => b.Volume);

        double score = 5.0;

        if (avgVol > 0)
        {
            double ratio      = curVol / avgVol;
            bool   bullishBar = bars[^1].Close > bars[^2].Close;

            if (isLong)
            {
                if      (bullishBar && ratio > 1.5) score += 2.0;
                else if (bullishBar && ratio > 1.0) score += 1.0;
                else if (!bullishBar && ratio > 1.5) score -= 1.0;
            }
            else
            {
                if      (!bullishBar && ratio > 1.5) score += 2.0;
                else if (!bullishBar && ratio > 1.0) score += 1.0;
                else if (bullishBar  && ratio > 1.5) score -= 1.0;
            }
        }

        // OBV slope over laatste 10 bars
        if (bars.Count >= 11)
        {
            double obvSlope = CalcObvSlope(bars.TakeLast(10).ToList());
            if ( isLong && obvSlope > 0) score += 1.0;
            if (!isLong && obvSlope < 0) score += 1.0;
        }

        return Math.Clamp(score, 0, 10);
    }

    private static double CalcObvSlope(List<OhlcvBar> bars)
    {
        double obv = 0;
        double first = 0;
        for (int i = 1; i < bars.Count; i++)
        {
            obv += bars[i].Close > bars[i - 1].Close ?  bars[i].Volume
                 : bars[i].Close < bars[i - 1].Close ? -bars[i].Volume
                 : 0;
            if (i == 1) first = obv;
        }
        return obv - first;
    }

    // =========================================================================
    // Factor 4 — Volatiliteit & ruimte  (ATR + Bollinger)
    // =========================================================================

    private static double F4_Volatility(OhlcvBar cur, double atr, BacktestParameters pars)
    {
        if (atr <= 0 || cur.Close <= 0) return 5.0;

        double atrPct  = atr / cur.Close * 100;
        double needed  = pars.TpGrossPct;  // movement needed for target

        // Te rustig → +3% is onwaarschijnlijk binnen de horizon
        if (atrPct < 1.0)  return 2.0;
        if (atrPct < 1.5)  return 3.5;

        // Goldilocks zone: genoeg beweging, niet te chaotisch
        if (atrPct < 3.0)  return 7.0;
        if (atrPct < 5.0)  return 8.0;
        if (atrPct < 7.0)  return 6.0;

        // Te wild → SL snel geraakt
        double score = 4.0;
        if (atrPct > needed * 3) score -= 2.0;
        return Math.Clamp(score, 0, 10);
    }

    // =========================================================================
    // Factor 5 — Support & Resistance
    // =========================================================================

    private static double F5_SR(List<OhlcvBar> bars, OhlcvBar cur, bool isLong, BacktestParameters pars)
    {
        double score  = 5.0;
        double price  = cur.Close;
        double tp     = isLong
            ? price * (1 + pars.TpGrossPct / 100.0)
            : price * (1 - pars.TpGrossPct / 100.0);

        // Pivot highs en lows over de laatste 60 bars
        var recent = bars.TakeLast(60).ToList();
        var highs  = FindPivots(recent, isHigh: true);
        var lows   = FindPivots(recent, isHigh: false);

        if (isLong)
        {
            // Voordeel: support net onder entry (steun in de buurt)
            if (lows.Any(l => l < price && l > price * 0.97))
                score += 2.0;
            // Nadeel: resistance tússen entry en TP (blokkeert doel)
            if (highs.Any(r => r > price * 1.005 && r < tp))
                score -= 2.0;
        }
        else
        {
            if (highs.Any(r => r > price && r < price * 1.03))
                score += 2.0;
            if (lows.Any(s => s < price * 0.995 && s > tp))
                score -= 2.0;
        }

        // Positie t.o.v. 20-bars high/low
        if (bars.Count >= 20)
        {
            var last20 = bars.TakeLast(20).ToList();
            double h20 = last20.Max(b => b.High);
            double l20 = last20.Min(b => b.Low);
            double range = h20 - l20;
            if (range <= 0) return Math.Clamp(score, 0, 10);

            double pctFromBottom = (price - l20) / range;

            if (isLong)
            {
                // Dicht bij 20-bar low = potentieel steun + oversold
                if (pctFromBottom < 0.30) score += 1.5;
                else if (pctFromBottom > 0.80) score -= 1.0;
            }
            else
            {
                double pctFromTop = 1.0 - pctFromBottom;
                if (pctFromTop < 0.30) score += 1.5;
                else if (pctFromTop > 0.80) score -= 1.0;
            }
        }

        return Math.Clamp(score, 0, 10);
    }

    private static List<double> FindPivots(List<OhlcvBar> bars, bool isHigh)
    {
        var pivots = new List<double>();
        for (int i = 2; i < bars.Count - 2; i++)
        {
            if (isHigh)
            {
                double h = bars[i].High;
                if (h > bars[i-1].High && h > bars[i-2].High
                 && h > bars[i+1].High && h > bars[i+2].High)
                    pivots.Add(h);
            }
            else
            {
                double l = bars[i].Low;
                if (l < bars[i-1].Low && l < bars[i-2].Low
                 && l < bars[i+1].Low && l < bars[i+2].Low)
                    pivots.Add(l);
            }
        }
        return pivots;
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static double CalcAtr(List<Quote> quotes)
    {
        if (quotes.Count < 14) return 0;
        return quotes.GetAtr(14).LastOrDefault()?.Atr ?? 0;
    }

    // =========================================================================
    // Sprint B — ScoreWithGatekeepers (F6 + F7 als gatekeeper)
    // =========================================================================

    /// <summary>
    /// Voert de 5-factor score uit (Sprint A) en voegt F6/F7 toe als gatekeepers.
    /// De 5-factor score zelf blijft ongewijzigd zodat de kalibratie uit Fase 1 geldig blijft.
    /// F6 &lt; 4 of F7 &lt; 3 markeert de setup als gefilterd.
    /// </summary>
    public ThreePctScoreResult? ScoreWithGatekeepers(
        List<OhlcvBar>      bars,
        string              symbol,
        string              coinName,
        string              bias,
        BacktestParameters  pars,
        OrderBookSnapshot?  orderBook   = null,
        FuturesPositioning? positioning = null)
    {
        var base_ = Score(bars, symbol, coinName, bias, pars);
        if (base_ is null) return null;

        bool isLong = bias != "Short";

        // ── Factor 6: Liquiditeit ──────────────────────────────────────────────
        double f6       = 5.0;
        bool   f6avail  = orderBook is not null;

        if (orderBook is not null)
            f6 = F6_Liquidity(orderBook);

        // ── Factor 7: Positionering ────────────────────────────────────────────
        double f7       = 5.0;
        bool   f7avail  = positioning?.IsAvailable ?? false;

        if (positioning?.IsAvailable == true)
            f7 = F7_Positioning(positioning, isLong);

        // ── Gatekeeper-check ──────────────────────────────────────────────────
        const double F6MinThreshold = 4.0;
        const double F7MinThreshold = 3.0;

        bool   isQualified  = true;
        string filterReason = string.Empty;

        if (f6avail && f6 < F6MinThreshold)
        {
            isQualified  = false;
            filterReason = $"Liquiditeitsrisico (F6={f6:0.0} < {F6MinThreshold})";
        }
        else if (f7avail && f7 < F7MinThreshold)
        {
            isQualified  = false;
            filterReason = $"Extreme positionering (F7={f7:0.0} < {F7MinThreshold})";
        }

        return new ThreePctScoreResult
        {
            Symbol         = base_.Symbol,
            CoinName       = base_.CoinName,
            Bias           = base_.Bias,
            TotalScore     = base_.TotalScore,
            F1Trend        = base_.F1Trend,
            F2Momentum     = base_.F2Momentum,
            F3Volume       = base_.F3Volume,
            F4Volatility   = base_.F4Volatility,
            F5SR           = base_.F5SR,
            EntryPrice     = base_.EntryPrice,
            StopLoss       = base_.StopLoss,
            TakeProfit     = base_.TakeProfit,
            RiskReward     = base_.RiskReward,
            Atr            = base_.Atr,
            F6Liquidity    = Math.Round(f6, 2),
            F7Positioning  = Math.Round(f7, 2),
            F6Available    = f6avail,
            F7Available    = f7avail,
            IsQualified    = isQualified,
            FilterReason   = filterReason,
            FactorSummary  = base_.FactorSummary +
                             $"  F6:{(f6avail ? f6.ToString("0.0") : "n/a")}  F7:{(f7avail ? f7.ToString("0.0") : "n/a")}",
        };
    }

    // ── Factor 6: Liquiditeit ─────────────────────────────────────────────────

    private static double F6_Liquidity(OrderBookSnapshot ob)
    {
        double score = 5.0;

        // Spread (hoe krap, hoe beter)
        if      (ob.SpreadPct < 0.02) score += 2.0;
        else if (ob.SpreadPct < 0.05) score += 1.0;
        else if (ob.SpreadPct < 0.10) score += 0.0;
        else if (ob.SpreadPct < 0.20) score -= 1.0;
        else                          score -= 2.0;   // te breed, hoge slippage

        // Diepte (minimale kant van het boek)
        double depth = ob.MinDepthUsdt;
        if      (depth > 500_000) score += 2.0;
        else if (depth > 100_000) score += 1.0;
        else if (depth >  20_000) score += 0.0;
        else                      score -= 2.0;       // te dun

        return Math.Clamp(score, 0, 10);
    }

    // ── Factor 7: Positionering ───────────────────────────────────────────────

    private static double F7_Positioning(FuturesPositioning pos, bool isLong)
    {
        double score = 5.0;

        // Funding rate (in %)
        // Positief = longs betalen shorts (crowded long → risico squeeze voor longs)
        // Negatief = shorts betalen longs (gunstig voor longs)
        double fr = pos.FundingRatePct;
        if (isLong)
        {
            if      (fr < -0.03) score += 2.0;   // shorts betalen → gunstig
            else if (fr <  0.01) score += 0.5;
            else if (fr >  0.10) score -= 2.0;   // extreem crowded long
            else if (fr >  0.05) score -= 1.0;
        }
        else  // Short
        {
            if      (fr >  0.05) score += 2.0;   // longs betalen → gunstig voor shorts
            else if (fr >  0.01) score += 0.5;
            else if (fr < -0.05) score -= 2.0;   // extreem crowded short
            else if (fr < -0.01) score -= 1.0;
        }

        // Long/Short ratio
        double ls = pos.LongShortRatio;
        if (isLong)
        {
            // Lage ratio = veel shorts = potentiële squeeze omhoog
            if      (ls < 0.8) score += 2.0;
            else if (ls < 1.0) score += 1.0;
            else if (ls > 1.8) score -= 1.0;
            else if (ls > 2.5) score -= 2.0;  // extreem crowded long
        }
        else
        {
            if      (ls > 2.0) score += 2.0;
            else if (ls > 1.5) score += 1.0;
            else if (ls < 0.7) score -= 1.0;
            else if (ls < 0.5) score -= 2.0;  // extreem crowded short
        }

        return Math.Clamp(score, 0, 10);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // =========================================================================
    // Sprint C — BuildDetailInfo (voor de detail-dialog)
    // =========================================================================

    /// <summary>
    /// Berekent alle gedetailleerde indicatorwaarden voor het detailvenster.
    /// Geeft null terug bij onvoldoende bars.
    /// </summary>
    public static SetupDetailInfo? BuildDetailInfo(
        ThreePctLiveRow         row,
        List<OhlcvBar>          bars,
        List<OhlcvBar>?         btcBars,
        OrderBookSnapshot?      orderBook,
        FuturesPositioning?     positioning,
        IReadOnlyList<MacroEvent> upcomingEvents,
        BacktestParameters      pars)
    {
        if (bars.Count < 50) return null;

        var quotes = ToQuotes(bars);
        var cur    = bars[^1];
        bool isLong = row.Bias != "Short";

        // ── Indicatoren ───────────────────────────────────────────────────────
        double rsi      = (double?)quotes.GetRsi(14).LastOrDefault()?.Rsi ?? 0;
        double atr      = quotes.GetAtr(14).LastOrDefault()?.Atr ?? 0;
        double ema50    = quotes.GetEma(50) .LastOrDefault()?.Ema  ?? 0;
        double ema200   = quotes.GetEma(200).LastOrDefault()?.Ema  ?? 0;
        double atrPct   = cur.Close > 0 ? atr / cur.Close * 100 : 0;

        double ema50Dist  = ema50  > 0 ? (cur.Close - ema50)  / ema50  * 100 : 0;
        double ema200Dist = ema200 > 0 ? (cur.Close - ema200) / ema200 * 100 : 0;

        // MACD histogram
        double macdHist = quotes.GetMacd(12, 26, 9).LastOrDefault()?.Histogram ?? 0;

        // EMA-context
        bool golden = ema50 > ema200;
        string emaCtx = (golden, cur.Close > ema50) switch
        {
            (true,  true)  => "Golden Cross ↑ — prijs boven EMA50",
            (true,  false) => "Golden Cross — prijs ONDER EMA50 (pullback zone)",
            (false, false) => "Death Cross ↓ — prijs onder EMA50",
            (false, true)  => "Death Cross — prijs boven EMA50 (bearish rebound?)",
        };

        // Volume ratio
        double curVol   = bars[^1].Volume;
        double avgVol20 = bars.Count >= 21
            ? bars.TakeLast(21).SkipLast(1).Average(b => b.Volume)
            : 0;
        double volRatio = avgVol20 > 0 ? curVol / avgVol20 * 100 : 0;

        // Squeeze (BB vs Keltner)
        bool isSqueeze = false;
        try
        {
            var bbL  = quotes.GetBollingerBands(20, 2).LastOrDefault();
            var kcL  = quotes.GetKeltner(20, 1.5, 10).LastOrDefault();
            if (bbL is not null && kcL is not null)
            {
                double bbW = (bbL.UpperBand ?? 0) - (bbL.LowerBand ?? 0);
                double kcW = (kcL.UpperBand ?? 0) - (kcL.LowerBand ?? 0);
                isSqueeze = kcW > 0 && bbW < kcW;
            }
        }
        catch { }

        // ── S/R niveaus ───────────────────────────────────────────────────────
        var recentBars   = bars.TakeLast(60).ToList();
        var allHighs     = FindPivots(recentBars, isHigh: true);
        var allLows      = FindPivots(recentBars, isHigh: false);

        var nearSup = allLows
            .Where(l => l < cur.Close)
            .OrderByDescending(l => l)
            .Take(3)
            .ToList();
        var nearRes = allHighs
            .Where(h => h > cur.Close)
            .OrderBy(h => h)
            .Take(3)
            .ToList();

        // ── BTC-correlatie ────────────────────────────────────────────────────
        double btcCorr = 0;
        if (btcBars is not null && btcBars.Count >= 10)
            btcCorr = new CorrelationService().ComputePearson(bars, btcBars, 60);

        // ── Invalidatieniveau ─────────────────────────────────────────────────
        string invalidation = isLong
            ? $"Setup ongeldig bij dagelijkse close onder SL {Fmt(row.StopLoss)}" +
              (nearSup.Count > 0 ? $" (dichtstbijzijnde steun: {Fmt(nearSup[0])})" : string.Empty)
            : $"Setup ongeldig bij dagelijkse close boven SL {Fmt(row.StopLoss)}" +
              (nearRes.Count > 0 ? $" (dichtstbijzijnde weerstand: {Fmt(nearRes[0])})" : string.Empty);

        return new SetupDetailInfo
        {
            Row             = row,
            Bias            = row.Bias,
            BuiltAt         = DateTime.Now,
            F1Trend         = row.F6Score,       // direct uit LiveRow
            F2Momentum      = row.F7Score,
            F3Volume        = 0,                 // individuele factorscores zijn niet op LiveRow
            F4Volatility    = 0,                 // beschikbaar via FactorBreakdown string
            F5SR            = 0,
            Rsi             = rsi,
            MacdHistogram   = macdHist,
            Ema50           = ema50,
            Ema200          = ema200,
            Ema50DistPct    = Math.Round(ema50Dist,  1),
            Ema200DistPct   = Math.Round(ema200Dist, 1),
            AtrPct          = Math.Round(atrPct, 2),
            IsSqueeze       = isSqueeze,
            EmaContext      = emaCtx,
            VolumeRatioPct  = Math.Round(volRatio, 0),
            NearSupports    = nearSup,
            NearResistances = nearRes,
            BtcCorrelation  = Math.Round(btcCorr, 2),
            CorrelationLabel= CorrelationService.CorrelationLabel(btcCorr),
            BidAskSpreadPct = orderBook?.SpreadPct,
            MinDepthUsdt    = orderBook?.MinDepthUsdt,
            FundingRatePct  = positioning?.IsAvailable == true ? positioning.FundingRatePct : (double?)null,
            LongShortRatio  = positioning?.IsAvailable == true ? positioning.LongShortRatio : (double?)null,
            OpenInterest    = positioning?.IsAvailable == true ? positioning.OpenInterest   : (double?)null,
            InvalidationNote = invalidation,
            UpcomingEvents  = upcomingEvents,
        };
    }

    private static string Fmt(double p) => p switch
    {
        >= 1_000 => $"{p:#,0.00}",
        >= 1     => $"{p:F4}",
        >= 0.01  => $"{p:F6}",
        _        => $"{p:F8}",
    };

    /// <summary>
    /// Converteert OhlcvBar-lijst naar Skender Quote-objecten MET volumedata.
    /// Dit is de kritieke fix t.o.v. IndicatorService.LoadQuotesAsync (die Volume=0 instelt).
    /// Internal zodat MarketRegimeService het ook kan gebruiken.
    /// </summary>
    internal static List<Quote> ToQuotes(List<OhlcvBar> bars) =>
        bars.Select(b => new Quote
        {
            Date   = b.Date,
            Open   = (decimal)b.Open,
            High   = (decimal)b.High,
            Low    = (decimal)b.Low,
            Close  = (decimal)b.Close,
            Volume = (decimal)b.Volume,   // ← echte volumedata, niet 0
        }).ToList();
}
