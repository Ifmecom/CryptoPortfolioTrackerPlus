using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUI3Localizer;

namespace CryptoPortfolioTracker.Dialogs;

[ObservableObject]
public partial class AboutDialog : ContentDialog
{
    private readonly ILocalizer loc = Localizer.Get();
    private readonly ElementTheme _theme;

    [ObservableProperty] private string imagePath;
    [ObservableProperty] private string btcImage;
    [ObservableProperty] private string ethImage;
    [ObservableProperty] private string usdcImage;
    [ObservableProperty] private string version;
    [ObservableProperty] private string btcAddress;
    [ObservableProperty] private string ethAddress;
    [ObservableProperty] private string usdcAddress;
    [ObservableProperty] private string usdtAddress;

    public AboutDialog(ElementTheme theme)
    {
        ImagePath   = AppConstants.AppPath + "\\Assets\\CryptoPortfolioTracker.ico";
        BtcImage    = AppConstants.AppPath + "\\Assets\\bitcoin.png";
        EthImage    = AppConstants.AppPath + "\\Assets\\ethereum.png";
        UsdcImage   = AppConstants.AppPath + "\\Assets\\usdc.png";
        Version     = AppConstants.ProductVersion;

        // Donation addresses — update these when addresses change
        EthAddress  = "0x3c8459bB0D53838065C874c2296fC21Be8113ea9";
        UsdcAddress = "0x0295413b74E0bF85378615b91BCb8ff6C5A9cF83";
        UsdtAddress = "0x0295413b74E0bF85378615b91BCb8ff6C5A9cF83";
        BtcAddress  = "bc1qqe0c0x7j70cepd5clnfw95d8p8x934qv46en9e";

        InitializeComponent();
        DataContext = this;
        _theme = theme;
        SetDialogTitleAndButtons();
    }

    private void Dialog_Loading(Microsoft.UI.Xaml.FrameworkElement sender, object args)
    {
        if (sender.ActualTheme != _theme)
            sender.RequestedTheme = _theme;
    }

    private void SetDialogTitleAndButtons()
    {
        Title = loc.GetLocalizedString("AboutDialog_Title");
        PrimaryButtonText = loc.GetLocalizedString("Common_CloseButton");
        IsPrimaryButtonEnabled = true;
    }
}
