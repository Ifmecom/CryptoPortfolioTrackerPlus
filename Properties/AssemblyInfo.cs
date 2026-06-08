using System.Runtime.CompilerServices;

// Geeft het testproject toegang tot internal members (bv. WatchedSetupService
// interne testconstructor).
[assembly: InternalsVisibleTo("CryptoPortfolioTracker.Tests")]
