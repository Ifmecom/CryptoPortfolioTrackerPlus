using CryptoPortfolioTracker.Enums;

namespace CryptoPortfolioTracker.Services;

public interface IExchangeAccountService
{
    // ── HMAC ────────────────────────────────────────────────────────────────

    /// <summary>Encrypts and saves (or updates) the API key + HMAC secret for an exchange.</summary>
    Task SaveHmacAccountAsync(ExchangeKind exchange, string apiKey, string apiSecret);

    // ── RSA ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a fresh RSA-2048 key pair, stores the private key encrypted with DPAPI,
    /// and returns the PEM-encoded public key so the user can paste it into Bybit.
    /// </summary>
    Task<string> GenerateRsaKeyPairAsync(ExchangeKind exchange);

    /// <summary>
    /// Stores the Bybit API Key after the user pasted the public key into Bybit and received it.
    /// The RSA private key must already be stored (call GenerateRsaKeyPairAsync first).
    /// </summary>
    Task SaveRsaApiKeyAsync(ExchangeKind exchange, string apiKey);

    /// <summary>Returns the stored PEM public key, or null when not generated yet.</summary>
    Task<string?> GetPublicKeyPemAsync(ExchangeKind exchange);

    // ── Shared ──────────────────────────────────────────────────────────────

    /// <summary>Returns the auth method ("HMAC" or "RSA") for the stored account, or null.</summary>
    Task<string?> GetAuthMethodAsync(ExchangeKind exchange);

    /// <summary>Returns true when an account record exists for this exchange.</summary>
    Task<bool> IsConfiguredAsync(ExchangeKind exchange);

    /// <summary>Removes the account record for this exchange.</summary>
    Task DeleteAccountAsync(ExchangeKind exchange);

    /// <summary>Makes a read-only API call to verify the key is valid.</summary>
    Task<(bool Success, string Message)> TestConnectionAsync(ExchangeKind exchange);

    /// <summary>
    /// Haalt de live balansen op van de exchange en vergelijkt ze met de assets
    /// in het account met de opgegeven naam. Niets wordt gewijzigd.
    /// </summary>
    Task<List<BalanceComparison>> VerifyExchangeBalancesAsync(ExchangeKind exchange, string accountName);

    /// <summary>
    /// Importeert nieuwe MEXC spot-trades automatisch als transacties.
    /// Reeds geïmporteerde trades worden overgeslagen (via SourceId-deduplicatie).
    /// </summary>
    /// <param name="accountName">Naam van het portfolio-account dat overeenkomt met MEXC (bijv. "MEXC").</param>
    /// <returns>(Imported, Skipped) — aantal nieuw geïmporteerde en overgeslagen trades.</returns>
    Task<(int Imported, int Skipped)> SyncMexcTradesAsync(string accountName);
}

/// <summary>Resultaat van een balans-vergelijking per coin.</summary>
public record BalanceComparison(
    string Symbol,
    double AppQty,
    double ExchangeQty)
{
    public double Difference  => ExchangeQty - AppQty;
    public bool   Matches     => Math.Abs(Difference) < Math.Max(0.000001, AppQty * 0.0001); // 0.01% tolerantie
}
