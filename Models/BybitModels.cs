using System;
using System.Collections.Generic;

namespace CryptoPortfolioTracker.Models;

/// <summary>
/// Handelsregels van één Bybit spot-paar (uit <c>/v5/market/instruments-info?category=spot</c>).
/// Bedragen als <see cref="decimal"/> zodat afronden op stapgroottes exact gaat.
/// </summary>
public sealed record BybitSpotInstrument(
    string  Symbol,          // bv. "SOLUSDC"
    string  BaseCoin,        // "SOL"
    string  QuoteCoin,       // "USDC"
    string  Status,          // "Trading"
    decimal BasePrecision,   // stapgrootte van de hoeveelheid, bv. 0.001
    decimal QuotePrecision,  // stapgrootte van het bedrag in quote, bv. 0.0001
    decimal MinOrderQty,
    decimal MinOrderAmt,     // minimale orderwaarde in quote
    decimal TickSize)        // stapgrootte van de prijs
{
    public bool IsTrading => string.Equals(Status, "Trading", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Status van één order bij Bybit (uit <c>/v5/order/realtime</c>).</summary>
public sealed record BybitOrderInfo(
    string  OrderId,
    string  OrderLinkId,
    string  Symbol,
    string  Side,            // "Buy" / "Sell"
    string  OrderStatus,     // New / PartiallyFilled / Filled / Cancelled / Rejected / Untriggered / Deactivated …
    decimal Price,
    decimal Qty,
    decimal AvgPrice,
    decimal CumExecQty,
    decimal CumExecValue,
    decimal TakeProfit,
    decimal StopLoss,
    string  RejectReason,
    long    CreatedTimeMs,
    long    UpdatedTimeMs);

/// <summary>Eén uitvoering (fill) bij Bybit (uit <c>/v5/execution/list</c>).</summary>
public sealed record BybitExecution(
    string  ExecId,
    string  OrderId,
    string  OrderLinkId,
    string  Symbol,
    string  Side,
    decimal ExecPrice,
    decimal ExecQty,
    decimal ExecFee,
    string  FeeCurrency,
    string  StopOrderType,   // "" / "TakeProfit" / "StopLoss" / "tpslOrder" …
    long    ExecTimeMs);

/// <summary>Saldo van één munt in het Unified Trading Account.</summary>
public sealed record BybitCoinBalance(string Coin, decimal WalletBalance, decimal Locked)
{
    public decimal Available => Math.Max(0, WalletBalance - Locked);
}

/// <summary>Resultaat van een Bybit-call: retCode 0 = gelukt.</summary>
public sealed record BybitResult<T>(bool Ok, int RetCode, string RetMsg, T? Data)
{
    public static BybitResult<T> Fail(int code, string msg) => new(false, code, msg, default);
    public static BybitResult<T> Success(T data) => new(true, 0, "OK", data);
}

/// <summary>Ontsleutelde API-gegevens voor één exchange-account (alleen in het geheugen).</summary>
public sealed record ExchangeCredentials(string ApiKey, string Secret, string AuthMethod);

/// <summary>
/// Door <see cref="Services.BybitOrderPlanner"/> berekende spot-order, klaar om te versturen.
/// Bij <see cref="IsValid"/> = false bevat <see cref="Errors"/> de redenen (Nederlands).
/// </summary>
public sealed record BybitOrderPlan(
    bool    IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    string  Symbol,
    decimal Qty,
    decimal Price,
    decimal TakeProfit,
    decimal StopLoss,
    bool    IsMarketEmulated,
    string  OrderLinkId,
    string  JsonBody);
