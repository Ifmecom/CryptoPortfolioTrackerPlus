using System;

namespace CryptoPortfolioTracker.Models;

/// <summary>
/// Gemeten uitkomst van één signaal (signal-outcome-tracker, v1.46). Eén rij per bron + coin + dag:
/// wat deed de koers 1 / 3 / 7 / 14 dagen na het signaal, gemeten in de richting van het signaal?
///
/// Bronnen:
/// <list type="bullet">
///   <item><c>"Signal"</c> — een <see cref="Signal"/> uit de SignalEngine (Score = CombinedScore 0–100).</item>
///   <item><c>"Pattern"</c> — een Pattern Trading-scan (Score = TradabilityScore 0–100, richting = PrimaryDirection).</item>
/// </list>
///
/// Instap = slotkoers van de daily candle (UTC) waarin het signaal viel — dus altijd ná het signaal,
/// zonder lookahead. Rendementen zijn richting-gecorrigeerd: positief = het signaal had gelijk
/// (Long: koers steeg, Short: koers daalde). Coin-identiteit is gedenormaliseerd (geen FK), zoals bij
/// <see cref="WatchedSetup"/> en <see cref="PatternStateRecord"/>.
/// </summary>
public class SignalOutcome
{
    public int Id { get; set; }

    // ── Herkomst ────────────────────────────────────────────────────────────
    /// <summary>"Signal" of "Pattern" — zie <see cref="Services.SignalOutcomeSources"/>.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Id van de bron-<see cref="Signal"/> (alleen bij Source = "Signal").</summary>
    public int? SourceRefId { get; set; }

    public string CoinApiId  { get; set; } = string.Empty;
    public string CoinSymbol { get; set; } = string.Empty;

    /// <summary>"Long" of "Short" (Flat/Neutraal wordt niet gevolgd).</summary>
    public string Direction { get; set; } = string.Empty;

    /// <summary>Ruwe score op het moment van het signaal (0–100).</summary>
    public double Score { get; set; }

    /// <summary>BTC-marktregime op het moment van het signaal ("RiskOn"/"Neutral"/"RiskOff"), leeg als onbekend.</summary>
    public string MarketRegime { get; set; } = string.Empty;

    /// <summary>Exact tijdstip van het signaal (UTC).</summary>
    public DateTime SignalAt { get; set; }

    /// <summary>UTC-datum van het signaal (00:00) — sleutel voor één meting per coin per dag.</summary>
    public DateTime SignalDay { get; set; }

    // ── Meting ──────────────────────────────────────────────────────────────
    /// <summary>Slotkoers van de signaaldag (instap). 0 zolang nog niet gemeten.</summary>
    public double EntryPrice { get; set; }

    /// <summary>Richting-gecorrigeerd rendement in % na 1 / 3 / 7 / 14 dagen (null = nog niet bekend).</summary>
    public double? Return1d  { get; set; }
    public double? Return3d  { get; set; }
    public double? Return7d  { get; set; }
    public double? Return14d { get; set; }

    /// <summary>Grootste beweging mét het signaal mee binnen 14 dagen, in % (op basis van high/low).</summary>
    public double? MaxFavorablePct { get; set; }

    /// <summary>Grootste beweging tégen het signaal in binnen 14 dagen, in % (positief getal).</summary>
    public double? MaxAdversePct { get; set; }

    /// <summary>True zodra alle horizons (t/m 14 dagen) gemeten zijn — daarna wordt de rij niet meer opgehaald.</summary>
    public bool IsComplete { get; set; }

    /// <summary>Aantal mislukte meetpogingen (geen koersdata) — na te veel pogingen wordt de rij overgeslagen.</summary>
    public int FailedAttempts { get; set; }

    public DateTime? EvaluatedAt { get; set; }

    /// <summary>Rendement voor een horizon (1, 3, 7 of 14 dagen); null als onbekend of geen geldige horizon.</summary>
    public double? ReturnFor(int horizonDays) => horizonDays switch
    {
        1  => Return1d,
        3  => Return3d,
        7  => Return7d,
        14 => Return14d,
        _  => null,
    };
}
