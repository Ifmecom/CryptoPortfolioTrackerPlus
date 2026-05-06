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
            var prices = await LoadClosingPricesAsync(coin);
            prices.Add(coin.Price);

            int period = _settings.RsiPeriod;
            if (period == 0 || prices == null || prices.Count < period + 1)
            {
                coin.Rsi = 0;
                return;
            }

            var rsiValues = new List<double>();
            var gains = new List<double>();
            var losses = new List<double>();

            for (int i = 1; i <= period; i++)
            {
                double change = prices[i] - prices[i - 1];
                if (change > 0)
                {
                    gains.Add(change);
                    losses.Add(0);
                }
                else
                {
                    gains.Add(0);
                    losses.Add(-change);
                }
            }

            double avgGain = gains.Average();
            double avgLoss = losses.Average();
            double alpha = 1.0 / period;

            double rs = avgGain / avgLoss;
            double rsi = 100 - (100 / (1 + rs));
            rsiValues.Add(rsi);

            for (int i = period + 1; i < prices.Count; i++)
            {
                double change = prices[i] - prices[i - 1];
                double gain = change > 0 ? change : 0;
                double loss = change < 0 ? -change : 0;

                avgGain = (gain * alpha) + (avgGain * (1 - alpha));
                avgLoss = (loss * alpha) + (avgLoss * (1 - alpha));

                rs = avgGain / avgLoss;
                rsi = 100 - (100 / (1 + rs));
                rsiValues.Add(rsi);
            }

            coin.Rsi = rsiValues.Last();
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
            var macd      = await CalculateMacdAsync(coin);
            var bollinger = await CalculateBollingerAsync(coin);
            await CalculateAtrAsync(coin);
            await CalculateStochRsiAsync(coin);
            await CalculateRsiAsync(coin);

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
        await CalculateRsiAsync(coin);
        await CalculateMaAsync(coin);
        await CalculateMacdAsync(coin);
        await CalculateBollingerAsync(coin);
        await CalculateAtrAsync(coin);
        await CalculateStochRsiAsync(coin);
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