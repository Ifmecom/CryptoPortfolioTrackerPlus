using CryptoPortfolioTracker.Enums;

namespace CryptoPortfolioTracker.Models;

/// <summary>
/// Gedeelde, app-brede marktcontext: BTC-regime + Fear &amp; Greed + eerstvolgende macro-event.
/// Aggregeert bestaande bronnen (MarketRegime, FearGreed, MacroEvent) voor de context-balk
/// die in meerdere trading-tabs wordt getoond.
/// </summary>
public sealed record MarketContext(
    MarketRegime Regime,
    string  RegimeSummary,
    int     FearGreedValue,
    string  FearGreedClassification,
    bool    HasFearGreed,
    string  NextEventType,
    int     NextEventDays,
    string  NextEventLocalTime,   // lokale kloktijd van het event, bv. "20:00"
    bool    HasNextEvent)
{
    public string RegimeText => Regime switch
    {
        MarketRegime.RiskOn  => "🟢 Risk-On",
        MarketRegime.RiskOff => "🔴 Risk-Off",
        _                    => "🟡 Neutraal",
    };

    public string FearGreedText => HasFearGreed
        ? $"😨 F&G {FearGreedValue} · {FearGreedClassification}"
        : "F&G —";

    public string NextEventText
    {
        get
        {
            if (!HasNextEvent) return string.Empty;
            string when = NextEventDays <= 0 ? "vandaag"
                        : NextEventDays == 1 ? "morgen"
                        : $"over {NextEventDays}d";
            string time = string.IsNullOrEmpty(NextEventLocalTime) ? string.Empty : $" om {NextEventLocalTime}";
            return $"⚠ {NextEventType} {when}{time}";
        }
    }
}
