namespace CryptoPortfolioTracker.Services.Sentiment;

// Keyword-based sentiment scorer. Score range: -1.0 (very negative) to +1.0 (very positive).
// Upgrade path: replace Score() with ONNX FinBERT inference in Phase 3.
public static class SentimentAnalyzer
{
    private static readonly string[] BullishKeywords =
    [
        "bullish", "moon", "pump", "breakout", "rally", "surge", "buy", "long",
        "uptrend", "accumulate", "strong", "ath", "all-time high", "explosive",
        "undervalued", "gem", "launch", "partnership", "adoption", "upgrade"
    ];

    private static readonly string[] BearishKeywords =
    [
        "bearish", "dump", "crash", "sell", "short", "downtrend", "weak", "scam",
        "rug", "exit", "overvalued", "avoid", "liquidation", "fear", "panic",
        "drop", "plunge", "correction", "fraud", "hack", "exploit", "delisted"
    ];

    public static double Score(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;

        var lower = text.ToLowerInvariant();
        int bullish = BullishKeywords.Count(k => lower.Contains(k));
        int bearish = BearishKeywords.Count(k => lower.Contains(k));
        int total = bullish + bearish;

        if (total == 0) return 0;
        return Math.Round((double)(bullish - bearish) / total, 3);
    }

    public static double Confidence(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;

        var lower = text.ToLowerInvariant();
        int hits = BullishKeywords.Count(k => lower.Contains(k))
                 + BearishKeywords.Count(k => lower.Contains(k));

        // More keyword hits = higher confidence, capped at 1.0
        return Math.Min(1.0, hits * 0.2);
    }
}
