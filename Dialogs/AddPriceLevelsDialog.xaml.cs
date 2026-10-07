namespace CryptoPortfolioTracker.Dialogs;

[ObservableObject]
public sealed partial class AddPriceLevelsDialog : ContentDialog
{
    private readonly ILocalizer loc = Localizer.Get();
    private readonly PriceLevelsViewModel _viewModel;
    [ObservableProperty] private double tpValue;
    [ObservableProperty] private string tpNote = string.Empty;

    [ObservableProperty] private double buyValue;
    [ObservableProperty] private string buyNote =string.Empty;

    [ObservableProperty] private double stopValue;
    [ObservableProperty] private string stopNote = string.Empty;


    [ObservableProperty] private bool isValidTest = false;
   // [ObservableProperty] private Validator validator;
    [ObservableProperty] private string decimalSeparator;

    public ICollection<PriceLevel> newPriceLevels { get; set; }
     

    public AddPriceLevelsDialog(Coin coin, PriceLevelsViewModel viewModel, PriceLevelsRequest? prefill = null)
    {
        _viewModel = viewModel;
        InitializeComponent();
       // DataContext = this;

        DecimalSeparator = _viewModel.AppSettings.NumberFormat.NumberDecimalSeparator;
        newPriceLevels = new List<PriceLevel>(); // Initialize newPriceLevels
        InitializeFields(coin.PriceLevels);
        if (prefill is not null) ApplyPrefill(prefill);

        SetDialogTitleAndButtons(coin);
    }


    private void InitializeFields(ICollection<PriceLevel> priceLevels)
    {
        //newPriceLevels = priceLevels;

        foreach (var level in priceLevels)
        {
            if (level.Type == PriceLevelType.TakeProfit)
            {
                TpValue = level.Value;
                TpNote = level.Note;
            }
            else if (level.Type == PriceLevelType.Buy)
            {
                BuyValue = level.Value;
                BuyNote = level.Note;
            }
            else if (level.Type == PriceLevelType.Stop)
            {
                StopValue = level.Value;
                StopNote = level.Note;
            }
        }
    }

    /// <summary>Niveaus uit AI Research (v1.49) over de bestaande heen; lege niveaus laten het bestaande staan.</summary>
    private void ApplyPrefill(PriceLevelsRequest pf)
    {
        if (pf.Buy is > 0)        { BuyValue  = pf.Buy.Value;        BuyNote  = pf.Note; }
        if (pf.Stop is > 0)       { StopValue = pf.Stop.Value;       StopNote = pf.Note; }
        if (pf.TakeProfit is > 0) { TpValue   = pf.TakeProfit.Value; TpNote   = pf.Note; }
    }

    private void SetDialogTitleAndButtons(Coin coin)
    {
        Title = loc.GetLocalizedString("PriceLevelsDialog_AddTitle") + " " + coin.Name;
        PrimaryButtonText = loc.GetLocalizedString("PriceLevelsDialog_PrimaryButton");
        CloseButtonText = loc.GetLocalizedString("PriceLevelsDialog_CloseButton");
    }

    private void Button_Click_Accept(ContentDialog sender, ContentDialogButtonClickEventArgs e)
    {
        newPriceLevels = new List<PriceLevel>
        {
            new PriceLevel
            {
                Type = PriceLevelType.TakeProfit,
                Value = TpValue,
                Note = TpNote
            },
            new PriceLevel
            {
                Type = PriceLevelType.Buy,
                Value = BuyValue,
                Note = BuyNote
            },
            new PriceLevel
            {
                Type = PriceLevelType.Stop,
                Value = StopValue,
                Note = StopNote
            }
        };

       
    }

    private void Dialog_Loading(Microsoft.UI.Xaml.FrameworkElement sender, object args)
    {
        if (sender.ActualTheme != _viewModel.AppSettings.AppTheme)
        {
            sender.RequestedTheme = _viewModel.AppSettings.AppTheme;
        }
    }
}


