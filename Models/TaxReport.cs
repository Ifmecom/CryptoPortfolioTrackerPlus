namespace CryptoPortfolioTracker.Models;

/// <summary>
/// Result of a tax calculation.
/// </summary>
public class TaxReport
{
    public string  CountryName    { get; set; } = string.Empty;
    public int     Year           { get; set; }

    /// <summary>Total tax due in EUR.</summary>
    public decimal TaxDue         { get; set; }

    /// <summary>Effective tax rate as a percentage of net assets (e.g. 2.17 means 2.17 %).</summary>
    public decimal EffectiveRate  { get; set; }

    public IReadOnlyList<TaxReportLine> Breakdown { get; set; } = Array.Empty<TaxReportLine>();
}

/// <summary>
/// A single line in the tax breakdown table.
/// </summary>
public class TaxReportLine
{
    public string  Label          { get; init; } = string.Empty;
    public decimal Amount         { get; init; }

    /// <summary>When true the amount is displayed with a minus prefix (e.g. a deduction).</summary>
    public bool    IsSubtraction  { get; init; }

    /// <summary>When true the row is rendered in bold (a sub-total or final total).</summary>
    public bool    IsTotalLine    { get; init; }

    // ── Computed display helpers (used by x:Bind in XAML) ───────────────────

    /// <summary>Inverse of IsTotalLine — used for conditional Visibility in DataTemplate.</summary>
    public bool    IsRegularLine  => !IsTotalLine;

    /// <summary>Formatted amount string, e.g. "€ 57.000" or "− € 50.000".</summary>
    public string  AmountDisplay  => $"{(IsSubtraction ? "− " : "")}€ {Amount:N0}";
}
