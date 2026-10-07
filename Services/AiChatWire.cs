using System.Text.Json;
using System.Text.Json.Nodes;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// JSON van de OpenAI-compatibele chat-API (OpenAI, xAI, Gemini, Groq, OpenRouter, DeepSeek, Mistral). Puur en getest.
/// Bewust zonder temperature/max_tokens: nieuwere modellen weigeren daar soms afwijkende waarden voor.
/// </summary>
public static class AiChatWire
{
    public static string BuildOpenAiBody(string model, string system, IReadOnlyList<AiChatTurn> turns)
    {
        var messages = new JsonArray { new JsonObject { ["role"] = "system", ["content"] = system } };
        foreach (var t in turns)
            messages.Add(new JsonObject { ["role"] = t.Role == "assistant" ? "assistant" : "user", ["content"] = t.Text });
        return new JsonObject { ["model"] = model, ["messages"] = messages }.ToJsonString();
    }

    /// <summary>Tekst uit <c>choices[0].message.content</c>; null als die er niet is.</summary>
    public static string? ParseOpenAiText(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array
                && choices.GetArrayLength() > 0
                && choices[0].TryGetProperty("message", out var msg)
                && msg.TryGetProperty("content", out var content))
            {
                if (content.ValueKind == JsonValueKind.String) return content.GetString();
                if (content.ValueKind == JsonValueKind.Array)        // sommige aanbieders geven delen terug
                    return string.Concat(content.EnumerateArray()
                        .Where(p => p.TryGetProperty("text", out _))
                        .Select(p => p.GetProperty("text").GetString()));
            }
        }
        catch (JsonException) { }
        return null;
    }

    // ── Anthropic Messages API (Claude) ──────────────────────────────────────
    // Bewust via HTTP en niet via de Anthropic-SDK: die dwingt System.Text.Json 10 af, waarvan een .NET 6-app de
    // netstandard2.0-variant krijgt zonder DateOnly-ondersteuning — dan breekt o.a. graph.json (v1.49-regressie).

    public const string AnthropicEndpoint = "https://api.anthropic.com/v1/messages";
    public const string AnthropicVersion = "2023-06-01";
    public const string AnthropicFallbackBeta = "server-side-fallback-2026-07-01";

    /// <summary>Huidige generatie (Opus 5.x, Fable, Sonnet 5.x): effort expliciet zetten (Opus 5.5 staat standaard op medium).</summary>
    public static bool AnthropicSupportsEffort(string model) =>
        model.StartsWith("claude-opus-5", StringComparison.Ordinal)
        || model.StartsWith("claude-fable", StringComparison.Ordinal)
        || model.StartsWith("claude-sonnet-5", StringComparison.Ordinal);

    /// <summary>Opus 5.x en Fable: server-side fallback ("default") zodat een weigering door een ander model wordt beantwoord.</summary>
    public static bool AnthropicUsesFallback(string model) =>
        model.StartsWith("claude-opus-5", StringComparison.Ordinal)
        || model.StartsWith("claude-fable", StringComparison.Ordinal);

    public static string BuildAnthropicBody(string model, string system, IReadOnlyList<AiChatTurn> turns, int maxTokens = 16000)
    {
        var messages = new JsonArray();
        foreach (var t in turns)
            messages.Add(new JsonObject { ["role"] = t.Role == "assistant" ? "assistant" : "user", ["content"] = t.Text });

        var body = new JsonObject
        {
            ["model"] = model,
            ["max_tokens"] = maxTokens,
            ["system"] = system,
            ["messages"] = messages,
        };
        if (AnthropicSupportsEffort(model)) body["output_config"] = new JsonObject { ["effort"] = "medium" };
        if (AnthropicUsesFallback(model)) body["fallbacks"] = "default";
        return body.ToJsonString();
    }

    /// <summary>Tekstblokken samengevoegd + stop_reason (+ uitleg bij een weigering). Null-tekst als het antwoord onleesbaar is.</summary>
    public static (string? Text, string StopReason, string? RefusalExplanation) ParseAnthropic(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var stop = root.TryGetProperty("stop_reason", out var sr) && sr.ValueKind == JsonValueKind.String ? sr.GetString() ?? "" : "";
            string? refusal = root.TryGetProperty("stop_details", out var sd) && sd.ValueKind == JsonValueKind.Object
                              && sd.TryGetProperty("explanation", out var ex) && ex.ValueKind == JsonValueKind.String
                ? ex.GetString() : null;

            if (!root.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                return (null, stop, refusal);

            var text = string.Join("\n", content.EnumerateArray()
                .Where(b => b.TryGetProperty("type", out var ty) && ty.GetString() == "text" && b.TryGetProperty("text", out _))
                .Select(b => b.GetProperty("text").GetString())
                .Where(s => !string.IsNullOrEmpty(s)));
            return (text, stop, refusal);
        }
        catch (JsonException)
        {
            return (null, "", null);
        }
    }

    /// <summary>Leesbare foutmelding uit een foutantwoord ({"error":{"message":…}} of [{"error":…}] bij Gemini).</summary>
    public static string ParseError(int status, string body)
    {
        string? message = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0
                ? doc.RootElement[0] : doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out var err))
                message = err.ValueKind == JsonValueKind.String ? err.GetString()
                        : err.TryGetProperty("message", out var m) ? m.GetString() : null;
        }
        catch (JsonException) { }

        var hint = status switch
        {
            401 or 403 => "Sleutel ongeldig of zonder rechten.",
            404        => "Model of endpoint niet gevonden — controleer de modelnaam.",
            429        => "Limiet bereikt (te veel verzoeken of tegoed op).",
            >= 500     => "De aanbieder heeft een storing.",
            _          => "Verzoek geweigerd.",
        };
        return string.IsNullOrWhiteSpace(message) ? $"{hint} (HTTP {status})" : $"{hint} {message} (HTTP {status})";
    }
}
