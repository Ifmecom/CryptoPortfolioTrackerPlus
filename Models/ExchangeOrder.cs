using System;
using CryptoPortfolioTracker.Enums;

namespace CryptoPortfolioTracker.Models;

public class ExchangeOrder
{
    public int Id { get; set; }
    public int? SignalId { get; set; }
    public ExchangeKind Exchange { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public OrderSide Side { get; set; }
    public OrderType Type { get; set; }
    public double Qty { get; set; }
    public double Entry { get; set; }
    public double StopLoss { get; set; }
    public double TakeProfit { get; set; }
    public OrderStatus Status { get; set; }
    public string ExternalOrderId { get; set; } = string.Empty;
    public bool IsPaper { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? FilledAt { get; set; }
    /// <summary>Exit price when the user manually closes the position.</summary>
    public double ClosePrice { get; set; }
    /// <summary>UTC timestamp when the position was manually closed.</summary>
    public DateTime? ClosedAt { get; set; }

    /// <summary>Free-text note the user can attach to this trade.</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>Second take-profit target (0 = not set).</summary>
    public double TakeProfit2 { get; set; }

    /// <summary>Leverage multiplier (1 = spot / no leverage).</summary>
    public int Leverage { get; set; } = 1;

    /// <summary>Spot, Futures or Margin.</summary>
    public MarketType MarketType { get; set; } = MarketType.Spot;

    public Signal? Signal { get; set; }
}
