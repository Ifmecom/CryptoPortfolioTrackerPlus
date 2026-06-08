using System.Collections.Concurrent;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Eenvoudige thread-safe in-memory cache met een vaste time-to-live per item.
/// Gedeeld patroon voor rate-limited API-calls (orderboek, futures, etc.).
///
/// Pure logica (geen netwerk/UI) — unit-testbaar met een injecteerbare klok.
/// </summary>
public sealed class TtlCache<TValue>
{
    private readonly ConcurrentDictionary<string, (TValue value, DateTime expiry)> _store
        = new(StringComparer.OrdinalIgnoreCase);

    private readonly TimeSpan      _ttl;
    private readonly Func<DateTime> _now;

    /// <param name="ttl">Levensduur per item.</param>
    /// <param name="clock">Optionele klok (voor tests); standaard <see cref="DateTime.UtcNow"/>.</param>
    public TtlCache(TimeSpan ttl, Func<DateTime>? clock = null)
    {
        _ttl = ttl;
        _now = clock ?? (() => DateTime.UtcNow);
    }

    /// <summary>Probeert een nog-geldige waarde uit de cache te halen.</summary>
    public bool TryGet(string key, out TValue value)
    {
        if (_store.TryGetValue(key, out var entry) && entry.expiry > _now())
        {
            value = entry.value;
            return true;
        }
        value = default!;
        return false;
    }

    /// <summary>Plaatst (of vervangt) een waarde met een verse TTL.</summary>
    public void Set(string key, TValue value)
        => _store[key] = (value, _now().Add(_ttl));

    /// <summary>
    /// Haalt de waarde uit de cache, of berekent + cachet hem via <paramref name="factory"/>.
    /// </summary>
    public async Task<TValue> GetOrAddAsync(string key, Func<Task<TValue>> factory)
    {
        if (TryGet(key, out var cached)) return cached;
        var value = await factory();
        Set(key, value);
        return value;
    }

    /// <summary>Verwijdert alle items (bijv. bij portfoliowissel).</summary>
    public void Clear() => _store.Clear();
}
