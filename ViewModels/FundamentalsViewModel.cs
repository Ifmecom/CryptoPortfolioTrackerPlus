using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CryptoPortfolioTracker.Converters;
using CryptoPortfolioTracker.Models;
using CryptoPortfolioTracker.Services;
using Microsoft.UI.Dispatching;
using Serilog;
using Serilog.Core;

namespace CryptoPortfolioTracker.ViewModels;

public partial class FundamentalsViewModel : BaseViewModel
{
    public static FundamentalsViewModel? Current;

    private readonly IFundamentalsService _fundamentals;
    private readonly ILibraryService      _libraryService;
    private readonly DispatcherQueue?     _dispatcher;

    [ObservableProperty] private ObservableCollection<FundamentalRow> rows = new();
    [ObservableProperty] private string statusText = string.Empty;
    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private bool   isBusy;
    [ObservableProperty] private bool   onlyAnalyzed;
    [ObservableProperty] private bool   onlyFavorites;
    [ObservableProperty] private bool   isBatchRunning;

    public bool CanBatch => !IsBatchRunning;
    partial void OnIsBatchRunningChanged(bool value) => OnPropertyChanged(nameof(CanBatch));

    private List<FundamentalRow> _all = new();
    private readonly HashSet<string> _favorites = new(StringComparer.OrdinalIgnoreCase);
    private const int MaxFavorites = 10;

    public string FavoritesCountText => $"{_favorites.Count}/{MaxFavorites} favoriet";

    /// <summary>Versheidsdrempel in dagen — leest/schrijft de persistente instelling.</summary>
    public int FreshnessDays
    {
        get => AppSettings.FundamentalsFreshnessDays;
        set
        {
            int v = Math.Clamp(value, 1, 90);
            if (v == AppSettings.FundamentalsFreshnessDays) return;
            AppSettings.FundamentalsFreshnessDays = v;
            FundamentalRow.FreshnessDays = v;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FreshnessDaysValue));
            foreach (var r in _all) r.NotifyFreshnessChanged();
        }
    }

    /// <summary>Double-wrapper voor de NumberBox-binding.</summary>
    public double FreshnessDaysValue
    {
        get => FreshnessDays;
        set => FreshnessDays = (int)Math.Round(value);
    }

    // CoinGecko demo-tier: ruime marge tussen calls bij een batch-refresh.
    private const int BatchDelayMs = 2200;

    public bool HasRows => Rows.Count > 0;
    partial void OnRowsChanged(ObservableCollection<FundamentalRow> value) => OnPropertyChanged(nameof(HasRows));

    public FundamentalsViewModel(
        IFundamentalsService fundamentals,
        ILibraryService      libraryService,
        Settings             appSettings)
        : base(appSettings)
    {
        Current        = this;
        _fundamentals  = fundamentals;
        _libraryService = libraryService;
        _dispatcher    = DispatcherQueue.GetForCurrentThread();

        Logger = Log.Logger.ForContext(Constants.SourceContextPropertyName, nameof(FundamentalsViewModel).PadRight(22));
    }

    public Settings AppSettingsPublic => AppSettings;

    public async Task ViewLoadingAsync() => await LoadAsync();

    public void Terminate() => Current = null;

    /// <summary>Persisteert de handmatige due-diligence van een coin, herberekent de score en ververst de rij.</summary>
    public async Task SaveDueDiligenceAsync(FundamentalRow row)
    {
        if (row?.Data is null) return;
        try
        {
            await _fundamentals.SaveDueDiligenceAsync(row.Data);
            row.RaiseAllChanged();
            ApplyFilterAndSort();
            StatusText = $"{row.Symbol}: due-diligence opgeslagen — score {row.Data.TotalScore:0} ({row.Data.Verdict}), betrouwbaarheid {row.Data.Confidence:0}%.";
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Fundamentals: due-diligence opslaan mislukt voor {ApiId}", row.ApiId);
            StatusText = $"{row.Symbol}: opslaan mislukt — {ex.Message}";
        }
    }

    // ── Commands ────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task Refresh() => await LoadAsync();

    [RelayCommand]
    private async Task Analyze(FundamentalRow? row)
    {
        if (row is null || string.IsNullOrWhiteSpace(row.ApiId) || row.IsAnalyzing) return;
        row.IsAnalyzing = true;
        StatusText = $"Fundamentals ophalen voor {row.Symbol}…";
        try
        {
            var result = await _fundamentals.RefreshAsync(row.ApiId, row.Symbol, row.Name);
            if (result is not null)
            {
                row.Data = result;
                StatusText = $"{row.Symbol}: score {result.TotalScore:0} — {result.Verdict}";
            }
            else
            {
                StatusText = $"{row.Symbol}: ophalen mislukt (geen data van CoinGecko).";
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Fundamentals: analyse mislukt voor {ApiId}", row.ApiId);
            StatusText = $"{row.Symbol}: fout — {ex.Message}";
        }
        finally
        {
            row.IsAnalyzing = false;
        }
    }

    /// <summary>
    /// Haalt uitsluitend coins op die nog niet of langer dan <see cref="FundamentalRow.FreshnessDays"/>
    /// dagen geleden zijn geanalyseerd — verse coins worden overgeslagen om API-calls te besparen.
    /// </summary>
    [RelayCommand]
    private async Task RefreshStale()
    {
        var targets = _all.Where(r => !r.HasData || r.IsStale).ToList();
        if (targets.Count == 0)
        {
            StatusText = $"Alles is up-to-date (jonger dan {FundamentalRow.FreshnessDays} dagen).";
            return;
        }
        await BatchRefreshAsync(targets, "Ontbrekende/verouderde",
            $"Verse coins (< {FundamentalRow.FreshnessDays} dagen) overgeslagen.");
    }

    [RelayCommand]
    private async Task RefreshFavorites()
    {
        var targets = _all.Where(r => r.IsFavorite).ToList();
        if (targets.Count == 0)
        {
            StatusText = "Nog geen favorieten — markeer coins met de ster.";
            return;
        }
        await BatchRefreshAsync(targets, "Favorieten", string.Empty);
    }

    /// <summary>Markeer/ontmarkeer een coin als favoriet (max 10) en bewaar dit persistent.</summary>
    [RelayCommand]
    private void ToggleFavorite(FundamentalRow? row)
    {
        if (row is null || string.IsNullOrWhiteSpace(row.ApiId)) return;

        if (_favorites.Contains(row.ApiId))
        {
            _favorites.Remove(row.ApiId);
            row.IsFavorite = false;
        }
        else
        {
            if (_favorites.Count >= MaxFavorites)
            {
                StatusText = $"Maximaal {MaxFavorites} favorieten — verwijder er eerst een.";
                return;
            }
            _favorites.Add(row.ApiId);
            row.IsFavorite = true;
        }

        AppSettings.FundamentalsFavorites = string.Join(",", _favorites);
        OnPropertyChanged(nameof(FavoritesCountText));
        ApplyFilterAndSort();
    }

    private async Task BatchRefreshAsync(List<FundamentalRow> targets, string label, string skipNote)
    {
        if (IsBatchRunning) return;
        IsBatchRunning = true;
        int done = 0, ok = 0;
        try
        {
            foreach (var row in targets)
            {
                done++;
                StatusText = $"{label} ophalen… {done}/{targets.Count} — {row.Symbol}";
                row.IsAnalyzing = true;
                try
                {
                    var result = await _fundamentals.RefreshAsync(row.ApiId, row.Symbol, row.Name);
                    if (result is not null) { row.Data = result; ok++; }
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "Fundamentals: batch-analyse mislukt voor {ApiId}", row.ApiId);
                }
                finally { row.IsAnalyzing = false; }

                if (done < targets.Count)
                    await Task.Delay(BatchDelayMs);
            }
            ApplyFilterAndSort();
            StatusText = $"Klaar — {ok}/{targets.Count} coins bijgewerkt. {skipNote}".TrimEnd();
        }
        finally { IsBatchRunning = false; }
    }

    [RelayCommand]
    private void Sort()
    {
        ApplyFilterAndSort();
    }

    partial void OnSearchTextChanged(string value)    => ApplyFilterAndSort();
    partial void OnOnlyAnalyzedChanged(bool value)    => ApplyFilterAndSort();
    partial void OnOnlyFavoritesChanged(bool value)   => ApplyFilterAndSort();

    // ── Internals ─────────────────────────────────────────────────────────────

    private async Task LoadAsync()
    {
        IsBusy = true;
        StatusText = "Laden…";
        try
        {
            // Versheidsdrempel + favorieten uit de instellingen overnemen
            FundamentalRow.FreshnessDays = AppSettings.FundamentalsFreshnessDays;
            OnPropertyChanged(nameof(FreshnessDays));
            LoadFavorites();

            var coins = new List<Coin>();
            var coinsResult = await _libraryService.GetCoinsFromContext();
            coinsResult.IfSucc(cs => coins = cs.Where(c => !string.IsNullOrEmpty(c.ApiId)).ToList());

            var stored = await _fundamentals.GetAllAsync();
            var byApiId = stored.ToDictionary(s => s.ApiId, StringComparer.OrdinalIgnoreCase);

            var built = coins
                .GroupBy(c => c.ApiId)
                .Select(g => g.First())
                .Select(c =>
                {
                    byApiId.TryGetValue(c.ApiId, out var data);
                    return new FundamentalRow(c) { Data = data, IsFavorite = _favorites.Contains(c.ApiId) };
                })
                .ToList();

            _all = built;
            _dispatcher?.TryEnqueue(() =>
            {
                ApplyFilterAndSort();
                OnPropertyChanged(nameof(FavoritesCountText));
                int analyzed = _all.Count(r => r.HasData);
                StatusText = $"{_all.Count} coins — {analyzed} geanalyseerd. Klik 'Analyseer' bij een coin om fundamentals op te halen.";
            });
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Fundamentals: LoadAsync mislukt");
            StatusText = $"Laden mislukt: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    private void LoadFavorites()
    {
        _favorites.Clear();
        var csv = AppSettings.FundamentalsFavorites ?? string.Empty;
        foreach (var id in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (_favorites.Count >= MaxFavorites) break;
            _favorites.Add(id);
        }
    }

    private void ApplyFilterAndSort()
    {
        IEnumerable<FundamentalRow> q = _all;

        if (OnlyFavorites)
            q = q.Where(r => r.IsFavorite);

        if (OnlyAnalyzed)
            q = q.Where(r => r.HasData);

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            q = q.Where(r =>
                (r.Name?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.Symbol?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        // Favorieten bovenaan, dan geanalyseerde coins (hoogste score boven), dan op naam.
        var ordered = q
            .OrderByDescending(r => r.IsFavorite)
            .ThenByDescending(r => r.HasData)
            .ThenByDescending(r => r.TotalScore)
            .ThenBy(r => r.Name)
            .ToList();

        Rows = new ObservableCollection<FundamentalRow>(ordered);
    }
}

/// <summary>
/// UI-rij voor de Fundamentals-pagina: koppelt een coin aan zijn (optionele)
/// opgeslagen <see cref="CoinFundamentals"/> en levert kant-en-klare display-helpers.
/// </summary>
public partial class FundamentalRow : ObservableObject
{
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush BrushExceptional = New(0x1A, 0x5C, 0x2E);
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush BrushStrong      = New(0x27, 0x96, 0x42);
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush BrushPromising   = New(0x3C, 0xB3, 0x71);
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush BrushSpeculative = New(0xE6, 0x7E, 0x22);
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush BrushHighRisk     = New(0xCD, 0x5C, 0x5C);
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush BrushAvoid        = New(0xC0, 0x39, 0x2B);
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush BrushGrey         = New(0x80, 0x80, 0x80);

    private static Microsoft.UI.Xaml.Media.SolidColorBrush New(byte r, byte g, byte b)
        => new(Windows.UI.Color.FromArgb(0xFF, r, g, b));

    public FundamentalRow(Coin coin)
    {
        ApiId    = coin.ApiId;
        Name     = coin.Name;
        Symbol   = (coin.Symbol ?? string.Empty).ToUpperInvariant();
        ImageUri = coin.ImageUri;
    }

    public string ApiId    { get; }
    public string Name     { get; }
    public string Symbol   { get; }
    public string ImageUri { get; }

    [ObservableProperty] private CoinFundamentals? data;
    [ObservableProperty] private bool isAnalyzing;
    [ObservableProperty] private bool isFavorite;

    partial void OnDataChanged(CoinFundamentals? value) => NotifyAll();
    partial void OnIsAnalyzingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanAnalyze));
        OnPropertyChanged(nameof(AnalyzeLabel));
    }
    partial void OnIsFavoriteChanged(bool value)
    {
        OnPropertyChanged(nameof(FavoriteGlyph));
        OnPropertyChanged(nameof(FavoriteBrush));
    }

    public string FavoriteGlyph => IsFavorite ? "★" : "☆";
    public Microsoft.UI.Xaml.Media.SolidColorBrush FavoriteBrush => IsFavorite ? BrushGold : BrushGrey;

    /// <summary>Herwaardeer alle weergave-properties (na opslaan van due-diligence).</summary>
    public void RaiseAllChanged() => NotifyAll();

    /// <summary>Herwaardeer de versheid-afhankelijke weergave (na wijziging van de drempel).</summary>
    public void NotifyFreshnessChanged()
    {
        OnPropertyChanged(nameof(IsStale));
        OnPropertyChanged(nameof(IsFresh));
        OnPropertyChanged(nameof(AgeText));
        OnPropertyChanged(nameof(StalenessText));
        OnPropertyChanged(nameof(FreshnessBrush));
    }

    /// <summary>Na hoeveel dagen opgeslagen fundamentals als "verouderd" gelden (instelbaar).</summary>
    public static int FreshnessDays = 7;

    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush BrushGold = New(0xFF, 0xC1, 0x07);

    private void NotifyAll()
    {
        foreach (var p in new[]
        {
            nameof(HasData), nameof(TotalScore), nameof(TotalScoreText), nameof(DataScoreText),
            nameof(Verdict), nameof(ScoreBrush), nameof(RankText), nameof(MarketCapText),
            nameof(FdvText), nameof(VolumeText), nameof(VolMcText), nameof(FdvMcText),
            nameof(CirculatingText), nameof(AthText), nameof(TvlText), nameof(HasTvl), nameof(CommunityText), nameof(DevText),
            nameof(UpdatedText), nameof(AnalyzeLabel), nameof(IsStale), nameof(IsFresh),
            nameof(AgeText), nameof(FreshnessBrush), nameof(StalenessText),
        })
            OnPropertyChanged(p);
    }

    public bool   HasData     => Data is not null;
    public bool   CanAnalyze  => !IsAnalyzing;
    public string AnalyzeLabel => IsAnalyzing ? "Bezig…" : (HasData ? "↻ Vernieuw" : "Analyseer");

    public double TotalScore     => Data?.TotalScore ?? 0;
    public string TotalScoreText => HasData ? $"{Data!.TotalScore:0}" : "—";
    public string DataScoreText  => HasData ? $"Data {Data!.DataScore:0}" : "";
    public string Verdict        => HasData ? Data!.Verdict : "Niet geanalyseerd";

    public Microsoft.UI.Xaml.Media.SolidColorBrush ScoreBrush => !HasData ? BrushGrey : Data!.TotalScore switch
    {
        >= 90 => BrushExceptional,
        >= 80 => BrushStrong,
        >= 70 => BrushPromising,
        >= 60 => BrushSpeculative,
        >= 50 => BrushHighRisk,
        _     => BrushAvoid,
    };

    public string RankText      => HasData && Data!.MarketCapRank is { } r ? $"#{r}" : "—";
    public string MarketCapText => HasData ? Functions.FormatUsdCompact(Data!.MarketCap) : "—";
    public string FdvText       => HasData ? Functions.FormatUsdCompact(Data!.Fdv) : "—";
    public string VolumeText    => HasData ? Functions.FormatUsdCompact(Data!.TotalVolume) : "—";

    public string VolMcText => HasData && Data!.MarketCap > 0
        ? $"{Data.TotalVolume / Data.MarketCap * 100:0.0}%" : "—";
    public string FdvMcText => HasData && Data!.MarketCap > 0 && Data.Fdv > 0
        ? Functions.FormatRatioX(Data.Fdv / Data.MarketCap) : "—";

    public string CirculatingText => HasData ? Functions.FormatSupply(Data!.CirculatingSupply) : "—";
    public string AthText  => HasData ? $"ATH {Functions.FormatPercentSigned(Data!.AthChangePct)}" : "";
    public string TvlText  => HasData && Data!.Tvl > 0 ? $"TVL {Functions.FormatUsdCompact(Data.Tvl)}" : "";
    public bool   HasTvl   => HasData && Data!.Tvl > 0;
    public string CommunityText => HasData ? $"𝕏 {Functions.FormatSupply(Data!.TwitterFollowers)}" : "";
    public string DevText  => HasData ? $"GH {Functions.FormatSupply(Data!.GithubStars)}★ · {Data!.CommitCount4Weeks} commits/4w" : "";

    public string UpdatedText => HasData && Data!.UpdatedAt > DateTime.MinValue
        ? $"Bijgewerkt: {Data.UpdatedAt.ToLocalTime():dd-MM-yy HH:mm}" : "";

    /// <summary>True als de data ouder is dan <see cref="FreshnessDays"/> dagen.</summary>
    public bool IsStale => HasData && Data!.UpdatedAt > DateTime.MinValue
        && (DateTime.UtcNow - Data.UpdatedAt).TotalDays > FreshnessDays;

    /// <summary>True als er recente (verse) data is.</summary>
    public bool IsFresh => HasData && !IsStale;

    /// <summary>Leeftijd van de data, bv. "3d geleden". Leeg zonder data.</summary>
    public string AgeText => HasData && Data!.UpdatedAt > DateTime.MinValue
        ? Functions.FormatAge(Data.UpdatedAt) : "";

    /// <summary>Toont "verouderd" wanneer de data ouder is dan de versheidsdrempel.</summary>
    public string StalenessText => IsStale ? "· verouderd" : "";

    public Microsoft.UI.Xaml.Media.SolidColorBrush FreshnessBrush =>
        IsStale ? BrushSpeculative : BrushGrey;
}
