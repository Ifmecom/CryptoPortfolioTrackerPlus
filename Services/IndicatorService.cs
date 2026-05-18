using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Infrastructure.Response.Coins;
using CryptoPortfolioTracker.Models;
using Skender.Stock.Indicators;

namespace CryptoPortfolioTracker.Services;

public class IndicatorService : IIndicatorService
{
    private readonly Settings _settings;

    public IndicatorService(Settings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public async Task CalculateRsiAsync(Coin coin)
    {
        if (coin is null) throw new ArgumentNullException(nameof(coin));

        try
        {
            var quotes = await LoadQuotesAsync(coin);
            if (quotes.Count < 15)
            {
                coin.Rsi = 0;
                return;
            }

            var rsiResult = quotes.GetRsi(14).LastOrDefault();
            coin.Rsi = rsiResult?.Rsi is not null ? (double)rsiResult.Rsi : 0;
        }
        catch
        {
            coin.Rsi = 0;
        }
    }

    public async Task<double> CalculateMaAsync(Coin coin)
    {
        if (coin is null) throw new ArgumentNullException(nameof(coin));

        var prices = await LoadClosingPricesAsync(coin);
        prices.Add(coin.Price);

        int period = _settings.MaPeriod;
        string maType = _settings.MaType;

        if (period == 0 || prices == null || prices.Count < period)
        {
            coin.Ema = 0;
            return 0;
        }

        if (string.Equals(maType, "SMA", StringComparison.OrdinalIgnoreCase))
        {
            double smaRecent = prices.TakeLast(period).Average();
            coin.Ema = smaRecent;
            UpdatePriceLevelEma(coin);
            return smaRecent;
        }

        // EMA calculation
        double sma = prices.Take(period).Average();
        double multiplier = 2.0 / (period + 1);
        double ema = sma;

        for (int i = period; i < prices.Count; i++)
        {
            ema = ((prices[i] - ema) * multiplier) + ema;
        }

        coin.Ema = ema;
        UpdatePriceLevelEma(coin);
        return ema;
    }

    public void EvaluatePriceLevels(Coin coin, double newValue)
    {
        if (coin is null) throw new ArgumentNullException(nameof(coin));
        if (newValue == 0) return;

        var withinRangePerc = _settings.WithinRangePerc;
        var closeToPerc = _settings.CloseToPerc;

        foreach (var level in coin.PriceLevels)
        {
            if (level.Value == 0) continue;

            var dist = (100 * (newValue - level.Value) / level.Value);
            level.DistanceToValuePerc = dist;

            if ((level.Type == PriceLevelType.TakeProfit && newValue >= level.Value) ||
                (level.Type == PriceLevelType.Buy && newValue <= level.Value) ||
                (level.Type == PriceLevelType.Stop && newValue <= level.Value) ||
                (level.Type == PriceLevelType.Ema && newValue <= level.Value))
            {
                level.Status = PriceLevelStatus.TaggedPrice;
                continue;
            }

            if (dist >= -1 * closeToPerc)
            {
                level.Status = PriceLevelStatus.CloseToPrice;
                continue;
            }

            if (dist >= -1 * withinRangePerc)
            {
                level.Status = PriceLevelStatus.WithinRange;
                continue;
            }

            level.Status = PriceLevelStatus.NotWithinRange;
        }

        // Note: original model invoked OnPropertyChanged(nameof(PriceLevels)).
        // If UI binding requires a change notification for the collection itself,
        // caller should raise it (or Coin should expose a method to do so).
    }

    // --- Helpers ---

    private async Task<List<double>> LoadClosingPricesAsync(Coin coin)
    {
        // Loads market chart JSON and returns the recent closing prices (no caching here).
        try
        {
            var fileName = Path.Combine(AppConstants.ChartsFolder, $"MarketChart_{coin.ApiId}.json");
            if (!File.Exists(fileName))
                return new List<double>();

            var suffix = coin.Name.Contains("_pre-listing") ? "-prelisting" : "";
            var marketChart = new MarketChartById();
            await marketChart.LoadMarketChartJson(coin.ApiId + suffix);

            if (marketChart.Prices?.Length > 0)
                return marketChart.Prices.TakeLast(150).Select(p => (double)p[1].Value).ToList();

            return new List<double>();
        }
        catch
        {
            return new List<double>();
        }
    }

    private void UpdatePriceLevelEma(Coin coin)
    {
        var priceLevelEma = coin.PriceLevels.FirstOrDefault(p => p.Type == PriceLevelType.Ema);
        if (priceLevelEma != null)
        {
            priceLevelEma.Value = coin.Ema;
            if (coin.Ema != 0)
                priceLevelEma.DistanceToValuePerc = (100 * (coin.Price - coin.Ema) / coin.Ema);
        }
        else
        {
            var newPriceLevel = new PriceLevel
            {
                Type = PriceLevelType.Ema,
                Value = coin.Ema,
                Coin = coin,
                DistanceToValuePerc = coin.Ema != 0 ? (100 * (coin.Price - coin.Ema) / coin.Ema) : 0
            };
            coin.PriceLevels.Add(newPriceLevel);
        }
    }

    // -----------------------------------------------------------------------
    // PLUS — Sprint 1.2: extended indicators via Skender.Stock.Indicators
    // -----------------------------------------------------------------------

    public async Task<MacdData> CalculateMacdAsync(Coin coin)
    {
        if (coin is null) throw new ArgumentNullException(nameof(coin));
        try
        {
            var quotes = await LoadQuotesAsync(coin);
            if (quotes.Count < 35) return new MacdData(0, 0, 0);

            var result = quotes.GetMacd(12, 26, 9).LastOrDefault();
            if (result is null) return new MacdData(0, 0, 0);

            var macd      = result.Macd      ?? 0;
            var signal    = result.Signal    ?? 0;
            var histogram = result.Histogram ?? 0;

            coin.Macd       = macd;
            coin.MacdSignal = signal;
            return new MacdData(macd, signal, histogram);
        }
        catch { return new MacdData(0, 0, 0); }
    }

    public async Task<BollingerData> CalculateBollingerAsync(Coin coin)
    {
        if (coin is null) throw new ArgumentNullException(nameof(coin));
        try
        {
            var quotes = await LoadQuotesAsync(coin);
            if (quotes.Count < 20) return new BollingerData(0, 0, 0);

            var result = quotes.GetBollingerBands(20, 2).LastOrDefault();
            if (result is null) return new BollingerData(0, 0, 0);

            var upper  = result.UpperBand ?? 0;
            var middle = result.Sma       ?? 0;
            var lower  = result.LowerBand ?? 0;

            coin.BollingerUpper = upper;
            coin.BollingerLower = lower;
            return new BollingerData(upper, middle, lower);
        }
        catch { return new BollingerData(0, 0, 0); }
    }

    public async Task<double> CalculateAtrAsync(Coin coin)
    {
        if (coin is null) throw new ArgumentNullException(nameof(coin));
        try
        {
            var quotes = await LoadQuotesAsync(coin);
            if (quotes.Count < 14) return 0;

            var result = quotes.GetAtr(14).LastOrDefault();
            var atr = result?.Atr ?? 0;
            coin.Atr = atr;
            return atr;
        }
        catch { return 0; }
    }

    public async Task<double> CalculateStochRsiAsync(Coin coin)
    {
        if (coin is null) throw new ArgumentNullException(nameof(coin));
        try
        {
            var quotes = await LoadQuotesAsync(coin);
            if (quotes.Count < 50) return 0;

            var result = quotes.GetStochRsi(14, 14, 3, 1).LastOrDefault();
            var stochRsi = result?.StochRsi ?? 0;
            coin.StochRsi = stochRsi;
            return stochRsi;
        }
        catch { return 0; }
    }

    public async Task<TaScore> CalculateTaScoreAsync(Coin coin, Timeframe tf)
    {
        if (coin is null) throw new ArgumentNullException(nameof(coin));

        var triggered = new List<string>();
        double score = 50.0;

        try
        {
            // Laad quotes EENMALIG en geef ze door aan alle sub-methoden.
            // Zonder cache laadde elk van de 6 sub-methoden het JSON-bestand opnieuw:
            // 50 coins × 6 reads = 300 I/O-operaties per cyclus.
            var quotes = await LoadQuotesAsync(coin);

            var macd      = CalculateMacdFromQuotes(coin, quotes);
            var bollinger = CalculateBollingerFromQuotes(coin, quotes);
            CalculateAtrFromQuotes(coin, quotes);
            CalculateStochRsiFromQuotes(coin, quotes);
            CalculateRsiFromQuotes(coin, quotes);
            CalculateExtendedIndicatorsFromQuotes(coin, quotes);

            // RSI rules
            if (coin.Rsi > 0)
            {
                if (coin.Rsi < 30) { score += 15; triggered.Add("RSI oversold (<30)"); }
                else if (coin.Rsi > 70) { score -= 15; triggered.Add("RSI overbought (>70)"); }
                else if (coin.Rsi < 50) { score += 5; triggered.Add("RSI bearish zone"); }
                else { score += 5; triggered.Add("RSI bullish zone"); }
            }

            // MACD rules
            if (macd.Macd != 0)
            {
                if (macd.Macd > macd.Signal) { score += 10; triggered.Add("MACD above signal"); }
                else { score -= 10; triggered.Add("MACD below signal"); }

                if (macd.Histogram > 0) { score += 5; triggered.Add("MACD histogram positive"); }
                else { score -= 5; triggered.Add("MACD histogram negative"); }
            }

            // Bollinger rules
            if (bollinger.Upper > 0 && coin.Price > 0)
            {
                if (coin.Price < bollinger.Lower) { score += 10; triggered.Add("Price below Bollinger lower band"); }
                else if (coin.Price > bollinger.Upper) { score -= 10; triggered.Add("Price above Bollinger upper band"); }
            }

            // StochRSI rules
            if (coin.StochRsi > 0)
            {
                if (coin.StochRsi < 20) { score += 10; triggered.Add("StochRSI oversold (<20)"); }
                else if (coin.StochRsi > 80) { score -= 10; triggered.Add("StochRSI overbought (>80)"); }
            }

            // EMA Cross rules
            if (coin.EmaCross == "Bullish")  { score += 8; triggered.Add($"EMA 9/21 bullish cross ({coin.EmaCrossBarsAgo}d ago)"); }
            else if (coin.EmaCross == "Bearish") { score -= 8; triggered.Add($"EMA 9/21 bearish cross ({coin.EmaCrossBarsAgo}d ago)"); }

            // MA50 distance
            if (coin.Ma50DistPerc > 0) { score += 5; triggered.Add($"Price above MA50 (+{coin.Ma50DistPerc:F1}%)"); }
            else if (coin.Ma50DistPerc < 0) { score -= 5; triggered.Add($"Price below MA50 ({coin.Ma50DistPerc:F1}%)"); }

            // %B — momentum context
            if (coin.BollingerPctB < 15) { score += 5; triggered.Add($"%B oversold ({coin.BollingerPctB:F0})"); }
            else if (coin.BollingerPctB > 85) { score -= 5; triggered.Add($"%B overbought ({coin.BollingerPctB:F0})"); }

            // ADX — trendsterkte als versterker/demper
            // Sterke trend (>25): versterkt de dominante richting met +5 of -5
            // Zijwaartse markt (<20): dempt alle richtingssignalen richting neutraal
            if (coin.Adx >= 25)
            {
                if (score > 50) { score += 5;  triggered.Add($"ADX={coin.Adx:F0} strong trend confirms bullish"); }
                else            { score -= 5;  triggered.Add($"ADX={coin.Adx:F0} strong trend confirms bearish"); }
            }
            else if (coin.Adx > 0 && coin.Adx < 20)
            {
                // Zijwaartse markt: trek score richting 50 (demping van ±4)
                double dampen = (score - 50) * 0.08;
                score -= dampen;
                triggered.Add($"ADX={coin.Adx:F0} sideways market, signals dampened");
            }

            // Squeeze — volatiliteitscompressie: breakout-verwachting in richting van de score
            // Actieve squeeze voegt vertrouwen toe dat een aankomende beweging sterker zal zijn
            if (coin.IsSqueeze)
            {
                if (score > 55)      { score += 4; triggered.Add("Squeeze active — bullish breakout expected"); }
                else if (score < 45) { score -= 4; triggered.Add("Squeeze active — bearish breakout expected"); }
                else                  { triggered.Add("Squeeze active — direction unclear, wait for breakout"); }
            }

            // 52-weeks afstand — contrair signaal bij extreme dips + bevestiging bij toppen
            // Ver onder 52w high én oversold RSI = potentieel koopmoment
            // Dichtbij 52w high = overbought risico op lange termijn
            if (coin.High52wPerc <= -50 && coin.Rsi < 40)
            {
                score += 8;
                triggered.Add($"Deep discount: {coin.High52wPerc:F0}% from 52w high + RSI oversold");
            }
            else if (coin.High52wPerc <= -30 && coin.Rsi < 35)
            {
                score += 5;
                triggered.Add($"Significant discount: {coin.High52wPerc:F0}% from 52w high + RSI low");
            }
            else if (coin.High52wPerc >= -5 && coin.Rsi > 65)
            {
                score -= 5;
                triggered.Add($"Near 52w high ({coin.High52wPerc:F0}%) + RSI elevated — overbought risk");
            }

            score = Math.Clamp(score, 0, 100);
            var direction = score >= 60 ? SignalDirection.Long
                          : score <= 40 ? SignalDirection.Short
                          : SignalDirection.Flat;

            return new TaScore(direction, score, triggered);
        }
        catch
        {
            return new TaScore(SignalDirection.Flat, 50, triggered);
        }
    }

    public async Task RecalculateAllAsync(Coin coin)
    {
        if (coin is null) throw new ArgumentNullException(nameof(coin));
        // Laad quotes eenmalig — was eerder 7 losse LoadQuotesAsync calls voor dezelfde coin
        var quotes = await LoadQuotesAsync(coin);
        CalculateRsiFromQuotes(coin, quotes);
        await CalculateMaAsync(coin);           // gebruikt aparte LoadClosingPricesAsync
        CalculateMacdFromQuotes(coin, quotes);
        CalculateBollingerFromQuotes(coin, quotes);
        CalculateAtrFromQuotes(coin, quotes);
        CalculateStochRsiFromQuotes(coin, quotes);
        CalculateExtendedIndicatorsFromQuotes(coin, quotes);
    }

    public async Task CalculateExtendedIndicatorsAsync(Coin coin)
    {
        if (coin is null) throw new ArgumentNullException(nameof(coin));
        try
        {
            var quotes = await LoadQuotesAsync(coin);
            if (quotes.Count < 22) return;

            var closes = quotes.Select(q => (double)q.Close).ToList();

            // EMA Cross (9/21)
            var ema9List  = quotes.GetEma(9).Select(r => r.Ema ?? 0).ToList();
            var ema21List = quotes.GetEma(21).Select(r => r.Ema ?? 0).ToList();
            coin.EmaCross = "–";
            coin.EmaCrossBarsAgo = 0;
            for (int i = ema9List.Count - 1; i >= 1; i--)
            {
                bool currAbove = ema9List[i] > ema21List[i];
                bool prevAbove = ema9List[i - 1] > ema21List[i - 1];
                if (currAbove != prevAbove)
                {
                    coin.EmaCross = currAbove ? "Bullish" : "Bearish";
                    coin.EmaCrossBarsAgo = ema9List.Count - 1 - i;
                    break;
                }
            }

            // %B — Bollinger position (uses already-computed upper/lower if available)
            if (coin.BollingerUpper > 0 && coin.BollingerUpper != coin.BollingerLower)
            {
                coin.BollingerPctB = Math.Round(
                    Math.Clamp((coin.Price - coin.BollingerLower) / (coin.BollingerUpper - coin.BollingerLower) * 100.0, -50, 150), 1);
            }

            // MA50 distance %
            if (quotes.Count >= 50)
            {
                var sma50 = quotes.GetSma(50).LastOrDefault()?.Sma ?? 0;
                if (sma50 > 0)
                    coin.Ma50DistPerc = Math.Round((coin.Price - sma50) / sma50 * 100.0, 1);
            }

            // ADX
            if (quotes.Count >= 28)
            {
                var adxResult = quotes.GetAdx(14).LastOrDefault();
                coin.Adx = Math.Round(adxResult?.Adx ?? 0, 1);
            }

            // Squeeze: BB(20,2) width vs Keltner(20,1.5,10) width
            if (quotes.Count >= 30)
            {
                var bbLast = quotes.GetBollingerBands(20, 2).LastOrDefault();
                var kcLast = quotes.GetKeltner(20, 1.5, 10).LastOrDefault();
                if (bbLast is not null && kcLast is not null)
                {
                    double bbWidth = (bbLast.UpperBand ?? 0) - (bbLast.LowerBand ?? 0);
                    double kcWidth = (kcLast.UpperBand ?? 0) - (kcLast.LowerBand ?? 0);
                    coin.IsSqueeze = kcWidth > 0 && bbWidth < kcWidth;
                }
            }

            // % from 52-week high (max of last ~180 daily closes)
            if (closes.Count >= 2)
            {
                int lookback = Math.Min(closes.Count, 180);
                double high52w = closes.TakeLast(lookback).Max();
                if (high52w > 0)
                    coin.High52wPerc = Math.Round((coin.Price - high52w) / high52w * 100.0, 1);
            }
        }
        catch { /* per-coin failures are silent */ }
    }

    // -----------------------------------------------------------------------
    // Quotes-first overloads — vermijden herhaalde JSON file reads in CalculateTaScoreAsync
    // -----------------------------------------------------------------------

    private static MacdData CalculateMacdFromQuotes(Coin coin, List<Quote> quotes)
    {
        try
        {
            if (quotes.Count < 35) return new MacdData(0, 0, 0);
            var result = quotes.GetMacd(12, 26, 9).LastOrDefault();
            if (result is null) return new MacdData(0, 0, 0);
            coin.Macd       = result.Macd      ?? 0;
            coin.MacdSignal = result.Signal    ?? 0;
            return new MacdData(coin.Macd, coin.MacdSignal, result.Histogram ?? 0);
        }
        catch { return new MacdData(0, 0, 0); }
    }

    private static BollingerData CalculateBollingerFromQuotes(Coin coin, List<Quote> quotes)
    {
        try
        {
            if (quotes.Count < 20) return new BollingerData(0, 0, 0);
            var result = quotes.GetBollingerBands(20, 2).LastOrDefault();
            if (result is null) return new BollingerData(0, 0, 0);
            coin.BollingerUpper = result.UpperBand ?? 0;
            coin.BollingerLower = result.LowerBand ?? 0;
            return new BollingerData(coin.BollingerUpper, result.Sma ?? 0, coin.BollingerLower);
        }
        catch { return new BollingerData(0, 0, 0); }
    }

    private static void CalculateAtrFromQuotes(Coin coin, List<Quote> quotes)
    {
        try
        {
            if (quotes.Count < 14) return;
            coin.Atr = quotes.GetAtr(14).LastOrDefault()?.Atr ?? 0;
        }
        catch { coin.Atr = 0; }
    }

    private static void CalculateStochRsiFromQuotes(Coin coin, List<Quote> quotes)
    {
        try
        {
            if (quotes.Count < 50) return;
            coin.StochRsi = quotes.GetStochRsi(14, 14, 3, 1).LastOrDefault()?.StochRsi ?? 0;
        }
        catch { coin.StochRsi = 0; }
    }

    private static void CalculateRsiFromQuotes(Coin coin, List<Quote> quotes)
    {
        try
        {
            if (quotes.Count < 15) { coin.Rsi = 0; return; }
            var r = quotes.GetRsi(14).LastOrDefault();
            coin.Rsi = r?.Rsi is not null ? (double)r.Rsi : 0;
        }
        catch { coin.Rsi = 0; }
    }

    private void CalculateExtendedIndicatorsFromQuotes(Coin coin, List<Quote> quotes)
    {
        try
        {
            if (quotes.Count < 22) return;
            var closes    = quotes.Select(q => (double)q.Close).ToList();
            var ema9List  = quotes.GetEma(9).Select(r => r.Ema ?? 0).ToList();
            var ema21List = quotes.GetEma(21).Select(r => r.Ema ?? 0).ToList();
            coin.EmaCross = "–";
            coin.EmaCrossBarsAgo = 0;
            for (int i = ema9List.Count - 1; i >= 1; i--)
            {
                bool currAbove = ema9List[i] > ema21List[i];
                bool prevAbove = ema9List[i - 1] > ema21List[i - 1];
                if (currAbove != prevAbove)
                {
                    coin.EmaCross = currAbove ? "Bullish" : "Bearish";
                    coin.EmaCrossBarsAgo = ema9List.Count - 1 - i;
                    break;
                }
            }
            if (coin.BollingerUpper > 0 && coin.BollingerUpper != coin.BollingerLower)
                coin.BollingerPctB = Math.Round(
                    Math.Clamp((coin.Price - coin.BollingerLower) / (coin.BollingerUpper - coin.BollingerLower) * 100.0, -50, 150), 1);
            if (quotes.Count >= 50)
            {
                var sma50 = quotes.GetSma(50).LastOrDefault()?.Sma ?? 0;
                if (sma50 > 0) coin.Ma50DistPerc = Math.Round((coin.Price - sma50) / sma50 * 100.0, 1);
            }
            if (quotes.Count >= 28)
                coin.Adx = Math.Round(quotes.GetAdx(14).LastOrDefault()?.Adx ?? 0, 1);
            if (quotes.Count >= 30)
            {
                var bbLast = quotes.GetBollingerBands(20, 2).LastOrDefault();
                var kcLast = quotes.GetKeltner(20, 1.5, 10).LastOrDefault();
                if (bbLast is not null && kcLast is not null)
                {
                    double bbWidth = (bbLast.UpperBand ?? 0) - (bbLast.LowerBand ?? 0);
                    double kcWidth = (kcLast.UpperBand ?? 0) - (kcLast.LowerBand ?? 0);
                    coin.IsSqueeze = kcWidth > 0 && bbWidth < kcWidth;
                }
            }
            if (closes.Count >= 2)
            {
                int lookback = Math.Min(closes.Count, 180);
                double high52w = closes.TakeLast(lookback).Max();
                if (high52w > 0) coin.High52wPerc = Math.Round((coin.Price - high52w) / high52w * 100.0, 1);
            }
        }
        catch { /* per-coin failures are silent */ }
    }

    // Loads closing prices as Skender Quote objects (OHLCV with close = price from chart)
    private async Task<List<Quote>> LoadQuotesAsync(Coin coin)
    {
        try
        {
            var fileName = Path.Combine(AppConstants.ChartsFolder, $"MarketChart_{coin.ApiId}.json");
            if (!File.Exists(fileName)) return new List<Quote>();

            var suffix = coin.Name.Contains("_pre-listing") ? "-prelisting" : "";
            var marketChart = new MarketChartById();
            await marketChart.LoadMarketChartJson(coin.ApiId + suffix);

            if (marketChart.Prices?.Length > 0)
            {
                return marketChart.Prices
                    .TakeLast(200)
                    .Select((p, i) => new Quote
                    {
                        Date   = DateTimeOffset.FromUnixTimeMilliseconds((long)p[0].Value).UtcDateTime,
                        Open   = (decimal)p[1].Value,
                        High   = (decimal)p[1].Value,
                        Low    = (decimal)p[1].Value,
                        Close  = (decimal)p[1].Value,
                        Volume = 0
                    })
                    .ToList();
            }
        }
        catch { }
        return new List<Quote>();
    }
}