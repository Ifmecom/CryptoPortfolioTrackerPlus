using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;
using CryptoPortfolioTracker.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Core;

namespace CryptoPortfolioTracker.ViewModels;

public partial class ThreePctViewModel : BaseViewModel
{
    private static readonly ILogger Logger = Log.Logger.ForContext(
        Constants.SourceContextPropertyName, nameof(ThreePctViewModel).PadRight(22));

    private readonly IThreePctBacktestService   _backtest;
    private readonly IThreePctScoringService    _scorer;
    private readonly IBinanceDataService        _binance;
    private readonly IBinanceFuturesDataService _futures;
    private readonly IOrderBookService          _orderBook;
    private readonly IMarketRegimeService       _regimeService;
    private readonly ICorrelationService        _correlation;
    private readonly IMacroEventService         _macroEvents;
    private readonly ITradeService              _tradeService;
    private readonly IFundamentalsService       _fundamentals;
    private readonly PortfolioService           _portfolioService;

    private IReadOnlyDictionary<string, CoinFundamentals> _fundMap =
        new Dictionary<string, CoinFundamentals>();

    /// <summary>Notes-tag waarmee 3%-Trading paper trades worden gemarkeerd.</summary>
    private const string StrategyTag = "[3%]";

    // ── Scan-data cache (Sprint C) ────────────────────────────────────────────
    private readonly Dictionary<string, List<OhlcvBar>>         _barsCache  = new();
    private readonly Dictionary<string, OrderBookSnapshot?>     _obCache    = new();
    private readonly Dictionary<string, FuturesPositioning?>    _posCache   = new();
    private List<OhlcvBar> _btcBarsCache = new();

    // ── Backtest settings ────────────────────────────────────────────────────
    [ObservableProperty] private string selectedTimeframe  = "1d";
    [ObservableProperty] private string selectedBias       = "Long";
    [ObservableProperty] private string backtestSymbol     = "BTCUSDT";
    [ObservableProperty] private double tpNetPct           = 3.0;
    [ObservableProperty] private double feePct             = 0.1;
    [ObservableProperty] private double slAtrMultiple      = 1.5;
    [ObservableProperty] private int    maxHorizonBars     = 15;
    [ObservableProperty] private int    backtestProgress   = 0;
    [ObservableProperty] private string backtestStatus     = string.Empty;
    [ObservableProperty] private bool   isBacktestRunning  = false;

    public IReadOnlyList<string> TimeframeOptions { get; } = new[] { "1d", "4h", "1h" };
    public IReadOnlyList<string> BiasOptions      { get; } = new[] { "Long", "Short", "Both" };

    // ── Calibration table ────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<ScoreClassCalibration> calibrationRows = new();
    [ObservableProperty] private string calibrationInfo = "Nog geen kalibratie uitgevoerd.";

    // ── Live scan ────────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<ThreePctLiveRow> liveRows = new();
    [ObservableProperty] private string liveScanStatus = string.Empty;
    [ObservableProperty] private bool   isLiveScanRunning = false;
    [ObservableProperty] private string liveScanBias      = "Long";

    // ── Marktregime (Sprint B) ────────────────────────────────────────────────
    [ObservableProperty] private string regimeSummary      = "Nog niet bepaald";
    [ObservableProperty] private string regimeEmaCross     = "–";
    [ObservableProperty] private string regimeDominance    = "–";
    [ObservableProperty] private string regimeBtcRsi       = "–";
    [ObservableProperty] private bool   isRegimeLoading    = false;

    // ── Diversified shortlist (Sprint C) ─────────────────────────────────────
    [ObservableProperty] private ObservableCollection<ThreePctLiveRow> diversifiedRows = new();
    [ObservableProperty] private string diversifiedInfo = string.Empty;

    /// <summary>True wanneer er shortlist-info te tonen is (voor Visibility-binding).</summary>
    public bool HasDiversifiedInfo => !string.IsNullOrEmpty(DiversifiedInfo);

    partial void OnDiversifiedInfoChanged(string value)
        => OnPropertyChanged(nameof(HasDiversifiedInfo));

    // ── Paper trades (forward-test met live data) ─────────────────────────────
    [ObservableProperty] private ObservableCollection<TradeJournalRow> paperTradeRows = new();
    [ObservableProperty] private string paperStatus        = "Nog geen 3%-paper trades — activeer er een vanuit de Live Scan.";
    [ObservableProperty] private string paperCountDisplay  = "–";
    [ObservableProperty] private string paperWinRateDisplay = "–";
    [ObservableProperty] private string paperPnlDisplay    = "–";
    [ObservableProperty] private bool   isPaperLoading;
    [ObservableProperty] private Microsoft.UI.Xaml.Media.SolidColorBrush paperPnlBrush
        = TradeJournalViewModel.BrushGrey;

    /// <summary>Exposed voor dialogs die de view bouwt.</summary>
    public Settings Settings => AppSettings;

    public bool HasPaperTrades => PaperTradeRows.Count > 0;

    partial void OnPaperTradeRowsChanged(ObservableCollection<TradeJournalRow> value)
        => OnPropertyChanged(nameof(HasPaperTrades));

    // IsLoading is inherited from BaseViewModel

    private CancellationTokenSource? _cts;

    public ThreePctViewModel(
        IThreePctBacktestService   backtest,
        IThreePctScoringService    scorer,
        IBinanceDataService        binance,
        IBinanceFuturesDataService futures,
        IOrderBookService          orderBook,
        IMarketRegimeService       regimeService,
        ICorrelationService        correlation,
        IMacroEventService         macroEvents,
        ITradeService              tradeService,
        IFundamentalsService       fundamentals,
        PortfolioService           portfolioService,
        Settings                   appSettings)
        : base(appSettings)
    {
        _backtest         = backtest;
        _scorer           = scorer;
        _binance          = binance;
        _futures          = futures;
        _orderBook        = orderBook;
        _regimeService    = regimeService;
        _correlation      = correlation;
        _macroEvents      = macroEvents;
        _tradeService     = tradeService;
        _fundamentals     = fundamentals;
        _portfolioService = portfolioService;
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public void ViewLoading()
    {
        // Laad eventueel eerder opgeslagen kalibratie
        var saved = _backtest.LoadCalibration();
        if (saved is not null)
            ApplyCalibration(saved);

        // Laad regime asynchroon op de achtergrond
        _ = LoadRegimeContextAsync();

        // Laad bestaande 3%-paper trades (forward-test overzicht)
        _ = RefreshPaperTrades();
    }

    [RelayCommand]
    private async Task LoadRegimeContextAsync()
    {
        IsRegimeLoading = true;
        try
        {
            var ctx = await _regimeService.GetRegimeContextAsync();
            RegimeSummary   = ctx.Summary;
            RegimeEmaCross  = ctx.EmaStatus;
            RegimeDominance = $"{ctx.BtcDominancePct:0.0}%  {ctx.DominanceLabel}";
            RegimeBtcRsi    = $"{ctx.BtcRsi:0.0}";
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "ThreePctViewModel: regime context load failed");
            RegimeSummary = "Regime kon niet worden bepaald";
        }
        finally { IsRegimeLoading = false; }
    }

    public void Terminate()
    {
        _cts?.Cancel();
        _cts = null;
    }

    // =========================================================================
    // FASE 1 — Backtest / Kalibratie
    // =========================================================================

    [RelayCommand(CanExecute = nameof(CanRunBacktest))]
    private async Task RunBacktest()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        IsBacktestRunning = true;
        RunBacktestCommand.NotifyCanExecuteChanged();
        CancelBacktestCommand.NotifyCanExecuteChanged();
        CalibrationRows.Clear();
        BacktestProgress = 0;

        var pars = BuildParameters();

        var progress = new Progress<(int done, int total, string status)>(p =>
        {
            BacktestStatus   = p.status;
            BacktestProgress = p.total > 0 ? (int)(100.0 * p.done / p.total) : 0;
        });

        try
        {
            var results = await Task.Run(
                () => _backtest.RunAsync(BacktestSymbol.ToUpperInvariant().Trim(), pars, progress, _cts.Token),
                _cts.Token);

            ApplyCalibration(results);
        }
        catch (OperationCanceledException)
        {
            BacktestStatus = "Gestopt.";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "ThreePctViewModel.RunBacktest failed");
            BacktestStatus = $"Fout: {ex.Message}";
        }
        finally
        {
            IsBacktestRunning = false;
            RunBacktestCommand.NotifyCanExecuteChanged();
            CancelBacktestCommand.NotifyCanExecuteChanged();
            BacktestProgress = 0;
        }
    }

    private bool CanRunBacktest() => !IsBacktestRunning;

    [RelayCommand(CanExecute = nameof(CanCancelBacktest))]
    private void CancelBacktest()
    {
        _cts?.Cancel();
        BacktestStatus = "Annuleren…";
    }

    private bool CanCancelBacktest() => IsBacktestRunning;

    private void ApplyCalibration(List<ScoreClassCalibration> results)
    {
        CalibrationRows = new ObservableCollection<ScoreClassCalibration>(
            results.OrderBy(r => r.ScoreClass));

        int total = results.Sum(r => r.TradeCount);
        if (total > 0)
        {
            var best = results
                .Where(r => r.IsReliable && r.Expectancy > 0)
                .OrderByDescending(r => r.Expectancy)
                .FirstOrDefault();

            string bestInfo = best is not null
                ? $"Beste scoreklasse: {best.ScoreClass} → hitrate {best.HitratePct:0.0}%, expectancy {best.Expectancy:+0.000;-0.000}R"
                : "Geen scoreklasse met positieve expectancy gevonden.";

            var ts = results.FirstOrDefault()?.CalibratedAt;
            CalibrationInfo = $"{total} gesimuleerde trades — {bestInfo}" +
                              (ts.HasValue ? $" | Gekalibreerd: {ts.Value.ToLocalTime():dd-MM-yyyy HH:mm}" : string.Empty);
        }
        else
        {
            CalibrationInfo = "Kalibratie gaf geen bruikbare trades (te weinig data of verkeerde parameters).";
        }
    }

    // =========================================================================
    // FASE 2 — Live Scan
    // =========================================================================

    [RelayCommand(CanExecute = nameof(CanRunLiveScan))]
    private async Task RunLiveScan()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        IsLiveScanRunning = true;
        RunLiveScanCommand.NotifyCanExecuteChanged();
        CancelLiveScanCommand.NotifyCanExecuteChanged();
        LiveRows.Clear();
        DiversifiedRows.Clear();
        DiversifiedInfo = string.Empty;
        _barsCache.Clear();
        _obCache.Clear();
        _posCache.Clear();
        _btcBarsCache = new();
        LiveScanStatus = "Coins ophalen…";

        var calibration = _backtest.LoadCalibration();
        var pars        = BuildParameters();
        pars.Bias       = LiveScanBias;

        try
        {
            // Sprint C: haal BTC-bars éénmalig op voor correlatie
            LiveScanStatus = "BTC-data ophalen voor correlatie…";
            _btcBarsCache = await _binance.GetKlinesAsync("BTCUSDT", pars.Timeframe, limit: 100);

            var coins = await GetCoinsAsync();
            if (!coins.Any())
            {
                LiveScanStatus = "Geen coins gevonden in de database.";
                return;
            }

            // #1: fundamenteel kwaliteitsoordeel ophalen voor de badge
            try { _fundMap = await _fundamentals.GetScoreMapAsync(_cts.Token); }
            catch (Exception ex) { Logger.Warning(ex, "ThreePct: fundamentals-map laden mislukt"); }

            LiveScanStatus = $"0 / {coins.Count} coins gescand…";
            var rows = new List<ThreePctLiveRow>();
            int done = 0;

            foreach (var coin in coins)
            {
                _cts.Token.ThrowIfCancellationRequested();
                done++;
                LiveScanStatus = $"{done} / {coins.Count} — {coin.Symbol}";

                try
                {
                    var symbol = _binance.ResolveBinanceSymbol(coin.ApiId, coin.Symbol);
                    var bars   = await _binance.GetKlinesAsync(symbol, pars.Timeframe, limit: 250);

                    if (bars.Count < 210) continue;

                    // Sprint C: sla bars op voor detail-dialog en correlatie
                    _barsCache[coin.Symbol] = bars;

                    // ── Sprint B: haal F6 (order book) en F7 (futures) op ───────
                    OrderBookSnapshot?  ob  = null;
                    FuturesPositioning? pos = null;

                    try { ob  = await _orderBook.GetSnapshotAsync(symbol, _cts.Token); }
                    catch { /* graceful: F6 blijft 5.0 neutraal */ }

                    try { pos = await _futures.GetPositioningAsync(symbol, _cts.Token); }
                    catch { /* graceful: F7 blijft 5.0 neutraal */ }

                    // Sprint C: sla F6/F7 op voor detail-dialog
                    _obCache[coin.Symbol]  = ob;
                    _posCache[coin.Symbol] = pos;

                    // BTC-correlatie berekenen
                    double btcCorr = double.NaN;
                    if (_btcBarsCache.Count >= 10)
                        btcCorr = _correlation.ComputePearson(bars, _btcBarsCache, 60);

                    var biasesToCheck = pars.Bias == "Both"
                        ? new[] { "Long", "Short" }
                        : new[] { pars.Bias };

                    foreach (var bias in biasesToCheck)
                    {
                        var score = _scorer.ScoreWithGatekeepers(
                            bars, coin.Symbol, coin.Name, bias, pars, ob, pos);
                        if (score is null) continue;

                        string cls = IThreePctScoringService.GetScoreClass(score.TotalScore);

                        var cal = calibration?.FirstOrDefault(c => c.ScoreClass == cls);

                        _fundMap.TryGetValue(coin.ApiId ?? string.Empty, out var fund);
                        rows.Add(new ThreePctLiveRow
                        {
                            Symbol          = coin.Symbol,
                            CoinName        = coin.Name,
                            Score           = score.TotalScore,
                            ScoreClass      = cls,
                            HistHitrate     = cal?.HitratePct   ?? 0,
                            Expectancy      = cal?.Expectancy   ?? 0,
                            Bias            = bias,
                            EntryPrice      = score.EntryPrice,
                            StopLoss        = score.StopLoss,
                            TakeProfit      = score.TakeProfit,
                            RiskReward      = score.RiskReward,
                            FactorBreakdown = score.FactorSummary,
                            IsReliable      = cal?.IsReliable   ?? false,
                            F6Score         = score.F6Liquidity,
                            F7Score         = score.F7Positioning,
                            IsFiltered      = !score.IsQualified,
                            FilterReason    = score.FilterReason,
                            BtcCorrelation  = btcCorr,
                            FundamentalScore   = fund?.TotalScore ?? 0,
                            FundamentalVerdict = fund?.Verdict ?? string.Empty,
                            HasFundamental     = fund is not null,
                        });
                    }

                    await Task.Delay(150, _cts.Token); // iets ruimer voor extra API-calls
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "ThreePctViewModel: scan failed for {Coin}", coin.Symbol);
                }
            }

            // Sprint B: qualified setups eerst, daarna gefilterde als context
            var qualified = rows
                .Where(r => !r.IsFiltered && (r.Expectancy > 0 || r.Score > 60))
                .OrderByDescending(r => r.Expectancy)
                .ThenByDescending(r => r.Score)
                .Take(25)
                .ToList();

            var filtered = rows
                .Where(r => r.IsFiltered && r.Score > 60)
                .OrderByDescending(r => r.Score)
                .Take(5)
                .ToList();

            // ── Sprint C: diversified shortlist ─────────────────────────────────
            var diversifiedPicks = _correlation.BuildDiversifiedShortlist(
                qualified, _barsCache, _btcBarsCache, maxPositions: 5, maxCorrelation: 0.80);
            var diversifiedSymbols = diversifiedPicks.Select(r => r.Symbol).ToHashSet();

            // Mark diversified picks in the main list
            var sortedWithBadge = qualified
                .Select(r => diversifiedSymbols.Contains(r.Symbol)
                    ? new ThreePctLiveRow
                      {
                          Symbol = r.Symbol, CoinName = r.CoinName, Score = r.Score,
                          ScoreClass = r.ScoreClass, HistHitrate = r.HistHitrate,
                          Expectancy = r.Expectancy, Bias = r.Bias,
                          EntryPrice = r.EntryPrice, StopLoss = r.StopLoss,
                          TakeProfit = r.TakeProfit, RiskReward = r.RiskReward,
                          FactorBreakdown = r.FactorBreakdown, IsReliable = r.IsReliable,
                          F6Score = r.F6Score, F7Score = r.F7Score,
                          IsFiltered = r.IsFiltered, FilterReason = r.FilterReason,
                          BtcCorrelation = r.BtcCorrelation,
                          FundamentalScore = r.FundamentalScore,
                          FundamentalVerdict = r.FundamentalVerdict,
                          HasFundamental = r.HasFundamental,
                          IsDiversifiedPick = true,
                      }
                    : r)
                .Concat(filtered)
                .ToList();

            LiveRows = new ObservableCollection<ThreePctLiveRow>(sortedWithBadge);
            DiversifiedRows = new ObservableCollection<ThreePctLiveRow>(diversifiedPicks);
            DiversifiedInfo = diversifiedPicks.Count > 0
                ? $"Aanbevolen shortlist: {string.Join(", ", diversifiedPicks.Select(r => r.Symbol))}  (max correlatie 0.80, gesorteerd op expectancy)"
                : "Geen gediversifieerde shortlist (te weinig data of te hoge correlaties)";

            var sorted = sortedWithBadge;

            int filteredCount = rows.Count(r => r.IsFiltered);
            LiveScanStatus = calibration is null
                ? $"{qualified.Count} setups gevonden (geen kalibratie — run eerst Fase 1); {filteredCount} gefilterd op F6/F7"
                : $"{qualified.Count} gekwalificeerde setups; {filteredCount} gefilterd op liquiditeit/positionering";
        }
        catch (OperationCanceledException)
        {
            LiveScanStatus = "Scan gestopt.";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "ThreePctViewModel.RunLiveScan failed");
            LiveScanStatus = $"Fout: {ex.Message}";
        }
        finally
        {
            IsLiveScanRunning = false;
            RunLiveScanCommand.NotifyCanExecuteChanged();
            CancelLiveScanCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanRunLiveScan() => !IsLiveScanRunning;

    [RelayCommand(CanExecute = nameof(CanCancelLiveScan))]
    private void CancelLiveScan() { _cts?.Cancel(); }

    private bool CanCancelLiveScan() => IsLiveScanRunning;

    // =========================================================================
    // Sprint C — Detail dialog
    // =========================================================================

    [RelayCommand]
    private async Task ShowDetail(ThreePctLiveRow row)
    {
        try
        {
            var bars    = _barsCache.GetValueOrDefault(row.Symbol);
            var ob      = _obCache.GetValueOrDefault(row.Symbol);
            var pos     = _posCache.GetValueOrDefault(row.Symbol);
            var pars    = BuildParameters();
            var events  = _macroEvents.GetUpcoming(15);

            if (bars is null)
            {
                // Bars nog niet gecacht — haal ze op
                var symbol = _binance.ResolveBinanceSymbol(string.Empty, row.Symbol);
                bars = await _binance.GetKlinesAsync(symbol, pars.Timeframe, limit: 250);
            }

            var detail = ThreePctScoringService.BuildDetailInfo(
                row, bars ?? new(), _btcBarsCache.Count > 0 ? _btcBarsCache : null,
                ob, pos, events, pars);

            if (detail is null)
            {
                await ShowMessageDialog(
                    "Detail niet beschikbaar",
                    "Onvoldoende OHLCV-data om het detailvenster te berekenen.",
                    "Sluiten");
                return;
            }

            var dialog = new Dialogs.SetupDetailDialog(detail, AppSettings)
            {
                XamlRoot = Views.ThreePctView.Current?.XamlRoot,
            };
            await App.ShowContentDialogAsync(dialog);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "ThreePctViewModel.ShowDetail failed for {Symbol}", row.Symbol);
        }
    }

    // =========================================================================
    // Paper trades — activeren vanuit de scan + forward-test overzicht
    // =========================================================================

    /// <summary>Opent de PaperTradeDialog voorgevuld vanuit een live-scan rij en plaatst,
    /// na bevestiging, een als 3%-strategie gemarkeerde paper trade.</summary>
    [RelayCommand]
    private async Task OpenPaperTrade(ThreePctLiveRow row)
    {
        var ctx = _portfolioService.Context;
        if (ctx is null) return;

        try
        {
            var sym  = row.Symbol.ToLowerInvariant();
            var coin = await ctx.Coins.FirstOrDefaultAsync(c => c.Symbol != null && c.Symbol.ToLower() == sym);
            if (coin is null)
            {
                PaperStatus = $"Coin {row.Symbol} staat niet in de portfolio — kan geen paper trade plaatsen.";
                return;
            }

            var setup = new TradeSetupAdvice
            {
                Direction  = row.Bias,
                EntryPrice = row.EntryPrice,
                StopLoss   = row.StopLoss,
                Target1    = row.TakeProfit,
                Target2    = 0,
                Confidence = row.ScoreClass,
            };
            setup.Reasoning.Add(
                $"3% Trading — score {row.Score:0.0} ({row.ScoreClass}), hist. hitrate {row.HitrateDisplay}, expectancy {row.ExpectancyDisplay}.");

            var dialog = new Dialogs.PaperTradeDialog(coin, setup, AppSettings)
            {
                XamlRoot = Views.ThreePctView.Current?.XamlRoot,
            };
            await App.ShowContentDialogAsync(dialog);
            if (!dialog.Confirmed) return;

            var req = dialog.BuildOrderRequest();
            if (req is null) return;

            // Markeer als 3%-strategie zodat de overzichtstab erop kan filteren.
            req = req with
            {
                Notes = StrategyTag + (string.IsNullOrWhiteSpace(req.Notes) ? "" : " " + req.Notes),
            };

            var signal = new Signal
            {
                CoinId    = coin.Id,
                CreatedAt = DateTime.UtcNow,
                Direction = row.Bias == "Short" ? SignalDirection.Short : SignalDirection.Long,
                Reasoning = "3% Trading paper trade",
            };

            await _tradeService.PlacePaperAsync(coin, signal, req);
            await RefreshPaperTrades();
            PaperStatus = $"Paper {req.Side} order geplaatst voor {row.Symbol} — {req.AmountUsdt:F0} USDT.";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "ThreePctViewModel.OpenPaperTrade failed for {Symbol}", row.Symbol);
            PaperStatus = $"Order mislukt: {ex.Message}";
        }
    }

    /// <summary>Forward-test: vul pending limit-orders en sluit getriggerde TP/SL op live koers,
    /// herlaad daarna de 3%-paper trades en herbereken de statistieken.</summary>
    [RelayCommand]
    private async Task RefreshPaperTrades()
    {
        var ctx = _portfolioService.Context;
        if (ctx is null) return;

        IsPaperLoading = true;
        try
        {
            var coins = await ctx.Coins.AsNoTracking().ToListAsync();
            var priceMap = coins
                .Where(c => !string.IsNullOrEmpty(c.Symbol))
                .GroupBy(c => c.Symbol!.ToUpperInvariant())
                .ToDictionary(g => g.Key, g => g.First().Price, StringComparer.OrdinalIgnoreCase);

            // Forward-test met live data: zelfde engine als het Trade Journal.
            await _tradeService.AutoFillPendingAsync(priceMap);
            await _tradeService.AutoCloseTriggeredAsync(priceMap);

            var orders = await ctx.ExchangeOrders.AsNoTracking()
                .Where(o => o.IsPaper && o.Notes.Contains(StrategyTag))
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            PaperTradeRows = new ObservableCollection<TradeJournalRow>(
                orders.Select(o => new TradeJournalRow(o, priceMap)));

            ComputePaperStats(orders);
            PaperStatus = orders.Count == 0
                ? "Nog geen 3%-paper trades — activeer er een vanuit de Live Scan."
                : $"{orders.Count} 3%-paper trades — laatste refresh {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "ThreePctViewModel.RefreshPaperTrades failed");
            PaperStatus = $"Laden mislukt: {ex.Message}";
        }
        finally { IsPaperLoading = false; }
    }

    /// <summary>Sluit een open paper trade op de huidige koers, of annuleert een pending order.</summary>
    [RelayCommand]
    private async Task ClosePaperTrade(TradeJournalRow row)
    {
        if (row is null) return;
        try
        {
            if (row.Order.Status == OrderStatus.Pending)
                await _tradeService.CancelAsync(row.Order);
            else if (row.CurrentPrice > 0)
                await _tradeService.ClosePaperAsync(row.Order, row.CurrentPrice);

            await RefreshPaperTrades();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "ThreePctViewModel.ClosePaperTrade failed for #{Id}", row.Id);
            PaperStatus = $"Sluiten mislukt: {ex.Message}";
        }
    }

    private void ComputePaperStats(List<ExchangeOrder> orders)
    {
        int total = orders.Count;
        int open  = orders.Count(o => o.Status is OrderStatus.Filled or OrderStatus.Pending);

        var closed = orders
            .Where(o => o.Status == OrderStatus.Closed && o.ClosePrice > 0 && o.Entry > 0)
            .ToList();

        static double Pnl(ExchangeOrder o) => o.Side == OrderSide.Buy
            ? (o.ClosePrice - o.Entry) * o.Qty
            : (o.Entry - o.ClosePrice) * o.Qty;

        int won  = closed.Count(o => Pnl(o) > 0);
        int lost = closed.Count(o => Pnl(o) < 0);

        // Totale PnL = gerealiseerd (gesloten) + ongerealiseerd (open) via de rij-projectie.
        double totalPnl = PaperTradeRows.Sum(r => r.PnlUsdt);

        PaperCountDisplay   = total == 0 ? "–" : $"{total} trades · {open} open";
        PaperWinRateDisplay = (won + lost) > 0
            ? $"{100.0 * won / (won + lost):0.0}%  ({won}W / {lost}L)"
            : "–";
        PaperPnlDisplay = total == 0 ? "–" : $"{totalPnl:+0.00;-0.00} USDT";
        PaperPnlBrush   = totalPnl > 0 ? TradeJournalViewModel.BrushGreen
                        : totalPnl < 0 ? TradeJournalViewModel.BrushRed
                        : TradeJournalViewModel.BrushGrey;
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private BacktestParameters BuildParameters() => new()
    {
        Timeframe     = SelectedTimeframe,
        TpNetPct      = TpNetPct,
        FeePct        = FeePct,
        SlippagePct   = 0.05,
        SLAtrMultiple = SlAtrMultiple,
        SLMinPct      = 1.0,
        MaxHorizonBars = MaxHorizonBars,
        Bias          = SelectedBias,
    };

    private async Task<List<Coin>> GetCoinsAsync()
    {
        try
        {
            var ctx = _portfolioService.Context;
            if (ctx is null) return new List<Coin>();
            return await ctx.Coins.AsNoTracking().ToListAsync();
        }
        catch { return new List<Coin>(); }
    }
}
