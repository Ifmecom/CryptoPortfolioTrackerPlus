using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;
using CryptoPortfolioTracker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Xaml.Controls;
using Serilog;
using Serilog.Core;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace CryptoPortfolioTracker.ViewModels;

public partial class SourcesViewModel : BaseViewModel
{
    private static readonly ILogger Logger = Log.Logger.ForContext(Constants.SourceContextPropertyName, nameof(SourcesViewModel).PadRight(22));

    private readonly ISourcesService   _sourcesService;
    private readonly PortfolioService  _portfolioService;
    private readonly ISentimentService _sentimentService;

    [ObservableProperty] private ObservableCollection<BronSource> sources = new();
    [ObservableProperty] private BronSource? selectedSource;
    [ObservableProperty] private string portfolioName = string.Empty;
    [ObservableProperty] private string statusMessage = string.Empty;

    // ── Sentiment collection state ────────────────────────────────────────────
    [ObservableProperty] private bool   isCollecting;
    [ObservableProperty] private string lastRunText  = "Nog niet gestart";
    [ObservableProperty] private int    totalReadings;
    [ObservableProperty] private int    readings24H;

    // ── Edit form ────────────────────────────────────────────────────────────
    [ObservableProperty] private bool    isEditPanelVisible;
    [ObservableProperty] private string  editHandle  = string.Empty;
    [ObservableProperty] private string  editUrl     = string.Empty;
    [ObservableProperty] private int     editTypeIndex;          // maps to SentimentSource enum
    [ObservableProperty] private double  editReliability = 0.8;
    [ObservableProperty] private bool    editIsActive = true;
    [ObservableProperty] private bool    isNewSource;
    [ObservableProperty] private string  editPanelTitle = "Bron toevoegen";

    public SourcesViewModel(ISourcesService sourcesService,
                            PortfolioService portfolioService,
                            ISentimentService sentimentService,
                            Settings appSettings)
        : base(appSettings)
    {
        _sourcesService   = sourcesService;
        _portfolioService = portfolioService;
        _sentimentService = sentimentService;
    }

    public async Task ViewLoading()
    {
        PortfolioName = _portfolioService.CurrentPortfolio?.Name ?? string.Empty;

        // Subscribe to service state changes
        _sentimentService.StateChanged += OnSentimentStateChanged;
        SyncSentimentState();

        await _sourcesService.SeedDefaultsIfEmptyAsync();
        await RefreshAsync();
        await RefreshReadingStatsAsync();
    }

    public void Terminate()
    {
        _sentimentService.StateChanged -= OnSentimentStateChanged;
    }

    private void OnSentimentStateChanged(object? sender, EventArgs e)
    {
        // Marshal to UI thread
        SyncSentimentState();
        _ = RefreshReadingStatsAsync();
    }

    private void SyncSentimentState()
    {
        IsCollecting = _sentimentService.IsCollecting;
        LastRunText  = _sentimentService.LastRunAt.HasValue
            ? $"Laatste run: {_sentimentService.LastRunAt.Value.ToLocalTime():HH:mm} — {_sentimentService.LastRunStatus}"
            : _sentimentService.LastRunStatus;
    }

    private async Task RefreshAsync()
    {
        var list = await _sourcesService.GetAllAsync();
        Sources = new ObservableCollection<BronSource>(list);
        StatusMessage = $"{Sources.Count} bron(nen) geladen";
    }

    private async Task RefreshReadingStatsAsync()
    {
        try
        {
            var context = _portfolioService.Context;
            TotalReadings = await context.SentimentReadings.CountAsync();
            var since24H  = DateTime.UtcNow - TimeSpan.FromHours(24);
            Readings24H   = await context.SentimentReadings.CountAsync(r => r.Timestamp >= since24H);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "SourcesViewModel: failed to load reading stats");
        }
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanRunCollection))]
    private async Task RunCollectionNow()
    {
        await _sentimentService.RunNowAsync();
        await RefreshReadingStatsAsync();
    }

    private bool CanRunCollection() => !IsCollecting;

    partial void OnIsCollectingChanged(bool value)
    {
        RunCollectionNowCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task PruneReadings()
    {
        // Prune is integrated into RunNowAsync; expose a manual prune via re-run
        // For now just refresh the stats (prune happens automatically after each run)
        await RefreshReadingStatsAsync();
        StatusMessage = $"Stats bijgewerkt — {TotalReadings} readings in totaal.";
    }

    [RelayCommand]
    private void OpenAddPanel()
    {
        IsNewSource      = true;
        EditPanelTitle   = "Bron toevoegen";
        EditHandle       = string.Empty;
        EditUrl          = string.Empty;
        EditTypeIndex    = 0;
        EditReliability  = 0.8;
        EditIsActive     = true;
        IsEditPanelVisible = true;
    }

    [RelayCommand]
    private void OpenEditPanel(BronSource source)
    {
        if (source is null) return;
        SelectedSource  = source;
        IsNewSource     = false;
        EditPanelTitle  = "Bron bewerken";
        EditHandle      = source.Handle;
        EditUrl         = source.Url;
        EditTypeIndex   = (int)source.Type;
        EditReliability = source.ReliabilityScore;
        EditIsActive    = source.IsActive;
        IsEditPanelVisible = true;
    }

    [RelayCommand]
    private void CloseEditPanel()
    {
        IsEditPanelVisible = false;
        SelectedSource = null;
    }

    [RelayCommand]
    private async Task SaveSource()
    {
        if (string.IsNullOrWhiteSpace(EditHandle))
        {
            StatusMessage = "Vul een naam/handle in.";
            return;
        }

        var source = new BronSource
        {
            Type             = (SentimentSource)EditTypeIndex,
            Handle           = EditHandle.Trim(),
            Url              = EditUrl.Trim(),
            ReliabilityScore = EditReliability,
            IsActive         = EditIsActive,
        };

        try
        {
            if (IsNewSource)
            {
                await _sourcesService.AddAsync(source);
                StatusMessage = $"'{source.Handle}' toegevoegd.";
            }
            else
            {
                source.Id = SelectedSource!.Id;
                await _sourcesService.UpdateAsync(source);
                StatusMessage = $"'{source.Handle}' bijgewerkt.";
            }

            IsEditPanelVisible = false;
            await RefreshAsync();
        }
        catch (System.Exception ex)
        {
            Logger.Error(ex, "SaveSource failed");
            StatusMessage = $"Fout: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteSource(BronSource source)
    {
        if (source is null) return;
        try
        {
            await _sourcesService.DeleteAsync(source);
            StatusMessage = $"'{source.Handle}' verwijderd.";
            await RefreshAsync();
        }
        catch (System.Exception ex)
        {
            Logger.Error(ex, "DeleteSource failed");
            StatusMessage = $"Fout: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ToggleActive(BronSource source)
    {
        if (source is null) return;
        try
        {
            await _sourcesService.ToggleActiveAsync(source);
            await RefreshAsync();
        }
        catch (System.Exception ex)
        {
            Logger.Error(ex, "ToggleActive failed");
        }
    }
}
