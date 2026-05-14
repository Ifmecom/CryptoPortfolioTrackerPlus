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

    // New: expose flush so callers owning Settings can wait for persistence
    public Task FlushPreferenceStoreAsync(CancellationToken ct = default) =>
        _store.FlushAsync(ct);
}