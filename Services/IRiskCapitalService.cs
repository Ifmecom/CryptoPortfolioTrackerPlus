using System.Threading;
using System.Threading.Tasks;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Levert de kapitaalbasis voor risico-berekeningen, afhankelijk van de instelling:
/// virtueel paper-kapitaal of de werkelijke portfoliowaarde.
/// </summary>
public interface IRiskCapitalService
{
    /// <summary>Het te gebruiken kapitaal (USDT) volgens de gekozen basis.</summary>
    Task<double> GetCapitalAsync(CancellationToken ct = default);

    /// <summary>De werkelijke portfoliowaarde (som holdings × prijs), ongeacht de instelling.</summary>
    Task<double> GetRealPortfolioValueAsync(CancellationToken ct = default);

    /// <summary>Leesbare omschrijving van de gekozen basis, bv. "virtueel paper-kapitaal".</summary>
    string BasisLabel { get; }
}
