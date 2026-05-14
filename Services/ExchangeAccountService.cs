using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CryptoPortfolioTracker.Configuration;
using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Serilog;
using Serilog.Core;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Manages exchange API keys with two auth methods:
///   HMAC — Bybit System-generated key (key + secret, DPAPI encrypted)
///   RSA  — Bybit Self-generated key (private key DPAPI encrypted, public key stored plain)
/// </summary>
public class ExchangeAccountService : IExchangeAccountService
{
    private static readonly ILogger Logger =
        Log.Logger.ForContext(Constants.SourceContextPropertyName, nameof(ExchangeAccountService).PadRight(22));

    private readonly PortfolioService _portfolioService;
    private readonly Settings         _appSettings;
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    // Bybit EU uses a separate API host from the global platform
    private string BybitBaseUrl => _appSettings.BybitIsEu
        ? "https://api.bybit.eu"
        : "https://api.bybit.com";

    public ExchangeAccountService(PortfolioService portfolioService, Settings appSettings)
    {
        _portfolioService = portfolioService;
        _appSettings      = appSettings;
    }

    // -----------------------------------------------------------------------
    // DPAPI helpers — machine + user bound, never portable
    // -----------------------------------------------------------------------

    private static string Encrypt(string plainText)
    {
        var bytes     = Encoding.UTF8.GetBytes(plainText);
        var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    private static string Decrypt(string cipherBase64)
    {
        var bytes     = Convert.FromBase64String(cipherBase64);
        var decrypted = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(decrypted);
    }

    // -----------------------------------------------------------------------
    // HMAC
    // -----------------------------------------------------------------------

    public async Task SaveHmacAccountAsync(ExchangeKind exchange, string apiKey, string apiSecret)
    {
        var context = _portfolioService.Context
            ?? throw new InvalidOperationException("No DB context available.");

        var existing = await context.ExchangeAccounts
            .FirstOrDefaultAsync(a => a.Exchange == exchange);

        if (existing is not null)
        {
            existing.ApiKeyEncrypted    = Encrypt(apiKey);
            existing.ApiSecretEncrypted = Encrypt(apiSecret);
            existing.AuthMethod         = "HMAC";
            existing.PublicKeyPem       = string.Empty;
            existing.IsActive           = true;
            context.ExchangeAccounts.Update(existing);
        }
        else
        {
            context.ExchangeAccounts.Add(new ExchangeAccount
            {
                Exchange           = exchange,
                ApiKeyEncrypted    = Encrypt(apiKey),
                ApiSecretEncrypted = Encrypt(apiSecret),
                AuthMethod         = "HMAC",
                PublicKeyPem       = string.Empty,
                IsActive           = true,
                Permissions        = string.Empty,
            });
        }

        await context.SaveChangesAsync();
        Logger.Information("ExchangeAccountService: saved {Exchange} HMAC key", exchange);
    }

    // -----------------------------------------------------------------------
    // RSA — step 1: generate key pair
    // -----------------------------------------------------------------------

    public async Task<string> GenerateRsaKeyPairAsync(ExchangeKind exchange)
    {
        var context = _portfolioService.Context
            ?? throw new InvalidOperationException("No DB context available.");

        using var rsa = RSA.Create(2048);

        // Private key: PKCS#8 DER → Base64 → DPAPI encrypt
        var privateKeyDer        = rsa.ExportPkcs8PrivateKey();
        var privateKeyB64        = Convert.ToBase64String(privateKeyDer);
        var privateKeyEncrypted  = Encrypt(privateKeyB64);

        // Public key: SubjectPublicKeyInfo DER → PEM
        var publicKeyDer  = rsa.ExportSubjectPublicKeyInfo();
        var publicKeyB64  = Convert.ToBase64String(publicKeyDer);
        var publicKeyPem  = $"-----BEGIN PUBLIC KEY-----\n{ChunkBase64(publicKeyB64)}\n-----END PUBLIC KEY-----";

        var existing = await context.ExchangeAccounts
            .FirstOrDefaultAsync(a => a.Exchange == exchange);

        if (existing is not null)
        {
            existing.ApiSecretEncrypted = privateKeyEncrypted;
            existing.ApiKeyEncrypted    = string.Empty;   // user pastes this after Bybit step
            existing.AuthMethod         = "RSA";
            existing.PublicKeyPem       = publicKeyPem;
            existing.IsActive           = false;          // not active until API Key is filled in
            context.ExchangeAccounts.Update(existing);
        }
        else
        {
            context.ExchangeAccounts.Add(new ExchangeAccount
            {
                Exchange           = exchange,
                ApiKeyEncrypted    = string.Empty,
                ApiSecretEncrypted = privateKeyEncrypted,
                AuthMethod         = "RSA",
                PublicKeyPem       = publicKeyPem,
                IsActive           = false,
                Permissions        = string.Empty,
            });
        }

        await context.SaveChangesAsync();
        Logger.Information("ExchangeAccountService: RSA key pair generated for {Exchange}", exchange);

        return publicKeyPem;
    }

    // -----------------------------------------------------------------------
    // RSA — step 2: save the API Key returned by Bybit
    // -----------------------------------------------------------------------

    public async Task SaveRsaApiKeyAsync(ExchangeKind exchange, string apiKey)
    {
        var context = _portfolioService.Context
            ?? throw new InvalidOperationException("No DB context available.");

        var existing = await context.ExchangeAccounts
            .FirstOrDefaultAsync(a => a.Exchange == exchange && a.AuthMethod == "RSA");

        if (existing is null)
            throw new InvalidOperationException("Generate an RSA key pair first.");

        existing.ApiKeyEncrypted = Encrypt(apiKey);
        existing.IsActive        = true;
        context.ExchangeAccounts.Update(existing);
        await context.SaveChangesAsync();

        Logger.Information("ExchangeAccountService: RSA API Key saved for {Exchange}", exchange);
    }

    // -----------------------------------------------------------------------
    // Shared read helpers
    // -----------------------------------------------------------------------

    public async Task<string?> GetPublicKeyPemAsync(ExchangeKind exchange)
    {
        var context = _portfolioService.Context;
        if (context is null) return null;

        var account = await context.ExchangeAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Exchange == exchange && a.AuthMethod == "RSA");

        return string.IsNullOrEmpty(account?.PublicKeyPem) ? null : account.PublicKeyPem;
    }

    public async Task<string?> GetAuthMethodAsync(ExchangeKind exchange)
    {
        var context = _portfolioService.Context;
        if (context is null) return null;

        var account = await context.ExchangeAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Exchange == exchange);

        return account?.AuthMethod;
    }

    public async Task<bool> IsConfiguredAsync(ExchangeKind exchange)
    {
        var context = _portfolioService.Context;
        if (context is null) return false;

        return await context.ExchangeAccounts
            .AsNoTracking()
            .AnyAsync(a => a.Exchange == exchange && a.IsActive);
    }

    public async Task DeleteAccountAsync(ExchangeKind exchange)
    {
        var context = _portfolioService.Context;
        if (context is null) return;

        var account = await context.ExchangeAccounts
            .FirstOrDefaultAsync(a => a.Exchange == exchange);

        if (account is null) return;

        context.ExchangeAccounts.Remove(account);
        await context.SaveChangesAsync();
        Logger.Information("ExchangeAccountService: removed {Exchange} account", exchange);
    }

    // -----------------------------------------------------------------------
    // Connection test — dispatches to HMAC or RSA
    // -----------------------------------------------------------------------

    public async Task<(bool Success, string Message)> TestConnectionAsync(ExchangeKind exchange)
    {
        var context = _portfolioService.Context;
        if (context is null) return (false, "Geen database beschikbaar.");

        var account = await context.ExchangeAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Exchange == exchange && a.IsActive);

        if (account is null)
            return (false, "Geen sleutels geconfigureerd.");

        string apiKey;
        try { apiKey = Decrypt(account.ApiKeyEncrypted); }
        catch { return (false, "❌ API Key kan niet worden ontsleuteld — opgeslagen op andere machine?"); }

        return exchange switch
        {
            ExchangeKind.Bybit => account.AuthMethod == "RSA"
                ? await TestBybitRsaAsync(apiKey, account.ApiSecretEncrypted, BybitBaseUrl)
                : await TestBybitHmacAsync(apiKey, account.ApiSecretEncrypted, BybitBaseUrl),
            ExchangeKind.Mexc => await TestMexcHmacAsync(apiKey, account.ApiSecretEncrypted),
            _                 => (false, "Onbekende exchange.")
        };
    }

    // -----------------------------------------------------------------------
    // Bybit V5 — HMAC test (sign-type 2)
    // -----------------------------------------------------------------------

    private static async Task<(bool, string)> TestBybitHmacAsync(string apiKey, string secretEncrypted, string baseUrl)
    {
        string apiSecret;
        try { apiSecret = Decrypt(secretEncrypted); }
        catch { return (false, "❌ Secret kan niet worden ontsleuteld."); }

        const int recvWindow = 5000;
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var paramStr  = $"{timestamp}{apiKey}{recvWindow}";
        var sign      = HmacSha256(apiSecret, paramStr);

        return await CallBybitQueryApiAsync(apiKey, timestamp, sign, "2", recvWindow, baseUrl);
    }

    // -----------------------------------------------------------------------
    // Bybit V5 — RSA test (sign-type 3)
    // -----------------------------------------------------------------------

    private static async Task<(bool, string)> TestBybitRsaAsync(string apiKey, string privateKeyEncrypted, string baseUrl)
    {
        string privateKeyB64;
        try { privateKeyB64 = Decrypt(privateKeyEncrypted); }
        catch { return (false, "❌ Private key kan niet worden ontsleuteld."); }

        const int recvWindow = 5000;
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var paramStr  = $"{timestamp}{apiKey}{recvWindow}";

        string sign;
        try
        {
            var privateKeyDer = Convert.FromBase64String(privateKeyB64);
            using var rsa     = RSA.Create();
            rsa.ImportPkcs8PrivateKey(privateKeyDer, out _);
            var paramBytes    = Encoding.UTF8.GetBytes(paramStr);
            var sigBytes      = rsa.SignData(paramBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            sign              = Convert.ToBase64String(sigBytes);
        }
        catch (Exception ex)
        {
            return (false, $"❌ RSA-signing mislukt: {ex.Message}");
        }

        return await CallBybitQueryApiAsync(apiKey, timestamp, sign, "3", recvWindow, baseUrl);
    }

    // -----------------------------------------------------------------------
    // Shared Bybit HTTP call
    // -----------------------------------------------------------------------

    private static async Task<(bool, string)> CallBybitQueryApiAsync(
        string apiKey, string timestamp, string sign, string signType, int recvWindow, string baseUrl)
    {
        var url = $"{baseUrl}/v5/user/query-api";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-BAPI-API-KEY",    apiKey);
        request.Headers.Add("X-BAPI-TIMESTAMP",  timestamp);
        request.Headers.Add("X-BAPI-SIGN",        sign);
        request.Headers.Add("X-BAPI-SIGN-TYPE",  signType);
        request.Headers.Add("X-BAPI-RECV-WINDOW", recvWindow.ToString());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            var response = await _http.SendAsync(request);
            var body     = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(body);
            var retCode   = doc.RootElement.GetProperty("retCode").GetInt32();
            var retMsg    = doc.RootElement.GetProperty("retMsg").GetString() ?? string.Empty;

            if (retCode == 0)
            {
                Logger.Information("Bybit connection test OK (sign-type {Type})", signType);
                return (true, "✅ Verbinding geslaagd — sleutels zijn geldig");
            }

            Logger.Warning("Bybit test failed: {Code} {Msg} (url={Url})", retCode, retMsg, url);
            return (false, $"❌ Bybit {retCode} — {retMsg}\n({url})");
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Bybit connection test exception");
            return (false, $"❌ Fout: {ex.Message}");
        }
    }

    // -----------------------------------------------------------------------
    // Balans verificatie
    // -----------------------------------------------------------------------

    public async Task<List<BalanceComparison>> VerifyExchangeBalancesAsync(ExchangeKind exchange, string accountName)
    {
        var context = _portfolioService.Context;
        if (context is null) return new();

        // Haal exchange-balansen op
        var exchangeBalances = await FetchExchangeBalancesAsync(exchange);

        // Haal app-assets op voor dit account
        var appAssets = await context.Assets
            .AsNoTracking()
            .Include(a => a.Coin)
            .Include(a => a.Account)
            .Where(a => a.Account.Name == accountName && a.Qty > 0)
            .ToListAsync();

        var results = new List<BalanceComparison>();

        // Vergelijk app-assets met exchange
        foreach (var asset in appAssets)
        {
            var rawSymbol  = asset.Coin.Symbol;
            var normSymbol = NormalizeSymbol(rawSymbol);

            if (!exchangeBalances.TryGetValue(normSymbol, out var exchangeQty))
            {
                // Secundaire match op coin-naam (genormaliseerd):
                // MEXC gebruikt soms de volledige naam als ticker,
                // bijv. "ALTLAYER" voor ALT/Altlayer of "WEN" voor $WEN/Wen.
                var normName = NormalizeName(asset.Coin.Name);
                exchangeBalances.TryGetValue(normName, out exchangeQty);
            }

            // Toon de app-notatie als weergavenaam
            results.Add(new BalanceComparison(rawSymbol, asset.Qty, exchangeQty));
        }

        // Coins die WEL op exchange staan maar NIET in app
        var appNormSymbols = new HashSet<string>(appAssets.Select(a => NormalizeSymbol(a.Coin.Symbol)), StringComparer.OrdinalIgnoreCase);
        var appNormNames   = new HashSet<string>(appAssets.Select(a => NormalizeName(a.Coin.Name)),     StringComparer.OrdinalIgnoreCase);
        foreach (var (symbol, qty) in exchangeBalances)
        {
            if (!appNormSymbols.Contains(symbol) && !appNormNames.Contains(symbol))
                results.Add(new BalanceComparison(symbol, 0, qty));
        }

        return results.OrderBy(r => r.Symbol).ToList();
    }

    private async Task<Dictionary<string, double>> FetchExchangeBalancesAsync(ExchangeKind exchange)
    {
        var context = _portfolioService.Context;
        if (context is null) return new();

        var account = await context.ExchangeAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Exchange == exchange && a.IsActive);

        if (account is null) return new();

        string apiKey;
        try { apiKey = Decrypt(account.ApiKeyEncrypted); }
        catch { return new(); }

        return exchange switch
        {
            ExchangeKind.Mexc  => await FetchMexcBalancesAsync(apiKey, account.ApiSecretEncrypted),
            ExchangeKind.Bybit => new(), // later uitbreiden
            _                  => new()
        };
    }

    private static async Task<Dictionary<string, double>> FetchMexcBalancesAsync(string apiKey, string secretEncrypted)
    {
        string apiSecret;
        try { apiSecret = Decrypt(secretEncrypted); }
        catch { return new(); }

        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        // ── Spot wallet ──────────────────────────────────────────────────────
        try
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            var queryStr  = $"timestamp={timestamp}";
            var sign      = HmacSha256(apiSecret, queryStr);
            var url       = $"https://api.mexc.com/api/v3/account?{queryStr}&signature={sign}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-MEXC-APIKEY", apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _http.SendAsync(request);
            var body     = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(body);
                foreach (var b in doc.RootElement.GetProperty("balances").EnumerateArray())
                {
                    var sym   = NormalizeSymbol(b.GetProperty("asset").GetString() ?? string.Empty);
                    var free  = double.Parse(b.GetProperty("free").GetString()   ?? "0", System.Globalization.CultureInfo.InvariantCulture);
                    var locked= double.Parse(b.GetProperty("locked").GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
                    var total = free + locked;
                    if (total > 0)
                        result[sym] = result.TryGetValue(sym, out var existing) ? existing + total : total;
                }
            }
        }
        catch { /* spot mislukking: doorgaan met futures */ }

        // ── Futures wallet ───────────────────────────────────────────────────
        // Futures worden als aparte regels opgeslagen ("USDT (Futures)") zodat
        // de balansverificatie spot en futures afzonderlijk weergeeft.
        try
        {
            var ts      = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            var toSign  = apiKey + ts;
            var sign    = HmacSha256(apiSecret, toSign);
            var url     = "https://contract.mexc.com/api/v1/private/account/assets";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("ApiKey",       apiKey);
            request.Headers.Add("Request-Time", ts);
            request.Headers.Add("Signature",    sign);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _http.SendAsync(request);
            var body     = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("data", out var data))
                {
                    foreach (var b in data.EnumerateArray())
                    {
                        var sym  = NormalizeSymbol(b.GetProperty("currency").GetString() ?? string.Empty);
                        if (!b.TryGetProperty("cashBalance", out var cb)) continue;
                        var total = cb.GetDouble();
                        if (total > 0)
                        {
                            // Aparte sleutel zodat Spot en Futures als twee regels verschijnen
                            var futuresKey = $"{sym} (Futures)";
                            result[futuresKey] = result.TryGetValue(futuresKey, out var existing) ? existing + total : total;
                        }
                    }
                }
            }
        }
        catch { /* futures mislukking: spot-resultaat is al gevuld */ }

        return result;
    }

    // -----------------------------------------------------------------------
    // MEXC V3 — HMAC test
    // -----------------------------------------------------------------------

    private static async Task<(bool, string)> TestMexcHmacAsync(string apiKey, string secretEncrypted)
    {
        string apiSecret;
        try { apiSecret = Decrypt(secretEncrypted); }
        catch { return (false, "❌ Secret kan niet worden ontsleuteld."); }

        var timestamp  = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var queryStr   = $"timestamp={timestamp}";
        var sign       = HmacSha256(apiSecret, queryStr);
        var url        = $"https://api.mexc.com/api/v3/account?{queryStr}&signature={sign}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-MEXC-APIKEY", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            var response = await _http.SendAsync(request);
            var body     = await response.Content.ReadAsStringAsync();

            // Succes: HTTP 200 met JSON dat balances bevat
            if (response.IsSuccessStatusCode)
            {
                Logger.Information("MEXC connection test OK");
                return (true, "✅ Verbinding geslaagd — MEXC-sleutels zijn geldig");
            }

            // Fout: probeer retCode/msg te lezen
            try
            {
                using var doc = JsonDocument.Parse(body);
                var code = doc.RootElement.TryGetProperty("code", out var c) ? c.GetInt32() : 0;
                var msg  = doc.RootElement.TryGetProperty("msg", out var m)  ? m.GetString() ?? body : body;
                Logger.Warning("MEXC test failed: {Code} {Msg}", code, msg);
                return (false, $"❌ MEXC {code} — {msg}");
            }
            catch
            {
                return (false, $"❌ MEXC HTTP {(int)response.StatusCode} — {body}");
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "MEXC connection test exception");
            return (false, $"❌ Fout: {ex.Message}");
        }
    }

    // -----------------------------------------------------------------------
    // MEXC automatische trade-synchronisatie (alleen toekomstige trades)
    // -----------------------------------------------------------------------

    private static readonly string SyncBaselineFile =
        Path.Combine(AppConstants.AppDataPath, "mexc_sync_baseline.txt");

    public async Task<(int Imported, int Skipped)> SyncMexcTradesAsync(string accountName)
    {
        // ── Baseline: eerste keer = baseline zetten, niks importeren ────────
        if (!File.Exists(SyncBaselineFile))
        {
            File.WriteAllText(SyncBaselineFile, DateTime.UtcNow.ToString("O"));
            Logger.Information("SyncMexcTrades: eerste run, baseline gezet op {Now} — toekomstige trades worden geïmporteerd", DateTime.UtcNow);
            return (0, 0);
        }

        if (!DateTime.TryParse(File.ReadAllText(SyncBaselineFile).Trim(), null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var syncFrom))
            syncFrom = DateTime.UtcNow.AddHours(-1);

        var startTimeMs = new DateTimeOffset(syncFrom, TimeSpan.Zero).ToUnixTimeMilliseconds();

        // ── Credentials ophalen ──────────────────────────────────────────────
        var context = _portfolioService.Context;
        if (context is null) return (0, 0);

        var exchangeAccount = await context.ExchangeAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Exchange == ExchangeKind.Mexc && a.IsActive);
        if (exchangeAccount is null) return (0, 0);

        string apiKey, apiSecret;
        try
        {
            apiKey    = Decrypt(exchangeAccount.ApiKeyEncrypted);
            apiSecret = Decrypt(exchangeAccount.ApiSecretEncrypted);
        }
        catch { return (0, 0); }

        // ── Portfolio-account + coins ophalen ───────────────────────────────
        var portfolioAccountId = await context.Accounts
            .AsNoTracking()
            .Where(a => a.Name == accountName)
            .Select(a => a.Id)
            .FirstOrDefaultAsync();
        if (portfolioAccountId == 0)
        {
            Logger.Warning("SyncMexcTrades: account '{Name}' niet gevonden", accountName);
            return (0, 0);
        }

        var assets = await context.Assets
            .AsNoTracking()
            .Include(a => a.Coin)
            .Where(a => a.Account.Name == accountName
                     && a.Coin.Symbol.ToLower() != "usdt"
                     && a.Coin.Symbol.ToLower() != "usdc")
            .ToListAsync();

        if (!assets.Any()) return (0, 0);

        // ── Bestaande SourceIds laden voor deduplicatie ──────────────────────
        var existingSourceIds = new HashSet<string>(
            await context.Transactions
                .AsNoTracking()
                .Where(t => t.SourceId != null)
                .Select(t => t.SourceId!)
                .ToListAsync(),
            StringComparer.Ordinal);

        int imported = 0, skipped = 0;

        // ── Per coin trades ophalen en verwerken ─────────────────────────────
        foreach (var asset in assets)
        {
            var coinSymbol = NormalizeSymbol(asset.Coin.Symbol);

            var trades = await FetchMexcMyTradesAsync(apiKey, apiSecret, coinSymbol + "USDT", startTimeMs);
            if (trades is null)
            {
                var normName = NormalizeName(asset.Coin.Name);
                trades = await FetchMexcMyTradesAsync(apiKey, apiSecret, normName + "USDT", startTimeMs);
            }
            if (trades is null || !trades.Any()) continue;

            foreach (var trade in trades)
            {
                var sourceId = $"MEXC:{trade.Id}";
                if (existingSourceIds.Contains(sourceId)) { skipped++; continue; }

                try
                {
                    context.ChangeTracker.Clear();

                    // Laad tracked entiteiten voor deze trade
                    var coin    = await context.Coins.FindAsync(asset.Coin.Id);
                    var account = await context.Accounts.FindAsync(portfolioAccountId);
                    if (coin is null || account is null) continue;

                    // Haal bestaande asset op of maak nieuw
                    var dbAsset = await context.Assets
                        .Where(a => a.Coin.Symbol.ToLower() == coin.Symbol.ToLower()
                                 && a.Account.Name.ToLower() == account.Name.ToLower())
                        .Include(a => a.Coin)
                        .Include(a => a.Account)
                        .FirstOrDefaultAsync();

                    if (dbAsset is null)
                    {
                        dbAsset = new Asset { Coin = coin, Account = account, Qty = 0, AverageCostPrice = 0 };
                        context.Assets.Add(dbAsset);
                        coin.IsAsset = true;
                        await context.SaveChangesAsync();
                        context.ChangeTracker.Clear();
                        coin    = await context.Coins.FindAsync(asset.Coin.Id);
                        account = await context.Accounts.FindAsync(portfolioAccountId);
                        dbAsset = await context.Assets
                            .Where(a => a.Coin.Symbol.ToLower() == coin!.Symbol.ToLower()
                                     && a.Account.Name.ToLower() == account!.Name.ToLower())
                            .Include(a => a.Coin).Include(a => a.Account)
                            .FirstAsync();
                    }

                    // Bijwerken qty + gemiddelde aankoopprijs
                    if (trade.IsBuyer)
                    {
                        var newQty = dbAsset.Qty + trade.Qty;
                        dbAsset.AverageCostPrice = newQty > 0
                            ? (dbAsset.Qty * dbAsset.AverageCostPrice + trade.Qty * trade.Price) / newQty
                            : 0;
                        dbAsset.Qty = newQty;
                    }
                    else
                    {
                        dbAsset.Qty = Math.Max(0, dbAsset.Qty - trade.Qty);
                    }

                    // Aanmaken Mutation + Transaction
                    var mutation = new Mutation
                    {
                        Type      = trade.IsBuyer ? TransactionKind.Deposit : TransactionKind.Withdraw,
                        Direction = trade.IsBuyer ? MutationDirection.In    : MutationDirection.Out,
                        Qty       = trade.Qty,
                        Price     = trade.Price,
                        Asset     = dbAsset
                    };
                    var transaction = new Transaction
                    {
                        TimeStamp = trade.TradeTime,
                        Note      = string.Empty,
                        SourceId  = sourceId,
                        Mutations = new List<Mutation> { mutation }
                    };
                    mutation.Transaction = transaction;

                    context.Transactions.Add(transaction);
                    await context.SaveChangesAsync();
                    context.ChangeTracker.Clear();

                    existingSourceIds.Add(sourceId);
                    imported++;
                    Logger.Debug("SyncMexcTrades: {Kind} {Coin} qty={Qty} @ {Price} ({Id})",
                        trade.IsBuyer ? "Deposit" : "Withdraw", asset.Coin.Symbol, trade.Qty, trade.Price, trade.Id);
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "SyncMexcTrades: fout bij verwerken trade {Id}", trade.Id);
                    try { context.ChangeTracker.Clear(); } catch { }
                }
            }
        }

        // Baseline bijwerken voor volgende sync
        File.WriteAllText(SyncBaselineFile, DateTime.UtcNow.ToString("O"));
        Logger.Information("SyncMexcTrades: {Imported} geïmporteerd, {Skipped} overgeslagen", imported, skipped);
        return (imported, skipped);
    }

    // ── MEXC myTrades API-aanroep ────────────────────────────────────────────

    private sealed record MexcTrade(
        string   Id,
        double   Price,
        double   Qty,
        double   QuoteQty,
        DateTime TradeTime,
        bool     IsBuyer);

    /// <summary>
    /// Haalt gevulde trades op voor het handelspaar, gefilterd op startTimeMs.
    /// Geeft null terug als het paar onbekend is (HTTP 400) of bij een netwerkfout.
    /// </summary>
    private static async Task<List<MexcTrade>?> FetchMexcMyTradesAsync(
        string apiKey, string apiSecret, string symbol, long startTimeMs)
    {
        try
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            var queryStr  = $"symbol={symbol}&startTime={startTimeMs}&limit=1000&timestamp={timestamp}";
            var sign      = HmacSha256(apiSecret, queryStr);
            var url       = $"https://api.mexc.com/api/v3/myTrades?{queryStr}&signature={sign}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-MEXC-APIKEY", apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _http.SendAsync(request);
            var body     = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Logger.Debug("SyncMexcTrades: {Symbol} → HTTP {Code} (overgeslagen)", symbol, (int)response.StatusCode);
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            var trades    = new List<MexcTrade>();
            foreach (var t in doc.RootElement.EnumerateArray())
            {
                var id       = t.GetProperty("id").GetString()       ?? string.Empty;
                var price    = double.Parse(t.GetProperty("price").GetString()    ?? "0", System.Globalization.CultureInfo.InvariantCulture);
                var qty      = double.Parse(t.GetProperty("qty").GetString()      ?? "0", System.Globalization.CultureInfo.InvariantCulture);
                var quoteQty = double.Parse(t.GetProperty("quoteQty").GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
                var timeMs   = t.GetProperty("time").GetInt64();
                var isBuyer  = t.GetProperty("isBuyer").GetBoolean();
                trades.Add(new MexcTrade(id, price, qty, quoteQty,
                    DateTimeOffset.FromUnixTimeMilliseconds(timeMs).LocalDateTime, isBuyer));
            }
            return trades;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "SyncMexcTrades: FetchMyTrades fout voor {Symbol}", symbol);
            return null;
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static string HmacSha256(string secret, string message)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(message))).ToLowerInvariant();
    }

    /// <summary>
    /// Normaliseert een coin-symbool voor vergelijking:
    /// verwijdert '$'-prefix en converteert naar uppercase.
    /// Hierdoor worden "$MYRO" en "MYRO" als gelijk beschouwd.
    /// </summary>
    private static string NormalizeSymbol(string symbol)
        => symbol.TrimStart('$').ToUpperInvariant();

    /// <summary>
    /// Normaliseert een coin-naam voor secundaire matching:
    /// verwijdert spaties, koppeltekens en punten, converteert naar uppercase.
    /// Bijv. "Altlayer" → "ALTLAYER", "My Lovely Coin" → "MYLOVELYCOIN".
    /// MEXC gebruikt soms de volledige naam (zonder spaties) als ticker.
    /// </summary>
    private static string NormalizeName(string name)
        => name.ToUpperInvariant()
               .Replace(" ", "")
               .Replace("-", "")
               .Replace(".", "");

    /// <summary>Wraps a Base64 string at 64 characters per line (PEM convention).
    /// Uses Unix line endings (\n) — required by most PEM parsers including Bybit.</summary>
    private static string ChunkBase64(string b64, int lineLength = 64)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < b64.Length; i += lineLength)
        {
            if (i > 0) sb.Append('\n');
            sb.Append(b64.Substring(i, Math.Min(lineLength, b64.Length - i)));
        }
        return sb.ToString();
    }
}
