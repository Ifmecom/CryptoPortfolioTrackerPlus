using CryptoPortfolioTracker.Enums;

namespace CryptoPortfolioTracker.Models;

/// <summary>
/// Lightweight value object carrying all user-specified order parameters.
/// SL/TP values are ABSOLUTE PRICES (not percentages).
/// A value of 0 means "not set".
/// </summary>
public record OrderRequest(
    ExchangeKind Exchange,
    OrderSide    Side,
    MarketType   MarketType,
    OrderType    OrderType,
    double       AmountUsdt,
    double       LimitPrice,       // 0 = market order (use current price)
    double       StopLossPrice,    // 0 = no stop loss
    double       TakeProfitPrice,  // 0 = no take profit
    double       TakeProfit2Price, // 0 = no second take profit
    int          Leverage,         // 1 = no leverage (spot/margin 1x)
    string       Notes = "");
