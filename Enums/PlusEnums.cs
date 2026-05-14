namespace CryptoPortfolioTracker.Enums;

public enum SentimentSource
{
    Reddit,
    Telegram,
    Rss,
    CryptoPanic
}

public enum SignalDirection
{
    Long,
    Short,
    Flat
}

public enum Timeframe
{
    OneHour,
    FourHour,
    OneDay
}

public enum ExchangeKind
{
    Mexc,
    Bybit
}

public enum OrderSide
{
    Buy,
    Sell
}

public enum OrderType
{
    Market,
    Limit,
    StopLimit
}

public enum OrderStatus
{
    Pending,
    Filled,
    PartiallyFilled,
    Cancelled,
    Rejected,
    /// <summary>Position manually closed by the user at market price.</summary>
    Closed
}

public enum MarketRegime
{
    RiskOn,
    Neutral,
    RiskOff
}
