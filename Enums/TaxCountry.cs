namespace CryptoPortfolioTracker.Enums;

/// <summary>
/// Countries for which a tax calculator is available.
/// Add new values here and register a matching ITaxCalculator in TaxViewModel._calculators.
/// </summary>
public enum TaxCountry
{
    Netherlands,
    // Future: Germany, Belgium, UnitedKingdom, UnitedStates, France, Other
}
