using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Pure helpers om een gedetecteerd patroon over scans heen te identificeren (P7, Stap 2).
///
/// De <b>fingerprint</b> is de grove groepsleutel <c>coin|timeframe|type</c>. Het fijne onderscheid
/// op ankerniveau (key level) gebeurt bewust NIET in de fingerprint maar via <see cref="LevelMatches"/>
/// op nabijheid — zo vermijden we bucket-randproblemen (twee scans waarvan het niveau net een
/// bucketgrens overschrijdt zouden anders als verschillende patronen tellen) én ondersteunen we
/// meerdere gelijktijdige patronen van hetzelfde type op verschillende niveaus.
/// </summary>
public static class PatternFingerprint
{
    /// <summary>Maximale relatieve afstand tussen twee ankerniveaus om als hetzelfde patroon te tellen.</summary>
    public const double LevelTolerancePct = 0.015;   // 1,5%

    /// <summary>Grove groepsleutel: <c>coin|timeframe|type</c>.</summary>
    public static string For(string coinApiId, string timeframe, PatternType type)
        => $"{coinApiId}|{timeframe}|{(int)type}";

    /// <summary>Grove groepsleutel voor een concrete detectie.</summary>
    public static string For(string coinApiId, PatternResult pattern)
        => For(coinApiId, pattern.Timeframe, pattern.Type);

    /// <summary>
    /// Twee ankerniveaus horen bij hetzelfde patroon als ze binnen <paramref name="tolPct"/> van
    /// elkaar liggen. Niveau-loze patronen (KeyLevel ~0, bv. symmetrische driehoek/consolidatie)
    /// matchen onderling — daar is de fingerprint zelf al onderscheidend genoeg.
    /// </summary>
    public static bool LevelMatches(double a, double b, double tolPct = LevelTolerancePct)
    {
        if (a <= 0 || b <= 0) return a <= 0 && b <= 0;   // beide niveau-loos → match; anders niet
        double bigger = a > b ? a : b;
        return System.Math.Abs(a - b) / bigger <= tolPct;
    }
}
