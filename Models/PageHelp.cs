using System.Collections.Generic;

namespace CryptoPortfolioTracker.Models;

/// <summary>Eén blok in de pagina-uitleg (bijv. "Wat zie je hier") met opsommingspunten.</summary>
public sealed record PageHelpSection(string Icon, string Heading, IReadOnlyList<string> Points);

/// <summary>
/// Uitleg bij één pagina/menu-optie, getoond via de ⓘ-knop rechtsboven in de header (v1.48).
/// Inhoud staat in <c>Helpers/PageHelpCatalog</c>.
/// </summary>
public sealed record PageHelp(string Title, string Intro, IReadOnlyList<PageHelpSection> Sections);
