using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Pure vertaling van een <see cref="OrderRequest"/> naar een Bybit spot-order (v1.47).
///
/// Uitgangspunten (Bybit EU Demo = alleen spot):
/// <list type="bullet">
///   <item>Alleen kopen (Long). Shorten kan op spot niet.</item>
///   <item>Instap is altijd een <b>limit</b>-order, zodat stop-loss en take-profit direct aan de order
///         gekoppeld kunnen worden en Bybit ze zelf bewaakt. Een "market"-order wordt nagebootst met een
///         limit iets boven de vraagprijs (<see cref="MarketSlippagePct"/>): vult direct, maar nooit
///         duurder dan die grens.</item>
///   <item>Hoeveelheid en prijzen worden afgerond op de stapgroottes van het paar; minimale
///         hoeveelheid en orderwaarde worden gecontroleerd vóór verzenden.</item>
///   <item>Eén take-profit (TP1). TP2 en gedeeltelijk sluiten worden (nog) niet op Bybit gezet.</item>
/// </list>
/// </summary>
public static class BybitOrderPlanner
{
    /// <summary>Maximale prijsafwijking boven de ask bij een nagebootste market-order.</summary>
    public const decimal MarketSlippagePct = 0.5m;

    public static BybitOrderPlan Plan(
        OrderRequest req,
        decimal askPrice,
        BybitSpotInstrument inst,
        string orderLinkId)
    {
        var errors   = new List<string>();
        var warnings = new List<string>();

        if (req.Side != OrderSide.Buy)
            errors.Add("Shorten kan niet op spot. Bybit EU Demo ondersteunt alleen spot — kies Long of gebruik paper trading.");
        if (req.MarketType != MarketType.Spot)
            errors.Add("Alleen spot-orders worden ondersteund op Bybit EU Demo (geen margin/futures).");
        if (!inst.IsTrading)
            errors.Add($"{inst.Symbol} is op dit moment niet verhandelbaar op Bybit (status: {inst.Status}).");
        if (req.AmountUsdt <= 0)
            errors.Add("Het orderbedrag moet groter dan 0 zijn.");

        // ── Prijs ────────────────────────────────────────────────────────────
        bool emulateMarket = !(req.OrderType == OrderType.Limit && req.LimitPrice > 0);
        decimal price;
        if (emulateMarket)
        {
            if (askPrice <= 0) errors.Add("Geen actuele koers van Bybit ontvangen.");
            price = RoundUp(askPrice * (1 + MarketSlippagePct / 100m), inst.TickSize);
        }
        else
        {
            price = RoundDown((decimal)req.LimitPrice, inst.TickSize);
            if (askPrice > 0 && price > askPrice * 1.05m)
                warnings.Add("De limietprijs ligt meer dan 5% boven de huidige koers — de order vult direct tegen marktprijs.");
        }

        // Referentie voor SL/TP-controle: de verwachte instap.
        decimal reference = emulateMarket ? askPrice : price;

        // ── Hoeveelheid ──────────────────────────────────────────────────────
        decimal qty = 0;
        if (price > 0 && req.AmountUsdt > 0)
        {
            qty = RoundDown((decimal)req.AmountUsdt / price, inst.BasePrecision);
            if (qty <= 0 || qty < inst.MinOrderQty)
                errors.Add($"Hoeveelheid {BybitApi.Num(qty)} {inst.BaseCoin} is kleiner dan het minimum ({BybitApi.Num(inst.MinOrderQty)}). Verhoog het bedrag.");
            else if (inst.MinOrderAmt > 0 && qty * price < inst.MinOrderAmt)
                errors.Add($"Orderwaarde {BybitApi.Num(Math.Round(qty * price, 2))} {inst.QuoteCoin} is lager dan het minimum ({BybitApi.Num(inst.MinOrderAmt)} {inst.QuoteCoin}).");
        }

        // ── Stop-loss / take-profit ──────────────────────────────────────────
        decimal sl = req.StopLossPrice   > 0 ? RoundDown((decimal)req.StopLossPrice,   inst.TickSize) : 0;
        decimal tp = req.TakeProfitPrice > 0 ? RoundUp  ((decimal)req.TakeProfitPrice, inst.TickSize) : 0;

        if (sl > 0 && reference > 0 && sl >= reference)
            errors.Add($"Stop-loss ({BybitApi.Num(sl)}) moet onder de instap ({BybitApi.Num(reference)}) liggen.");
        if (tp > 0 && reference > 0 && tp <= reference)
            errors.Add($"Take-profit ({BybitApi.Num(tp)}) moet boven de instap ({BybitApi.Num(reference)}) liggen.");
        if (sl <= 0)
            warnings.Add("Geen stop-loss ingesteld — de positie is niet beschermd op Bybit.");

        if (req.TakeProfit2Price > 0)
            warnings.Add("TP2 wordt niet op Bybit gezet; alleen TP1 (volledige positie).");
        if (req.TakeProfitPrice > 0 && req.Tp1ClosePct < 100)
            warnings.Add($"Gedeeltelijk sluiten ({req.Tp1ClosePct:0}% op TP1) wordt niet ondersteund; Bybit sluit de hele positie op TP1.");

        string body = errors.Count == 0
            ? BuildBody(inst.Symbol, qty, price, tp, sl, orderLinkId)
            : string.Empty;

        return new BybitOrderPlan(
            IsValid:          errors.Count == 0,
            Errors:           errors,
            Warnings:         warnings,
            Symbol:           inst.Symbol,
            Qty:              qty,
            Price:            price,
            TakeProfit:       tp,
            StopLoss:         sl,
            IsMarketEmulated: emulateMarket,
            OrderLinkId:      orderLinkId,
            JsonBody:         body);
    }

    /// <summary>JSON-body voor <c>POST /v5/order/create</c> (getallen als strings, invariant).</summary>
    public static string BuildBody(string symbol, decimal qty, decimal price, decimal tp, decimal sl, string orderLinkId)
    {
        var body = new Dictionary<string, string>
        {
            ["category"]    = "spot",
            ["symbol"]      = symbol,
            ["side"]        = "Buy",
            ["orderType"]   = "Limit",
            ["qty"]         = BybitApi.Num(qty),
            ["price"]       = BybitApi.Num(price),
            ["timeInForce"] = "GTC",
            ["orderLinkId"] = orderLinkId,
        };
        if (tp > 0) { body["takeProfit"] = BybitApi.Num(tp); body["tpOrderType"] = "Market"; }
        if (sl > 0) { body["stopLoss"]   = BybitApi.Num(sl); body["slOrderType"] = "Market"; }
        return JsonSerializer.Serialize(body);
    }

    /// <summary>JSON-body voor een market-verkoop van <paramref name="qty"/> basismunt (handmatig sluiten).</summary>
    public static string BuildMarketSellBody(string symbol, decimal qty, string orderLinkId)
        => JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["category"]    = "spot",
            ["symbol"]      = symbol,
            ["side"]        = "Sell",
            ["orderType"]   = "Market",
            ["qty"]         = BybitApi.Num(qty),
            ["marketUnit"]  = "baseCoin",
            ["orderLinkId"] = orderLinkId,
        });

    /// <summary>Uniek order-id (max. 36 tekens) zodat de app zijn eigen orders terugvindt.</summary>
    public static string NewOrderLinkId(string prefix = "cpt")
        => $"{prefix}-{Guid.NewGuid():N}"[..Math.Min(36, prefix.Length + 33)];

    /// <summary>Spot-symbool bij een munt en quote-munt, bv. ("sol", "USDC") → "SOLUSDC".</summary>
    public static string SymbolFor(string coinSymbol, string quoteCoin)
        => $"{(coinSymbol ?? string.Empty).Trim().TrimStart('$').ToUpperInvariant()}{quoteCoin.ToUpperInvariant()}";

    // ── Afronden op stapgrootte ─────────────────────────────────────────────

    public static decimal RoundDown(decimal value, decimal step)
        => step <= 0 ? value : Math.Floor(value / step) * step;

    public static decimal RoundUp(decimal value, decimal step)
        => step <= 0 ? value : Math.Ceiling(value / step) * step;
}
