namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Pure, testbare classificatie van orderboek-liquiditeit (F6) op basis van bid-ask spread
/// en minimale orderboekdiepte — dezelfde drempels als de 3%-Trading-gatekeeper, herbruikt
/// als on-demand liquiditeitslabel in Pattern Trading.
/// </summary>
public static class LiquidityClassifier
{
    public const double GoodSpreadPct  = 0.05;
    public const double BadSpreadPct   = 0.20;
    public const double GoodDepthUsdt  = 100_000;
    public const double BadDepthUsdt   = 20_000;

    public enum Level { Unknown, Thin, Medium, Good }

    public static Level Classify(double spreadPct, double minDepthUsdt)
    {
        if (minDepthUsdt <= 0) return Level.Unknown;
        if (spreadPct > BadSpreadPct || minDepthUsdt < BadDepthUsdt) return Level.Thin;
        if (spreadPct <= GoodSpreadPct && minDepthUsdt >= GoodDepthUsdt) return Level.Good;
        return Level.Medium;
    }

    public static string Label(Level level) => level switch
    {
        Level.Good   => "💧 Liquide",
        Level.Medium => "💧 Matig",
        Level.Thin   => "⚠ Dun",
        _            => "—",
    };

    public static bool IsIlliquid(Level level) => level == Level.Thin;
}
