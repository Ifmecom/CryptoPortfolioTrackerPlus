using CryptoPortfolioTracker.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CryptoPortfolioTracker.Services;

public class FearGreedService : IFearGreedService
{
    private static readonly ILogger Logger =
        Log.Logger.ForContext(Constants.SourceContextPropertyName, typeof(FearGreedService).Name.PadRight(22));

    private readonly PortfolioService _portfolioService;
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    private const string ApiUrl = "https://api.alternative.me/fng/?limit=1";

    public FearGreedService(PortfolioService portfolioService)
    {
        _portfolioService = portfolioService;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<FearGreedReading?> GetCurrentAsync(int maxAgeMinutes = 60)
    {
        try
        {
            var context = _portfolioService.Context;
            if (context is null)
            {
                Logger.Warning("FearGreed: Context is null — portfolio nog niet geladen");
                return null;
            }

            var latest = await context.FearGreedReadings
                .AsNoTracking()
                .OrderByDescending(r => r.Timestamp)
                .FirstOrDefaultAsync();

            if (latest is not null && (DateTime.UtcNow - latest.Timestamp).TotalMinutes < maxAgeMinutes)
                return latest;

            // Stale or no data — fetch fresh
            return await FetchAndStoreAsync();
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "GetCurrentAsync failed — returning null");
            return null;
        }
    }

    public async Task<FearGreedReading?> FetchAndStoreAsync()
    {
        try
        {
            var json = await _http.GetStringAsync(ApiUrl);
            var response = JsonSerializer.Deserialize<FngApiResponse>(json);

            if (response?.Data is null || response.Data.Length == 0)
            {
                Logger.Warning("FearGreed API returned empty data");
                return null;
            }

            var item = response.Data[0];
            var reading = new FearGreedReading
            {
                Value          = int.Parse(item.Value),
                Classification = item.ValueClassification ?? "Unknown",
                Timestamp      = DateTime.UtcNow,
            };

            var context = _portfolioService.Context;
            if (context is null) return reading; // return value even if we can't persist

            context.FearGreedReadings.Add(reading);
            await context.SaveChangesAsync();

            Logger.Information("FearGreed stored: {Value} ({Classification})", reading.Value, reading.Classification);
            return reading;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "FetchAndStoreAsync failed");
            return null;
        }
    }

    public async Task<List<FearGreedReading>> GetHistoryAsync(int days = 30)
    {
        try
        {
            var context = _portfolioService.Context;
            if (context is null) return new List<FearGreedReading>();

            var cutoff = DateTime.UtcNow.AddDays(-days);
            return await context.FearGreedReadings
                .AsNoTracking()
                .Where(r => r.Timestamp >= cutoff)
                .OrderByDescending(r => r.Timestamp)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "GetHistoryAsync failed");
            return new List<FearGreedReading>();
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // JSON deserialization helpers
    // ──────────────────────────────────────────────────────────────────────────

    private class FngApiResponse
    {
        [JsonPropertyName("data")]
        public FngDataItem[]? Data { get; set; }
    }

    private class FngDataItem
    {
        [JsonPropertyName("value")]
        public string Value { get; set; } = "0";

        [JsonPropertyName("value_classification")]
        public string? ValueClassification { get; set; }
    }
}
