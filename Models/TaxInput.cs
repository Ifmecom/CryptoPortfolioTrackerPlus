namespace CryptoPortfolioTracker.Models;

/// <summary>
/// Country-agnostic input for a tax calculation.
/// Each ITaxCalculator uses the fields it needs and ignores the rest.
/// </summary>
public class TaxInput
{
    /// <summary>Tax year (e.g. 2024).</summary>
    public int     Year             { get; set; }

    // ── Asset values on the reference date (e.g. 1 January) ─────────────────
    /// <summary>Total crypto portfolio value in EUR on the reference date.</summary>
    public decimal CryptoValue      { get; set; }

    /// <summary>Bank / savings account balances in EUR.</summary>
    public decimal BankSavings      { get; set; }

    /// <summary>Other investments (stocks, bonds, real estate, etc.) in EUR.</summary>
    public decimal OtherAssets      { get; set; }

    /// <summary>Deductible debts in EUR (country-specific rules apply).</summary>
    public decimal Debts            { get; set; }

    // ── Country-specific options ─────────────────────────────────────────────
    /// <summary>[NL] Whether a fiscal partner (fiscaal partner) is present, which doubles the tax-free threshold.</summary>
    public bool HasFiscalPartner    { get; set; }
}
