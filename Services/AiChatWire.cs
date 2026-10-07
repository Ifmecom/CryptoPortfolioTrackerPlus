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
