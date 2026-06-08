using System;

namespace CryptoPortfolioTracker.Converters;

/// <summary>
/// Partial class — pure string formatters zonder WinUI-afhankelijkheden.
/// Dit bestand wordt ook gecompileerd in het testproject
/// (CryptoPortfolioTracker.Tests) zodat de formatters unit-testbaar zijn.
/// </summary>
public partial class Functions
{
    /// <summary>
    /// Formatteer een koers als leesbare string met passend aantal decimalen.
    /// </summary>
    public static string FormatCryptoPrice(double price) => price switch
    {
        <= 0     => "—",
        >= 1000  => $"${price:#,0.00}",
        >= 1     => $"${price:F4}",
        >= 0.01  => $"${price:F6}",
        _        => $"${price:F8}",
    };

    /// <summary>Risk/reward ratio als "1 : x.x"-string.</summary>
    public static string FormatRR(double rr)
        => rr > 0 ? $"1 : {rr:F1}" : "—";

    /// <summary>
    /// Formatteer een P&amp;L percentage met expliciet +/- teken.
    /// Retourneert een lege string als de waarde null is.
    /// </summary>
    public static string FormatPnlPct(double? pnlPct)
        => pnlPct.HasValue ? $"{pnlPct.Value:+0.0;-0.0}%" : string.Empty;

    /// <summary>
    /// Formatteer een verstreken tijdsduur als leesbare string.
    /// Minuten (< 1 uur), uren (< 1 dag), of dagen.
    /// </summary>
    public static string FormatAge(DateTime addedAt)
    {
        var span = DateTime.UtcNow - addedAt;
        if (span.TotalDays  >= 1) return $"{(int)span.TotalDays}d geleden";
        if (span.TotalHours >= 1) return $"{(int)span.TotalHours}u geleden";
        return $"{(int)span.TotalMinutes}m geleden";
    }

    /// <summary>
    /// Formatteer een candle-timestamp als lokale tijd.
    /// Vandaag → "HH:mm" | dit jaar → "d MMM HH:mm" | ouder → "d MMM yy HH:mm".
    /// Geeft een lege string terug als dt null is.
    /// </summary>
    public static string FormatCandleTime(DateTime? dt)
    {
        if (!dt.HasValue) return string.Empty;
        var local = dt.Value.ToLocalTime();
        var now   = DateTime.Now;
        if (local.Date == now.Date)  return local.ToString("HH:mm");
        if (local.Year == now.Year)  return local.ToString("d MMM HH:mm");
        return local.ToString("d MMM yy HH:mm");
    }

    /// <summary>Compact USD-bedrag: $1.2B / $345.6M / $12.3K / $123. "—" bij 0.</summary>
    public static string FormatUsdCompact(double value)
    {
        if (value <= 0) return "—";
        if (value >= 1_000_000_000_000) return $"${value / 1_000_000_000_000:0.##}T";
        if (value >= 1_000_000_000)     return $"${value / 1_000_000_000:0.##}B";
        if (value >= 1_000_000)         return $"${value / 1_000_000:0.##}M";
        if (value >= 1_000)             return $"${value / 1_000:0.##}K";
        return $"${value:0.##}";
    }

    /// <summary>Compact aantal (zonder $): 1.23B / 456.7M / 12.3K. "—" bij 0.</summary>
    public static string FormatSupply(double value)
    {
        if (value <= 0) return "—";
        if (value >= 1_000_000_000_000) return $"{value / 1_000_000_000_000:0.##}T";
        if (value >= 1_000_000_000)     return $"{value / 1_000_000_000:0.##}B";
        if (value >= 1_000_000)         return $"{value / 1_000_000:0.##}M";
        if (value >= 1_000)             return $"{value / 1_000:0.##}K";
        return $"{value:0.##}";
    }

    /// <summary>Percentage met expliciet teken: "+12.3%". "—" als waarde 0 is.</summary>
    public static string FormatPercentSigned(double pct)
        => pct == 0 ? "—" : $"{pct:+0.0;-0.0}%";

    /// <summary>Ratio met één decimaal en suffix, bijv. "2.4×". "—" bij 0.</summary>
    public static string FormatRatioX(double ratio)
        => ratio <= 0 ? "—" : $"{ratio:0.0}×";
}
