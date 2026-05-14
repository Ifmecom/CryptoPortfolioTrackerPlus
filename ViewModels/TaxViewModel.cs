using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CryptoPortfolioTracker.Models;
using CryptoPortfolioTracker.Services;
using CryptoPortfolioTracker.Services.Tax;
using Serilog;
using Serilog.Core;
using System.Collections.ObjectModel;

namespace CryptoPortfolioTracker.ViewModels;

/// <summary>Lightweight descriptor for a country option in the ComboBox.</summary>
public record TaxCountryOption(string DisplayName, ITaxCalculator Calculator);

public partial class TaxViewModel : BaseViewModel
{
    private static readonly ILogger Logger =
        Log.Logger.ForContext(Constants.SourceContextPropertyName, nameof(TaxViewModel).PadRight(22));

    // ── Registered calculators ───────────────────────────────────────────────
    // To add a new country: implement ITaxCalculator and add an instance here.
    private static readonly IReadOnlyList<ITaxCalculator> Calculators = new ITaxCalculator[]
    {
        new NetherlandsTaxCalculator(),
        // new GermanyTaxCalculator(),
        // new BelgiumTaxCalculator(),
        // new UnitedKingdomTaxCalculator(),
        // new UnitedStatesTaxCalculator(),
    };

    // ── Country & year ───────────────────────────────────────────────────────

    public IReadOnlyList<TaxCountryOption> CountryOptions { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(YearOptions))]
    [NotifyPropertyChangedFor(nameof(ReferenceDateDisplay))]
    private TaxCountryOption? selectedCountryOption;

    public IReadOnlyList<int> YearOptions =>
        SelectedCountryOption?.Calculator.SupportedYears ?? Array.Empty<int>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReferenceDateDisplay))]
    private int selectedYear;

    public string ReferenceDateDisplay
    {
        get
        {
            if (SelectedCountryOption is null || SelectedYear == 0) return string.Empty;
            try
            {
                var d = SelectedCountryOption.Calculator.ReferenceDate(SelectedYear);
                return $"Peildatum: {d.Day} {d.ToString("MMMM")} {d.Year}";
            }
            catch { return string.Empty; }
        }
    }

    // ── Input amounts (double so WinUI NumberBox binds without converter) ────

    [ObservableProperty] private double cryptoValue;
    [ObservableProperty] private double bankSavings;
    [ObservableProperty] private double otherAssets;
    [ObservableProperty] private double debts;
    [ObservableProperty] private bool   hasFiscalPartner;

    // ── Results ───────────────────────────────────────────────────────────────

    [ObservableProperty] private bool   hasResult;
    [ObservableProperty] private string taxDueDisplay        = string.Empty;
    [ObservableProperty] private string effectiveRateDisplay = string.Empty;
    [ObservableProperty] private string resultTitle          = string.Empty;
    [ObservableProperty] private string statusMessage        = string.Empty;

    [ObservableProperty]
    private ObservableCollection<TaxReportLine> reportLines = new();

    // ── Constructor ───────────────────────────────────────────────────────────

    public TaxViewModel(Settings appSettings) : base(appSettings)
    {
        CountryOptions = Calculators
            .Select(c => new TaxCountryOption(c.CountryName, c))
            .ToList();

        SelectedCountryOption = CountryOptions.FirstOrDefault();
    }

    // ── Reactive handlers ─────────────────────────────────────────────────────

    partial void OnSelectedCountryOptionChanged(TaxCountryOption? value)
    {
        SelectedYear  = value?.Calculator.SupportedYears.Last() ?? 0;
        HasResult     = false;
        StatusMessage = string.Empty;
        ReportLines.Clear();
    }

    partial void OnSelectedYearChanged(int value)
    {
        HasResult     = false;
        StatusMessage = string.Empty;
        ReportLines.Clear();
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private void Calculate()
    {
        if (SelectedCountryOption is null) return;

        try
        {
            var input = new TaxInput
            {
                Year             = SelectedYear,
                CryptoValue      = (decimal)CryptoValue,
                BankSavings      = (decimal)BankSavings,
                OtherAssets      = (decimal)OtherAssets,
                Debts            = (decimal)Debts,
                HasFiscalPartner = HasFiscalPartner,
            };

            var report = SelectedCountryOption.Calculator.Calculate(input);

            ReportLines.Clear();
            foreach (var line in report.Breakdown)
                ReportLines.Add(line);

            TaxDueDisplay        = $"€ {report.TaxDue:N0}";
            EffectiveRateDisplay = $"Effectief tarief: {report.EffectiveRate:N2}% van netto vermogen";
            ResultTitle          = $"Berekening {SelectedYear}  ·  {report.CountryName}";
            HasResult            = true;
            StatusMessage        = string.Empty;

            Logger.Information("TaxViewModel.Calculate completed — year {Year}, due {Due}",
                SelectedYear, report.TaxDue);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "TaxViewModel.Calculate failed");
            StatusMessage = $"Berekening mislukt: {ex.Message}";
            HasResult     = false;
        }
    }
}
