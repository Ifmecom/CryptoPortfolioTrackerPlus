using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace CryptoPortfolioTracker.Helpers;

public static class AnalysisHelpers
{
    public static SolidColorBrush SignedColor(double value)
        => value > 0 ? Green() : value < 0 ? Red() : Neutral();

    public static SolidColorBrush StochRsiColor(double value)
        => value > 0 && value < 20 ? Green()
         : value >= 80             ? Red()
         : Neutral();

    public static SolidColorBrush RegimeColor(string regime)
        => regime == "RiskOn"  ? Green()
         : regime == "RiskOff" ? Red()
         : Neutral();

    public static SolidColorBrush DirectionColor(string direction)
        => direction == "Long"  ? Green()
         : direction == "Short" ? Red()
         : Neutral();

    public static SolidColorBrush ScoreColor(double score)
        => score >= 60 ? Green()
         : score <= 40 ? Red()
         : Neutral();

    /// <summary>Buy/Long = green, Sell/Short = red, anything else = neutral.</summary>
    public static SolidColorBrush SideColor(string side)
        => side is "Buy"  or "Long"  ? Green()
         : side is "Sell" or "Short" ? Red()
         : Neutral();

    public static SolidColorBrush EmaCrossColor(string cross)
        => cross == "Bullish" ? Green() : cross == "Bearish" ? Red() : Neutral();

    public static SolidColorBrush PctBColor(double pctB)
        => pctB < 20 ? Green() : pctB > 80 ? Red() : Neutral();

    public static SolidColorBrush Ma50Color(double distPerc)
        => distPerc > 0 ? Green() : distPerc < 0 ? Red() : Neutral();

    public static SolidColorBrush AdxColor(double adx)
        => adx >= 25 ? new SolidColorBrush(Color.FromArgb(255, 255, 165, 0))
         : Neutral();

    public static SolidColorBrush SqueezeColor(bool isSqueeze)
        => isSqueeze ? new SolidColorBrush(Color.FromArgb(255, 60, 179, 113)) : Neutral();

    public static SolidColorBrush RsiColor(double rsi)
        => rsi < 30 ? Green() : rsi > 70 ? Red() : Neutral();

    private static SolidColorBrush Green()   => new(Color.FromArgb(255, 60,  179, 113));
    private static SolidColorBrush Red()     => new(Color.FromArgb(255, 205, 92,  92));
    private static SolidColorBrush Neutral() => new(Color.FromArgb(255, 140, 140, 140));
}
