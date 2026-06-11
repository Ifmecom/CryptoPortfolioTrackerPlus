# Patroon-spec — status & gap-analyse

**Doel van dit document:** per patroon vastleggen wanneer het *aanwezig*, *bevestigd* en *geïnvalideerd*
is (de spec), én of de **code dat ook daadwerkelijk doet**. De volledige spec staat in
[`PATTERN_HANDBOOK.md`](PATTERN_HANDBOOK.md); dit document is de brug tussen die spec en
`Services/PatternDetectionService.cs`, en de werklijst om de detectie betrouwbaar te maken.

Legenda code-status: ✅ volgt de spec · ⚠️ gedeeltelijk / afwijkend · ❌ niet geïmplementeerd

> Onderzoeksconclusie: de spec is goed; de **kloof zit in de handhaving**. De grootste bronnen van
> "rare" of ontbrekende patronen zijn (1) bevestiging op de live-koers i.p.v. de slotkoers, (2) geen
> aanrakings-/ATR-proportionaliteitsvalidatie, en (3) geen continue invalidatie — een patroon wordt
> elke scan opnieuw "vers" gedetecteerd zonder geheugen.

> **Spec vastgelegd (handboek v2.1).** De drie kernbeslissingen uit de spec-review zijn nu in
> `PATTERN_HANDBOOK.md` verwerkt: ① drie-staten-bevestiging (In formatie → **Voorlopig** = live koers raakt
> niveau → **Bevestigd** = slotkoers erbuiten); ② minimale grootte = ATR-band **én** prijs-%-band (strengste
> wint); ③ trendlijnen vereisen R² **én** ≥2 aanrakingen binnen 1%. Dit document is vanaf nu de
> **implementatie-werklijst** om de code op die spec te brengen.

---

## 1. Systeembrede regels (handboek → code)

| Handboek-regel | Code-status | Toelichting |
|---|---|---|
| **Swing-detectie** (§2.1) | ⚠️ afwijkend van handboektekst | Code v1.38: wicks, **lookback 5**, significantie **0,4×ATR**. Handboek §2.1/F1/F12 zegt nog lookback 3 + 0,5%. → handboek bijwerken. |
| **Bar-index regressie** (§2.3, F2) | ✅ | Kanaal/driehoek/wedge gebruiken `LinearRegressionByBarIdx` + geprojecteerde lijnen. |
| **Aanrakingsvalidatie: R² én ≥2 binnen 1%** (§3.1, F10) | ✅ (Fase A) | `CountTouches` ≥2 per lijn binnen 1% náást de R²-gate — actief in kanaal/driehoek/wedge. |
| **Grootte: ATR-band én prijs-%-band** (§3.3, F11) | ✅ (Fase A) | Kanaal/wedge: gap `≥0,5×ATR` en `≤15×ATR` náást de prijs-%-band; driehoek: start-gap `≥0,5×ATR`. |
| **Interne-schending-filter** (§3.2: >30% bars buiten de lijn → verwerpen) | ❌ | Niet geïmplementeerd. Een rommelige structuur die toevallig een goede R² heeft, wordt niet afgewezen op interne doorbraken. |
| **Maximale patroonleeftijd** (§4.3) | ❌ | Geen max-age; alleen `HasRecentSwing` (laatste swing ≤20 bars). Een 100 bars oud kanaal kan nog gerapporteerd worden. |
| **Verouderd / al uitgespeeld** (§3.2, F6: >8% voorbij sleutelniveau) | ✅ (Fase A) | `IsPatternStale` nu in double bottom/top, H&S, Inv. H&S, wedge, kanaal, asc/desc-driehoek, bull/bear-flag en cup&handle. (Breakout/Breakdown nog open.) |
| **Drie-staten-bevestiging** (§5, F7) | ✅ (Fase B) | `PatternStatus` (Forming/Tentative/Confirmed) centraal bepaald in `DetectFromBars` (`ApplyStatus`/`EvalStatus`): Bevestigd op `bars[^1].Close` + marge, Voorlopig op live koers. `IsConfirmed` afgeleid. Status in badge-tooltip, overflow-tooltip en grafieklabel. |
| **Volume-bevestiging bij breakout** (§5.1) | ❌ | Volume wordt alleen in `DetectVolumeSpike` gebruikt. Breakout-bevestiging kijkt niet naar volume (kan wel — Binance-klines hebben volume). |
| **Continue invalidatie** (§6) | ❌ ontbreekt als concept | Detectie is stateless: elke scan opnieuw. Een patroon "invalideert" alleen impliciet doordat het de volgende scan niet meer gedetecteerd wordt. Er is geen expliciete invalidatie-/levensduurstatus per patroon. |
| **Tekenen: begrensde segmenten** (§7) | ✅ (v1.38) | Alle patronen tekenen begrensde trendlijn-segmenten; grafiek toont 1 patroon per timeframe. |
| **Tekenen: body-respecterende lijnen** (§7.2) | ⚠️ tegenstrijdig | Handboek §7.2 zegt "body-respecterend"; v1.38 gebruikt bewust wicks. → handboek bijwerken. |

---

## 2. Per patroon

### Level 2 — prijsstructuur

| Patroon | Aanwezig (detectie) | Bevestiging | Invalidatie | Grootste gap |
|---|---|---|---|---|
| **Double Bottom** | ✅ na v1.38-fix (opleving ≥5% tússen de bodems) | ⚠️ via live `currentPrice` i.p.v. slotkoers | ⚠️ staleness ✅, maar geen live "sluit >2% onder bodem"-tracking | Bevestiging op slotkoers (F7) |
| **Double Top** | ✅ | ⚠️ live-koers | ⚠️ idem | Bevestiging op slotkoers |
| **Bull Flag** | ✅ v1.38 (wicks, pool-richting, consolidatie-slope) | ⚠️ `IsConfirmed=false` standaard; geen slotkoers-breakouttoets | ⚠️ retrace<50% bij detectie, geen live-tracking | Geen breakout-bevestiging + geen staleness |
| **Bear Flag** | ✅ v1.38 | ⚠️ idem | ⚠️ idem | idem |
| **Ascending Triangle** | ✅ v1.38 (regressie + R²) | ⚠️ `distPct<3` (afstand tot weerstand) i.p.v. slotkoers-breakout | ❌ geen "sluit onder steunlijn"-check | Bevestiging + invalidatie niet conform §5/§6 |
| **Descending Triangle** | ✅ v1.38 | ⚠️ idem | ❌ idem | idem |
| **Symmetrical Triangle** | ✅ v1.38 | ❌ `IsConfirmed=false`; geen directional-breakout-check | ❌ geen apex-vervaltoets (§10.10) | Apex + breakout ontbreken |
| **Consolidation** | ✅ (range ≤8% / 15 bars) | n.v.t. | n.v.t. | Handboek noemt 7% + ondergrens 1,5%; code 8% zonder ondergrens |
| **Breakout/Breakdown** | ✅ (0,5–5% voorbij niveau) | ⚠️ zones kloppen, maar geen volume | ❌ geen staleness-hergebruik van `IsPatternStale` | Volume + staleness |
| **Uptrend/Downtrend** | ✅ (HH+HL / LH+LL) | n.v.t. | ⚠️ alleen bij her-scan | — |
| **Support Bounce / Resistance Rejection** | ⚠️ niet geverifieerd in detail | — | — | Nog te reviewen |

### Level 3 — klassieke patronen

| Patroon | Aanwezig | Bevestiging | Invalidatie | Grootste gap |
|---|---|---|---|---|
| **Head & Shoulders** | ✅ (3% hoofd, 15% symmetrie, 12 bars, ≥15% trend ervoor) | ⚠️ `currentPrice < neklijn×0,995` (live, niet slotkoers) | ❌ geen "prijs boven hoofd"-tracking; geen staleness | F7 + staleness |
| **Inverse H&S** | ✅ | ⚠️ live-koers | ❌ idem | idem |
| **Rising Wedge** | ✅ v1.38 (regressie, convergentie ≥30%, R²) | ⚠️ live-koers | ❌ geen "breekt boven bovenlijn"-tracking | F7 + invalidatie |
| **Falling Wedge** | ✅ v1.38 | ⚠️ live-koers | ❌ idem | idem |
| **Cup & Handle** | ✅ (diepte 10–40%, rim 6%, handle 45%) | ⚠️ live-koers | ⚠️ geen "onder cup-bodem"-tracking | F7 + ATR + staleness |
| **Adam & Eve** | ✅ na v1.38-fix (zelfde opleving-bug als Double Bottom) | ⚠️ live-koers | ⚠️ | F7 |
| **Ascending/Descending Channel** | ✅ v1.38 (regressie + R² + parallel + breedte) | ⚠️ "prijs nadert wand" i.p.v. wand-sluiting | ❌ geen "sluit buiten wand ≥1%"-check | Bevestiging + invalidatie |

### Level 1 — indicator (geen bars)

RSI/MACD/EMA/BB-squeeze/ADX: ✅ conform §9. Dit zijn momentane indicatorsignalen — geen
"aanwezig/bevestigd/geïnvalideerd"-levenscyclus. Geen actie nodig.

---

## 3. Prioriteiten om de detectie betrouwbaar te maken

**P1 — Drie-staten-bevestiging (§5, F7).** ✅ **Gedaan (Fase B).** `PatternStatus` (Forming/Tentative/Confirmed)
centraal bepaald: Bevestigd = `bars[^1].Close` voorbij sleutelniveau + marge (driehoek/kanaal ≥1%,
neklijn/wedge/flag ≥0,5%, breakout/breakdown ≥1,5%); Voorlopig = live koers erbuiten. Status getoond in
badge-tooltip, overflow-tooltip en grafieklabel. Fixt het F7-probleem (bevestiging op slotkoers i.p.v. live).

**P2 — Staleness breed toepassen (F6).** ✅ **Gedaan (Fase A).** `IsPatternStale` toegevoegd aan kanaal,
driehoek, wedge, H&S, Inv. H&S, bull/bear-flag en cup&handle. Resteert: breakout/breakdown-detector.

**P3 — Grootte: ATR-band én prijs-%-band (§3.3, F11).** ✅ **Gedaan (Fase A)** voor kanaal/driehoek/wedge.

**P4 — Aanrakingsvalidatie: R² én ≥2 binnen 1% (§3.1, F10).** ✅ **Gedaan (Fase A)** via `CountTouches`.

**P5 — Bevestiging/invalidatie voor driehoeken & kanalen (§5/§6).** Driehoek-bevestiging op een
slotkoers-breakout (≥1%) i.p.v. alleen afstand; kanaal-invalidatie op een sluiting buiten de wand (≥1%).

**P6 — Symmetrische driehoek: apex (§10.10).** Bereken het convergentiepunt en verval het patroon als
prijs de apex bereikt zonder breakout.

**P7 — Continue invalidatie (concept).** Optioneel/groot: een patroon-geheugen zodat een eerder
gedetecteerd patroon expliciet "geïnvalideerd" of "bevestigd" kan worden i.p.v. puur stateless her-scan.

**Doc-onderhoud:** ✅ gedaan — handboek is bijgewerkt naar **v2.1** (§2.1 wicks/lookback 5/0,4×ATR;
§2.4 15M; §3.1 R²+aanrakingen; §3.3 ATR+%; §5 drie-staten-bevestiging; §7.2 wicks/regressie; F1/F10/F11/F12).
De Double-Bottom-diepte (§10.4) was al correct in het handboek — de code is daar mee in lijn gebracht.

---

**Fase A + B geïmplementeerd.** P1–P4 zijn gedaan; de "Bevestiging"-kolommen in §2 (die ⚠️ "via live
koers" tonen) zijn daarmee achterhaald — bevestiging loopt nu centraal via het drie-staten-model.
Resteert nog: P5 (driehoek/kanaal bevestiging/invalidatie verfijnen), P6 (sym-driehoek apex),
P7 (continue invalidatie met geheugen), en de open detail-items (breakout/breakdown staleness,
Support Bounce/Resistance Rejection-review).

---

*Spec vastgelegd in handboek v2.1; Fase A + B (P1–P4) geïmplementeerd met tests.*
