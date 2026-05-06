using CryptoPortfolioTracker.Enums;
using System.Collections.Generic;

namespace CryptoPortfolioTracker.Models;

public record MacdData(double Macd, double Signal, double Histogram);

public record BollingerData(double Upper, double Middle, double Lower);

public record TaScore(
    SignalDirection Direction,
    double Score,
    List<string> TriggeredRules);
