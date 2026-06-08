namespace CryptoPortfolioTracker.Enums;

/// <summary>Lifecycle status of a watched pattern trade setup.</summary>
public enum WatchedSetupStatus
{
    /// <summary>Setup identified, waiting for price to reach the entry zone or trigger.</summary>
    Watching = 0,

    /// <summary>Price reached TP1 or TP2 — setup played out as expected.</summary>
    Won = 1,

    /// <summary>Price hit the stop-loss level — setup failed.</summary>
    Lost = 2,

    /// <summary>Manually dismissed (outdated, setup invalidated, etc.).</summary>
    Expired = 3,

    /// <summary>Entry price has been reached — trade is considered active/open.</summary>
    Open = 4,
}
