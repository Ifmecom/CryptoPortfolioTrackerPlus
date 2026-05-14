using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services.Tax;

/// <summary>
/// Dutch Box 3 wealth-tax calculator (vermogensrendementsheffing).
///
/// Method (2022 – present, post-kerstarrest):
///   1. Rendementsgrondslag = spaargeld + overige bezittingen – schulden  (min 0)
///   2. Belastbaar bedrag   = max(0, rendementsgrondslag – heffingsvrij vermogen)
///   3. Fictief rendement   = belastbaar bedrag split proportionally over asset classes,
///                            each class multiplied by its own fictitious-return percentage.
///   4. Belasting           = fictief rendement × belastingtarief
///
/// Crypto is classified as "overige bezittingen" (other assets), the higher-rate bracket.
///
/// Sources:
///   https://www.belastingdienst.nl/wps/wcm/connect/bldcontentnl/belastingdienst/
///   prive/vermogen_en_aanmerkelijk_belang/vermogen/belasting_betalen_over_uw_vermogen/
/// </summary>
public sealed class NetherlandsTaxCalculator : ITaxCalculator
{
    public TaxCountry         Country       => TaxCountry.Netherlands;
    public string             CountryName   => "Nederland";
    public IReadOnlyList<int> SupportedYears { get; } = new[] { 2022, 2023, 2024 };

    public DateOnly ReferenceDate(int year) => new DateOnly(year, 1, 1);

    // ────────────────────────────────────────────────────────────────────────

    public TaxReport Calculate(TaxInput input)
    {
        var p = GetRates(input.Year)
            ?? throw new NotSupportedException(
                $"Belastingjaar {input.Year} wordt niet ondersteund door de NL-calculator.");

        decimal crypto   = input.CryptoValue;
        decimal savings  = input.BankSavings;
        decimal other    = input.OtherAssets;
        decimal debts    = input.Debts;
        bool    partner  = input.HasFiscalPartner;

        // ── Step 1 ───────────────────────────────────────────────────────────
        decimal totalGross  = crypto + savings + other;
        decimal grondslag   = Math.Max(0m, totalGross - debts);
        decimal threshold   = partner ? p.HVV * 2 : p.HVV;
        decimal taxBase     = Math.Max(0m, grondslag - threshold);

        // ── Step 2: proportionally allocate taxBase to asset classes ─────────
        decimal fictReturn = 0m;
        if (taxBase > 0 && totalGross > 0)
        {
            // Each class gets a share proportional to its gross weight.
            decimal savingsAlloc = Math.Min(taxBase, savings / totalGross * taxBase);
            decimal cryptoAlloc  = Math.Min(taxBase - savingsAlloc, crypto  / totalGross * taxBase);
            decimal otherAlloc   = taxBase - savingsAlloc - cryptoAlloc;

            fictReturn = savingsAlloc * p.SavingsRate
                       + cryptoAlloc  * p.OtherRate
                       + otherAlloc   * p.OtherRate;
        }

        // ── Step 3 ───────────────────────────────────────────────────────────
        decimal taxDue       = Math.Round(fictReturn * p.TaxRate, 2);
        decimal effectiveRate = grondslag > 0
            ? Math.Round(taxDue / grondslag * 100m, 2)
            : 0m;

        // ── Build breakdown ──────────────────────────────────────────────────
        var lines = new List<TaxReportLine>
        {
            new() { Label = "Cryptowaarde op 1 januari",        Amount = crypto  },
        };
        if (savings > 0)
            lines.Add(new() { Label = "Banktegoeden",           Amount = savings });
        if (other > 0)
            lines.Add(new() { Label = "Overige beleggingen",    Amount = other   });
        if (debts > 0)
            lines.Add(new() { Label = "Schulden (aftrekpost)",  Amount = debts,  IsSubtraction = true });

        lines.Add(new() { Label = "Netto vermogen (rendementsgrondslag)", Amount = grondslag, IsTotalLine = true });
        lines.Add(new() { Label = $"Heffingsvrij vermogen{(partner ? " (partners)" : "")}",
                          Amount = threshold, IsSubtraction = true });
        lines.Add(new() { Label = "Belastbaar box 3 vermogen",  Amount = taxBase,   IsTotalLine = true });
        lines.Add(new() { Label = $"Fictief rendement  ·  overige bezittingen {p.OtherRate:P2}  ·  spaargeld {p.SavingsRate:P2}",
                          Amount = fictReturn });
        lines.Add(new() { Label = $"Box 3 inkomstenbelasting ({p.TaxRate:P0})",
                          Amount = taxDue, IsTotalLine = true });

        return new TaxReport
        {
            CountryName   = CountryName,
            Year          = input.Year,
            TaxDue        = taxDue,
            EffectiveRate = effectiveRate,
            Breakdown     = lines,
        };
    }

    // ── Year-specific rates ──────────────────────────────────────────────────
    // Update each year after Belastingdienst publishes the official percentages.
    //
    // | Jaar | HVV (p.p.) | Spaargeld | Overige bez. | Tarief |
    // |------|-----------|-----------|--------------|--------|
    // | 2022 | €50.650   | 0,00 %    | 5,53 %       | 31 %   |
    // | 2023 | €57.000   | 0,92 %    | 6,17 %       | 32 %   |
    // | 2024 | €57.000   | 1,03 %    | 6,04 %       | 36 %   |

    private static NlRates? GetRates(int year) => year switch
    {
        2022 => new(HVV: 50_650m, SavingsRate: 0.0000m, OtherRate: 0.0553m, TaxRate: 0.31m),
        2023 => new(HVV: 57_000m, SavingsRate: 0.0092m, OtherRate: 0.0617m, TaxRate: 0.32m),
        2024 => new(HVV: 57_000m, SavingsRate: 0.0103m, OtherRate: 0.0604m, TaxRate: 0.36m),
        _    => null,
    };

    private record NlRates(
        decimal HVV,
        decimal SavingsRate,
        decimal OtherRate,
        decimal TaxRate);
}
