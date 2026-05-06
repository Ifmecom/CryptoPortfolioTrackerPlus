using CryptoPortfolioTracker.Enums;

namespace CryptoPortfolioTracker.Models;

public class ExchangeAccount
{
    public int Id { get; set; }
    public ExchangeKind Exchange { get; set; }
    public string ApiKeyEncrypted { get; set; } = string.Empty;
    public string ApiSecretEncrypted { get; set; } = string.Empty;
    public string Permissions { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
