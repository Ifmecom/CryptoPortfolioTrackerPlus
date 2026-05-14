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

    /// <summary>"HMAC" or "RSA"</summary>
    public string AuthMethod { get; set; } = "HMAC";

    /// <summary>PEM-encoded public key — not sensitive, stored plain.</summary>
    public string PublicKeyPem { get; set; } = string.Empty;
}
