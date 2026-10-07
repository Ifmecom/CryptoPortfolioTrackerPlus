namespace CryptoPortfolioTracker.Models;

// ─────────────────────────────────────────────────────────────────────────────
// AI Research (v1.49) — pure modellen, geen WinUI (worden meegecompileerd in de tests)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Een AI-chatsite die in de app wordt ingebed (gratis versie, inloggen gebeurt in de site zelf).</summary>
/// <param name="QueryUrlTemplate">URL met <c>{q}</c> voor een vooringevulde vraag; null = site ondersteunt dat niet.</param>
public sealed record AiWebProvider(string Id, string Name, string HomeUrl, string? QueryUrlTemplate, string Note);

public enum AiApiFormat { OpenAiCompatible, Anthropic }

/// <summary>Een AI-aanbieder met API; de app stelt de vraag dan zelf (met sleutel van de gebruiker).</summary>
public sealed record AiApiProvider(
    string Id, string Name, AiApiFormat Format, string Endpoint, string DefaultModel,
    bool HasFreeTier, string KeyUrl, string Note);

public static class AiCatalog
{
    public static readonly IReadOnlyList<AiWebProvider> WebProviders = new[]
    {
        new AiWebProvider("chatgpt",    "ChatGPT",    "https://chatgpt.com/",              "https://chatgpt.com/?q={q}",                 "Werkt ook zonder account."),
        new AiWebProvider("claude",     "Claude",     "https://claude.ai/new",             "https://claude.ai/new?q={q}",                "Inloggen nodig (gratis account; e-mail-login werkt het best)."),
        new AiWebProvider("grok",       "Grok",       "https://grok.com/",                 "https://grok.com/?q={q}",                    "Sterk in actuele X/Twitter-sentiment."),
        new AiWebProvider("gemini",     "Gemini",     "https://gemini.google.com/app",     null,                                         "Google-login kan ingebedde browsers weigeren — gebruik dan 'Open in browser'."),
        new AiWebProvider("perplexity", "Perplexity", "https://www.perplexity.ai/",        "https://www.perplexity.ai/search?q={q}",     "Zoekt op het web en geeft bronnen — goed voor nieuws en onderzoek."),
        new AiWebProvider("deepseek",   "DeepSeek",   "https://chat.deepseek.com/",        null,                                         "Inloggen nodig."),
        new AiWebProvider("copilot",    "Copilot",    "https://copilot.microsoft.com/",    "https://copilot.microsoft.com/?q={q}",       "Werkt ook zonder account."),
    };

    public static readonly IReadOnlyList<AiApiProvider> ApiProviders = new[]
    {
        new AiApiProvider("gemini",     "Google Gemini", AiApiFormat.OpenAiCompatible, "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions", "gemini-2.5-flash", true,  "https://aistudio.google.com/apikey",     "Gratis tier met een Google-account."),
        new AiApiProvider("groq",       "Groq",          AiApiFormat.OpenAiCompatible, "https://api.groq.com/openai/v1/chat/completions",                         "llama-3.3-70b-versatile", true, "https://console.groq.com/keys",     "Gratis tier, zeer snel (open modellen)."),
        new AiApiProvider("openrouter", "OpenRouter",    AiApiFormat.OpenAiCompatible, "https://openrouter.ai/api/v1/chat/completions",                           "meta-llama/llama-3.3-70b-instruct:free", true, "https://openrouter.ai/keys", "Gratis modellen eindigen op ':free'."),
        new AiApiProvider("mistral",    "Mistral",       AiApiFormat.OpenAiCompatible, "https://api.mistral.ai/v1/chat/completions",                              "mistral-small-latest", true,   "https://console.mistral.ai/api-keys",   "Gratis experimenteer-tier."),
        new AiApiProvider("anthropic",  "Claude (Anthropic)", AiApiFormat.Anthropic,   "https://api.anthropic.com",                                               "claude-opus-5-5", false,       "https://console.anthropic.com/settings/keys", "Betaald (API-tegoed)."),
        new AiApiProvider("openai",     "ChatGPT (OpenAI)",   AiApiFormat.OpenAiCompatible, "https://api.openai.com/v1/chat/completions",                         "gpt-5-mini", false,            "https://platform.openai.com/api-keys",  "Betaald (API-tegoed)."),
        new AiApiProvider("xai",        "Grok (xAI)",    AiApiFormat.OpenAiCompatible, "https://api.x.ai/v1/chat/completions",                                    "grok-4", false,                "https://console.x.ai",                  "Betaald (API-tegoed)."),
        new AiApiProvider("deepseek",   "DeepSeek",      AiApiFormat.OpenAiCompatible, "https://api.deepseek.com/chat/completions",                               "deepseek-chat", false,         "https://platform.deepseek.com/api_keys", "Betaald, maar goedkoop."),
    };

    public static AiApiProvider? Api(string? id) => ApiProviders.FirstOrDefault(p => p.Id == id);
    public static AiWebProvider? Web(string? id) => WebProviders.FirstOrDefault(p => p.Id == id);

    /// <summary>URL voor een vooringevulde vraag, of de startpagina als de site dat niet kent.</summary>
    public static string UrlFor(AiWebProvider p, string? question) =>
        p.QueryUrlTemplate is null || string.IsNullOrWhiteSpace(question)
            ? p.HomeUrl
            : p.QueryUrlTemplate.Replace("{q}", Uri.EscapeDataString(question.Trim()));
}

/// <summary>Een munt zoals de AI-pagina hem kent (uit de bibliotheek).</summary>
public sealed record AiCoinRef(string ApiId, string Symbol, string Name, double Price, bool IsAsset);

/// <summary>Een positie voor de portfolio-context die met een vraag mee kan.</summary>
public sealed record AiHolding(string Symbol, string Name, double Qty, double Price, double AverageCostPrice)
{
    public double Value => Qty * Price;
    public double PnlPct => AverageCostPrice > 0 ? (Price - AverageCostPrice) / AverageCostPrice * 100 : 0;
}

public enum SmartActionKind
{
    Buy,             // aankoop vastleggen (transactie)
    Sell,            // verkoop vastleggen (transactie)
    PaperTrade,      // paper trade (Long/Short, met niveaus als die bekend zijn)
    PriceLevels,     // prijsniveaus/alerts zetten (koop / stop / winst)
    AddCoin,         // munt toevoegen aan de bibliotheek
    TradeAdvies,     // munt analyseren in Trade Advies
    Fundamentals,    // munt bekijken in Fundamentals
    TradingView,     // grafiek openen op TradingView
    SaveNote,        // antwoord bewaren als notitie bij de munt
}

/// <summary>Een voorgestelde vervolgstap. Voert nooit zelf iets uit: de app opent steeds een venster ter bevestiging.</summary>
public sealed record SmartAction
{
    public SmartActionKind Kind { get; init; }
    public string Symbol { get; init; } = string.Empty;
    public string CoinApiId { get; init; } = string.Empty;   // leeg bij AddCoin
    public string CoinName { get; init; } = string.Empty;
    public string Direction { get; init; } = string.Empty;   // "Long"/"Short" bij PaperTrade
    public double? Entry { get; init; }
    public double? StopLoss { get; init; }
    public double? Target { get; init; }
    public string SearchText { get; init; } = string.Empty;  // bij AddCoin
    public string Reason { get; init; } = string.Empty;      // waarom voorgesteld (kort)
    public string Source { get; init; } = string.Empty;      // "vraag" / "antwoord" / "AI-voorstel" / "site"

    public string Title => Kind switch
    {
        SmartActionKind.Buy          => $"Aankoop {Symbol} vastleggen",
        SmartActionKind.Sell         => $"Verkoop {Symbol} vastleggen",
        SmartActionKind.PaperTrade   => $"Paper trade {Direction} {Symbol}",
        SmartActionKind.PriceLevels  => $"Prijsniveaus {Symbol} zetten",
        SmartActionKind.AddCoin      => $"{(string.IsNullOrEmpty(Symbol) ? SearchText : Symbol)} toevoegen aan bibliotheek",
        SmartActionKind.TradeAdvies  => $"{Symbol} analyseren in Trade Advies",
        SmartActionKind.Fundamentals => $"{Symbol} in Fundamentals",
        SmartActionKind.TradingView  => $"{Symbol}-grafiek op TradingView",
        SmartActionKind.SaveNote     => $"Antwoord bewaren bij {Symbol}",
        _ => Symbol,
    };

    public string Glyph => Kind switch
    {
        SmartActionKind.Buy          => "",
        SmartActionKind.Sell         => "",
        SmartActionKind.PaperTrade   => "",
        SmartActionKind.PriceLevels  => "",
        SmartActionKind.AddCoin      => "",
        SmartActionKind.TradeAdvies  => "",
        SmartActionKind.Fundamentals => "",
        SmartActionKind.TradingView  => "",
        SmartActionKind.SaveNote     => "",
        _ => "",
    };

    /// <summary>Korte toelichting met de herkende niveaus.</summary>
    public string Detail
    {
        get
        {
            var parts = new List<string>();
            if (Entry is > 0)    parts.Add($"entry {Fmt(Entry.Value)}");
            if (StopLoss is > 0) parts.Add($"stop {Fmt(StopLoss.Value)}");
            if (Target is > 0)   parts.Add($"doel {Fmt(Target.Value)}");
            var levels = string.Join(" · ", parts);
            var why = string.IsNullOrWhiteSpace(Reason) ? string.Empty : Reason;
            return string.Join(" — ", new[] { levels, why }.Where(s => s.Length > 0));
        }
    }

    private static string Fmt(double v) => v switch
    {
        >= 1000 => v.ToString("#,0.##"),
        >= 1    => v.ToString("0.####"),
        _       => v.ToString("0.########"),
    };
}

/// <summary>Eén bericht in het API-gesprek.</summary>
public sealed record AiChatTurn(string Role, string Text);   // Role: "user" / "assistant"

/// <summary>Een vraag + antwoord in de lokale geschiedenis.</summary>
public sealed record AiHistoryEntry(DateTime TimestampUtc, string Provider, string Question, string Answer);

// ── Navigatie-opdrachten: de AI-pagina springt naar een andere pagina, die de opdracht uitvoert ──

public abstract record AppNavigationRequest;
public sealed record TransactionRequest(bool IsBuy, string Symbol, string Note) : AppNavigationRequest;
public sealed record AddCoinRequest(string SearchText) : AppNavigationRequest;
public sealed record PriceLevelsRequest(string CoinApiId, double? Buy, double? Stop, double? TakeProfit, string Note) : AppNavigationRequest;
public sealed record TradeAdviesRequest(string CoinApiId) : AppNavigationRequest;
public sealed record FundamentalsRequest(string Symbol) : AppNavigationRequest;
