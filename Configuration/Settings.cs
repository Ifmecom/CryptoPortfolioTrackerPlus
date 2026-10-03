using System;
using System.Globalization;
using CryptoPortfolioTracker.Models;
using Microsoft.UI.Dispatching;

namespace CryptoPortfolioTracker.Configuration;

public partial class Settings : ObservableObject
{
    private readonly IPreferenceStore _store;

    public Settings(IPreferenceStore store)
    {
        _store = store;
    }

    public ElementTheme AppTheme
    {
        get => _store.Get("AppTheme", ElementTheme.Default);
        set
        {
            _store.Set("AppTheme", value);
            OnPropertyChanged(nameof(AppTheme));
        }
    }

    public string AppCultureLanguage
    {
        get => _store.Get("AppCultureLanguage", CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLower() == "nl" ? "nl" : "en-US");
        set
        {
            _store.Set("AppCultureLanguage", value.ToLower());
            OnPropertyChanged(nameof(AppCultureLanguage));

            if (App.Localizer == null)
            {
                return;
            }

            App.Localizer.SetLanguage(value.ToLower());
        }
    }

    public string UserID
    {
        get => _store.Get("UserId", Guid.NewGuid().ToString());
        set
        {
            _store.Set("UserId", value);
            OnPropertyChanged(nameof(UserID));
        }
    }

    public int PriceUpdateIntervalMinutes
    {
        get => _store.Get("PriceUpdateIntervalMinutes", 2);
        set
        {
            _store.Set("PriceUpdateIntervalMinutes", value);
            OnPropertyChanged(nameof(PriceUpdateIntervalMinutes));
        }
    }

    public bool IsScrollBarsExpanded
    {
        get => _store.Get("IsScrollBarsExpanded", false);
        set
        {
            _store.Set("IsScrollBarsExpanded", value);
            OnPropertyChanged(nameof(IsScrollBarsExpanded));
        }
    }

    public bool IsHidingZeroBalances
    {
        get => _store.Get("IsHidingZeroBalances", false);
        set
        {
            _store.Set("IsHidingZeroBalances", value);
            OnPropertyChanged(nameof(IsHidingZeroBalances));
        }
    }

    public NumberFormatInfo NumberFormat
    {
        get
        {
            var decimalSeparator = _store.Get("NumberFormat - Decimal Separator", CultureInfo.CurrentUICulture.NumberFormat.NumberDecimalSeparator);
            var groupSeparator = _store.Get("NumberFormat - Group Separator", CultureInfo.CurrentUICulture.NumberFormat.NumberGroupSeparator);
            var nf = (NumberFormatInfo)CultureInfo.CurrentUICulture.NumberFormat.Clone();
            nf.NumberDecimalSeparator = decimalSeparator;
            nf.NumberGroupSeparator = groupSeparator;
            return nf;
        }
        set
        {
            _store.Set("NumberFormat - Decimal Separator", value.NumberDecimalSeparator);
            _store.Set("NumberFormat - Group Separator", value.NumberGroupSeparator);
            OnPropertyChanged(nameof(NumberFormat));
        }
    }

    public bool IsCheckForUpdate
    {
        get => _store.Get("IsCheckForUpdate", true);
        set
        {
            _store.Set("IsCheckForUpdate", value);
            OnPropertyChanged(nameof(IsCheckForUpdate));
        }
    }

    public AppFontSize FontSize
    {
        get => _store.Get("FontSize", AppFontSize.Normal);
        set
        {
            _store.Set("FontSize", value);
            OnPropertyChanged(nameof(FontSize));
        }
    }

    public bool IsHidingCapitalFlow
    {
        get => _store.Get("IsHidingCapitalFlow", false);
        set
        {
            _store.Set("IsHidingCapitalFlow", value);
            OnPropertyChanged(nameof(IsHidingCapitalFlow));
        }
    }

    public int WithinRangePerc
    {
        get => _store.Get("WithinRangePerc", 5);
        set
        {
            _store.Set("WithinRangePerc", value);
            OnPropertyChanged(nameof(WithinRangePerc));
        }
    }

    public int CloseToPerc
    {
        get => _store.Get("CloseToPerc", 5);
        set
        {
            _store.Set("CloseToPerc", value);
            OnPropertyChanged(nameof(CloseToPerc));
        }
    }

    public int RsiPeriod
    {
        get => _store.Get("RsiPeriod", 14);
        set
        {
            _store.Set("RsiPeriod", value);
            OnPropertyChanged(nameof(RsiPeriod));
        }
    }

    public int MaPeriod
    {
        get => _store.Get("MaPeriod", 50);
        set
        {
            _store.Set("MaPeriod", value);
            OnPropertyChanged(nameof(MaPeriod));
        }
    }

    public string MaType
    {
        get => _store.Get("MaType", "SMA");
        set
        {
            _store.Set("MaType", value);
            OnPropertyChanged(nameof(MaType));
        }
    }

    public int MaxPieCoins
    {
        get => _store.Get("MaxPieCoins", 10);
        set
        {
            _store.Set("MaxPieCoins", value);
            OnPropertyChanged(nameof(MaxPieCoins));
        }
    }

    public bool AreValuesMasked
    {
        get => _store.Get("AreValuesMasked", false);
        set
        {
            _store.Set("AreValuesMasked", value);
            OnPropertyChanged(nameof(AreValuesMasked));
        }
    }

    public int HeatMapIndex
    {
        get => _store.Get("HeatMapIndex", 0);
        set
        {
            _store.Set("HeatMapIndex", value);
            OnPropertyChanged(nameof(HeatMapIndex));
        }
    }

    public Portfolio? LastPortfolio
    {
        get => _store.Get<Portfolio?>("LastPortfolio", null);
        set
        {
            _store.Set("LastPortfolio", value);
            OnPropertyChanged(nameof(LastPortfolio));
        }
    }

    public string LastVersion
    {
        get => _store.Get("LastVersion", "0.0.0");
        set
        {
            _store.Set("LastVersion", value);
            OnPropertyChanged(nameof(LastVersion));
        }
    }
    // -----------------------------------------------------------------------
    // Telegram notifications
    // -----------------------------------------------------------------------

    public bool IsTelegramEnabled
    {
        get => _store.Get("IsTelegramEnabled", false);
        set { _store.Set("IsTelegramEnabled", value); OnPropertyChanged(nameof(IsTelegramEnabled)); }
    }

    public string TelegramBotToken
    {
        get => _store.Get("TelegramBotToken", string.Empty);
        set { _store.Set("TelegramBotToken", value); OnPropertyChanged(nameof(TelegramBotToken)); }
    }

    public string TelegramChatId
    {
        get => _store.Get("TelegramChatId", string.Empty);
        set { _store.Set("TelegramChatId", value); OnPropertyChanged(nameof(TelegramChatId)); }
    }

    /// <summary>Minimum combined score (0-100) for a Long signal to trigger a notification.</summary>
    public double TelegramScoreThreshold
    {
        get => _store.Get("TelegramScoreThreshold", 65.0);
        set { _store.Set("TelegramScoreThreshold", value); OnPropertyChanged(nameof(TelegramScoreThreshold)); }
    }

    // -----------------------------------------------------------------------
    // Signalen & Trading
    // -----------------------------------------------------------------------

    /// <summary>Paper-trading mode — orders are only simulated, never sent to exchange.</summary>
    public bool IsPaperTradingEnabled
    {
        get => _store.Get("IsPaperTradingEnabled", true);
        set { _store.Set("IsPaperTradingEnabled", value); OnPropertyChanged(nameof(IsPaperTradingEnabled)); }
    }

    /// <summary>General minimum combined score for a Long signal to be saved/shown (0-100).</summary>
    public double SignalScoreThreshold
    {
        get => _store.Get("SignalScoreThreshold", 60.0);
        set { _store.Set("SignalScoreThreshold", value); OnPropertyChanged(nameof(SignalScoreThreshold)); }
    }

    /// <summary>
    /// Minimale ATR als % van de koers om een trade setup te tonen (0,5–5%). Onder deze drempel
    /// (en voor stablecoins) geeft de tool geen tradable setup — voorkomt setups op stille coins.
    /// </summary>
    public double MinSetupAtrPercent
    {
        get => _store.Get("MinSetupAtrPercent", 1.5);
        set { _store.Set("MinSetupAtrPercent", Math.Clamp(value, 0.5, 5.0)); OnPropertyChanged(nameof(MinSetupAtrPercent)); }
    }

    /// <summary>
    /// TradingView (v1.48): beursprefix voor tickers zonder bekende databron (bijv. "BINANCE", "BYBIT").
    /// Pattern Trading en Trade Advies gebruiken de beurs waar de koersdata vandaan kwam.
    /// </summary>
    public string TradingViewDefaultExchange
    {
        get => _store.Get("TradingViewDefaultExchange", "BINANCE") ?? "BINANCE";
        set { _store.Set("TradingViewDefaultExchange", string.IsNullOrWhiteSpace(value) ? "BINANCE" : value.Trim().ToUpperInvariant()); OnPropertyChanged(nameof(TradingViewDefaultExchange)); }
    }

    /// <summary>TradingView (v1.48): standaard grafiek-interval bij openen ("1D", "4H", "1H", "15M", "1W").</summary>
    public string TradingViewInterval
    {
        get => _store.Get("TradingViewInterval", "1D") ?? "1D";
        set { _store.Set("TradingViewInterval", string.IsNullOrWhiteSpace(value) ? "1D" : value.Trim().ToUpperInvariant()); OnPropertyChanged(nameof(TradingViewInterval)); }
    }

    /// <summary>TradingView-webhooks (v1.48): alerts ophalen van het geheime ntfy.sh-kanaal (standaard uit).</summary>
    public bool IsTradingViewWebhookEnabled
    {
        get => _store.Get("IsTradingViewWebhookEnabled", false);
        set { _store.Set("IsTradingViewWebhookEnabled", value); OnPropertyChanged(nameof(IsTradingViewWebhookEnabled)); }
    }

    /// <summary>Geheim ntfy.sh-kanaal voor de webhook (leeg = nog niet aangemaakt).</summary>
    public string TradingViewWebhookTopic
    {
        get => _store.Get("TradingViewWebhookTopic", string.Empty) ?? string.Empty;
        set { _store.Set("TradingViewWebhookTopic", value?.Trim() ?? string.Empty); OnPropertyChanged(nameof(TradingViewWebhookTopic)); }
    }

    /// <summary>Id van het laatst verwerkte ntfy-bericht (cursor, zodat alerts na een herstart niet opnieuw komen).</summary>
    public string TradingViewWebhookLastId
    {
        get => _store.Get("TradingViewWebhookLastId", string.Empty) ?? string.Empty;
        set => _store.Set("TradingViewWebhookLastId", value ?? string.Empty);
    }

    /// <summary>
    /// Bij 'Entry geraakt' voor een gevolgde Long-setup automatisch een Bybit Demo-order plaatsen (standaard uit).
    /// Telt mee voor het dagmaximum van automatisch handelen; de risk-guardrails gelden.
    /// </summary>
    public bool IsTradingViewWebhookAutoOrder
    {
        get => _store.Get("IsTradingViewWebhookAutoOrder", false);
        set { _store.Set("IsTradingViewWebhookAutoOrder", value); OnPropertyChanged(nameof(IsTradingViewWebhookAutoOrder)); }
    }

    /// <summary>Top X-keuze per pagina (v1.48): aantal te markeren setups (0 = uit, standaard 5).</summary>
    public int GetTopPickCount(string page) => _store.Get($"TopPicks.{page}.Count", 5);
    public void SetTopPickCount(string page, int value) => _store.Set($"TopPicks.{page}.Count", Math.Clamp(value, 0, 50));

    /// <summary>Top X per pagina: alleen de top tonen (true) of alleen markeren (false, standaard).</summary>
    public bool GetTopPicksOnly(string page) => _store.Get($"TopPicks.{page}.Only", false);
    public void SetTopPicksOnly(string page, bool value) => _store.Set($"TopPicks.{page}.Only", value);

    /// <summary>3% Trading: maximum aantal setups in de aanbevolen shortlist (1–20, standaard 5). (v1.48)</summary>
    public int ThreePctShortlistMax
    {
        get => _store.Get("ThreePctShortlistMax", 5);
        set { _store.Set("ThreePctShortlistMax", Math.Clamp(value, 1, 20)); OnPropertyChanged(nameof(ThreePctShortlistMax)); }
    }

    /// <summary>
    /// 3% Trading: maximale onderlinge correlatie binnen de shortlist (0,50–1,00, standaard 0,80).
    /// 1,00 = geen correlatiefilter: ook sterk samenbewegende munten mogen samen in de shortlist. (v1.48)
    /// </summary>
    public double ThreePctShortlistMaxCorrelation
    {
        get => _store.Get("ThreePctShortlistMaxCorrelation", 0.80);
        set { _store.Set("ThreePctShortlistMaxCorrelation", Math.Round(Math.Clamp(value, 0.5, 1.0), 2)); OnPropertyChanged(nameof(ThreePctShortlistMaxCorrelation)); }
    }

    // -----------------------------------------------------------------------
    // Risk-guardrails
    // -----------------------------------------------------------------------

    /// <summary>Maximum percentage of portfolio value to risk per single trade (1-25 %).</summary>
    public double MaxPortfolioPercPerTrade
    {
        get => _store.Get("MaxPortfolioPercPerTrade", 5.0);
        set { _store.Set("MaxPortfolioPercPerTrade", value); OnPropertyChanged(nameof(MaxPortfolioPercPerTrade)); }
    }

    /// <summary>Maximum number of simultaneously open positions (1-20).</summary>
    public int MaxOpenPositions
    {
        get => _store.Get("MaxOpenPositions", 5);
        set { _store.Set("MaxOpenPositions", value); OnPropertyChanged(nameof(MaxOpenPositions)); }
    }

    /// <summary>Daily loss limit as % of portfolio value — signal engine pauses for 24h when hit (1-30 %).</summary>
    public double DailyLossLimitPerc
    {
        get => _store.Get("DailyLossLimitPerc", 10.0);
        set { _store.Set("DailyLossLimitPerc", value); OnPropertyChanged(nameof(DailyLossLimitPerc)); }
    }

    /// <summary>Emergency kill-switch — when true, signal engine and auto-trading are suspended.</summary>
    public bool IsKillSwitchActive
    {
        get => _store.Get("IsKillSwitchActive", false);
        set { _store.Set("IsKillSwitchActive", value); OnPropertyChanged(nameof(IsKillSwitchActive)); }
    }

    /// <summary>
    /// Kapitaalbasis voor risico-berekeningen (positiegrootte + risico-dashboard):
    /// false = virtueel paper-kapitaal, true = werkelijke portfoliowaarde.
    /// </summary>
    public bool UseRealPortfolioForRisk
    {
        get => _store.Get("UseRealPortfolioForRisk", false);
        set { _store.Set("UseRealPortfolioForRisk", value); OnPropertyChanged(nameof(UseRealPortfolioForRisk)); }
    }

    /// <summary>Virtueel paper-kapitaal (USDT) waartegen paper-risico wordt berekend.</summary>
    public double PaperVirtualCapital
    {
        get => _store.Get("PaperVirtualCapital", 10_000.0);
        set { _store.Set("PaperVirtualCapital", value <= 0 ? 10_000.0 : value); OnPropertyChanged(nameof(PaperVirtualCapital)); }
    }

    // -----------------------------------------------------------------------
    // Exchange regio
    // -----------------------------------------------------------------------

    /// <summary>
    /// Wanneer true wordt api.bybit.eu gebruikt i.p.v. api.bybit.com.
    /// Vereist voor Bybit EU (bybit.eu) accounts.
    /// </summary>
    public bool BybitIsEu
    {
        get => _store.Get("BybitIsEu", false);
        set { _store.Set("BybitIsEu", value); OnPropertyChanged(nameof(BybitIsEu)); }
    }

    // -----------------------------------------------------------------------
    // Bybit Demo Trading + automatisch handelen (v1.47)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Gevonden API-domein voor Bybit Demo Trading (bv. https://api-demo.bybit.eu). Leeg = nog niet
    /// bepaald; 'Verbinding testen' probeert de kandidaten en slaat het werkende domein hier op.
    /// </summary>
    public string BybitDemoBaseUrl
    {
        get => _store.Get("BybitDemoBaseUrl", string.Empty);
        set { _store.Set("BybitDemoBaseUrl", value ?? string.Empty); OnPropertyChanged(nameof(BybitDemoBaseUrl)); }
    }

    /// <summary>Quote-munt voor spot-paren op Bybit EU (MiCA: standaard USDC, niet USDT).</summary>
    public string BybitQuoteCoin
    {
        get => _store.Get("BybitQuoteCoin", "USDC");
        set { _store.Set("BybitQuoteCoin", string.IsNullOrWhiteSpace(value) ? "USDC" : value.Trim().ToUpperInvariant()); OnPropertyChanged(nameof(BybitQuoteCoin)); }
    }

    /// <summary>Automatisch orders plaatsen op Bybit Demo na een Pattern Trading-scan (standaard uit).</summary>
    public bool IsAutoTradeEnabled
    {
        get => _store.Get("IsAutoTradeEnabled", false);
        set { _store.Set("IsAutoTradeEnabled", value); OnPropertyChanged(nameof(IsAutoTradeEnabled)); }
    }

    /// <summary>Minimale TradabilityScore voor een automatische order (50-100).</summary>
    public int AutoTradeMinScore
    {
        get => _store.Get("AutoTradeMinScore", 75);
        set { _store.Set("AutoTradeMinScore", Math.Clamp(value, 50, 100)); OnPropertyChanged(nameof(AutoTradeMinScore)); }
    }

    /// <summary>Maximaal aantal automatische orders per dag (1-20).</summary>
    public int AutoTradeMaxPerDay
    {
        get => _store.Get("AutoTradeMaxPerDay", 3);
        set { _store.Set("AutoTradeMaxPerDay", Math.Clamp(value, 1, 20)); OnPropertyChanged(nameof(AutoTradeMaxPerDay)); }
    }

    /// <summary>Risico per automatische trade als % van het demo-saldo (verlies bij stop-loss, 0,1-5%).</summary>
    public double AutoTradeRiskPct
    {
        get => _store.Get("AutoTradeRiskPct", 1.0);
        set { _store.Set("AutoTradeRiskPct", Math.Clamp(value, 0.1, 5.0)); OnPropertyChanged(nameof(AutoTradeRiskPct)); }
    }

    /// <summary>Maximale inleg per automatische trade als % van het beschikbare saldo (5-50%).</summary>
    public double AutoTradeMaxPositionPct
    {
        get => _store.Get("AutoTradeMaxPositionPct", 20.0);
        set { _store.Set("AutoTradeMaxPositionPct", Math.Clamp(value, 5.0, 50.0)); OnPropertyChanged(nameof(AutoTradeMaxPositionPct)); }
    }

    // -----------------------------------------------------------------------
    // Fundamentele analyse
    // -----------------------------------------------------------------------

    /// <summary>Aantal dagen dat opgehaalde fundamentals als "vers" gelden (1-90). Daarna "verouderd".</summary>
    public int FundamentalsFreshnessDays
    {
        get => _store.Get("FundamentalsFreshnessDays", 7);
        set { _store.Set("FundamentalsFreshnessDays", Math.Clamp(value, 1, 90)); OnPropertyChanged(nameof(FundamentalsFreshnessDays)); }
    }

    /// <summary>Gemarkeerde favoriete coins (CSV van CoinGecko-ApiId's, max 10).</summary>
    public string FundamentalsFavorites
    {
        get => _store.Get("FundamentalsFavorites", string.Empty);
        set { _store.Set("FundamentalsFavorites", value); OnPropertyChanged(nameof(FundamentalsFavorites)); }
    }

    // New: expose flush so callers owning Settings can wait for persistence
    public Task FlushPreferenceStoreAsync(CancellationToken ct = default) =>
        _store.FlushAsync(ct);
}