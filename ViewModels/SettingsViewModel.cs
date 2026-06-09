using System.Security.Cryptography;
using CryptoPortfolioTracker.Enums;
using Windows.ApplicationModel.DataTransfer;
using Windows.Security.Credentials;

namespace CryptoPortfolioTracker.ViewModels;

public partial class SettingsViewModel : BaseViewModel, INotifyPropertyChanged
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public static SettingsViewModel Current;
    public Settings AppSettings => base.AppSettings; // expose AppSettings publicly so that it can be used in dialogs called by this ViewModel

    // Proxy voor BybitIsEu — TwoWay x:Bind op chained properties crasht in WinUI 3
    public bool BybitIsEu
    {
        get => AppSettings.BybitIsEu;
        set { AppSettings.BybitIsEu = value; OnPropertyChanged(nameof(BybitIsEu)); }
    }


#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    [ObservableProperty]
    private string password;

    [ObservableProperty]
    private string duressPassword;

    [ObservableProperty]
    private string currentPassword;

    [ObservableProperty]
    private string newPassword;

    [ObservableProperty]
    private string currentDuressPassword;

    [ObservableProperty]
    private string newDuressPassword;

    

    [ObservableProperty]
    private ElementTheme appTheme;
    partial void OnAppThemeChanged(ElementTheme value) => AppSettings.AppTheme = value;

    [ObservableProperty]
    private int numberFormatIndex;
    partial void OnNumberFormatIndexChanged(int value) => SetNumberSeparatorsFromIndex(value);
    
    [ObservableProperty]
    private int appCultureIndex;
    partial void OnAppCultureIndexChanged(int value) => SetCulturePreferenceFromIndex(value);

    [ObservableProperty]
    private double fontSize;
    partial void OnFontSizeChanged(double value) => AppSettings.FontSize = (AppFontSize)value;

    [ObservableProperty]
    private bool isCheckForUpdate;
    partial void OnIsCheckForUpdateChanged(bool value) => AppSettings.IsCheckForUpdate = value;

    [ObservableProperty]
    private bool isScrollBarsExpanded;
    partial void OnIsScrollBarsExpandedChanged(bool value) => AppSettings.IsScrollBarsExpanded = value;

    [ObservableProperty]
    private bool areValuesMasked;
    partial void OnAreValuesMaskedChanged(bool value) => AppSettings.AreValuesMasked = value;

    [ObservableProperty]
    private bool isPasswordSet;

    [ObservableProperty]
    private bool isDuressPasswordSet;

    /// <summary>
    /// Checks if credentials exist in the Windows Credential Locker and sets IsPasswordSet/IsDuressPasswordSet accordingly.
    /// Call this from InitializeFields or when loading the SettingsView.
    /// </summary>
    public void CheckPasswordCredentials()
    {
        var vault = new PasswordVault();

        // Check main password
        try
        {
            var credential = vault.Retrieve("CryptoPortfolioTrackerPlus", "Password");
            IsPasswordSet = credential != null;
        }
        catch
        {
            IsPasswordSet = false;
        }

        // Check duress password
        try
        {
            var credential = vault.Retrieve("CryptoPortfolioTrackerPlus", "DuressPassword");
            IsDuressPasswordSet = credential != null;
        }
        catch
        {
            IsDuressPasswordSet = false;
        }
    }

    // -----------------------------------------------------------------------
    // Telegram
    // -----------------------------------------------------------------------

    [ObservableProperty] private bool isTelegramEnabled;
    partial void OnIsTelegramEnabledChanged(bool value) => AppSettings.IsTelegramEnabled = value;

    [ObservableProperty] private string telegramBotToken = string.Empty;
    partial void OnTelegramBotTokenChanged(string value) => AppSettings.TelegramBotToken = value;

    [ObservableProperty] private string telegramChatId = string.Empty;
    partial void OnTelegramChatIdChanged(string value) => AppSettings.TelegramChatId = value;

    [ObservableProperty] private double telegramScoreThreshold;
    partial void OnTelegramScoreThresholdChanged(double value) => AppSettings.TelegramScoreThreshold = value;

    [ObservableProperty] private string telegramTestStatus = string.Empty;

    // -----------------------------------------------------------------------
    // Signalen & Trading
    // -----------------------------------------------------------------------

    [ObservableProperty] private bool isPaperTradingEnabled;
    partial void OnIsPaperTradingEnabledChanged(bool value) => AppSettings.IsPaperTradingEnabled = value;

    [ObservableProperty] private double signalScoreThreshold;
    partial void OnSignalScoreThresholdChanged(double value) => AppSettings.SignalScoreThreshold = value;

    // -----------------------------------------------------------------------
    // Risk-guardrails
    // -----------------------------------------------------------------------

    [ObservableProperty] private double maxPortfolioPercPerTrade;
    partial void OnMaxPortfolioPercPerTradeChanged(double value) => AppSettings.MaxPortfolioPercPerTrade = value;

    [ObservableProperty] private int maxOpenPositions;
    partial void OnMaxOpenPositionsChanged(int value) => AppSettings.MaxOpenPositions = value;

    [ObservableProperty] private double dailyLossLimitPerc;
    partial void OnDailyLossLimitPercChanged(double value) => AppSettings.DailyLossLimitPerc = value;

    [ObservableProperty] private bool isKillSwitchActive;
    partial void OnIsKillSwitchActiveChanged(bool value) => AppSettings.IsKillSwitchActive = value;

    [ObservableProperty] private bool useRealPortfolioForRisk;
    partial void OnUseRealPortfolioForRiskChanged(bool value) => AppSettings.UseRealPortfolioForRisk = value;

    [ObservableProperty] private double paperVirtualCapital;
    partial void OnPaperVirtualCapitalChanged(double value) => AppSettings.PaperVirtualCapital = value;

    private readonly INotifierService _notifierService;
    private readonly IExchangeAccountService _exchangeAccountService;

    [RelayCommand]
    private async Task TestTelegram()
    {
        TelegramTestStatus = "Bezig met testen…";
        var ok = await _notifierService.TestConnectionAsync();
        TelegramTestStatus = ok ? "✅ Verbinding geslaagd" : "❌ Verbinding mislukt — controleer token en chat ID";
    }

    // -----------------------------------------------------------------------
    // Exchange API-sleutels — Bybit (RSA flow)
    // -----------------------------------------------------------------------

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestBybitConnectionCommand))]
    private bool isBybitConfigured;

    [ObservableProperty] private string bybitKeyPreview       = string.Empty;
    [ObservableProperty] private string bybitConnectionStatus = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestBybitConnectionCommand))]
    private bool isBybitTesting;
    [ObservableProperty] private string bybitAuthMethod       = string.Empty; // "HMAC" or "RSA"

    // RSA stap 1 — sleutelpaar genereren
    [ObservableProperty] private string bybitPublicKeyPem     = string.Empty;
    [ObservableProperty] private bool   bybitRsaKeyGenerated;
    [ObservableProperty] private string bybitIpWhitelist      = string.Empty; // optioneel IP voor Bybit EU

    // RSA stap 2 — API Key invullen (ontvangen van Bybit na plakken public key)
    [ObservableProperty] private string bybitRsaApiKey        = string.Empty;

    // HMAC (System-generated key) — API Key + Secret
    [ObservableProperty] private string bybitHmacApiKey       = string.Empty;
    [ObservableProperty] private string bybitHmacApiSecret    = string.Empty;

    // ── MEXC ─────────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestMexcConnectionCommand))]
    private bool isMexcConfigured;

    [ObservableProperty] private string mexcKeyPreview        = string.Empty;
    [ObservableProperty] private string mexcConnectionStatus  = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestMexcConnectionCommand))]
    private bool isMexcTesting;

    [ObservableProperty] private string mexcHmacApiKey        = string.Empty;
    [ObservableProperty] private string mexcHmacApiSecret     = string.Empty;

    // ── Stap 1: genereer RSA-sleutelpaar ──────────────────────────────────

    [RelayCommand]
    private async Task GenerateBybitRsaKeyPair()
    {
        try
        {
            var pem = await _exchangeAccountService.GenerateRsaKeyPairAsync(ExchangeKind.Bybit);
            BybitPublicKeyPem  = pem;
            BybitRsaKeyGenerated = true;
            BybitConnectionStatus = string.Empty;
            Logger.Information("SettingsViewModel: Bybit RSA key pair generated");
        }
        catch (Exception ex)
        {
            await ShowMessageDialog("Fout", $"Sleutelpaar aanmaken mislukt: {ex.Message}", "OK");
        }
    }

    // ── Stap 2: sla API Key op die Bybit teruggeeft ───────────────────────

    [RelayCommand]
    private async Task SaveBybitRsaApiKey()
    {
        if (string.IsNullOrWhiteSpace(BybitRsaApiKey))
        {
            await ShowMessageDialog("Ontbrekende gegevens", "Vul de API Key in die Bybit heeft aangemaakt.", "OK");
            return;
        }

        await _exchangeAccountService.SaveRsaApiKeyAsync(ExchangeKind.Bybit, BybitRsaApiKey);

        IsBybitConfigured = true;
        BybitAuthMethod   = "RSA";
        BybitKeyPreview   = BybitRsaApiKey.Length > 4
            ? "···" + BybitRsaApiKey[^4..]
            : BybitRsaApiKey;
        BybitRsaApiKey        = string.Empty;
        BybitConnectionStatus = string.Empty;

        await ShowMessageDialog("Opgeslagen", "Bybit RSA-koppeling is gereed. Gebruik 'Verbinding testen' om te verifiëren.", "OK");
    }

    // ── HMAC opslaan ─────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SaveBybitHmacKey()
    {
        if (string.IsNullOrWhiteSpace(BybitHmacApiKey) || string.IsNullOrWhiteSpace(BybitHmacApiSecret))
        {
            await ShowMessageDialog("Ontbrekende gegevens", "Vul zowel de API Key als de API Secret in.", "OK");
            return;
        }

        await _exchangeAccountService.SaveHmacAccountAsync(ExchangeKind.Bybit, BybitHmacApiKey.Trim(), BybitHmacApiSecret.Trim());

        IsBybitConfigured     = true;
        BybitAuthMethod       = "HMAC";
        BybitKeyPreview       = BybitHmacApiKey.Length > 4
            ? "···" + BybitHmacApiKey[^4..]
            : BybitHmacApiKey;
        BybitHmacApiKey       = string.Empty;
        BybitHmacApiSecret    = string.Empty;
        BybitConnectionStatus = string.Empty;

        await ShowMessageDialog("Opgeslagen", "Bybit HMAC-koppeling is gereed. Gebruik 'Verbinding testen' om te verifiëren.", "OK");
    }

    // ── Verbinding testen ─────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanTestBybit))]
    private async Task TestBybitConnection()
    {
        IsBybitTesting        = true;
        BybitConnectionStatus = "Bezig met testen…";

        var (ok, msg) = await _exchangeAccountService.TestConnectionAsync(ExchangeKind.Bybit);
        BybitConnectionStatus = msg;
        IsBybitTesting        = false;
    }
    private bool CanTestBybit() => IsBybitConfigured && !IsBybitTesting;

    // ── Verwijderen ───────────────────────────────────────────────────────

    [RelayCommand]
    private async Task DeleteBybitAccount()
    {
        var result = await ShowMessageDialog(
            "Sleutels verwijderen",
            "Weet je zeker dat je de Bybit-koppeling wilt verwijderen?",
            "Verwijderen", "Annuleren");

        if (result != ContentDialogResult.Primary) return;

        await _exchangeAccountService.DeleteAccountAsync(ExchangeKind.Bybit);
        IsBybitConfigured     = false;
        BybitKeyPreview       = string.Empty;
        BybitConnectionStatus = string.Empty;
        BybitAuthMethod       = string.Empty;
        BybitPublicKeyPem     = string.Empty;
        BybitRsaKeyGenerated  = false;
    }

    // ── Public key kopiëren naar klembord ────────────────────────────────

    [RelayCommand]
    private void CopyBybitPublicKey()
    {
        if (string.IsNullOrEmpty(BybitPublicKeyPem)) return;

        // Bybit EU verwacht het IP-adres op een nieuwe regel na de PEM-key
        var text = string.IsNullOrWhiteSpace(BybitIpWhitelist)
            ? BybitPublicKeyPem
            : $"{BybitPublicKeyPem}\n{BybitIpWhitelist.Trim()}";

        var dp = new DataPackage();
        dp.SetText(text);
        Clipboard.SetContent(dp);
        BybitConnectionStatus = string.IsNullOrWhiteSpace(BybitIpWhitelist)
            ? "📋 Public key gekopieerd (zonder IP)"
            : $"📋 Public key + IP {BybitIpWhitelist.Trim()} gekopieerd";
    }

    // ── MEXC opslaan ─────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SaveMexcHmacKey()
    {
        if (string.IsNullOrWhiteSpace(MexcHmacApiKey) || string.IsNullOrWhiteSpace(MexcHmacApiSecret))
        {
            await ShowMessageDialog("Ontbrekende gegevens", "Vul zowel de API Key als de API Secret in.", "OK");
            return;
        }

        await _exchangeAccountService.SaveHmacAccountAsync(ExchangeKind.Mexc, MexcHmacApiKey.Trim(), MexcHmacApiSecret.Trim());

        IsMexcConfigured     = true;
        MexcKeyPreview       = MexcHmacApiKey.Length > 4 ? "···" + MexcHmacApiKey[^4..] : MexcHmacApiKey;
        MexcHmacApiKey       = string.Empty;
        MexcHmacApiSecret    = string.Empty;
        MexcConnectionStatus = string.Empty;

        await ShowMessageDialog("Opgeslagen", "MEXC-koppeling is gereed. Gebruik 'Verbinding testen' om te verifiëren.", "OK");
    }

    [RelayCommand(CanExecute = nameof(CanTestMexc))]
    private async Task TestMexcConnection()
    {
        IsMexcTesting        = true;
        MexcConnectionStatus = "Bezig met testen…";

        var (ok, msg) = await _exchangeAccountService.TestConnectionAsync(ExchangeKind.Mexc);
        MexcConnectionStatus = msg;
        IsMexcTesting        = false;
    }
    private bool CanTestMexc() => IsMexcConfigured && !IsMexcTesting;

    [RelayCommand]
    private async Task VerifyMexcBalances()
    {
        MexcConnectionStatus = "Balansen ophalen…";
        var comparisons = await _exchangeAccountService.VerifyExchangeBalancesAsync(ExchangeKind.Mexc, "MEXC");

        if (!comparisons.Any())
        {
            MexcConnectionStatus = "⚠️ Geen balansen gevonden of geen actieve koppeling.";
            return;
        }

        var lines = new System.Text.StringBuilder();
        foreach (var c in comparisons)
        {
            if (c.Matches)
                lines.AppendLine($"✅ {c.Symbol}: app {c.AppQty:G6}  =  MEXC {c.ExchangeQty:G6}");
            else if (c.AppQty == 0)
                lines.AppendLine($"➕ {c.Symbol}: alleen op MEXC ({c.ExchangeQty:G6}) — nog niet in app");
            else
                lines.AppendLine($"⚠️ {c.Symbol}: app {c.AppQty:G6}  ≠  MEXC {c.ExchangeQty:G6}  (Δ {c.Difference:+G4;-G4})");
        }

        await ShowMessageDialog("MEXC balans verificatie", lines.ToString().TrimEnd(), "Sluiten");
        MexcConnectionStatus = $"Verificatie voltooid — {comparisons.Count(c => c.Matches)}/{comparisons.Count} overeenkomend";
    }

    [RelayCommand]
    private async Task DeleteMexcAccount()
    {
        var result = await ShowMessageDialog(
            "Sleutels verwijderen",
            "Weet je zeker dat je de MEXC-koppeling wilt verwijderen?",
            "Verwijderen", "Annuleren");

        if (result != ContentDialogResult.Primary) return;

        await _exchangeAccountService.DeleteAccountAsync(ExchangeKind.Mexc);
        IsMexcConfigured     = false;
        MexcKeyPreview       = string.Empty;
        MexcConnectionStatus = string.Empty;
    }

    private async Task LoadExchangeStatusAsync()
    {
        IsBybitConfigured = await _exchangeAccountService.IsConfiguredAsync(ExchangeKind.Bybit);
        BybitAuthMethod   = await _exchangeAccountService.GetAuthMethodAsync(ExchangeKind.Bybit) ?? string.Empty;

        if (BybitAuthMethod == "RSA")
        {
            var pem = await _exchangeAccountService.GetPublicKeyPemAsync(ExchangeKind.Bybit);
            BybitPublicKeyPem    = pem ?? string.Empty;
            BybitRsaKeyGenerated = !string.IsNullOrEmpty(pem);
        }

        IsMexcConfigured = await _exchangeAccountService.IsConfiguredAsync(ExchangeKind.Mexc);
        if (IsMexcConfigured)
            MexcKeyPreview = "···????"; // preview laden kan later uitgebreid worden
    }

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------

    public SettingsViewModel(Settings appSettings, INotifierService notifierService,
        IExchangeAccountService exchangeAccountService) : base(appSettings)
    {
        Logger = Log.Logger.ForContext(Constants.SourceContextPropertyName, typeof(SettingsViewModel).Name.PadRight(22));
        Current = this;
        _notifierService        = notifierService;
        _exchangeAccountService = exchangeAccountService;

        InitializeFields();
    }

    private void InitializeFields()
    {
        IsCheckForUpdate = AppSettings.IsCheckForUpdate;
        FontSize = (double)AppSettings.FontSize;
        IsScrollBarsExpanded = AppSettings.IsScrollBarsExpanded;
        AppTheme = AppSettings.AppTheme;
        NumberFormatIndex = AppSettings.NumberFormat.NumberDecimalSeparator == "," ? 0 : 1;
        AppCultureIndex = AppSettings.AppCultureLanguage[..2].ToLower() == "nl" ? 0 : 1;

        IsTelegramEnabled        = AppSettings.IsTelegramEnabled;
        TelegramBotToken         = AppSettings.TelegramBotToken;
        TelegramChatId           = AppSettings.TelegramChatId;
        TelegramScoreThreshold   = AppSettings.TelegramScoreThreshold;

        IsPaperTradingEnabled    = AppSettings.IsPaperTradingEnabled;
        SignalScoreThreshold     = AppSettings.SignalScoreThreshold;
        MaxPortfolioPercPerTrade = AppSettings.MaxPortfolioPercPerTrade;
        MaxOpenPositions         = AppSettings.MaxOpenPositions;
        DailyLossLimitPerc       = AppSettings.DailyLossLimitPerc;
        IsKillSwitchActive       = AppSettings.IsKillSwitchActive;
        UseRealPortfolioForRisk  = AppSettings.UseRealPortfolioForRisk;
        PaperVirtualCapital      = AppSettings.PaperVirtualCapital;

        CheckPasswordCredentials();
        _ = LoadExchangeStatusAsync();
    }

    
    private void SetCulturePreferenceFromIndex(int index)
    {
        string language = index == 0 ? "nl" : "en-US";
        AppSettings.AppCultureLanguage = language;
    }

    private void SetNumberSeparatorsFromIndex(int index)
    {
        var nf = new NumberFormatInfo
        {
            NumberDecimalSeparator = index == 0 ? "," : ".",
            NumberGroupSeparator = index == 0 ? "." : ","
        };
        AppSettings.NumberFormat = nf;
    }

    [RelayCommand]
    public async Task CheckUpdateNow()
    {
        Logger.Information("Checking for updates");
        var appUpdater = new AppUpdater();
        var loc = Localizer.Get();
        var result = await appUpdater.Check(AppConstants.VersionUrl, AppConstants.ProductVersion);

        if (result == AppUpdaterResult.NeedUpdate)
        {
            Logger.Information("Update Available");
            var dlgResult = await ShowMessageDialog(
                loc.GetLocalizedString("Messages_UpdateChecker_NewVersionTitle"),
                loc.GetLocalizedString("Messages_UpdateChecker_NewVersionMsg"),
                loc.GetLocalizedString("Common_DownloadButton"),
                loc.GetLocalizedString("Common_CancelButton"));

            if (dlgResult == ContentDialogResult.Primary)
            {
                Logger.Information("Downloading update");
                var downloadResult = await appUpdater.DownloadSetupFile();
                
                if (downloadResult == AppUpdaterResult.DownloadSuccesfull)
                {
                    //*** wait till there is no other dialog box open
                    await App.DialogCompletionTask;
                    Logger.Information("Download Succesfull");
                    var installRequest = await ShowMessageDialog(
                        loc.GetLocalizedString("Messages_UpdateChecker_DownloadSuccesTitle"),
                        loc.GetLocalizedString("Messages_UpdateChecker_DownloadSuccesMsg"),
                        loc.GetLocalizedString("Common_InstallButton"),
                        loc.GetLocalizedString("Common_CancelButton"));

                    if (installRequest == ContentDialogResult.Primary)
                    {
                        Logger.Information("Closing Application and Installing Update");
                        appUpdater.ExecuteSetupFile();
                    }
                }
                else
                {
                    Logger.Warning("Download failed");
                    await ShowMessageDialog(
                        loc.GetLocalizedString("Messages_UpdateChecker_DownloadFailedTitle"),
                        loc.GetLocalizedString("Messages_UpdateChecker_DownloadFailedMsg"),
                        loc.GetLocalizedString("Common_CloseButton"));
                }
            }
        }
        else
        {
            Logger.Information("Application is up-to-date");
            await ShowMessageDialog(
                loc.GetLocalizedString("Messages_UpdateChecker_UpToDate_Title"),
                loc.GetLocalizedString("Messages_UpdateChecker_UpToDate_Msg"),
                loc.GetLocalizedString("Common_OkButton"));
        }
    }

    [RelayCommand]
    private async Task ChangePassword()
    {
        var vault = new PasswordVault();
        var loc = Localizer.Get();

        if (!IsPasswordSet)
        {
            // No password set yet, allow setting new password directly
            if (string.IsNullOrWhiteSpace(NewPassword))
            {
                await ShowMessageDialog(
                    loc.GetLocalizedString("Messages_Password_New_Required_Title"),
                    loc.GetLocalizedString("Messages_Password_New_Required_Msg"),
                    loc.GetLocalizedString("Common_OkButton"));
                return;
            }

            var newHash = AuthenticationService.HashPassword(NewPassword);
            vault.Add(new PasswordCredential("CryptoPortfolioTrackerPlus", "Password", newHash));
            IsPasswordSet = true;
            await ShowMessageDialog(
                loc.GetLocalizedString("Messages_Password_Set_Title"),
                loc.GetLocalizedString("Messages_Password_Set_Msg"),
                loc.GetLocalizedString("Common_OkButton"));
        }
        else
        {
            // Password exists, require current password
            if (string.IsNullOrWhiteSpace(CurrentPassword) || string.IsNullOrWhiteSpace(NewPassword))
                return;
            var credential = vault.Retrieve("CryptoPortfolioTrackerPlus", "Password");
            string storedHash = credential.Password;
            if (AuthenticationService.VerifyPassword(CurrentPassword, storedHash))
            {
                var newHash = AuthenticationService.HashPassword(NewPassword);
                vault.Remove(credential);
                vault.Add(new PasswordCredential("CryptoPortfolioTrackerPlus", "Password", newHash));
                await ShowMessageDialog(
                    loc.GetLocalizedString("Messages_Password_Changed_Title"),
                    loc.GetLocalizedString("Messages_Password_Changed_Msg"),
                    loc.GetLocalizedString("Common_OkButton"));
            }
            else
            {
                await ShowMessageDialog(
                    loc.GetLocalizedString("Messages_Password_Incorrect_Title"),
                    loc.GetLocalizedString("Messages_Password_Incorrect_Msg"),
                    loc.GetLocalizedString("Common_OkButton"));
            }
        }

        CurrentPassword = string.Empty;
        NewPassword = string.Empty;
    }

    [RelayCommand]
    private async Task ChangeDuressPassword()
    {
        var loc = Localizer.Get();
        var vault = new PasswordVault();
        if (!IsDuressPasswordSet)
        {
            // No password set yet, allow setting new password directly
            if (string.IsNullOrWhiteSpace(NewDuressPassword))
            {
                await ShowMessageDialog(
                    loc.GetLocalizedString("Messages_duressPassword_New_Required_Title"),
                    loc.GetLocalizedString("Messages_duressPassword_New_Required_Msg"),
                    loc.GetLocalizedString("Common_OkButton"));
                return;
            }

            var newHash = AuthenticationService.HashPassword(NewDuressPassword);
            vault.Add(new PasswordCredential("CryptoPortfolioTrackerPlus", "DuressPassword", newHash));
            IsDuressPasswordSet = true;
            await ShowMessageDialog(
                loc.GetLocalizedString("Messages_duressPassword_Set_Title"),
                loc.GetLocalizedString("Messages_duressPassword_Set_Msg"),
                loc.GetLocalizedString("Common_OkButton"));
        }
        else
        {
            // Password exists, require current password
            if (string.IsNullOrWhiteSpace(CurrentDuressPassword) || string.IsNullOrWhiteSpace(NewDuressPassword))
                return;
            var credential = vault.Retrieve("CryptoPortfolioTrackerPlus", "DuressPassword");
            string storedHash = credential.Password;
            if (AuthenticationService.VerifyPassword(CurrentDuressPassword, storedHash))
            {
                var newHash = AuthenticationService.HashPassword(NewDuressPassword);
                vault.Remove(credential);
                vault.Add(new PasswordCredential("CryptoPortfolioTrackerPlus", "DuressPassword", newHash));
                await ShowMessageDialog(
                    loc.GetLocalizedString("Messages_duressPassword_Changed_Title"),
                    loc.GetLocalizedString("Messages_duressPassword_Changed_Msg"),
                    loc.GetLocalizedString("Common_OkButton"));
            }
            else
            {
                await ShowMessageDialog(
                    loc.GetLocalizedString("Messages_duressPassword_Incorrect_Title"),
                    loc.GetLocalizedString("Messages_duressPassword_Incorrect_Msg"),
                    loc.GetLocalizedString("Common_OkButton"));
            }
        }

        CurrentDuressPassword = string.Empty;
        NewDuressPassword = string.Empty;
    }

    
}

