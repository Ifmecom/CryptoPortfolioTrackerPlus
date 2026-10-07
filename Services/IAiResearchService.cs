using System.Threading;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// AI Research (v1.49): stelt vragen aan AI-API's met de sleutel van de gebruiker, levert munten en
/// portfolio-context uit de database, bewaart sleutels (DPAPI) en de vraaggeschiedenis.
/// De ingebedde websites (gratis versies) lopen niet via deze service maar via WebView2 in de view.
/// </summary>
public interface IAiResearchService
{
    Task<IReadOnlyList<AiCoinRef>> GetCoinsAsync();
    Task<IReadOnlyList<AiHolding>> GetHoldingsAsync();
    Task<string> BuildContextAsync();

    bool HasKey(string providerId);
    /// <summary>Lege of null sleutel = verwijderen.</summary>
    void SaveKey(string providerId, string? apiKey);
    string GetModel(string providerId);
    void SaveModel(string providerId, string? model);
    IReadOnlyList<AiApiProvider> ConfiguredProviders();

    /// <summary>Stel de vraag (laatste beurt in <paramref name="turns"/>) met de systeemprompt van de app.</summary>
    Task<string> AskAsync(string providerId, IReadOnlyList<AiChatTurn> turns, CancellationToken ct);

    Task<Coin?> GetCoinAsync(string coinApiId);
    /// <summary>Tekst achter de bestaande notitie van de munt zetten (veilig: alleen het Note-veld).</summary>
    Task<bool> AppendNoteAsync(string coinApiId, string text);

    IReadOnlyList<AiHistoryEntry> History { get; }
    Task AddHistoryAsync(AiHistoryEntry entry);
}
