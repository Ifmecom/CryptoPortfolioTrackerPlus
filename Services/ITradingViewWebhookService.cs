namespace CryptoPortfolioTracker.Services;

/// <summary>
/// TradingView-webhooks (v1.48). TradingView (betaald plan) post alerts naar een geheim kanaal op ntfy.sh;
/// deze service haalt ze elke 20 seconden op zolang de app draait en
/// <c>Settings.IsTradingViewWebhookEnabled</c> aan staat. Per alert: Telegram-melding, koppeling aan een
/// gevolgde setup (Setup Tracker) en — alleen als de gebruiker dat aanzet — een Bybit Demo-order bij 'Entry geraakt'.
/// Parsing en opmaak zitten in de pure <see cref="TradingViewAlerts"/>.
/// </summary>
public interface ITradingViewWebhookService
{
    /// <summary>Start de ophaal-lus (doet niets als hij al draait).</summary>
    void Start();

    void Stop();

    /// <summary>De laatst ontvangen alerts (nieuwste eerst, max. 50).</summary>
    IReadOnlyList<TradingViewAlert> RecentAlerts { get; }

    /// <summary>Statusregel voor Instellingen ("Luistert — laatste alert …", foutmelding, …).</summary>
    string Status { get; }

    /// <summary>Gaat af na elke ophaalronde met nieuwe alerts of een statuswijziging (achtergrondthread).</summary>
    event EventHandler? StateChanged;

    /// <summary>Haalt direct op (bijv. na 'Test').</summary>
    Task PollNowAsync(CancellationToken ct = default);

    /// <summary>Stuurt een testbericht naar het eigen kanaal; komt binnen een paar seconden terug als alert.</summary>
    Task<bool> SendTestAsync(CancellationToken ct = default);
}
