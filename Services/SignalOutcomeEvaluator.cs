using System;
using System.Collections.Generic;
using System.Linq;
using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

/// <summary>Bron-labels voor <see cref="SignalOutcome.Source"/>.</summary>
public static class SignalOutcomeSources
{
    public const string Signal  = "Signal";
    public const string Pattern = "Pattern";

    public static string DisplayName(string source) => source switch
    {
        Signal  => "Signalen",
        Pattern => "Pattern Trading",
        _       => source,
    };
}

/// <summary>Resultaat van één meting — <c>null</c>-velden zijn (nog) niet te bepalen.</summary>
public sealed record OutcomeMeasurement(
    double  EntryPrice,
    double? Return1d,
    double? Return3d,
    double? Return7d,
    double? Return14d,
    double? MaxFavorablePct,
    double? MaxAdversePct,
    bool    IsComplete);

/// <summary>
/// Pure, testbare kern van de signal-outcome-tracker (v1.46). Meet wat de koers deed ná een signaal,
/// uitsluitend op basis van gesloten daily candles (UTC):
/// <list type="bullet">
///   <item>Instap = slotkoers van de candle waarin het signaal viel (geen lookahead: dat moment ligt ná het signaal).</item>
///   <item>Horizon h = slotkoers van de candle h dagen later. Ontbreekt die exacte candle (datagat), dan telt
///         de laatste gesloten candle vóór die dag, mits die ná de instapdag ligt.</item>
///   <item>Rendementen zijn richting-gecorrigeerd: positief = het signaal had gelijk.</item>
///   <item>Max. mee-/tegenbeweging (MFE/MAE) op high/low over de dagen 1 t/m 14.</item>
/// </list>
/// </summary>
public static class SignalOutcomeEvaluator
{
    /// <summary>Gemeten horizons in dagen.</summary>
    public static readonly int[] Horizons = { 1, 3, 7, 14 };

    public const int MaxHorizonDays = 14;

    /// <summary>Na zoveel mislukte pogingen (geen koersdata) wordt een uitkomst niet meer opgehaald.</summary>
    public const int MaxFailedAttempts = 5;

    /// <summary>
    /// Meet één signaal. Retourneert <c>null</c> als de instap-candle (nog) niet beschikbaar of niet
    /// gesloten is — dan valt er nog niets te meten.
    /// </summary>
    public static OutcomeMeasurement? Measure(
        string direction,
        DateTime signalDayUtc,
        IReadOnlyList<OhlcvBar> dailyBars,
        DateTime nowUtc)
    {
        int sign = DirectionSign(direction);
        if (sign == 0 || dailyBars is null || dailyBars.Count == 0) return null;

        var day = signalDayUtc.Date;

        // Alleen gesloten candles (open-tijd + 1 dag ≤ nu), gesorteerd, één per dag.
        var closed = dailyBars
            .Where(b => b.Date.AddDays(1) <= nowUtc && b.Close > 0)
            .GroupBy(b => b.Date.Date)
            .Select(g => g.First())
            .OrderBy(b => b.Date)
            .ToList();

        var entryBar = closed.FirstOrDefault(b => b.Date.Date == day);
        if (entryBar is null) return null;

        double entry = entryBar.Close;

        double? ReturnAt(int h)
        {
            var target = day.AddDays(h);
            // De horizon-dag moet zelf al voorbij zijn (candle gesloten), anders is het te vroeg.
            if (target.AddDays(1) > nowUtc) return null;
            var bar = closed.LastOrDefault(b => b.Date.Date > day && b.Date.Date <= target);
            if (bar is null) return null;
            return Math.Round(sign * (bar.Close / entry - 1.0) * 100.0, 4);
        }

        double? r1  = ReturnAt(1);
        double? r3  = ReturnAt(3);
        double? r7  = ReturnAt(7);
        double? r14 = ReturnAt(14);

        // MFE/MAE over de (gesloten) dagen na de instap, maximaal 14.
        var window = closed
            .Where(b => b.Date.Date > day && b.Date.Date <= day.AddDays(MaxHorizonDays))
            .ToList();

        double? mfe = null, mae = null;
        if (window.Count > 0)
        {
            double maxHigh = window.Max(b => b.High > 0 ? b.High : b.Close);
            double minLow  = window.Min(b => b.Low  > 0 ? b.Low  : b.Close);

            double up   = (maxHigh / entry - 1.0) * 100.0;   // grootste stijging
            double down = (1.0 - minLow / entry) * 100.0;    // grootste daling

            mfe = Math.Round(Math.Max(0, sign > 0 ? up : down), 4);
            mae = Math.Round(Math.Max(0, sign > 0 ? down : up), 4);
        }

        bool complete = r1.HasValue && r3.HasValue && r7.HasValue && r14.HasValue;
        return new OutcomeMeasurement(entry, r1, r3, r7, r14, mfe, mae, complete);
    }

    /// <summary>Past een meting toe op een uitkomst-rij.</summary>
    public static void Apply(SignalOutcome outcome, OutcomeMeasurement m, DateTime nowUtc)
    {
        outcome.EntryPrice      = m.EntryPrice;
        outcome.Return1d        = m.Return1d;
        outcome.Return3d        = m.Return3d;
        outcome.Return7d        = m.Return7d;
        outcome.Return14d       = m.Return14d;
        outcome.MaxFavorablePct = m.MaxFavorablePct;
        outcome.MaxAdversePct   = m.MaxAdversePct;
        outcome.IsComplete      = m.IsComplete;
        outcome.EvaluatedAt     = nowUtc;
    }

    /// <summary>+1 voor Long, -1 voor Short, 0 voor alles anders (Flat/Neutraal/onbekend).</summary>
    public static int DirectionSign(string? direction) => direction switch
    {
        "Long"  => 1,
        "Short" => -1,
        _       => 0,
    };

    /// <summary>
    /// Kan deze uitkomst nu (opnieuw) gemeten worden? Alleen onvolledige rijen waarvan de signaaldag
    /// gesloten is en die niet te vaak zijn mislukt.
    /// </summary>
    public static bool IsDue(SignalOutcome o, DateTime nowUtc)
        => !o.IsComplete
           && o.FailedAttempts < MaxFailedAttempts
           && o.SignalDay.Date.AddDays(1) <= nowUtc;

    // ── Selectie van signalen ────────────────────────────────────────────────

    /// <summary>
    /// Kiest per coin per UTC-dag het eerste Long/Short-signaal. De SignalEngine kan meerdere keren per
    /// dag draaien; zonder deze ontdubbeling zouden dagen met veel klikken zwaarder meetellen.
    /// Flat-signalen worden niet gevolgd.
    /// </summary>
    public static List<Signal> FirstPerCoinPerDay(IEnumerable<Signal> signals)
        => signals
            .Where(s => s.Direction is SignalDirection.Long or SignalDirection.Short)
            .GroupBy(s => (s.CoinId, Day: s.CreatedAt.Date))
            .Select(g => g.OrderBy(s => s.CreatedAt).ThenBy(s => s.Id).First())
            .OrderBy(s => s.CreatedAt)
            .ToList();

    /// <summary>
    /// Leidt het BTC-regime af uit de opgeslagen regime-multiplier van een signaal. De SignalEngine
    /// kiest de multiplier op (richting, regime) en de richting blijft na de multiplier gelijk (hij
    /// schaalt alleen de afstand tot 50), dus de combinatie is eenduidig terug te rekenen.
    /// </summary>
    public static string RegimeFromMultiplier(SignalDirection direction, double multiplier)
    {
        static bool Eq(double a, double b) => Math.Abs(a - b) < 0.001;
        return direction switch
        {
            SignalDirection.Long when Eq(multiplier, 1.0)  => nameof(MarketRegime.RiskOn),
            SignalDirection.Long when Eq(multiplier, 0.7)  => nameof(MarketRegime.Neutral),
            SignalDirection.Long when Eq(multiplier, 0.3)  => nameof(MarketRegime.RiskOff),
            SignalDirection.Short when Eq(multiplier, 0.7) => nameof(MarketRegime.RiskOn),
            SignalDirection.Short when Eq(multiplier, 1.0) => nameof(MarketRegime.Neutral),
            SignalDirection.Short when Eq(multiplier, 1.3) => nameof(MarketRegime.RiskOff),
            _ => string.Empty,
        };
    }

    /// <summary>Bouwt een (nog ongemeten) uitkomst-rij voor een SignalEngine-signaal.</summary>
    public static SignalOutcome FromSignal(Signal s, string coinApiId, string coinSymbol) => new()
    {
        Source       = SignalOutcomeSources.Signal,
        SourceRefId  = s.Id,
        CoinApiId    = coinApiId,
        CoinSymbol   = coinSymbol,
        Direction    = s.Direction.ToString(),
        Score        = s.CombinedScore,
        MarketRegime = RegimeFromMultiplier(s.Direction, s.MarketRegimeMultiplier),
        SignalAt     = s.CreatedAt,
        SignalDay    = s.CreatedAt.Date,
    };

    /// <summary>Minimale TradabilityScore om een Pattern-scan te volgen — gelijk aan de setup-drempel.</summary>
    public const int MinPatternScore = 40;

    /// <summary>
    /// Bouwt een uitkomst-rij voor een Pattern Trading-resultaat, of <c>null</c> als er geen
    /// handelbare richting is (Neutraal, score onder de setup-drempel of geen coin-id).
    /// </summary>
    public static SignalOutcome? FromPatternScan(
        string coinApiId, string coinSymbol, string primaryDirection, int tradabilityScore,
        string marketRegime, DateTime scannedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(coinApiId)) return null;
        if (DirectionSign(primaryDirection) == 0) return null;
        if (tradabilityScore < MinPatternScore) return null;

        return new SignalOutcome
        {
            Source       = SignalOutcomeSources.Pattern,
            CoinApiId    = coinApiId,
            CoinSymbol   = coinSymbol,
            Direction    = primaryDirection,
            Score        = tradabilityScore,
            MarketRegime = marketRegime ?? string.Empty,
            SignalAt     = scannedAtUtc,
            SignalDay    = scannedAtUtc.Date,
        };
    }
}
