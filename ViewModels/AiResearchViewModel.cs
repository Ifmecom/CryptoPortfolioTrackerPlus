using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Threading;

namespace CryptoPortfolioTracker.ViewModels;

/// <summary>Een kant-en-klare vraag; <c>{munt}</c> wordt vervangen door de gekozen munt.</summary>
public sealed record AiQuickPrompt(string Label, string Template, bool NeedsCoin);

/// <summary>Eén bericht in het gesprek, met weergave-eigenschappen.</summary>
public sealed class AiChatItem
{
    public string Header { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public bool IsUser { get; init; }
    public HorizontalAlignment Alignment => IsUser ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    public double MaxBubbleWidth => IsUser ? 640 : 900;
}

/// <summary>
/// AI Research (v1.49): vragen stellen aan AI's — via API (eigen sleutel) of de ingebedde websites — en
/// uit vraag en antwoord slimme vervolgstappen in de app voorstellen. Acties openen altijd een
/// bestaand venster of een pagina ter bevestiging; er wordt nooit iets automatisch uitgevoerd.
/// </summary>
public partial class AiResearchViewModel : BaseViewModel
{
    private static readonly ILogger Logger = Log.Logger.ForContext(Constants.SourceContextPropertyName, nameof(AiResearchViewModel).PadRight(22));

    private readonly IAiResearchService _ai;
    private readonly ITradeService _tradeService;
    private readonly ITradingViewService? _tradingView;
    private readonly List<AiChatTurn> _turns = new();
    private CancellationTokenSource? _cts;

    // Acties uit het laatste antwoord, en uit de vraag die nu getypt wordt (live, met korte vertraging).
    private List<SmartAction> _answerActions = new();
    private List<SmartAction> _typingActions = new();
    private CancellationTokenSource? _typingCts;

    private string _lastQuestion = string.Empty;
    private string _lastAnswer = string.Empty;
    private string _lastAnswerSource = string.Empty;

    public IAiResearchService Service => _ai;
    public Settings AppSettingsPublic => AppSettings;
    public IReadOnlyList<AiCoinRef> Coins { get; private set; } = Array.Empty<AiCoinRef>();

    [ObservableProperty] private string question = string.Empty;
    [ObservableProperty] private string statusText = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private ObservableCollection<AiChatItem> chat = new();
    [ObservableProperty] private ObservableCollection<SmartAction> actions = new();
    [ObservableProperty] private ObservableCollection<AiApiProvider> apiProviders = new();
    [ObservableProperty] private AiApiProvider? selectedApiProvider;
    [ObservableProperty] private ObservableCollection<AiCoinRef> promptCoins = new();
    [ObservableProperty] private AiCoinRef? selectedPromptCoin;
    [ObservableProperty] private ObservableCollection<string> recentQuestions = new();

    public bool HasApiProvider => ApiProviders.Count > 0;
    public bool HasNoApiProvider => ApiProviders.Count == 0;
    public bool HasActions => Actions.Count > 0;
    public bool HasNoActions => Actions.Count == 0;
    public bool IsNotBusy => !IsBusy;

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotBusy));

    partial void OnQuestionChanged(string value) => _ = DetectWhileTypingAsync(value);

    private async Task DetectWhileTypingAsync(string text)
    {
        _typingCts?.Cancel();
        var cts = _typingCts = new CancellationTokenSource();
        try { await Task.Delay(350, cts.Token); }
        catch (OperationCanceledException) { return; }

        _typingActions = string.IsNullOrWhiteSpace(text) || text.Trim().Length < 3
            ? new List<SmartAction>()
            : AiIntentDetector.Detect(text, Coins, "vraag");
        PublishActions();
    }

    private void SetAnswerActions(IEnumerable<SmartAction> actions)
    {
        _answerActions = actions.ToList();
        PublishActions();
    }

    // Vraag-acties eerst: wat je nu typt is het meest actueel; daarna de rest uit het antwoord.
    private void PublishActions() =>
        Actions = new ObservableCollection<SmartAction>(AiIntentDetector.Merge(_typingActions, _answerActions));
    partial void OnActionsChanged(ObservableCollection<SmartAction> value)
    {
        OnPropertyChanged(nameof(HasActions));
        OnPropertyChanged(nameof(HasNoActions));
    }
    partial void OnApiProvidersChanged(ObservableCollection<AiApiProvider> value)
    {
        OnPropertyChanged(nameof(HasApiProvider));
        OnPropertyChanged(nameof(HasNoApiProvider));
    }
    partial void OnSelectedApiProviderChanged(AiApiProvider? value)
    {
        if (value is not null) AppSettings.AiResearchApiProvider = value.Id;
        OnPropertyChanged(nameof(ModelLabel));
    }

    public string ModelLabel => SelectedApiProvider is null ? string.Empty : _ai.GetModel(SelectedApiProvider.Id);

    public bool IncludeContext
    {
        get => AppSettings.AiResearchIncludeContext;
        set { if (value == AppSettings.AiResearchIncludeContext) return; AppSettings.AiResearchIncludeContext = value; OnPropertyChanged(); }
    }

    public IReadOnlyList<AiQuickPrompt> QuickPrompts { get; } = new[]
    {
        new AiQuickPrompt("Fundamenteel verhaal", "Wat is het fundamentele verhaal van {munt}: wat doet het project, wie gebruikt het en wat zijn de grootste risico's?", true),
        new AiQuickPrompt("Technische setup",     "Geef een technische analyse van {munt} op de daggrafiek met steun, weerstand en een concreet plan (entry, stop, doel).", true),
        new AiQuickPrompt("Catalysts",            "Welke catalysts komen er de komende weken aan voor {munt} (upgrades, token-unlocks, listings, rechtszaken)?", true),
        new AiQuickPrompt("Tokenomics",           "Leg de tokenomics van {munt} uit: supply, inflatie, unlocks en wie de grootste houders zijn.", true),
        new AiQuickPrompt("Concurrenten",         "Vergelijk {munt} met zijn belangrijkste concurrenten op technologie, adoptie en waardering.", true),
        new AiQuickPrompt("Nieuws (met bronnen)", "Wat is het belangrijkste nieuws van de afgelopen week over {munt}? Noem je bronnen.", true),
        new AiQuickPrompt("Portfolio-check",      "Beoordeel mijn portfolio: concentratierisico, onderlinge samenhang en wat ik zou kunnen herwegen.", false),
        new AiQuickPrompt("Marktsentiment",       "Wat is het huidige marktsentiment in crypto en wat betekent dat voor altcoins de komende weken?", false),
    };

    public AiResearchViewModel(Settings appSettings, IAiResearchService ai, ITradeService tradeService,
                               ITradingViewService? tradingView = null) : base(appSettings)
    {
        _ai = ai;
        _tradeService = tradeService;
        _tradingView = tradingView;
    }

    // ── Laden ────────────────────────────────────────────────────────────────

    public async Task ViewLoadingAsync()
    {
        try
        {
            Coins = await _ai.GetCoinsAsync();
            var current = SelectedPromptCoin?.ApiId;
            PromptCoins = new ObservableCollection<AiCoinRef>(
                Coins.Where(c => !TradeSetupGate.IsStablecoin(c.Symbol))
                     .OrderByDescending(c => c.IsAsset));                 // binnen bezit/niet-bezit op rang (zo geladen)
            SelectedPromptCoin = PromptCoins.FirstOrDefault(c => c.ApiId == current) ?? PromptCoins.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "AI Research: munten laden mislukt");
        }
        RefreshProviders();
        RecentQuestions = new ObservableCollection<string>(_ai.History.Select(h => h.Question).Distinct().Take(15));
    }

    public void RefreshProviders()
    {
        var keep = SelectedApiProvider?.Id ?? AppSettings.AiResearchApiProvider;
        ApiProviders = new ObservableCollection<AiApiProvider>(_ai.ConfiguredProviders());
        SelectedApiProvider = ApiProviders.FirstOrDefault(p => p.Id == keep) ?? ApiProviders.FirstOrDefault();
        OnPropertyChanged(nameof(ModelLabel));
    }

    public void Terminate() => _cts?.Cancel();

    // ── Vragen via API ───────────────────────────────────────────────────────

    [RelayCommand]
    private async Task AskAsync()
    {
        var q = Question?.Trim() ?? string.Empty;
        if (q.Length == 0 || IsBusy) return;
        if (SelectedApiProvider is null)
        {
            StatusText = "Stel eerst een API-sleutel in (knop 'Sleutels'), of gebruik een van de websites hieronder.";
            return;
        }

        var provider = SelectedApiProvider;
        IsBusy = true;
        StatusText = $"{provider.Name} denkt na…";
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        try
        {
            // Context alleen bij de eerste vraag van een gesprek; daarna kent het model hem al.
            string? context = IncludeContext && _turns.Count == 0 ? await _ai.BuildContextAsync() : null;
            _turns.Add(new AiChatTurn("user", AiPromptBuilder.BuildUserMessage(q, context)));
            Chat.Add(new AiChatItem { IsUser = true, Header = context is null ? "Jij" : "Jij · met portfolio-context", Text = q });
            Question = string.Empty;

            var raw = await _ai.AskAsync(provider.Id, _turns, _cts.Token);
            _turns.Add(new AiChatTurn("assistant", raw));

            var (text, aiActions) = AiPromptBuilder.ExtractActions(raw, Coins);
            Chat.Add(new AiChatItem { Header = $"{provider.Name} · {_ai.GetModel(provider.Id)} · {DateTime.Now:HH:mm}", Text = Readable(text) });

            _lastQuestion = q;
            _lastAnswer = text;
            _lastAnswerSource = provider.Name;
            SetAnswerActions(AiIntentDetector.Merge(
                aiActions,
                AiIntentDetector.Detect(q, Coins, "vraag", includeResearch: false),
                AiIntentDetector.Detect(text, Coins, "antwoord")));
            StatusText = Actions.Count > 0 ? $"{Actions.Count} vervolgstap(pen) voorgesteld." : "Geen vervolgstappen herkend.";

            await _ai.AddHistoryAsync(new AiHistoryEntry(DateTime.UtcNow, provider.Name, q, text));
            if (!RecentQuestions.Contains(q)) RecentQuestions.Insert(0, q);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Gestopt.";
            DropUnansweredTurn();
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "AI-vraag via {Provider} mislukt", provider.Name);
            StatusText = $"Mislukt: {ex.Message}";
            Chat.Add(new AiChatItem { Header = $"{provider.Name} · fout", Text = ex.Message });
            DropUnansweredTurn();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void DropUnansweredTurn()
    {
        if (_turns.Count > 0 && _turns[^1].Role == "user") _turns.RemoveAt(_turns.Count - 1);
    }

    [RelayCommand]
    private void Stop() => _cts?.Cancel();

    [RelayCommand]
    private void NewConversation()
    {
        _cts?.Cancel();
        _turns.Clear();
        Chat.Clear();
        SetAnswerActions(Array.Empty<SmartAction>());
        _lastAnswer = _lastQuestion = string.Empty;
        StatusText = "Nieuw gesprek.";
    }

    [RelayCommand]
    private void ApplyQuickPrompt(AiQuickPrompt prompt)
    {
        var coin = SelectedPromptCoin;
        var name = coin is null ? "deze munt" : $"{coin.Name} ({coin.Symbol.ToUpperInvariant()})";
        Question = prompt.Template.Replace("{munt}", name);
    }

    // ── Websites ─────────────────────────────────────────────────────────────

    /// <summary>Wat er naar een website gaat: alleen de vraag, of de vraag met context (voor het klembord).</summary>
    public async Task<string> BuildWebPromptAsync(bool withContext)
    {
        var q = Question?.Trim() ?? string.Empty;
        return withContext ? AiPromptBuilder.BuildUserMessage(q, await _ai.BuildContextAsync()) : q;
    }

    /// <summary>Tekst die de gebruiker op een website selecteerde (of de laatste tekst van de pagina) analyseren.</summary>
    public void AnalyzeWebText(string text, string siteName)
    {
        _lastQuestion = Question?.Trim() ?? string.Empty;
        _lastAnswer = text.Trim();
        _lastAnswerSource = siteName;
        SetAnswerActions(AiIntentDetector.Merge(
            AiIntentDetector.Detect(_lastQuestion, Coins, "vraag", includeResearch: false),
            AiIntentDetector.Detect(text, Coins, siteName)));
        StatusText = Actions.Count > 0
            ? $"{Actions.Count} vervolgstap(pen) uit {siteName} herkend."
            : $"Geen munten of vervolgstappen herkend in de tekst van {siteName}. Selecteer het antwoord en probeer opnieuw.";
    }

    // ── Acties uitvoeren ─────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ExecuteActionAsync(SmartAction action)
    {
        try
        {
            switch (action.Kind)
            {
                case SmartActionKind.Buy:
                case SmartActionKind.Sell:
                    Navigate("AssetsView", new TransactionRequest(action.Kind == SmartActionKind.Buy, action.Symbol, NoteFor(action)));
                    break;
                case SmartActionKind.AddCoin:
                    Navigate("CoinLibraryView", new AddCoinRequest(action.SearchText.Length > 0 ? action.SearchText : action.Symbol));
                    break;
                case SmartActionKind.PriceLevels:
                    Navigate("PriceLevelsView", new PriceLevelsRequest(action.CoinApiId, action.Entry, action.StopLoss, action.Target, NoteFor(action)));
                    break;
                case SmartActionKind.TradeAdvies:
                    Navigate("TradeAnalysisView", new TradeAdviesRequest(action.CoinApiId));
                    break;
                case SmartActionKind.Fundamentals:
                    Navigate("FundamentalsView", new FundamentalsRequest(action.Symbol));
                    break;
                case SmartActionKind.TradingView:
                    if (_tradingView is null) { StatusText = "TradingView is niet beschikbaar."; break; }
                    await _tradingView.OpenChartAsync(_tradingView.TickerFor(action.Symbol));
                    StatusText = $"{action.Symbol} geopend op TradingView.";
                    break;
                case SmartActionKind.PaperTrade:
                    await PaperTradeAsync(action);
                    break;
                case SmartActionKind.SaveNote:
                    await SaveNoteAsync(action);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "AI Research: actie {Kind} voor {Symbol} mislukt", action.Kind, action.Symbol);
            StatusText = $"Actie mislukt: {ex.Message}";
        }
    }

    private void Navigate(string viewTag, AppNavigationRequest request)
    {
        if (!AppNavigator.Request(viewTag, request))
            StatusText = "Kon de pagina niet openen.";
    }

    private string NoteFor(SmartAction a) =>
        $"Via AI Research ({(_lastAnswerSource.Length > 0 ? _lastAnswerSource : a.Source)}){(a.Reason.Length > 0 ? ": " + a.Reason : string.Empty)}";

    private async Task PaperTradeAsync(SmartAction action)
    {
        var coin = await _ai.GetCoinAsync(action.CoinApiId);
        if (coin is null) { StatusText = $"{action.Symbol} niet gevonden in de bibliotheek."; return; }

        var setup = new TradeSetupAdvice
        {
            Direction = action.Direction.Length > 0 ? action.Direction : "Long",
            EntryPrice = action.Entry ?? coin.Price,
            StopLoss = action.StopLoss ?? 0,
            Target1 = action.Target ?? 0,
            Confidence = "AI",
            Reasoning = new List<string> { NoteFor(action) },
        };

        var dialog = new PaperTradeDialog(coin, setup, AppSettings) { XamlRoot = MainPage.Current?.XamlRoot };
        if (action.Entry is > 0) dialog.UseLimitEntry(action.Entry.Value);   // genoemde entry = limit-order
        await App.ShowContentDialogAsync(dialog);
        if (!dialog.Confirmed) return;
        var req = dialog.BuildOrderRequest();
        if (req is null) return;

        var signal = new Signal
        {
            CoinId = coin.Id,
            CreatedAt = DateTime.UtcNow,
            Direction = setup.Direction == "Short" ? SignalDirection.Short : SignalDirection.Long,
            Reasoning = NoteFor(action),
        };
        await _tradeService.PlacePaperAsync(coin, signal, req);
        StatusText = $"Paper {req.Side} order geplaatst voor {action.Symbol} — {req.AmountUsdt:F0} USDT. Zie Trade Journal.";
    }

    private async Task SaveNoteAsync(SmartAction action)
    {
        if (string.IsNullOrWhiteSpace(_lastAnswer))
        {
            StatusText = "Er is nog geen antwoord om te bewaren.";
            return;
        }
        var body = _lastAnswer.Length > 4000 ? _lastAnswer[..4000] + "…" : _lastAnswer;
        var note = $"[AI Research · {_lastAnswerSource} · {DateTime.Now:dd-MM-yyyy HH:mm}]"
                 + (_lastQuestion.Length > 0 ? $"\nVraag: {_lastQuestion}" : string.Empty)
                 + $"\n{body}";
        StatusText = await _ai.AppendNoteAsync(action.CoinApiId, note)
            ? $"Antwoord bewaard als notitie bij {action.Symbol} (Coin Library → notitie)."
            : $"Notitie bij {action.Symbol} kon niet worden bewaard.";
    }

    // ── Hulp ─────────────────────────────────────────────────────────────────

    /// <summary>Markdown-opmaak die een TextBlock niet kan tonen wat rustiger maken (vet, koppen).</summary>
    public static string Readable(string text)
    {
        var t = Regex.Replace(text, @"\*\*(.+?)\*\*", "$1");
        t = Regex.Replace(t, @"(?m)^#{1,6}\s*", string.Empty);
        t = Regex.Replace(t, @"(?m)^\s*[-*]\s+", "• ");
        return t.Trim();
    }
}
