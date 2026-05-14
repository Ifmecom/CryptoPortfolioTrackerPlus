using CryptoPortfolioTracker.Enums;

namespace CryptoPortfolioTracker.Models;

/// <summary>Lightweight value object carrying user-specified order parameters.</summary>
public record OrderRequest(
    ExchangeKind Exchange,
    OrderSide    Side,
    double       AmountUsdt,
    double       StopLossPerc,
    double       TakeProfitPerc);
