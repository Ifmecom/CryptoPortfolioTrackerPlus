using CryptoPortfolioTracker.Configuration;
using CryptoPortfolioTracker.Models;
using Microsoft.Data.Sqlite;
using Serilog;
using Serilog.Core;
using System.Net.Http;
using System.Text.Json;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Stores watchlist entries in the <c>WatchlistCoins</c> SQLite table
/// (created by <see cref="PortfolioService.ApplyPlusSchemaAsync"/>).
/// Also provides a CoinGecko search for adding new coins.
///
/// Uses a dedicated <see cref="SqliteConnection"/> built from
/// <see cref="AppConstants"/> to avoid a dependency on the EF Core
/// relational extension assembly for GetDbConnection().
/// </summary>
public class WatchlistService : IWatchlistService
{
    private static readonly ILogger Logger =
        Log.Logger.ForContext(Constants.SourceContextPropertyName,
            nameof(WatchlistService).PadRight(22));

    private readonly PortfolioService _portfolio;
    private static readonly HttpClient _http;

    static WatchlistService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "CryptoPortfolioTracker/1.0 (Windows; +https://github.com/RemeJuan)");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    private const string CoinGeckoSearch =
        "https://api.coingecko.com/api/v3/search?query={0}";

    public WatchlistService(PortfolioService portfolio)
    {
        _portfolio = portfolio;
    }

    // =========================================================================
    // CRUD
    // =========================================================================

    public async Task<List<WatchlistItem>> GetAllAsync()
    {
        var items = new List<WatchlistItem>();
        await using var conn = OpenConnection();
        if (conn is null) return items;

        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT Id, ApiId, Name, Symbol, ImageUri, AddedAt " +
                "FROM WatchlistCoins ORDER BY AddedAt DESC";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new WatchlistItem
                {
                    Id       = reader.GetInt32(0),
                    ApiId    = reader.GetString(1),
                    Name     = reader.GetString(2),
                    Symbol   = reader.GetString(3),
                    ImageUri = reader.GetString(4),
                    AddedAt  = DateTime.Parse(reader.GetString(5)),
                });
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "WatchlistService.GetAllAsync failed");
        }
        return items;
    }

    public async Task AddAsync(WatchlistItem item)
    {
        await using var conn = OpenConnection();
        if (conn is null) return;

        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "INSERT OR IGNORE INTO WatchlistCoins (ApiId, Name, Symbol, ImageUri, AddedAt) " +
                "VALUES ($apiId, $name, $symbol, $imageUri, $addedAt)";
            cmd.Parameters.AddWithValue("$apiId",    item.ApiId);
            cmd.Parameters.AddWithValue("$name",     item.Name);
            cmd.Parameters.AddWithValue("$symbol",   item.Symbol);
            cmd.Parameters.AddWithValue("$imageUri", item.ImageUri);
            cmd.Parameters.AddWithValue("$addedAt",  item.AddedAt.ToString("o"));
            await cmd.ExecuteNonQueryAsync();
            Logger.Information("Watchlist: added {Name} ({ApiId})", item.Name, item.ApiId);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "WatchlistService.AddAsync failed for {ApiId}", item.ApiId);
        }
    }

    public async Task RemoveAsync(string apiId)
    {
        await using var conn = OpenConnection();
        if (conn is null) return;

        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM WatchlistCoins WHERE ApiId = $apiId";
            cmd.Parameters.AddWithValue("$apiId", apiId);
            await cmd.ExecuteNonQueryAsync();
            Logger.Information("Watchlist: removed {ApiId}", apiId);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "WatchlistService.RemoveAsync failed for {ApiId}", apiId);
        }
    }

    public async Task<bool> ExistsAsync(string apiId)
    {
        await using var conn = OpenConnection();
        if (conn is null) return false;

        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(1) FROM WatchlistCoins WHERE ApiId = $apiId";
            cmd.Parameters.AddWithValue("$apiId", apiId);
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt64(result) > 0;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "WatchlistService.ExistsAsync failed for {ApiId}", apiId);
            return false;
        }
    }

    // =========================================================================
    // CoinGecko search
    // =========================================================================

    public async Task<List<WatchlistSearchResult>> SearchCoinsAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return new();

        try
        {
            var url      = string.Format(CoinGeckoSearch, Uri.EscapeDataString(query.Trim()));
            var response = await _http.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                Logger.Warning("WatchlistService: CoinGecko rate-limited (429) for '{Query}'", query);
                throw new InvalidOperationException("CoinGecko rate limit bereikt — wacht even en probeer opnieuw.");
            }

            if (!response.IsSuccessStatusCode)
            {
                Logger.Warning("WatchlistService: CoinGecko returned {Code} for '{Query}'",
                    (int)response.StatusCode, query);
                return new();
            }

            var json      = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("coins", out var coinsArr))
                return new();

            return coinsArr.EnumerateArray()
                .Select(c => new WatchlistSearchResult
                {
                    ApiId         = c.TryGetProperty("id",             out var v) ? v.GetString() ?? "" : "",
                    Name          = c.TryGetProperty("name",           out var n) ? n.GetString() ?? "" : "",
                    Symbol        = c.TryGetProperty("symbol",         out var s) ? s.GetString()?.ToUpperInvariant() ?? "" : "",
                    ImageUri      = c.TryGetProperty("thumb",          out var i) ? i.GetString() ?? "" : "",
                    MarketCapRank = c.TryGetProperty("market_cap_rank", out var r) && r.ValueKind != JsonValueKind.Null
                                       ? r.GetInt32() : 99999,
                })
                .Where(r => !string.IsNullOrEmpty(r.ApiId))
                .OrderBy(r => r.MarketCapRank)
                .Take(10)
                .ToList();
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "WatchlistService.SearchCoinsAsync failed for '{Query}'", query);
            return new();
        }
    }

    // =========================================================================
    // Connection helper — opens a fresh SqliteConnection to the active portfolio
    // =========================================================================

    /// <summary>
    /// Creates and opens a <see cref="SqliteConnection"/> to the active portfolio
    /// database file.  Returns null when the portfolio is not yet initialised.
    /// The caller is responsible for disposing (use <c>await using</c>).
    /// </summary>
    private SqliteConnection? OpenConnection()
    {
        // Derive the active DB path from PortfolioService the same way
        // PortfolioContextFactory does: AppDataPath + active portfolio filename.
        var activeDb = _portfolio.ActivePortfolioPath;
        if (string.IsNullOrEmpty(activeDb)) return null;

        var connStr = $"Data Source={activeDb};Pooling=False";
        var conn    = new SqliteConnection(connStr);
        conn.Open();
        return conn;
    }
}
