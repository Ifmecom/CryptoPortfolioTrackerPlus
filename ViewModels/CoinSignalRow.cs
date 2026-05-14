using CryptoPortfolioTracker.Models;
using System.Collections.Generic;

namespace CryptoPortfolioTracker.ViewModels;

public class CoinSignalRow
{
    // Identity
    public long   Rank           { get; }
    public string Name           { get; }
    public string Symbol         { get; }
    public string ImageUri       { get; }
    public int    CoinId         { get; }

    // Indicators (from Coin)
    public double Macd           { get; }
    public double MacdSignal     { get; }
    public double BollingerUpper { get; }
    public double BollingerLower { get; }
    public double Atr            { get; }
    public double StochRsi       { get; }
    public double Sentiment      { get; }
    public string Regime         { get; }

    // Extended indicators (Sprint 1.2+)
    public string EmaCross        { get; }
    public int    EmaCrossBarsAgo { get; }
    public double BollingerPctB   { get; }
    public double Ma50DistPerc    { get; }
    public double RsiDaily        { get; }
    public double Adx             { get; }
    public bool   IsSqueeze       { get; }
    public double High52wPerc     { get; }

    // Signal evaluation (from latest Signal, if any)
    public int?   SignalId       { get; }
    public string Direction      { get; }
    public double CombinedScore  { get; }
    public double TaScore        { get; }
    public string Reasoning      { get; }

    // Backing coin price (for paper trade)
    public double Price          { get; }

    // Sparkline trend data (daily closes used as proxy for each timeframe)
    // Trend1h  → last 14 daily closes  (short-term 2-week window)
    // Trend4h  → last 30 daily closes  (medium-term monthly window)
    // TrendDay → last 90 daily closes  (long-term quarterly window)
    public IReadOnlyList<double> Trend1h  { get; }
    public IReadOnlyList<double> Trend4h  { get; }
    public IReadOnlyList<double> TrendDay { get; }

    // -----------------------------------------------------------------------
    // Display helpers
    // -----------------------------------------------------------------------

    public string MacdDisplay      => Macd == 0 && MacdSignal == 0 ? "–"
                                        : $"{Macd:+0.####;-0.####;0}";
    public string BollingerDisplay      => string.Empty; // kept for sort; display uses split properties below
    public string BollingerHighDisplay  => BollingerUpper == 0 ? "–" : $"H  {BollingerUpper:#,0.##}";
    public string BollingerPriceDisplay => BollingerUpper == 0 ? string.Empty : $"●  {Price:#,0.##}";
    public string BollingerLowDisplay   => BollingerUpper == 0 ? string.Empty : $"L  {BollingerLower:#,0.##}";
    public string AtrDisplay       => Atr == 0       ? "–" : $"{Atr:#,0.####}";
    public string StochRsiDisplay  => StochRsi == 0  ? "–" : $"{StochRsi:0.##}";
    public string SentimentDisplay => Sentiment == 0 ? "–" : $"{Sentiment:+0.000;-0.000}";
    public string ScoreDisplay     => CombinedScore == 0 ? "–" : $"{CombinedScore:F1}";
    public bool   HasSignal        => SignalId.HasValue;

    // Extended indicator display helpers
    public string EmaCrossDisplay      => EmaCross == "–" ? "–"
        : $"{(EmaCross == "Bullish" ? "▲" : "▼")} {EmaCross}\n{EmaCrossBarsAgo}d ago";
    public string RsiDailyDisplay      => RsiDaily == 0 ? "–" : $"{RsiDaily:F1}";
    public string BollingerPctBDisplay => BollingerPctB == 0 ? "–" : $"{BollingerPctB:F0}";
    public string Ma50DistDisplay      => Ma50DistPerc == 0 ? "–" : $"{Ma50DistPerc:+0.0;-0.0} %";
    public string AdxDisplay2          => Adx == 0 ? "–" : $"{Adx:F1}";
    public string SqueezeDisplay       => IsSqueeze ? "Aan" : "Af";
    public string High52wDisplay       => High52wPerc == 0 ? "–" : $"{High52wPerc:0.0} %";

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------

    public CoinSignalRow(Coin coin, Signal? latestSignal = null,
                         IReadOnlyList<double>? trend1h  = null,
                         IReadOnlyList<double>? trend4h  = null,
                         IReadOnlyList<double>? trendDay = null)
    {
        Rank           = coin.Rank;
        Name           = coin.Name;
        Symbol         = coin.Symbol?.ToUpperInvariant() ?? "";
        ImageUri       = coin.ImageUri;
        CoinId         = coin.Id;
        Price          = coin.Price;

        Macd           = coin.Macd;
        MacdSignal     = coin.MacdSignal;
        BollingerUpper = coin.BollingerUpper;
        BollingerLower = coin.BollingerLower;
        Atr            = coin.Atr;
        StochRsi       = coin.StochRsi;
        Sentiment      = coin.LatestSentimentScore;
        Regime         = coin.MarketRegime.ToString();

        Trend1h  = trend1h  ?? System.Array.Empty<double>();
        Trend4h  = trend4h  ?? System.Array.Empty<double>();
        TrendDay = trendDay ?? System.Array.Empty<double>();

        EmaCross        = coin.EmaCross;
        EmaCrossBarsAgo = coin.EmaCrossBarsAgo;
        BollingerPctB   = coin.BollingerPctB;
        Ma50DistPerc    = coin.Ma50DistPerc;
        RsiDaily        = coin.Rsi;
        Adx             = coin.Adx;
        IsSqueeze       = coin.IsSqueeze;
        High52wPerc     = coin.High52wPerc;

        if (latestSignal is not null)
        {
            SignalId      = latestSignal.Id;
            Direction     = latestSignal.Direction.ToString();
            CombinedScore = latestSignal.CombinedScore;
            TaScore       = latestSignal.TaScore;
            Reasoning     = latestSignal.Reasoning;
        }
        else
        {
            SignalId      = null;
            Direction     = "–";
            CombinedScore = 0;
            TaScore       = 0;
            Reasoning     = string.Empty;
        }
    }
}
