using CryptoPortfolioTracker.Converters;
using CryptoPortfolioTracker.Helpers;
using CryptoPortfolioTracker.Models;
using Microsoft.UI.Xaml.Media;

namespace CryptoPortfolioTracker.ViewModels;

/// <summary>Lightweight row model for the Dashboard signals widget.</summary>
public class DashboardSignalRow
{
    public string Name      { get; }
    public string Symbol    { get; }
    public string ImageUri  { get; }
    public string Direction { get; }
    public double Score     { get; }

    public string  ScoreDisplay     => $"{Score:F0}";
    public string  DirectionDisplay => Direction;

    public SolidColorBrush DirectionBrush => AnalysisHelpers.DirectionColor(Direction);
    public SolidColorBrush ScoreBrush     => AnalysisHelpers.ScoreColor(Score);

    public Microsoft.UI.Xaml.Media.ImageSource? Image =>
        Functions.StringToImageSource(ImageUri);

    public DashboardSignalRow(Signal signal, Coin coin)
    {
        Name      = coin.Name;
        Symbol    = coin.Symbol?.ToUpperInvariant() ?? "";
        ImageUri  = coin.ImageUri;
        Direction = signal.Direction.ToString();
        Score     = signal.CombinedScore;
    }
}
