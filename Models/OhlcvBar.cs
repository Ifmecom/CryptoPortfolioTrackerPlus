namespace CryptoPortfolioTracker.Models;

/// <summary>OHLCV candle from Binance klines endpoint.</summary>
public class OhlcvBar
{
    public DateTime Date   { get; set; }
    public double   Open   { get; set; }
    public double   High   { get; set; }
    public double   Low    { get; set; }
    public double   Close  { get; set; }
    public double   Volume { get; set; }
}
