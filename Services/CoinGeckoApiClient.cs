using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CryptoPortfolioTracker.Infrastructure.Response.Coins;
using Flurl;
using Flurl.Http;
using Newtonsoft.Json;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Thin REST-client voor de CoinGecko v3 API. Abstraheert de drie endpoints
/// die de applicatie gebruikt, zodat toekomstige uitbreidingen consistent zijn.
/// Zie https://docs.coingecko.com/reference/introduction
/// </summary>
public class CoinGeckoApiClient
{
    private const string UserAgent = "Mozilla/5.0 (compatible; AcmeInc/1.0)";

    private readonly string _baseUrl;
    private readonly string _apiKey;

    public CoinGeckoApiClient(string baseUrl, string apiKey)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _apiKey = apiKey;
    }

    /// <summary>GET /coins/list — alle coins met id, symbol en naam.</summary>
    public async Task<List<CoinList>> GetCoinListAsync(CancellationToken ct = default)
    {
        var request = BuildRequest("coins/list");
        return await request.GetJsonAsync<List<CoinList>>(ct);
    }

    /// <summary>GET /coins/{id} — volledige coin-details inclusief market_data.</summary>
    public async Task<CoinFullDataById> GetCoinDetailsAsync(string coinId, CancellationToken ct = default)
    {
        var request = BuildRequest("coins", coinId);
        return await request.GetJsonAsync<CoinFullDataById>(ct);
    }

    /// <summary>
    /// GET /coins/markets — marktdata voor een komma-gescheiden lijst van coin-id's.
    /// Retourneert prijs, marktcap, rank en procentuele veranderingen.
    /// </summary>
    public async Task<List<CoinMarkets>> GetCoinMarketsAsync(
        string coinIds,
        int perPage,
        CancellationToken ct = default)
    {
        var request = BuildRequest("coins/markets")
            .SetQueryParam("vs_currency", "usd")
            .SetQueryParam("ids", coinIds)
            .SetQueryParam("order", "market_cap_desc")
            .SetQueryParam("page", 1)
            .SetQueryParam("per_page", perPage)
            .SetQueryParam("sparkline", "false")
            .SetQueryParam("price_change_percentage", "24h,30d,1y");

        return await request.GetJsonAsync<List<CoinMarkets>>(ct);
    }

    // ------------------------------------------------------------------ //

    private IFlurlRequest BuildRequest(params string[] segments)
    {
        var url = _baseUrl.AppendPathSegments(segments);

        if (!string.IsNullOrEmpty(_apiKey))
            url = url.SetQueryParam("x_cg_demo_api_key", _apiKey);

        return url.WithHeader("User-Agent", UserAgent);
    }
}
