using CryptoPortfolioTracker.Enums;

namespace CryptoPortfolioTracker.Models;

/// <summary>
/// Eén keuze in de patroon-filter dropdown op de Pattern Trading-pagina.
/// <see cref="Type"/> is null voor de "Alle patronen"-keuze.
/// </summary>
public sealed class PatternFilterOption
{
    public PatternType? Type  { get; init; }
    public string       Label { get; init; } = "Alle patronen";

    public static PatternFilterOption All() => new();
}
