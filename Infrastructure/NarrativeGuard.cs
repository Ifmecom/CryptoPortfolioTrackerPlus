using CryptoPortfolioTracker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Serilog;

namespace CryptoPortfolioTracker.Infrastructure;

/// <summary>
/// Vangnet tegen het v1.48-incident (196.000 lege narratieven): de <see cref="Coin"/>-constructor zet <c>Narrative = new()</c>.
/// Een <c>Coins.Update(coin)</c> op een munt die zonder tracking (<c>AsNoTracking</c>) is geladen, loopt de hele graaf af en
/// voegt dat lege narratief dan in — en koppelt de munt eraan, waardoor hij zijn echte narratief kwijtraakt.
/// <para>
/// Vóór elke SaveChanges: een nieuw narratief zonder naam wordt nooit opgeslagen (een echt narratief heeft altijd een naam).
/// Munten die ernaar wezen worden eerst teruggekoppeld aan hun narratief zoals het in de database staat; pas daarna wordt het
/// lege narratief losgekoppeld. Andersom zou EF de munt als 'wees' meenemen en ging zijn echte wijziging verloren.
/// Bewezen in <c>NarrativeGuardTests</c> (echte SQLite).
/// </para>
/// </summary>
public static class NarrativeGuard
{
    /// <summary>Standaardnarratief van de app (zie <c>NarrativeService.GetDefaultNarrative</c>). Een munt moet altijd een narratief hebben.</summary>
    public const string DefaultNarrativeName = "- Not Assigned -";

    /// <summary>Haal lege, nieuwe narratieven uit de change tracker. Geeft het aantal tegengehouden narratieven.</summary>
    /// <remarks>Roep aan ná <c>DetectChanges()</c>.</remarks>
    public static int Apply(ChangeTracker tracker)
    {
        var empty = tracker.Entries<Narrative>()
            .Where(e => e.State == EntityState.Added && string.IsNullOrWhiteSpace(e.Entity.Name))
            .ToList();
        if (empty.Count == 0) return 0;

        var context = tracker.Context;
        var blocked = new HashSet<Narrative>(empty.Select(e => e.Entity), ReferenceEqualityComparer.Instance);
        var affected = tracker.Entries<Coin>()
            .Where(c => c.Entity.Narrative is { } n && blocked.Contains(n))
            .ToList();

        Narrative? fallback = null;
        foreach (var coin in affected)
        {
            // Het echte narratief volgens de database; een nieuwe munt krijgt het standaardnarratief.
            int? dbNarrativeId = coin.State == EntityState.Added
                ? null
                : context.Set<Coin>().AsNoTracking()
                    .Where(c => c.Id == coin.Entity.Id)
                    .Select(c => EF.Property<int?>(c, "NarrativeId"))
                    .FirstOrDefault();

            coin.Entity.Narrative = dbNarrativeId is int id
                ? context.Set<Narrative>().Find(id)!
                : fallback ??= DefaultNarrative(context, empty[0].Entity);
        }

        tracker.DetectChanges();                                   // FK's volgen de herstelde navigaties
        foreach (var e in empty.Where(e => !ReferenceEquals(e.Entity, fallback)))
            e.State = EntityState.Detached;                        // wijst nu nergens meer naar: veilig loskoppelen

        Log.Warning("NarrativeGuard: {Count} leeg narratief/narratieven tegengehouden; {Coins} munt(en) houden hun eigen narratief. " +
                    "Oorzaak: Coins.Update() op een munt zonder Include(Narrative) — zie CLAUDE.md.", empty.Count, affected.Count);
        return empty.Count;
    }

    /// <summary>
    /// Het bestaande standaardnarratief, of — als het er nog niet is — het eerste lege narratief, dat dan die naam krijgt
    /// (één keer aangemaakt in plaats van een naamloos narratief per munt).
    /// </summary>
    private static Narrative DefaultNarrative(DbContext context, Narrative firstEmpty)
    {
        var existing = context.Set<Narrative>().Local.FirstOrDefault(n => n.Name == DefaultNarrativeName)
                    ?? context.Set<Narrative>().FirstOrDefault(n => n.Name == DefaultNarrativeName);
        if (existing is not null) return existing;
        firstEmpty.Name = DefaultNarrativeName;
        return firstEmpty;
    }

    /// <summary>SaveChanges met vangnet.</summary>
    public static int Save(DbContext context, Func<int> save)
    {
        context.ChangeTracker.DetectChanges();
        Apply(context.ChangeTracker);
        return save();
    }

    public static Task<int> SaveAsync(DbContext context, Func<Task<int>> save)
    {
        context.ChangeTracker.DetectChanges();
        Apply(context.ChangeTracker);
        return save();
    }
}
