using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CryptoPortfolioTracker.Controls;
using CryptoPortfolioTracker.Converters;
using CryptoPortfolioTracker.Helpers;
using CryptoPortfolioTracker.Dialogs;
using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Infrastructure;
using CryptoPortfolioTracker.Models;
using CryptoPortfolioTracker.Services;
using CryptoPortfolioTracker.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Xaml.Controls;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Security.Authentication.Web.Provider;
using WinUI3Localizer;

namespace CryptoPortfolioTracker.ViewModels;

[ObservableRecipient]
public partial class DashboardViewModel : BaseViewModel
{
    public static DashboardViewModel? Current;
    private static ILocalizer loc = Localizer.Get();
    public readonly IDashboardService _dashboardService;
    private readonly IGraphService _graphService;
    private readonly IPriceLevelService _priceLevelService;
    public Settings AppSettings => base.AppSettings; // expose AppSettings publicly so that it can be used in dialogs called by this ViewModel

    [ObservableProperty] public string portfolioName = string.Empty;
    [ObservableProperty] public Portfolio currentPortfolio;

    async partial void OnCurrentPortfolioChanged(Portfolio? oldValue, Portfolio newValue)
    {
        await _dashboardService.CalculateIndicatorsAllCoins();
        await UpdateDashboardAsync();
        PortfolioName = newValue.Name;
    }

    [ObservableProperty] private string glyph = "\uEE47";
    [ObservableProperty] private string glyphPrivacy = "\uE890";

    [ObservableProperty] private FullScreenMode toggleFsMode = FullScreenMode.None;

    partial void OnToggleFsModeChanged(FullScreenMode value)
    {
        Glyph = value == FullScreenMode.None ? "\uEE47" : "\uEE49";
    }

    private static Func<double, string> labelerYAxis = value => string.Format("$ {0:N0}", value);

    [ObservableProperty] private bool isPrivacyMode;

    partial void OnIsPrivacyModeChanged(bool value)
    {
        GlyphPrivacy = value ? "\uED1A" : "\uE890";

        labelerYAxis = value ? value => "****" : value => string.Format("$ {0:N0}", value);

        AppSettings.AreValuesMasked = value;

        ReloadAffectedControls();
    }

    // -----------------------------------------------------------------------
    // Signal widgets
    // -----------------------------------------------------------------------

    [ObservableProperty] private ObservableCollection<DashboardSignalRow> topSignals = new();
    [ObservableProperty] private string currentRegime = "–";
    [ObservableProperty] private string regimeDescription = "Voer 'Evaluate Signals' uit om het regime te bepalen.";
    [ObservableProperty] private string lastEvaluated = "–";
    [ObservableProperty] private Microsoft.UI.Xaml.Media.SolidColorBrush regimeBrush =
        new(Windows.UI.Color.FromArgb(0xFF, 0x80, 0x80, 0x80));
    [ObservableProperty] private string regimeReasons = string.Empty;

    private async Task LoadSignalWidgetsAsync()
    {
        try
        {
            var context = _dashboardService.GetContext();
            if (context is null) return;

            var cutoff = DateTime.UtcNow.AddHours(-24);
            var coinIds = await context.Coins.AsNoTracking()
                .Where(c => c.IsAsset).Select(c => c.Id).ToListAsync();

            var allSignals = await context.Signals.AsNoTracking()
                .Where(s => coinIds.Contains(s.CoinId) && s.CreatedAt >= cutoff)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            var latestPerCoin = allSignals
                .GroupBy(s => s.CoinId)
                .Select(g => g.First())
                .ToList();

            var coinDict = await context.Coins.AsNoTracking()
                .Where(c => coinIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id);

            // Top 3 Long (highest) + top 3 Short (lowest)
            var topLong  = latestPerCoin.Where(s => s.Direction == SignalDirection.Long)
                               .OrderByDescending(s => s.CombinedScore).Take(3);
            var topShort = latestPerCoin.Where(s => s.Direction == SignalDirection.Short)
                               .OrderBy(s => s.CombinedScore).Take(3);

            var rows = topLong.Concat(topShort)
                .OrderByDescending(s => Math.Abs(s.CombinedScore - 50))
                .Select(s => coinDict.TryGetValue(s.CoinId, out var c) ? new DashboardSignalRow(s, c) : null)
                .Where(r => r is not null)
                .Select(r => r!)
                .ToList();

            TopSignals = new ObservableCollection<DashboardSignalRow>(rows);

            LastEvaluated = allSignals.Count > 0
                ? allSignals.Max(s => s.CreatedAt).ToString("dd-MM HH:mm") + " UTC"
                : "–";

            // Market regime from BTC (rank 1)
            var btc = await context.Coins.AsNoTracking().FirstOrDefaultAsync(c => c.Rank == 1);
            if (btc is not null)
            {
                CurrentRegime = btc.MarketRegime.ToString();
                RegimeDescription = btc.MarketRegime switch
                {
                    MarketRegime.RiskOn  => "BTC in opwaartse trend — gunstige condities voor Long-posities.",
                    MarketRegime.RiskOff => "BTC in neerwaartse trend — wees voorzichtig met Long-posities.",
                    _                   => "BTC neutraal — gemengde marktcondities.",
                };
                RegimeBrush = Helpers.AnalysisHelpers.RegimeColor(btc.MarketRegime.ToString());

                // Load BTC's latest signal reasoning
                var btcSignal = await context.Signals.AsNoTracking()
                    .Where(s => s.CoinId == btc.Id)
                    .OrderByDescending(s => s.CreatedAt)
                    .FirstOrDefaultAsync();

                if (btcSignal is not null && !string.IsNullOrWhiteSpace(btcSignal.Reasoning))
                {
                    var lines = btcSignal.Reasoning
                        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        .Select(l => l.Trim())
                        .Where(l => l.Length > 0)
                        .Take(6)
                        .ToList();
                    RegimeReasons = string.Join("\n", lines);
                }
                else
                {
                    RegimeReasons = string.Empty;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "LoadSignalWidgets failed");
        }
    }

    [ObservableProperty] bool needUpdateDashboard = false;
    async partial void OnNeedUpdateDashboardChanged(bool oldValue, bool newValue)
    {
        if (newValue)
        {
            await UpdateDashboardAsync();

            NeedUpdateDashboard = false;
        };
    }

    private async Task UpdateDashboardAsync()
    {
        //await SetSeriesHeatMap(SelectedHeatMapIndex);
        await RefreshHeatMapPoints();
        await GetTop5();
        GetValueGains();
        await LoadSignalWidgetsAsync();
    }

    public DashboardViewModel(IDashboardService dashboardService, 
                                IGraphService graphService, 
                                IPriceLevelService priceLevelService, 
                                IMessenger messenger,
                                Settings appSettings) : base(appSettings)
    {
        Current = this;
        Logger = Log.Logger.ForContext(Constants.SourceContextPropertyName, typeof(AssetsViewModel).Name.PadRight(22));

        messenger.Register<UpdateDashboardMessage>(this, (r, m) =>
        {
            NeedUpdateDashboard = true;
        });
        messenger.Register<UpdateProgressValueMessage>(this, (r, m) =>
        {
            ProgressValueGraph = m.ProgressValue;
        });
        messenger.Register<GraphUpdatedMessage>(this, (r, m) =>
        {
            SetValuesGraph();
        });

        _dashboardService = dashboardService;
        _graphService = graphService;
        _priceLevelService = priceLevelService;
        IsPrivacyMode = AppSettings.AreValuesMasked;
        CurrentPortfolio = _dashboardService.GetPortfolio();

    }

    /// <summary>  
    /// This method is called by the DashboardView_Loaded event.  
    /// </summary>  
    public void ViewLoading()
    {
        CurrentPortfolio = _dashboardService.GetPortfolio();
        PortfolioName = CurrentPortfolio.Name;

        IsPrivacyMode = AppSettings.AreValuesMasked;
    }

    public void Terminate()
    {
       
    }

    [RelayCommand]
    private void TogglePrivacyMode()
    {
        IsPrivacyMode = !IsPrivacyMode;
    }

    [RelayCommand]
    private void ToggleFullScreenMode(object mode)
    {
        if (Enum.IsDefined(typeof(FullScreenMode), mode))
        {
            var requestedMode = (FullScreenMode)mode;
            ToggleFsMode = ToggleFsMode == requestedMode ? FullScreenMode.None : requestedMode;
        }
    }

    [RelayCommand]
    private void PieToggleFullScreenMode(PieChartControl pie)
    {
        var requestedMode = FullScreenMode.None;

        switch (pie.Name)
        {
            case "PortfolioPie":
                {
                    requestedMode = FullScreenMode.PiePortfolio;
                    break;
                }
            case "AccountsPie":
                {
                    requestedMode = FullScreenMode.PieAccounts;
                    break;
                }
            case "NarrativesPie":
                {
                    requestedMode = FullScreenMode.PieNarratives;
                    break;
                }
            default: break;
        }
        ToggleFsMode = ToggleFsMode == requestedMode ? FullScreenMode.None : requestedMode;
    }

    [RelayCommand]
    public async Task ShowSettingsDialog()
    {
        var loc = Localizer.Get();
        try
        {
            Logger.Information("Showing DashboardSettings Dialog");
            var dialog = new DashboardSettingsDialog(this)
            {
                XamlRoot = DashboardView.Current.XamlRoot
            };
            var result = await App.ShowContentDialogAsync(dialog);

            if (result == ContentDialogResult.Primary)
            {
                // maybe do something
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to show DashboardSettings Dialog");
            await ShowMessageDialog(
                loc.GetLocalizedString("Messages_DashboardSettingsDialogFailed_Title"),
                ex.Message,
                loc.GetLocalizedString("Common_CloseButton"));
        }
    }


    private void ReloadAffectedControls()
    {
        ReloadValueGains();
        ReloadGraph();
    }

    public async Task RefreshRsiBubbles()
    {
        RsiRbContent = $"1D RSI({AppSettings.RsiPeriod})";
        await _dashboardService.CalculateRsiAllCoins();
        if (SelectedHeatMapIndex == 1) 
        { 
            await RefreshHeatMapPoints(); 
        }
    }

    public async Task RefreshMaBubbles()
    {
        MaRbContent = $"1D {AppSettings.MaType}({AppSettings.MaPeriod})";
        await _dashboardService.CalculateMaAllCoins();
        if (SelectedHeatMapIndex == 2) 
        {
            SetCustomSeparatorsMa();
            await RefreshHeatMapPoints(); 
        }
    }

    public void RefreshPortfolioPie()
    {
        var portfolioPie = PieChartControls?.FirstOrDefault(p => string.Equals(p?.Name, "PortfolioPie", StringComparison.OrdinalIgnoreCase));
        if (portfolioPie is not null)
        {
            SetSeriesPie(portfolioPie);
        }
    }

    public async Task RefreshTargetBubbles()
    {
        RsiRbContent = $"1D RSI({AppSettings.RsiPeriod})";
        if (SelectedHeatMapIndex == 0) 
        {
            SetCustomSeparatorsTarget();
            await _dashboardService.EvaluatePriceLevels();
            await SetSeriesHeatMap(SelectedHeatMapIndex);
        }
    }
}






