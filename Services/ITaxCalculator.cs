using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Contract for a country-specific tax calculator.
/// To add support for a new country:
///   1. Add a value to the TaxCountry enum.
///   2. Create a class in Services/Tax/ that implements this interface.
///   3. Register the class in TaxViewModel._calculators.
/// </summary>
public interface ITaxCalculator
{
    /// <summary>Identifies which country this calculator covers.</summary>
    TaxCountry Country { get; }

    /// <summary>Human-readable display name (used in the UI dropdown).</summary>
    string CountryName { get; }

    /// <summary>Calendar years for which this calculator has reliable rate data.</summary>
    IReadOnlyList<int> SupportedYears { get; }

    /// <summary>
    /// The date on which assets are assessed for this tax year.
    /// E.g. January 1st for the Netherlands.
    /// </summary>
    DateOnly ReferenceDate(int year);

    /// <summary>Perform the tax calculation and return a detailed report.</summary>
    TaxReport Calculate(TaxInput input);
}
