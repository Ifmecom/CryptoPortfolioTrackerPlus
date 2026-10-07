using System.Text.RegularExpressions;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Haalt uit een vraag of AI-antwoord welke munten genoemd worden en wat de gebruiker ermee wil
/// (kopen, verkopen, niveaus, toevoegen) en maakt daar <see cref="SmartAction"/>s van. Puur en getest.
/// <para>
/// Werkwijze: de tekst wordt in zinnen/regels geknipt. Een zin zonder munt hoort bij de munt van de
/// vorige zin (AI-antwoorden zetten niveaus vaak in losse opsommingsregels onder de munt). Per munt
/// worden intenties (NL/EN-trefwoorden) en niveaus (entry/stop/doel/steun/weerstand) verzameld.
/// Niveaus moeten passen bij de huidige koers (factor 5) en bij de richting, anders vallen ze weg.
/// </para>
/// </summary>
public static class AiIntentDetector
{
    // Afkortingen die als woord in hoofdletters vaak géén munt bedoelen (alleen met $ ervoor tellen ze dan wel).
    private static readonly HashSet<string> TickerStopwords = new(StringComparer.Ordinal)
    {
        "A", "I", "AI", "OK", "US", "EU", "UK", "NL", "BE", "DE", "IT", "IS", "ON", "OR", "AT", "AN", "IN", "TO", "OF", "BY",
        "NO", "SO", "GO", "ME", "WE", "UP", "AM", "PM", "CEO", "ETF", "ETFS", "API", "ATH", "ATL", "TVL", "RSI", "MA", "EMA",
        "SMA", "MACD", "DCA", "NFT", "NFTS", "DEX", "CEX", "FUD", "FOMO", "TA", "FA", "SL", "TP", "TP1", "TP2", "RR", "PNL",
        "USD", "EUR", "FAQ", "KYC", "AML", "SEC", "CFTC", "FED", "CPI", "GDP", "IPO", "ROI", "APY", "APR", "P2P", "L1", "L2",
        "DAO", "DEFI", "GAS", "HET", "EEN", "DE", "EN", "OP", "VAN", "MET", "VOOR", "BTW", "AVG", "OTC", "ICO", "IDO", "TGE",
        "YTD", "QE", "QT", "M2", "MVRV", "SOPR", "NUPL", "OI", "LTV", "KPI", "UI", "UX", "PS", "NB", "OBV", "ADX", "ATR", "VWAP",
        "BUY", "SELL", "LONG", "SHORT", "HODL", "WAGMI", "NGMI", "LFG", "IMO", "IMHO", "TL", "DR", "TLDR", "GPT", "LLM",
    };

    private static readonly Regex Splitter = new(@"(?<=[.!?])\s+(?=\S)|\r?\n+", RegexOptions.Compiled);
    private static readonly Regex DollarTicker = new(@"(?<![\w$])\$([A-Za-z][A-Za-z0-9]{1,9})\b", RegexOptions.Compiled);
    private static readonly Regex AddPhrase = new(
        @"(?:voeg|zet)\s+(?:de\s+munt\s+)?\$?([A-Za-z][\w-]{1,24})\s+(?:toe|aan\s+mijn|op\s+mijn|in\s+mijn)|\badd\s+\$?([A-Za-z][\w-]{1,24})\s+to\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex BuyWords = Words(
        "koop", "kopen", "koopt", "kocht", "gekocht", "bijkopen", "bijkoop", "instappen", "instap", "accumuleren", "accumuleer",
        "buy", "buying", "bought", "accumulate", "longen", "ingaan", "positie openen", "open a position");
    private static readonly Regex SellWords = Words(
        "verkoop", "verkopen", "verkocht", "uitstappen", "afbouwen", "winst nemen", "winst pakken", "take profit", "sell",
        "selling", "sold", "shorten", "exit", "dump", "dumpen");
    // "long"/"short" als richting, maar niet in "long-term", "short term", "korte termijn".
    private static readonly Regex LongWord  = new(@"(?<![\p{L}])long(?![\p{L}])(?![\s-]*(?:term|termijn))", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ShortWord = new(@"(?<![\p{L}])short(?![\p{L}])(?![\s-]*(?:term|termijn|squeeze))", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private const string Num = AiNumberParser.NumberPattern;
    // Trefwoord, dan max. 25 tekens zonder cijfers (": rond ", " zone ", " at ~"), dan het getal.
    private static readonly Regex EntryLevel  = Level(@"entry|instap(?:prijs|niveau)?|ingang|koopzone|buy\s+zone|buy\s+(?:at|around|near|below)|koop\s+(?:rond|bij|onder)|kopen\s+(?:rond|bij|onder)");
    private static readonly Regex StopLevel   = Level(@"stop[\s-]?loss|stop|sl|invalidatie|invalidation");
    private static readonly Regex TargetLevel = Level(@"take[\s-]?profit|tp\s?1|tp|target|koersdoel|winstdoel|doel|price\s+target");
    private static readonly Regex SupportLevel    = Level(@"support|steun(?:niveau)?");
    private static readonly Regex ResistanceLevel = Level(@"resistance|weerstand(?:sniveau)?");

    private static Regex Words(params string[] words) =>
        new(@"(?<![\p{L}])(?:" + string.Join("|", words.Select(Regex.Escape).Select(w => w.Replace("\\ ", @"\s+"))) + @")(?![\p{L}])",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static Regex Level(string keywords) =>
        new(@"(?<![\p{L}])(?:" + keywords + @")(?![\p{L}])[^\d\n]{0,25}?" + Num, RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Detecteer munten en vervolgstappen in <paramref name="text"/>.</summary>
    /// <param name="source">Waar de tekst vandaan komt ("vraag", "antwoord", "site"), voor de weergave.</param>
    /// <param name="includeResearch">Ook Trade Advies / Fundamentals / TradingView / notitie per munt voorstellen.</param>
    public static List<SmartAction> Detect(string? text, IReadOnlyList<AiCoinRef> coins, string source, bool includeResearch = true)
    {
        var result = new List<SmartAction>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        var index = new CoinIndex(coins);
        var sentences = Splitter.Split(text).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

        // Zinnen per munt groeperen; zinnen zonder munt horen bij de vorige munt(en).
        var blocks = new Dictionary<AiCoinRef, List<string>>();
        var order = new List<AiCoinRef>();
        var current = new List<AiCoinRef>();
        foreach (var s in sentences)
        {
            var found = index.Find(s);
            if (found.Count > 0) current = found;
            foreach (var c in current)
            {
                if (!blocks.TryGetValue(c, out var list)) { blocks[c] = list = new(); order.Add(c); }
                list.Add(s);
            }
        }

        foreach (var coin in order)
        {
            if (TradeSetupGate.IsStablecoin(coin.Symbol)) continue;
            var block = string.Join("\n", blocks[coin]);
            result.AddRange(ActionsForCoin(coin, block, source, includeResearch));
        }

        // Onbekende munten: "$XYZ" of "voeg XYZ toe" die niet in de bibliotheek staan.
        foreach (var unknown in UnknownCoins(text, index))
            result.Add(new SmartAction
            {
                Kind = SmartActionKind.AddCoin, Symbol = unknown.ToUpperInvariant(), SearchText = unknown,
                Reason = "genoemd, maar niet in je bibliotheek", Source = source,
            });

        return Dedupe(result);
    }

    private static IEnumerable<SmartAction> ActionsForCoin(AiCoinRef coin, string block, string source, bool includeResearch)
    {
        bool buy = BuyWords.IsMatch(block) || LongWord.IsMatch(block), sell = SellWords.IsMatch(block) || ShortWord.IsMatch(block);
        double? price = coin.Price > 0 ? coin.Price : null;

        double? entry = FirstLevel(EntryLevel, block, price);
        double? stop = FirstLevel(StopLevel, block, price);
        double? target = FirstLevel(TargetLevel, block, price);
        double? support = FirstLevel(SupportLevel, block, price);
        double? resistance = FirstLevel(ResistanceLevel, block, price);

        // Referentie voor de richting: genoemde entry, anders het steunniveau (instappen op steun), anders de koers.
        double? reference = entry ?? support ?? price;

        // Richting voor de paper trade: expliciet short/verkoop wint, anders long bij koopintentie of niveaus.
        string? direction = sell && !buy ? "Short"
                          : buy ? "Long"
                          : (entry ?? stop ?? target) is not null ? InferDirection(reference, stop, target) : null;

        // Niveaus toetsen aan de prijs waartegen de paper trade vult: de genoemde entry (limit), anders de actuele koers.
        var (pEntry, pStop, pTarget) = direction is null ? (null, null, null) : ValidLevels(direction, entry ?? price ?? reference, stop, target);

        var common = new SmartAction { Symbol = coin.Symbol.ToUpperInvariant(), CoinApiId = coin.ApiId, CoinName = coin.Name, Source = source };

        if (buy)
            yield return common with { Kind = SmartActionKind.Buy, Reason = "koopintentie herkend" };
        if (sell && coin.IsAsset)
            yield return common with { Kind = SmartActionKind.Sell, Reason = "verkoopintentie herkend" };

        if (direction is not null)
            yield return common with
            {
                Kind = SmartActionKind.PaperTrade, Direction = direction,
                Entry = entry is not null ? pEntry : null, StopLoss = pStop, Target = pTarget,
                Reason = pStop is null && pTarget is null ? "eerst oefenen met nepgeld" : "niveaus uit de tekst",
            };

        double? lvlBuy = entry ?? support, lvlTp = target ?? resistance;
        if (lvlBuy is not null || stop is not null || lvlTp is not null)
            yield return common with
            {
                Kind = SmartActionKind.PriceLevels, Entry = lvlBuy, StopLoss = stop, Target = lvlTp,
                Reason = "als koop-, stop- en winstniveau",
            };

        if (!includeResearch) yield break;
        yield return common with { Kind = SmartActionKind.TradeAdvies };
        yield return common with { Kind = SmartActionKind.Fundamentals };
        yield return common with { Kind = SmartActionKind.TradingView };
        yield return common with { Kind = SmartActionKind.SaveNote };
    }

    private static string InferDirection(double? entry, double? stop, double? target)
    {
        if (entry is > 0 && stop is > 0) return stop < entry ? "Long" : "Short";
        if (entry is > 0 && target is > 0) return target > entry ? "Long" : "Short";
        return "Long";
    }

    /// <summary>Alleen niveaus die bij de richting passen (Long: stop &lt; entry &lt; doel).</summary>
    public static (double? Entry, double? Stop, double? Target) ValidLevels(string direction, double? entry, double? stop, double? target)
    {
        if (entry is not > 0) return (null, null, null);
        bool longDir = direction == "Long";
        double? s = stop is > 0 && (longDir ? stop < entry : stop > entry) ? stop : null;
        double? t = target is > 0 && (longDir ? target > entry : target < entry) ? target : null;
        return (entry, s, t);
    }

    private static double? FirstLevel(Regex rx, string text, double? price)
    {
        foreach (Match m in rx.Matches(text))
        {
            var v = AiNumberParser.Pick(AiNumberParser.Candidates(m.Groups[1].Value, m.Groups[2].Value), price);
            if (v is not > 0) continue;
            // Moet in de buurt van de koers liggen (geen percentages, jaartallen of marktwaardes).
            if (price is > 0 && (v < price / 5 || v > price * 5)) continue;
            return v;
        }
        return null;
    }

    private static IEnumerable<string> UnknownCoins(string text, CoinIndex index)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in DollarTicker.Matches(text))
        {
            var sym = m.Groups[1].Value;
            if (sym.All(char.IsDigit) || index.Knows(sym) || TradeSetupGate.IsStablecoin(sym)) continue;
            if (seen.Add(sym)) yield return sym;
        }
        foreach (Match m in AddPhrase.Matches(text))
        {
            var word = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
            if (word.Length < 2 || index.Knows(word) || TradeSetupGate.IsStablecoin(word)) continue;
            if (seen.Add(word)) yield return word;
        }
    }

    private static List<SmartAction> Dedupe(List<SmartAction> actions)
    {
        var seen = new HashSet<string>();
        var list = new List<SmartAction>();
        foreach (var a in actions)
            if (seen.Add($"{a.Kind}|{a.Symbol}|{a.SearchText}".ToUpperInvariant())) list.Add(a);
        return list;
    }

    /// <summary>Voeg acties samen: eerdere lijsten winnen bij dezelfde soort + munt (bijv. AI-voorstel boven herkenning).</summary>
    public static List<SmartAction> Merge(params IEnumerable<SmartAction>[] lists) =>
        Dedupe(lists.SelectMany(l => l).ToList());

    // ── Munt-herkenning ──────────────────────────────────────────────────────

    private sealed class CoinIndex
    {
        private readonly Dictionary<string, AiCoinRef> _bySymbol = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<(Regex Rx, AiCoinRef Coin, int Len)> _names = new();

        public CoinIndex(IReadOnlyList<AiCoinRef> coins)
        {
            // Bij dubbele symbolen wint de munt in bezit, daarna de eerste (bibliotheek staat op rang).
            foreach (var c in coins.OrderByDescending(c => c.IsAsset))
                if (!string.IsNullOrWhiteSpace(c.Symbol) && !_bySymbol.ContainsKey(c.Symbol.Trim()))
                    _bySymbol[c.Symbol.Trim()] = c;

            foreach (var c in coins)
            {
                var name = c.Name?.Trim() ?? string.Empty;
                if (name.Length < 3 || name.Equals(c.Symbol, StringComparison.OrdinalIgnoreCase)) continue;
                _names.Add((new Regex(@"(?<![\p{L}\d])" + Regex.Escape(name) + @"(?![\p{L}\d])", RegexOptions.IgnoreCase), c, name.Length));
            }
            // Langste naam eerst ("Bitcoin Cash" vóór "Bitcoin").
            _names.Sort((a, b) => b.Len.CompareTo(a.Len));
        }

        public bool Knows(string word) =>
            _bySymbol.ContainsKey(word) || _names.Any(n => n.Coin.Name.Equals(word, StringComparison.OrdinalIgnoreCase));

        public List<AiCoinRef> Find(string sentence)
        {
            var found = new List<AiCoinRef>();
            var masked = sentence;

            foreach (var (rx, coin, _) in _names)
            {
                if (!rx.IsMatch(masked)) continue;
                if (!found.Contains(coin)) found.Add(coin);
                masked = rx.Replace(masked, m => new string(' ', m.Length));   // "Bitcoin Cash" niet ook als "Bitcoin" tellen
            }

            foreach (Match m in Regex.Matches(masked, @"(?<![\w$])(\$)?([A-Za-z][A-Za-z0-9]{0,9})(?![\w])"))
            {
                bool dollar = m.Groups[1].Success;
                var word = m.Groups[2].Value;
                if (!_bySymbol.TryGetValue(word, out var coin)) continue;
                // Zonder $: alleen exact in hoofdletters, min. 2 tekens en geen gewone afkorting.
                if (!dollar && (word != word.ToUpperInvariant() || word.Length < 2 || TickerStopwords.Contains(word))) continue;
                if (!found.Contains(coin)) found.Add(coin);
            }
            return found;
        }
    }
}
