using System.Collections.Generic;

namespace CryptoPortfolioTracker.Models;

/// <summary>Eén open positie als input voor de risico-berekening.</summary>
public readonly record struct RiskPosition(string Symbol, double Entry, double StopLoss, double Qty, double CurrentPrice)
{
    /// <summary>Verlies in USDT als de stop-loss wordt geraakt. 0 als er geen SL is.</summary>
    public double RiskUsd => StopLoss > 0 && Entry > 0 && Qty > 0
        ? System.Math.Abs(Entry - StopLoss) * Qty : 0;

    /// <summary>Huidige marktwaarde van de positie (valt terug op entry zonder live koers).</summary>
    public double ExposureUsd => Qty * (CurrentPrice > 0 ? CurrentPrice : Entry);
}

public enum RiskSeverity { Ok, Warning, Critical }

public record RiskAlert(RiskSeverity Severity, string Message);

/// <summary>Portfolio-breed risico-overzicht met guardrail-status.</summary>
public class RiskDashboard
{
    public int    OpenPositions  { get; init; }
    public int    MaxOpenPositions { get; init; }

    public double TotalOpenRiskUsd     { get; init; }
    public double OpenRiskPct          { get; init; }   // % van kapitaal
    public double LargestPositionRiskPct { get; init; }

    public double ExposureUsd  { get; init; }
    public double ExposurePct  { get; init; }

    public double DayRealizedPnlUsd { get; init; }
    public double DailyLossLimitUsd { get; init; }
    public double DayLossPct        { get; init; }      // verlies als positief %

    public bool   KillSwitchActive { get; init; }
    public double Capital          { get; init; }
    /// <summary>Omschrijving van de gebruikte kapitaalbasis (paper vs echte portfolio).</summary>
    public string CapitalBasis     { get; init; } = string.Empty;

    public List<RiskAlert> Alerts { get; init; } = new();

    /// <summary>Hoogste severity over alle alerts ("OK" / "Let op" / "Grens bereikt").</summary>
    public RiskSeverity Status { get; init; } = RiskSeverity.Ok;
}
