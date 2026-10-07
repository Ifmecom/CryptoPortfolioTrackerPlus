using System.Text.RegularExpressions;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Leest koersen uit vrije AI-tekst, in Engelse én Nederlandse notatie ("$65,000.50", "65.000,50", "0.00012", "65k").
/// Een getal als "1.234" is dubbelzinnig (1,234 of 1234); met een referentiekoers kiest de parser
/// de lezing die het dichtst bij de huidige koers ligt.
/// </summary>
public static class AiNumberParser
{
    /// <summary>Getal met optioneel $/€ ervoor en k/m erachter. Groep 1 = getal, groep 2 = suffix.</summary>
    public const string NumberPattern = @"[$€]?\s?(\d{1,3}(?:[.,  ]\d{3})+(?:[.,]\d+)?|\d+(?:[.,]\d+)?)\s?([kKmM](?![a-zA-Z]))?";

    private static readonly Regex Number = new(NumberPattern, RegexOptions.Compiled);

    /// <summary>Alle mogelijke waarden van één getal-token (vaak één, bij dubbelzinnige notatie twee).</summary>
    public static IReadOnlyList<double> Candidates(string token, string? suffix = null)
    {
        var raw = token.Replace(" ", " ").Trim();
        var results = new List<double>();
        double mult = suffix?.ToLowerInvariant() switch { "k" => 1_000, "m" => 1_000_000, _ => 1 };

        void Add(string invariant)
        {
            if (double.TryParse(invariant, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            {
                v *= mult;
                if (!results.Contains(v)) results.Add(v);
            }
        }

        raw = raw.Replace(" ", string.Empty);
        int lastDot = raw.LastIndexOf('.'), lastComma = raw.LastIndexOf(',');

        if (lastDot >= 0 && lastComma >= 0)
        {
            // Beide aanwezig: het laatste teken is het decimaalteken.
            bool commaDecimal = lastComma > lastDot;
            Add(commaDecimal ? raw.Replace(".", "").Replace(',', '.') : raw.Replace(",", ""));
        }
        else if (lastDot >= 0 || lastComma >= 0)
        {
            char sep = lastDot >= 0 ? '.' : ',';
            int count = raw.Count(c => c == sep);
            string after = raw[(raw.LastIndexOf(sep) + 1)..];
            string before = raw[..raw.IndexOf(sep)];

            if (count > 1)
                Add(raw.Replace(sep.ToString(), ""));                         // 1.234.567 / 1,234,567
            else if (after.Length == 3 && before != "0" && before.Length <= 3)
            {
                Add(raw.Replace(sep.ToString(), ""));                         // duizendtal: 65,000 / 65.000
                Add(raw.Replace(sep, '.'));                                   // of decimaal: 1.234
            }
            else
                Add(raw.Replace(sep, '.'));                                   // decimaal: 0,5 / 2.75 / 0.00012
        }
        else
            Add(raw);

        return results;
    }

    /// <summary>Kies de lezing die het dichtst (logaritmisch) bij de referentiekoers ligt; zonder referentie de eerste.</summary>
    public static double? Pick(IReadOnlyList<double> candidates, double? reference)
    {
        if (candidates.Count == 0) return null;
        if (reference is not > 0 || candidates.Count == 1) return candidates[0];
        return candidates.Where(c => c > 0)
                         .OrderBy(c => Math.Abs(Math.Log(c / reference.Value)))
                         .Cast<double?>()
                         .FirstOrDefault() ?? candidates[0];
    }

    /// <summary>Lees het eerste getal uit de tekst.</summary>
    public static double? ParseFirst(string text, double? reference = null)
    {
        var m = Number.Match(text ?? string.Empty);
        return m.Success ? Pick(Candidates(m.Groups[1].Value, m.Groups[2].Value), reference) : null;
    }
}
