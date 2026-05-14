using System.Net.Http;
using System.Text.Json;
using CryptoPortfolioTracker.Models;
using Serilog;
using Serilog.Core;

namespace CryptoPortfolioTracker.Services;

public class BinanceDataService : IBinanceDataService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private const string BaseUrl = "https://api.binance.com/api/v3/klines";

    private static readonly ILogger Logger = Log.Logger.ForContext(
        Constants.SourceContextPropertyName, nameof(BinanceDataService).PadRight(22));

    // CoinGecko ApiId → Binance base asset symbol
    private static readonly Dictionary<string, string> IdToSymbol = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bitcoin"]                        = "BTC",
        ["ethereum"]                       = "ETH",
        ["solana"]                         = "SOL",
        ["binancecoin"]                    = "BNB",
        ["ripple"]                         = "XRP",
        ["cardano"]                        = "ADA",
        ["avalanche-2"]                    = "AVAX",
        ["polkadot"]                       = "DOT",
        ["chainlink"]                      = "LINK",
        ["litecoin"]                       = "LTC",
        ["bitcoin-cash"]                   = "BCH",
        ["uniswap"]                        = "UNI",
        ["stellar"]                        = "XLM",
        ["dogecoin"]                       = "DOGE",
        ["shiba-inu"]                      = "SHIB",
        ["matic-network"]                  = "MATIC",
        ["near"]                           = "NEAR",
        ["cosmos"]                         = "ATOM",
        ["algorand"]                       = "ALGO",
        ["filecoin"]                       = "FIL",
        ["internet-computer"]              = "ICP",
        ["hedera-hashgraph"]               = "HBAR",
        ["aptos"]                          = "APT",
        ["arbitrum"]                       = "ARB",
        ["optimism"]                       = "OP",
        ["injective-protocol"]             = "INJ",
        ["sui"]                            = "SUI",
        ["fetch-ai"]                       = "FET",
        ["the-graph"]                      = "GRT",
        ["aave"]                           = "AAVE",
        ["maker"]                          = "MKR",
        ["compound-governance-token"]      = "COMP",
        ["lido-dao"]                       = "LDO",
        ["render-token"]                   = "RNDR",
        ["theta-token"]                    = "THETA",
        ["vechain"]                        = "VET",
        ["tron"]                           = "TRX",
        ["kaspa"]                          = "KAS",
        ["ondo-finance"]                   = "ONDO",
        ["pepe"]                           = "PEPE",
        ["bonk"]                           = "BONK",
        ["worldcoin-wld"]                  = "WLD",
        ["the-sandbox"]                    = "SAND",
        ["decentraland"]                   = "MANA",
        ["axie-infinity"]                  = "AXS",
        ["gala"]                           = "GALA",
        ["flow"]                           = "FLOW",
        ["immutable-x"]                    = "IMX",
        ["starknet"]                       = "STRK",
        ["celestia"]                       = "TIA",
        ["sei-network"]                    = "SEI",
        ["pyth-network"]                   = "PYTH",
        ["jupiter-ag"]                     = "JUP",
        ["hyperliquid"]                    = "HYPE",
        ["pendle"]                         = "PENDLE",
        ["raydium"]                        = "RAY",
        ["artificial-superintelligence-alliance"] = "FET",
        ["bitcoin-sv"]                     = "BSV",
        ["monero"]                         = "XMR",
        ["ethereum-classic"]               = "ETC",
        ["toncoin"]                        = "TON",
        ["mantra-dao"]                     = "OM",
        ["virtual-protocol"]               = "VIRTUAL",
        ["movement"]                       = "MOVE",
    };

    public string ResolveBinanceSymbol(string coinApiId, string coinSymbol)
    {
        if (IdToSymbol.TryGetValue(coinApiId, out var baseAsset))
            return baseAsset + "USDT";

        // Fallback: uppercase symbol + USDT
        return coinSymbol.ToUpper() + "USDT";
    }

    public async Task<List<OhlcvBar>> GetKlinesAsync(string binanceSymbol, string interval, int limit = 200)
    {
        try
        {
            var url = $"{BaseUrl}?symbol={binanceSymbol}&interval={interval}&limit={limit}";
            var response = await _http.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                Logger.Debug("Binance klines {Symbol}/{Interval} → {Code}", binanceSymbol, interval, (int)response.StatusCode);
                return new List<OhlcvBar>();
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var bars = new List<OhlcvBar>(limit);

            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var openTime = DateTimeOffset.FromUnixTimeMilliseconds(row[0].GetInt64()).UtcDateTime;
                bars.Add(new OhlcvBar
                {
                    Date   = openTime,
                    Open   = double.Parse(row[1].GetString()!, System.Globalization.CultureInfo.InvariantCulture),
                    High   = double.Parse(row[2].GetString()!, System.Globalization.CultureInfo.InvariantCulture),
                    Low    = double.Parse(row[3].GetString()!, System.Globalization.CultureInfo.InvariantCulture),
                    Close  = double.Parse(row[4].GetString()!, System.Globalization.CultureInfo.InvariantCulture),
                    Volume = double.Parse(row[5].GetString()!, System.Globalization.CultureInfo.InvariantCulture),
                });
            }

            Logger.Debug("Binance {Symbol}/{Interval}: {Count} bars", binanceSymbol, interval, bars.Count);
            return bars;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to fetch Binance klines {Symbol}/{Interval}", binanceSymbol, interval);
            return new List<OhlcvBar>();
        }
    }
}
