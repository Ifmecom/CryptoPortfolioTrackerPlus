namespace CryptoPortfolioTracker.Helpers;

/// <summary>
/// Lichte timing-log voor pagina's (v1.48): <c>using var _ = PerfLog.Measure("AssetsView.ViewLoading");</c>
/// schrijft bij afloop één regel "Perf: … ms" naar het log. Bedoeld om laadtijden per pagina te kunnen
/// vergelijken (eerste bezoek vs. herhaalbezoek) zonder profiler.
/// </summary>
public static class PerfLog
{
    private static readonly ILogger Logger = Log.Logger.ForContext(Constants.SourceContextPropertyName, "Perf".PadRight(22));

    public static IDisposable Measure(string what) => new Scope(what);

    private sealed class Scope : IDisposable
    {
        private readonly string _what;
        private readonly Stopwatch _sw = Stopwatch.StartNew();
        private bool _done;

        public Scope(string what) => _what = what;

        public void Dispose()
        {
            if (_done) return;
            _done = true;
            Logger.Information("Perf: {What} {Ms} ms", _what, _sw.ElapsedMilliseconds);
        }
    }
}
