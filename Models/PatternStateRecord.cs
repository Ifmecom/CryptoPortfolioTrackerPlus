using CryptoPortfolioTracker.Enums;

namespace CryptoPortfolioTracker.Models;

/// <summary>
/// Persistent geheugen van één gedetecteerd patroon over scans heen (P7 — continue invalidatie).
/// De pure detector (<see cref="Services.IPatternDetectionService"/>) blijft stateless; deze tabel
/// laat een eerder gezien patroon expliciet <see cref="PatternLifecycle.Confirmed"/> /
/// <see cref="PatternLifecycle.Invalidated"/> / <see cref="PatternLifecycle.Expired"/> worden,
/// inclusief reden en tijdstip, i.p.v. stil te verdwijnen bij de volgende scan.
///
/// Coin-identiteit is gedenormaliseerd (geen FK), zoals bij <see cref="WatchedSetup"/>: een gescande
/// coin staat niet noodzakelijk in de portfolio.
/// </summary>
public class PatternStateRecord
{
    public int Id { get; set; }

    // ── Identiteit (fingerprint = stabiele sleutel om over scans heen te matchen) ──

    /// <summary>
    /// Stabiele sleutel `coin|tf|type|ankerbucket` (zie de reconciler, Stap 2). Patronen met dezelfde
    /// fingerprint worden als hetzelfde patroon over scans heen behandeld.
    /// </summary>
    public string Fingerprint { get; set; } = string.Empty;

    public string      CoinApiId  { get; set; } = string.Empty;
    public string      CoinSymbol { get; set; } = string.Empty;
    public string      Timeframe  { get; set; } = string.Empty;   // "1D" / "4H" / "1H" / "15M"
    public PatternType Type       { get; set; }
    public PatternCategory Category { get; set; }

    /// <summary>Het ankerniveau (weerstand/steun/neklijn) waarop de fingerprint is gebucket.</summary>
    public double KeyLevel { get; set; }

    /// <summary>Laatst bekende sterkte (0–100), voor weergave.</summary>
    public int Strength { get; set; }

    /// <summary>Laatst bekende patroonbeschrijving — context voor UI/notificatie.</summary>
    public string LastDescription { get; set; } = string.Empty;

    // ── Levenscyclus ──────────────────────────────────────────────────────────

    public PatternLifecycle Lifecycle { get; set; } = PatternLifecycle.Forming;

    /// <summary>True zolang het patroon nog "leeft" (niet Invalidated/Expired/PlayedOut).</summary>
    public bool IsActive { get; set; } = true;

    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt  { get; set; } = DateTime.UtcNow;

    /// <summary>Tijdstip van de laatste reconciliatie-pas (ook als het patroon deze scan niet werd gezien).</summary>
    public DateTime LastScanAt  { get; set; } = DateTime.UtcNow;

    /// <summary>Aantal opeenvolgende scans waarin het patroon werd gedetecteerd (confidence).</summary>
    public int TimesSeen { get; set; }

    /// <summary>Aantal opeenvolgende scans waarin het patroon NIET werd gedetecteerd (grace/hysterese).</summary>
    public int MissedScans { get; set; }

    // ── Laatste transitie ───────────────────────────────────────────────────────

    /// <summary>Mensgerichte reden van de laatste fase-overgang (bv. "Slotkoers sloot onder onderwand").</summary>
    public string LastTransitionReason { get; set; } = string.Empty;

    public DateTime? LastTransitionAt { get; set; }

    /// <summary>De fase waarvoor al een notificatie is verstuurd — voorkomt dubbele alerts (Stap 6).</summary>
    public PatternLifecycle? NotifiedLifecycle { get; set; }
}
