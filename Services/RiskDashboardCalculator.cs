using System;
using System.Collections.Generic;
using System.Linq;
using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Pure, testbare berekening van het portfolio-brede risico-overzicht + guardrail-status,
/// uit de open posities, de gerealiseerde dag-P&amp;L en de risk-instellingen.
/// </summary>
public static class RiskDashboardCalculator
{
    /// <summary>Totaal open risico boven dit % van kapitaal geldt als "hoge hitte".</summary>
    public const double TotalHeatWarnPct = 15.0;

    public static RiskDashboard Build(
        IReadOnlyList<RiskPosition> open,
        double dayRealizedPnl,
        double capital,
        int    maxOpenPositions,
        double dailyLossLimitPct,
        double maxRiskPctPerTrade,
        bool   killSwitch,
        string capitalBasis = "")
    {
        if (capital <= 0) capital = 1; // guard tegen deling door 0

        double totalRisk   = open.Sum(p => p.RiskUsd);
        double exposure    = open.Sum(p => p.ExposureUsd);
        double largestRisk  = open.Count > 0 ? open.Max(p => p.RiskUsd) : 0;
        double dailyLimitUsd = capital * dailyLossLimitPct / 100.0;
        double dayLossPct    = dayRealizedPnl < 0 ? -dayRealizedPnl / capital * 100.0 : 0;

        var alerts = new List<RiskAlert>();

        if (killSwitch)
            alerts.Add(new RiskAlert(RiskSeverity.Critical,
                "Kill-switch actief — auto-trading staat uit."));

        if (maxOpenPositions > 0 && open.Count >= maxOpenPositions)
            alerts.Add(new RiskAlert(RiskSeverity.Warning,
                $"Maximaal aantal open posities bereikt ({open.Count}/{maxOpenPositions})."));

        if (dailyLossLimitPct > 0 && dayRealizedPnl <= -dailyLimitUsd && dailyLimitUsd > 0)
            alerts.Add(new RiskAlert(RiskSeverity.Critical,
                $"Dagelijkse verlieslimiet bereikt ({dayLossPct:0.0}% ≥ {dailyLossLimitPct:0.#}%)."));

        if (maxRiskPctPerTrade > 0)
        {
            var heavy = open.Where(p => p.RiskUsd / capital * 100.0 > maxRiskPctPerTrade + 0.05).ToList();
            foreach (var p in heavy)
                alerts.Add(new RiskAlert(RiskSeverity.Warning,
                    $"{p.Symbol}: positie riskeert {p.RiskUsd / capital * 100.0:0.0}% (> limiet {maxRiskPctPerTrade:0.#}%)."));
        }

        double totalRiskPct = totalRisk / capital * 100.0;
        if (totalRiskPct > TotalHeatWarnPct)
            alerts.Add(new RiskAlert(RiskSeverity.Warning,
                $"Hoog totaal open risico ({totalRiskPct:0.0}% van kapitaal)."));

        var status = alerts.Count == 0 ? RiskSeverity.Ok
                   : alerts.Any(a => a.Severity == RiskSeverity.Critical) ? RiskSeverity.Critical
                   : RiskSeverity.Warning;

        return new RiskDashboard
        {
            OpenPositions          = open.Count,
            MaxOpenPositions       = maxOpenPositions,
            TotalOpenRiskUsd       = totalRisk,
            OpenRiskPct            = totalRiskPct,
            LargestPositionRiskPct = largestRisk / capital * 100.0,
            ExposureUsd            = exposure,
            ExposurePct            = exposure / capital * 100.0,
            DayRealizedPnlUsd      = dayRealizedPnl,
            DailyLossLimitUsd      = dailyLimitUsd,
            DayLossPct             = dayLossPct,
            KillSwitchActive       = killSwitch,
            Capital                = capital,
            CapitalBasis           = capitalBasis,
            Alerts                 = alerts,
            Status                 = status,
        };
    }
}
