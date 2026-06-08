namespace CryptoPortfolioTracker.Models;

/// <summary>
/// Chart overlay data produced alongside a <see cref="PatternResult"/>.
/// Consumed by <see cref="Views.CoinChartWindow"/> to draw key points and lines
/// on the TradingView Lightweight Charts candlestick chart.
/// </summary>
public class PatternAnnotation
{
    /// <summary>Circular markers placed above or below specific bars.</summary>
    public List<PatternPoint>     Markers    { get; set; } = new();

    /// <summary>Horizontal lines (necklines, key price levels).</summary>
    public List<PatternHLine>     HLines     { get; set; } = new();

    /// <summary>Diagonal trendlines connecting two price–time coordinates.</summary>
    public List<PatternTrendline> Trendlines { get; set; } = new();

    public bool IsEmpty => !Markers.Any() && !HLines.Any() && !Trendlines.Any();
}

public class PatternPoint
{
    public DateTime Time     { get; set; }
    public double   Price    { get; set; }
    public string   Label    { get; set; } = "";
    /// <summary>When true the marker sits above the bar; false = below.</summary>
    public bool     AboveBar { get; set; } = true;
}

public class PatternHLine
{
    public double Price { get; set; }
    public string Color { get; set; } = "#FFD700";
    public string Title { get; set; } = "";
}

public class PatternTrendline
{
    public DateTime StartTime  { get; set; }
    public double   StartPrice { get; set; }
    public DateTime EndTime    { get; set; }
    public double   EndPrice   { get; set; }
    public string   Color      { get; set; } = "#FFD700";
}
