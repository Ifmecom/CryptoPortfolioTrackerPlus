using CryptoPortfolioTracker.Services;
using Serilog;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace CryptoPortfolioTracker.Initializers;
public class IconCacheService
{
    private readonly string _iconsFolderPath;
    private readonly PortfolioService _portfolioService;
    private readonly ILogger _logger;
    private static readonly HttpClient _httpClient = new HttpClient();

    public IconCacheService(string iconsFolderPath, PortfolioService portfolioService, ILogger logger)
    {
        _iconsFolderPath = iconsFolderPath;
        _portfolioService = portfolioService;
        _logger = logger;
    }

    public async Task CacheLibraryIconsAsync()
    {
        if (Directory.Exists(_iconsFolderPath))
        {
            foreach (var file in Directory.GetFiles(_iconsFolderPath))
                File.Delete(file);
        }
        else
        {
            Directory.CreateDirectory(_iconsFolderPath);
        }

        var context = _portfolioService.Context;
        var coins = context?.Coins.Where(coin => !string.IsNullOrEmpty(coin.ImageUri)).ToList();

        if (coins == null) return;

        var seen = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var coin in coins)
        {
            var fileName = Path.GetFileName(coin.ImageUri.Split('?')[0]);
            if (fileName == "QuestionMarkBlue.png" || !seen.Add(fileName)) continue;

            var iconPath = Path.Combine(_iconsFolderPath, fileName);
            if (!await RetrieveCoinIconAsync(coin.ImageUri, iconPath))
            {
                _logger?.Warning("Failed to cache icon for {0}", coin.Name);
            }
        }
    }

    private async Task<bool> RetrieveCoinIconAsync(string imageUri, string iconPath)
    {
        try
        {
            var response = await _httpClient.GetAsync(imageUri);
            if (!response.IsSuccessStatusCode) return false;
            var bytes = await response.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(iconPath, bytes);
            return true;
        }
        catch
        {
            return false;
        }
    }
}