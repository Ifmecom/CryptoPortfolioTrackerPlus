using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using Anthropic;
using Anthropic.Exceptions;
using Microsoft.EntityFrameworkCore;
using AB = Anthropic.Models.Beta.Messages;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// EF/HTTP-lijm voor AI Research. Pure delen (prompt, herkenning, JSON) zitten in
/// <see cref="AiPromptBuilder"/>, <see cref="AiIntentDetector"/> en <see cref="AiChatWire"/>.
/// Claude gaat via de officiële Anthropic-SDK; de andere aanbieders via hun OpenAI-compatibele endpoint.
/// </summary>
public sealed class AiResearchService : IAiResearchService
{
    private static readonly ILogger Logger = Log.Logger.ForContext(Constants.SourceContextPropertyName, nameof(AiResearchService).PadRight(22));
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(120) };
    private const int MaxHistory = 100;
    private const int MaxTurns = 12;                       // laatste 6 vraag/antwoord-paren gaan mee

    private readonly PortfolioService _portfolioService;
    private readonly Settings _settings;
    private readonly List<AiHistoryEntry> _history = new();
    private bool _historyLoaded;

    public AiResearchService(PortfolioService portfolioService, Settings settings)
    {
        _portfolioService = portfolioService;
        _settings = settings;
    }

    private static string HistoryFile => Path.Combine(AppConstants.AppDataPath, "AiResearchHistory.json");

    // ── Munten en portfolio ──────────────────────────────────────────────────

    public async Task<IReadOnlyList<AiCoinRef>> GetCoinsAsync()
    {
        var coins = await _portfolioService.Context.Coins.AsNoTracking()
            .OrderBy(c => c.Rank)
            .Select(c => new { c.ApiId, c.Symbol, c.Name, c.Price, c.IsAsset })
            .ToListAsync();
        return coins.Select(c => new AiCoinRef(c.ApiId, c.Symbol ?? string.Empty, c.Name ?? string.Empty, c.Price, c.IsAsset)).ToList();
    }

    public async Task<IReadOnlyList<AiHolding>> GetHoldingsAsync()
    {
        var assets = await _portfolioService.Context.Assets.AsNoTracking()
            .Where(a => a.Qty > 0)
            .Select(a => new { a.Coin.Symbol, a.Coin.Name, a.Coin.Price, a.Qty, a.AverageCostPrice })
            .ToListAsync();

        // Per munt optellen over accounts; gemiddelde aankoopprijs gewogen naar hoeveelheid.
        return assets
            .GroupBy(a => a.Symbol, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                double qty = g.Sum(a => a.Qty);
                double avg = qty > 0 ? g.Sum(a => a.Qty * a.AverageCostPrice) / qty : 0;
                var first = g.First();
                return new AiHolding(first.Symbol ?? string.Empty, first.Name ?? string.Empty, qty, first.Price, avg);
            })
            .ToList();
    }

    public async Task<string> BuildContextAsync() =>
        AiPromptBuilder.BuildContext(await GetHoldingsAsync(), await GetCoinsAsync(), DateTime.Now);

    public async Task<Coin?> GetCoinAsync(string coinApiId) =>
        await _portfolioService.Context.Coins.AsNoTracking()
            .Include(c => c.Narrative)
            .FirstOrDefaultAsync(c => c.ApiId == coinApiId);

    public async Task<bool> AppendNoteAsync(string coinApiId, string text)
    {
        var current = await _portfolioService.Context.Coins.AsNoTracking()
            .Where(c => c.ApiId == coinApiId).Select(c => c.Note).FirstOrDefaultAsync();
        var note = string.IsNullOrWhiteSpace(current) ? text.Trim() : $"{current.TrimEnd()}\n\n{text.Trim()}";

        // Alleen het Note-veld schrijven: nooit Coins.Update() op een losse coin (zie CLAUDE.md, v1.48-incident).
        int rows = await _portfolioService.Context.Coins
            .Where(c => c.ApiId == coinApiId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Note, note));
        return rows > 0;
    }

    // ── Sleutels en modellen ─────────────────────────────────────────────────

    public bool HasKey(string providerId) => !string.IsNullOrEmpty(_settings.GetAiApiKeyProtected(providerId));

    public void SaveKey(string providerId, string? apiKey)
    {
        var key = apiKey?.Trim() ?? string.Empty;
        _settings.SetAiApiKeyProtected(providerId, key.Length == 0 ? string.Empty : Protect(key));
    }

    public string GetModel(string providerId)
    {
        var m = _settings.GetAiModel(providerId);
        return string.IsNullOrWhiteSpace(m) ? AiCatalog.Api(providerId)?.DefaultModel ?? string.Empty : m;
    }

    public void SaveModel(string providerId, string? model)
    {
        var def = AiCatalog.Api(providerId)?.DefaultModel;
        _settings.SetAiModel(providerId, string.Equals(model?.Trim(), def, StringComparison.Ordinal) ? string.Empty : model ?? string.Empty);
    }

    public IReadOnlyList<AiApiProvider> ConfiguredProviders() =>
        AiCatalog.ApiProviders.Where(p => HasKey(p.Id)).ToList();

    private string? GetKey(string providerId)
    {
        var protectedKey = _settings.GetAiApiKeyProtected(providerId);
        if (string.IsNullOrEmpty(protectedKey)) return null;
        try { return Unprotect(protectedKey); }
        catch (Exception ex)
        {
            Logger.Warning(ex, "AI-sleutel voor {Provider} kon niet worden ontsleuteld", providerId);
            return null;
        }
    }

    private static string Protect(string plain) =>
        Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser));

    private static string Unprotect(string cipher) =>
        Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(cipher), null, DataProtectionScope.CurrentUser));

    // ── Vragen ───────────────────────────────────────────────────────────────

    public async Task<string> AskAsync(string providerId, IReadOnlyList<AiChatTurn> turns, CancellationToken ct)
    {
        var provider = AiCatalog.Api(providerId) ?? throw new InvalidOperationException($"Onbekende aanbieder '{providerId}'.");
        var key = GetKey(providerId) ?? throw new InvalidOperationException($"Geen API-sleutel ingesteld voor {provider.Name}.");
        var model = GetModel(providerId);
        var recent = turns.Skip(Math.Max(0, turns.Count - MaxTurns)).ToList();

        var started = DateTime.UtcNow;
        var answer = provider.Format == AiApiFormat.Anthropic
            ? await AskAnthropicAsync(key, model, recent, ct)
            : await AskOpenAiCompatibleAsync(provider, key, model, recent, ct);
        Logger.Information("AI-vraag via {Provider} ({Model}) beantwoord in {Ms} ms", provider.Name, model, (int)(DateTime.UtcNow - started).TotalMilliseconds);
        return answer;
    }

    private static async Task<string> AskOpenAiCompatibleAsync(AiApiProvider provider, string key, string model,
                                                               IReadOnlyList<AiChatTurn> turns, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, provider.Endpoint)
        {
            Content = new StringContent(AiChatWire.BuildOpenAiBody(model, AiPromptBuilder.SystemPrompt, turns), Encoding.UTF8, "application/json"),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        if (provider.Id == "openrouter")
        {
            // OpenRouter vraagt om een app-naam voor hun statistieken (optioneel).
            req.Headers.TryAddWithoutValidation("X-Title", "Crypto Portfolio Tracker Plus");
        }

        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(AiChatWire.ParseError((int)resp.StatusCode, body));

        return AiChatWire.ParseOpenAiText(body)
               ?? throw new InvalidOperationException($"{provider.Name} gaf een leeg of onbekend antwoord.");
    }

    private static async Task<string> AskAnthropicAsync(string key, string model, IReadOnlyList<AiChatTurn> turns, CancellationToken ct)
    {
        var client = new AnthropicClient { ApiKey = key };

        // Huidige generatie (Opus 5.x, Fable, Sonnet 5.x): effort expliciet; Opus/Fable krijgen een
        // server-side fallback zodat een geweigerd verzoek door een ander model wordt beantwoord.
        bool currentGen = model.StartsWith("claude-opus-5", StringComparison.Ordinal)
                       || model.StartsWith("claude-fable", StringComparison.Ordinal)
                       || model.StartsWith("claude-sonnet-5", StringComparison.Ordinal);
        bool withFallback = model.StartsWith("claude-opus-5", StringComparison.Ordinal)
                         || model.StartsWith("claude-fable", StringComparison.Ordinal);

        var parameters = new AB::MessageCreateParams
        {
            Model = model,
            MaxTokens = 16000,
            System = AiPromptBuilder.SystemPrompt,
            Messages = turns.Select(t => new AB::BetaMessageParam
            {
                Role = t.Role == "assistant" ? AB::Role.Assistant : AB::Role.User,
                Content = t.Text,
            }).ToList(),
            OutputConfig = currentGen ? new AB::BetaOutputConfig { Effort = AB::Effort.Medium } : null,
            // "default": de API kiest per weigeringscategorie zelf een fallback-model (geen modellijst bij te houden).
            Betas = withFallback ? [Anthropic.Models.Beta.AnthropicBeta.ServerSideFallback2026_07_01] : null,
            Fallbacks = withFallback ? new AB::Default() : null,
        };

        AB::BetaMessage response;
        try
        {
            response = await client.Beta.Messages.Create(parameters, ct);
        }
        catch (AnthropicNotFoundException ex)
        {
            throw new InvalidOperationException($"Model '{model}' niet gevonden — controleer de modelnaam. ({ex.Message})", ex);
        }
        catch (AnthropicUnauthorizedException ex)
        {
            throw new InvalidOperationException("Sleutel ongeldig of zonder rechten.", ex);
        }
        catch (AnthropicRateLimitException ex)
        {
            throw new InvalidOperationException("Limiet bereikt (te veel verzoeken of tegoed op). Probeer het straks opnieuw.", ex);
        }
        catch (Anthropic5xxException ex)
        {
            throw new InvalidOperationException("Anthropic heeft een storing. Probeer het straks opnieuw.", ex);
        }
        catch (AnthropicApiException ex)
        {
            throw new InvalidOperationException($"Verzoek geweigerd: {ex.Message}", ex);
        }
        catch (AnthropicIOException ex)
        {
            throw new InvalidOperationException("Geen verbinding met Anthropic.", ex);
        }

        if (response.StopReason == "refusal")
            return "Claude heeft deze vraag geweigerd." +
                   (response.StopDetails is { } d && !string.IsNullOrWhiteSpace(d.Explanation) ? $" ({d.Explanation})" : string.Empty);

        var text = string.Join("\n", response.Content
            .Select(b => b.TryPickText(out var t) ? t.Text : null)
            .Where(t => !string.IsNullOrEmpty(t)));
        if (response.StopReason == "max_tokens") text += "\n\n(Antwoord afgekapt: maximale lengte bereikt.)";
        return text.Length > 0 ? text : "Claude gaf geen tekstantwoord.";
    }

    // ── Geschiedenis ─────────────────────────────────────────────────────────

    public IReadOnlyList<AiHistoryEntry> History
    {
        get
        {
            if (!_historyLoaded) LoadHistory();
            return _history;
        }
    }

    public async Task AddHistoryAsync(AiHistoryEntry entry)
    {
        if (!_historyLoaded) LoadHistory();
        _history.Insert(0, entry);
        if (_history.Count > MaxHistory) _history.RemoveRange(MaxHistory, _history.Count - MaxHistory);
        try
        {
            await File.WriteAllTextAsync(HistoryFile, JsonSerializer.Serialize(_history, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "AI-geschiedenis opslaan mislukt");
        }
    }

    private void LoadHistory()
    {
        _historyLoaded = true;
        try
        {
            if (!File.Exists(HistoryFile)) return;
            var list = JsonSerializer.Deserialize<List<AiHistoryEntry>>(File.ReadAllText(HistoryFile));
            if (list is not null) _history.AddRange(list.Take(MaxHistory));
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "AI-geschiedenis laden mislukt");
        }
    }
}
