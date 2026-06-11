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
| **Aanrakingsvalidatie: R² én ≥2 binnen 1%** (§3.1, F10) | ⚠️ alleen R² | Spec v2.1 eist **beide**: R² (≥0,70 / wedge ≥0,55) **én** ≥2 swings binnen 1% van de lijn. Code heeft nu alleen de R²-helft → aanrakingscheck toevoegen. |
| **Grootte: ATR-band én prijs-%-band** (§3.3, F11) | ❌ ATR-deel | Spec v2.1: hoogte moet zowel `≥0,5×ATR(14)` als de prijs-%-ondergrens halen (strengste wint). Code heeft nu alleen de prijs-%-band → ATR-band toevoegen. |
| **Interne-schending-filter** (§3.2: >30% bars buiten de lijn → verwerpen) | ❌ | Niet geïmplementeerd. Een rommelige structuur die toevallig een goede R² heeft, wordt niet afgewezen op interne doorbraken. |
| **Maximale patroonleeftijd** (§4.3) | ❌ | Geen max-age; alleen `HasRecentSwing` (laatste swing ≤20 bars). Een 100 bars oud kanaal kan nog gerapporteerd worden. |
| **Verouderd / al uitgespeeld** (§3.2, F6: >8% voorbij sleutelniveau) | ⚠️ alleen 2 patronen | `IsPatternStale` wordt **alleen** door Double Bottom & Double Top aangeroepen. Ontbreekt bij H&S, Inv. H&S, wedge, kanaal, driehoek, flags, breakout/breakdown, cup&handle. |
| **Drie-staten-bevestiging** (§5, F7) | ❌ | Spec v2.1: In formatie → **Voorlopig** (live koers raakt niveau) → **Bevestigd** (slotkoers `bars[^1].Close` erbuiten + marge). Code heeft nu alleen één `IsConfirmed`-vlag op de live koers → tri-state introduceren. |
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

**P1 — Drie-staten-bevestiging (§5, F7).** Introduceer een tri-state (In formatie / Voorlopig / Bevestigd)
i.p.v. de enkele `IsConfirmed`-bool: **Voorlopig** = live `currentPrice` voorbij het niveau, **Bevestigd** =
`bars[^1].Close` erbuiten + marge (driehoek/kanaal ≥1%, neklijn/wedge/flag ≥0,5%). Toon beide labels.
Grootste bron van flikkerende/onterechte "bevestigd". *Raakt PatternResult + alle detectoren + de UI-badges.*

**P2 — Staleness breed toepassen (F6).** Roep `IsPatternStale` aan in álle reversal/breakout-detectoren,
niet alleen double bottom/top. Voorkomt het tonen van al-uitgespeelde patronen.

**P3 — Grootte: ATR-band én prijs-%-band (§3.3, F11).** Voeg de ATR-band (`≥0,5×ATR`, en `≤15×ATR` voor
wig/driehoek) toe naast de bestaande prijs-%-grenzen; strengste wint. ATR is al beschikbaar in `DetectFromBars`.

**P4 — Aanrakingsvalidatie: R² én ≥2 binnen 1% (§3.1, F10).** Voeg náást de R²-gate een check toe dat
≥2 swings per lijn binnen ~1% van de geprojecteerde regressielijn liggen. Sluit "regressielijn zonder echte
bounces" uit.

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

*Spec vastgelegd in handboek v2.1. Volgende stap: implementatie P1 (drie-staten-bevestiging) of P2
(staleness breed) — klein te beginnen, met tests.*
