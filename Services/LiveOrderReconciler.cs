using System;
using System.Collections.Generic;
using System.Linq;
using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

/// <summary>Nieuwe toestand van een live/demo-order na synchronisatie met de exchange.</summary>
public sealed record LiveOrderUpdate(
    OrderStatus Status,
    double      Entry,
    double      Qty,
    DateTime?   FilledAt,
    double      ClosePrice,
    DateTime?   ClosedAt,
    string?     Event)        // mensgerichte omschrijving van wat er veranderde (null = niets)
{
    public bool Changed => Event is not null;
}

/// <summary>
/// Pure synchronisatie-logica voor spot-orders op Bybit (v1.47). Vertaalt de orderstatus en de fills
/// van Bybit naar de toestand van een <see cref="ExchangeOrder"/> in de app:
/// <list type="bullet">
///   <item>Pending → Filled (of PartiallyFilled) zodra de instaporder gevuld is; instapprijs = gemiddelde
///         vulprijs, hoeveelheid = netto ontvangen munten (kopen-fee in de basismunt gaat eraf).</item>
///   <item>Pending → Cancelled/Rejected als Bybit de order annuleert zonder vulling.</item>
///   <item>Filled → Closed zodra er (vrijwel) de hele positie weer verkocht is — door de gekoppelde
///         TP/SL of handmatig. Sluitprijs = gewogen gemiddelde van de verkopen.</item>
/// </list>
/// </summary>
public static class LiveOrderReconciler
{
    /// <summary>Een positie geldt als gesloten als minstens dit deel verkocht is (afronding/fees).</summary>
    public const decimal ClosedFraction = 0.98m;

    public static LiveOrderUpdate Reconcile(
        ExchangeOrder order,
        BybitOrderInfo? entryOrder,
        IReadOnlyList<BybitExecution> symbolExecutions,
        string baseCoin)
    {
        var status   = order.Status;
        var entry    = order.Entry;
        var qty      = order.Qty;
        var filledAt = order.FilledAt;
        var close    = order.ClosePrice;
        var closedAt = order.ClosedAt;
        string? evt  = null;

        // ── 1. Instaporder ──────────────────────────────────────────────────
        if (status is OrderStatus.Pending or OrderStatus.PartiallyFilled && entryOrder is not null)
        {
            var entryFills = symbolExecutions
                .Where(x => x.OrderId == entryOrder.OrderId && x.Side == "Buy")
                .ToList();

            decimal grossQty = entryOrder.CumExecQty;
            decimal netQty   = NetBoughtQty(grossQty, entryFills, baseCoin);
            decimal avg      = entryOrder.AvgPrice > 0
                ? entryOrder.AvgPrice
                : (grossQty > 0 ? entryOrder.CumExecValue / grossQty : 0);

            switch (entryOrder.OrderStatus)
            {
                case "Filled":
                    status   = OrderStatus.Filled;
                    entry    = (double)avg;
                    qty      = (double)netQty;
                    filledAt = FirstFillTime(entryFills, entryOrder);
                    evt      = $"gevuld @ {BybitApi.Num(avg)}";
                    break;

                case "PartiallyFilled":
                case "PartiallyFilledCanceled":
                    if (grossQty > 0 && (status != OrderStatus.PartiallyFilled || (double)netQty != qty))
                    {
                        bool done = entryOrder.OrderStatus == "PartiallyFilledCanceled";
                        status   = done ? OrderStatus.Filled : OrderStatus.PartiallyFilled;
                        entry    = (double)avg;
                        qty      = (double)netQty;
                        filledAt = FirstFillTime(entryFills, entryOrder);
                        evt      = done ? $"deels gevuld en rest geannuleerd @ {BybitApi.Num(avg)}"
                                        : $"deels gevuld ({BybitApi.Num(grossQty)}) @ {BybitApi.Num(avg)}";
                    }
                    break;

                case "Cancelled":
                case "Deactivated":
                    if (grossQty > 0)
                    {
                        status   = OrderStatus.Filled;
                        entry    = (double)avg;
                        qty      = (double)netQty;
                        filledAt = FirstFillTime(entryFills, entryOrder);
                        evt      = $"deels gevuld, rest geannuleerd @ {BybitApi.Num(avg)}";
                    }
                    else
                    {
                        status = OrderStatus.Cancelled;
                        evt    = "geannuleerd op Bybit";
                    }
                    break;

                case "Rejected":
                    status = OrderStatus.Rejected;
                    evt    = string.IsNullOrWhiteSpace(entryOrder.RejectReason)
                        ? "afgewezen door Bybit"
                        : $"afgewezen door Bybit: {entryOrder.RejectReason}";
                    break;
            }
        }

        // ── 2. Sluiting (TP/SL of handmatig) ────────────────────────────────
        if (status == OrderStatus.Filled && qty > 0)
        {
            var since = filledAt ?? order.CreatedAt;
            long sinceMs = new DateTimeOffset(DateTime.SpecifyKind(since, DateTimeKind.Utc)).ToUnixTimeMilliseconds() - 1000;
            string entryId = entryOrder?.OrderId ?? string.Empty;

            var sells = symbolExecutions
                .Where(x => x.Side == "Sell" && x.ExecTimeMs >= sinceMs && x.OrderId != entryId)
                .ToList();

            decimal sold = sells.Sum(x => x.ExecQty);
            if (sold > 0 && sold >= (decimal)qty * ClosedFraction)
            {
                decimal vwap = sells.Sum(x => x.ExecPrice * x.ExecQty) / sold;
                status   = OrderStatus.Closed;
                close    = (double)vwap;
                closedAt = DateTimeOffset.FromUnixTimeMilliseconds(sells.Max(x => x.ExecTimeMs)).UtcDateTime;

                string reason = CloseReason(sells);
                evt = evt is null
                    ? $"gesloten ({reason}) @ {BybitApi.Num(Math.Round(vwap, 8))}"
                    : $"{evt}; gesloten ({reason}) @ {BybitApi.Num(Math.Round(vwap, 8))}";
            }
        }

        return new LiveOrderUpdate(status, entry, qty, filledAt, close, closedAt, evt);
    }

    /// <summary>Past een update toe op de order.</summary>
    public static void Apply(ExchangeOrder order, LiveOrderUpdate u)
    {
        order.Status     = u.Status;
        order.Entry      = u.Entry;
        order.Qty        = u.Qty;
        order.FilledAt   = u.FilledAt;
        order.ClosePrice = u.ClosePrice;
        order.ClosedAt   = u.ClosedAt;
    }

    /// <summary>Bij kopen op spot gaat de fee van de ontvangen basismunt af.</summary>
    public static decimal NetBoughtQty(decimal grossQty, IEnumerable<BybitExecution> buyFills, string baseCoin)
    {
        decimal feeInBase = buyFills
            .Where(x => string.Equals(x.FeeCurrency, baseCoin, StringComparison.OrdinalIgnoreCase))
            .Sum(x => x.ExecFee);
        return Math.Max(0, grossQty - feeInBase);
    }

    public static string CloseReason(IEnumerable<BybitExecution> sells)
    {
        var types = sells.Select(s => s.StopOrderType ?? string.Empty).ToList();
        if (types.Any(t => t.Contains("StopLoss", StringComparison.OrdinalIgnoreCase)))   return "stop-loss";
        if (types.Any(t => t.Contains("TakeProfit", StringComparison.OrdinalIgnoreCase))) return "take-profit";
        return "verkocht";
    }

    private static DateTime FirstFillTime(IReadOnlyList<BybitExecution> fills, BybitOrderInfo order)
    {
        long ms = fills.Count > 0 ? fills.Min(f => f.ExecTimeMs)
                : order.UpdatedTimeMs > 0 ? order.UpdatedTimeMs
                : order.CreatedTimeMs;
        return ms > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime : DateTime.UtcNow;
    }
}
