using System;
using System.Collections.Generic;
using System.Linq;
using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

/// <summary>Eén fase-overgang van een patroon, voor UI/notificatie.</summary>
public sealed record PatternTransition(
    PatternStateRecord Record,
    PatternLifecycle From,
    PatternLifecycle To,
    string Reason);

/// <summary>Resultaat van een reconciliatie-pas: op te slaan records + de transitie-events.</summary>
public sealed class PatternReconcileResult
{
    /// <summary>Nieuwe en bijgewerkte records die de store moet opslaan.</summary>
    public List<PatternStateRecord> Upserts { get; } = new();

    /// <summary>Fase-overgangen die deze pas plaatsvonden (voor notificaties/UI).</summary>
    public List<PatternTransition> Transitions { get; } = new();
}

/// <summary>
/// Pure kern van het patroon-geheugen (P7, Stap 3). Verzoent de onthouden records met de verse,
/// stateless detecties van één coin en bepaalt de levenscyclus-overgangen — zónder EF of I/O, zodat
/// dit volledig unit-testbaar is.
///
/// Filosofie: de stateless detector is de waarheid over "is dit patroon nú geldig". Verschijnt een
/// onthouden patroon niet meer, dan classificeert de reconciler dat verdwijnen met de gegevens die
/// hij heeft (categorie + sleutelniveau + live koers), met een grace-marge tegen flikkeren.
/// </summary>
public static class PatternReconciler
{
    // ── Redenen (mensgericht, NL) ───────────────────────────────────────────────
    public const string ReasonDetected     = "Patroon voor het eerst gedetecteerd";
    public const string ReasonTentative    = "Live koers raakte het sleutelniveau";
    public const string ReasonConfirmed    = "Slotkoers brak het sleutelniveau (bevestigd)";
    public const string ReasonPlayedOut    = "Breakout uitgespeeld — patroon niet meer actief";
    public const string ReasonFailedBreak  = "Teruggevallen binnen het niveau na de breakout (valse uitbraak)";
    public const string ReasonFellBack     = "Teruggevallen vóór de breakout — niveau niet vastgehouden";
    public const string ReasonExpired      = "Structuur niet meer aanwezig";

    /// <summary>
    /// Verzoen <paramref name="existingActive"/> (de actieve records van deze coin) met de verse
    /// <paramref name="detections"/>. <paramref name="currentPrice"/> is de live coinkoers (gedeeld
    /// over timeframes). <paramref name="graceScans"/> = aantal gemiste scans dat een patroon mag
    /// ontbreken vóór het terminaal wordt (anti-flikker).
    /// </summary>
    public static PatternReconcileResult Reconcile(
        string coinApiId,
        string coinSymbol,
        IReadOnlyList<PatternStateRecord> existingActive,
        IReadOnlyList<PatternResult> detections,
        double currentPrice,
        DateTime now,
        int graceScans = 1)
    {
        var result  = new PatternReconcileResult();
        var matched  = new HashSet<PatternStateRecord>();

        // ── 1. Verse detecties: match op bestaand record of maak een nieuw aan ──
        foreach (var det in detections)
        {
            string fp = PatternFingerprint.For(coinApiId, det);
            double keyLevel = det.KeyLevel ?? 0;

            var rec = existingActive.FirstOrDefault(r =>
                !matched.Contains(r) &&
                r.Fingerprint == fp &&
                PatternFingerprint.LevelMatches(keyLevel, r.KeyLevel));

            var newLc = MapStatus(det.Status);

            if (rec is null)
            {
                // Nieuw patroon onthouden.
                rec = new PatternStateRecord
                {
                    Fingerprint     = fp,
                    CoinApiId       = coinApiId,
                    CoinSymbol      = coinSymbol,
                    Timeframe       = det.Timeframe,
                    Type            = det.Type,
                    Category        = det.Category,
                    KeyLevel        = keyLevel,
                    Strength        = det.Strength,
                    LastDescription = det.Description ?? string.Empty,
                    Lifecycle       = newLc,
                    IsActive        = true,
                    FirstSeenAt     = now,
                    LastSeenAt      = now,
                    LastScanAt      = now,
                    TimesSeen       = 1,
                    MissedScans     = 0,
                    LastTransitionReason = ReasonForEnter(newLc),
                    LastTransitionAt     = now,
                };
                result.Upserts.Add(rec);
                result.Transitions.Add(new PatternTransition(rec, PatternLifecycle.Forming, newLc, rec.LastTransitionReason));
                continue;
            }

            // Bestaand patroon bijwerken.
            matched.Add(rec);
            var oldLc = rec.Lifecycle;

            rec.KeyLevel        = keyLevel;
            rec.Strength        = det.Strength;
            rec.Category        = det.Category;
            rec.LastDescription = det.Description ?? rec.LastDescription;
            rec.LastSeenAt      = now;
            rec.LastScanAt      = now;
            rec.TimesSeen      += 1;
            rec.MissedScans     = 0;
            rec.IsActive        = true;

            if (newLc != oldLc)
            {
                rec.Lifecycle            = newLc;
                rec.LastTransitionReason = ReasonForEnter(newLc);
                rec.LastTransitionAt     = now;
                result.Transitions.Add(new PatternTransition(rec, oldLc, newLc, rec.LastTransitionReason));
            }

            result.Upserts.Add(rec);
        }

        // ── 2. Onthouden records die deze scan NIET terugkwamen ──
        foreach (var rec in existingActive)
        {
            if (matched.Contains(rec)) continue;

            rec.LastScanAt   = now;
            rec.MissedScans += 1;

            if (rec.MissedScans <= graceScans)
            {
                // Grace: één blip telt nog niet als verval (anti-flikker).
                result.Upserts.Add(rec);
                continue;
            }

            var (to, reason) = Finalize(rec, currentPrice);
            var from = rec.Lifecycle;

            rec.Lifecycle            = to;
            rec.IsActive             = false;
            rec.LastTransitionReason = reason;
            rec.LastTransitionAt     = now;

            result.Upserts.Add(rec);
            result.Transitions.Add(new PatternTransition(rec, from, to, reason));
        }

        return result;
    }

    /// <summary>Map het momentane drie-staten-model naar de levenscyclus-fase.</summary>
    public static PatternLifecycle MapStatus(PatternStatus status) => status switch
    {
        PatternStatus.Confirmed => PatternLifecycle.Confirmed,
        PatternStatus.Tentative => PatternLifecycle.Tentative,
        _                       => PatternLifecycle.Forming,
    };

    private static string ReasonForEnter(PatternLifecycle lc) => lc switch
    {
        PatternLifecycle.Confirmed => ReasonConfirmed,
        PatternLifecycle.Tentative => ReasonTentative,
        _                          => ReasonDetected,
    };

    /// <summary>
    /// Bepaalt de terminale fase + reden voor een verdwenen patroon. Het sleutelniveau is het
    /// breakout-niveau, niet het invalidatie-niveau; daarom claimen we alleen "geïnvalideerd"
    /// als de prijs aantoonbaar is teruggevallen ná/rond de breakout. Anders: vervallen.
    /// </summary>
    private static (PatternLifecycle to, string reason) Finalize(PatternStateRecord rec, double currentPrice)
    {
        bool? onSide = OnBreakoutSide(rec.Category, rec.KeyLevel, currentPrice);

        if (rec.Lifecycle == PatternLifecycle.Confirmed)
        {
            if (onSide == true)  return (PatternLifecycle.PlayedOut,   ReasonPlayedOut);
            if (onSide == false) return (PatternLifecycle.Invalidated, ReasonFailedBreak);
            return (PatternLifecycle.Expired, ReasonExpired);
        }

        if (rec.Lifecycle == PatternLifecycle.Tentative && onSide == false)
            return (PatternLifecycle.Invalidated, ReasonFellBack);

        return (PatternLifecycle.Expired, ReasonExpired);
    }

    /// <summary>
    /// Staat de koers aan de breakout-kant van het sleutelniveau? true=ja, false=nee, null=onbekend
    /// (niveau-loos of neutraal patroon).
    /// </summary>
    private static bool? OnBreakoutSide(PatternCategory category, double keyLevel, double price)
    {
        if (keyLevel <= 0 || price <= 0) return null;
        return category switch
        {
            PatternCategory.Bullish => price >= keyLevel,
            PatternCategory.Bearish => price <= keyLevel,
            _                       => null,
        };
    }
}
