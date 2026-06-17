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
| **Interne-schending-filter** (§3.2: >30% bars buiten de lijn → verwerpen) | ✅ | `InternalViolationFraction` in kanaal/driehoek/wig: >30% slotkoersen buiten de band (1% tol) → verworpen, náást de R²-gate. |
| **Maximale patroonleeftijd** (§4.3) | ✅ | Span-cap per type náást `HasRecentSwing`: driehoek 60, wig 80, kanaal 120 bars (`winEnd − winStart`). Te uitgerekte structuren worden afgewezen. |
| **Verouderd / al uitgespeeld** (§3.2, F6: >8% voorbij sleutelniveau) | ✅ (Fase A) | `IsPatternStale` nu in double bottom/top, H&S, Inv. H&S, wedge, kanaal, asc/desc-driehoek, bull/bear-flag en cup&handle. (Breakout/Breakdown nog open.) |
| **Drie-staten-bevestiging** (§5, F7) | ✅ (Fase B) | `PatternStatus` (Forming/Tentative/Confirmed) centraal bepaald in `DetectFromBars` (`ApplyStatus`/`EvalStatus`): Bevestigd op `bars[^1].Close` + marge, Voorlopig op live koers. `IsConfirmed` afgeleid. Status in badge-tooltip, overflow-tooltip en grafieklabel. |
| **Volume-bevestiging bij breakout** (§5.1) | ✅ | `RelativeVolume` (laatste bar t.o.v. gemiddelde van 20) voedt een sterkte-correctie + tekstnoot op breakout/breakdown (≥1,5× = +8 "volume bevestigt", <0,8× = −12 "zwak volume"). Werkt op de Binance/KuCoin-klines (die hebben volume; de lokale MarketChart-JSON niet). |
| **Continue invalidatie** (§6) | ✅ (P7) | Patroon-geheugen over scans heen: `PatternStateRecord` (SQLite-tabel `PatternStates`) + pure `PatternReconciler` + `PatternStateStore`. Een patroon krijgt een levenscyclus (Forming→Tentative→Confirmed→PlayedOut, of Invalidated/Expired) met reden + tijdstip, grace-marge tegen flikkeren, en notificatie bij Bevestigd/Geïnvalideerd. De detector blijft stateless; de reconciliatie draait sequentieel ná de scan. |
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
| **Ascending Triangle** | ✅ v1.38 (regressie + R²) | ✅ slotkoers-breakout ≥1% via drie-staten-model (P5) | ✅ slotkoers >1% onder de stijgende steunlijn → verworpen (P5) | — |
| **Descending Triangle** | ✅ v1.38 | ✅ slotkoers-breakdown ≥1% (P5) | ✅ slotkoers >1% boven de dalende weerstandslijn → verworpen (P5) | — |
| **Symmetrical Triangle** | ✅ v1.38 | ✅ apex-verval i.p.v. valse bevestiging (P6) | ✅ apex-vervaltoets (§10.10): koers bereikt convergentiepunt → verworpen (P6) | — |
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
| **Ascending/Descending Channel** | ✅ v1.38 (regressie + R² + parallel + breedte) | ⚠️ "prijs nadert wand" i.p.v. wand-sluiting | ✅ slotkoers >1% buiten boven-/onderwand → verworpen (P5) | — |

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

**P5 — Bevestiging/invalidatie voor driehoeken & kanalen (§5/§6).** ✅ **Gedaan.** Driehoek-bevestiging
loopt op een slotkoers-breakout (≥1%) via het drie-staten-model (de oude `distPct<3`-claim is verwijderd:
`IsConfirmed=false` lokaal, `ApplyStatus` bepaalt Bevestigd op de slotkoers). Driehoek-invalidatie: een
slotkoers >1% door de "vlakke" kant (asc: onder de stijgende steun; desc: boven de dalende weerstand)
verwerpt het patroon. Kanaal-invalidatie: een slotkoers >1% buiten de boven- of onderwand verwerpt het
kanaal. Getest in `PatternGeometryTests` (apex/kanaal-tests, niet-vacuous geverifieerd).

**P6 — Symmetrische driehoek: apex (§10.10).** ✅ **Gedaan.** Het convergentiepunt wordt berekend als
`apexX = (lowInt − highInt) / (highSlope − lowSlope)` (bar-index). Bereikt de laatste bar de apex
(`lastIdx ≥ apexX`, met apex vóór ons) zonder breakout, dan vervalt het patroon (geldt voor asc/desc/sym
driehoeken). Getest met een convergerend-apex-bereikt scenario (verworpen) vs apex-nog-vóór-ons (herkend).

**P7 — Continue invalidatie (patroon-geheugen).** ✅ **Gedaan.** Een persistent geheugen geeft een eerder
gedetecteerd patroon expliciet een levenscyclus i.p.v. puur stateless her-scan:
- **Opslag:** `PatternStateRecord` → SQLite-tabel `PatternStates` (idempotent via `ApplyPlusSchemaAsync`,
  gedocumenteerde migratie `AddPatternState`).
- **Identiteit:** `PatternFingerprint` (grove sleutel `coin|tf|type` + niveau-nabijheid van 1,5%).
- **Logica:** pure `PatternReconciler` — nieuw/match/upgrade, grace-marge tegen flikkeren, en terminale
  classificatie van een verdwenen patroon (PlayedOut / Invalidated / Expired met reden + tijdstip).
- **Integratie:** `PatternStateStore` (EF) wordt sequentieel ná de parallelle scan aangeroepen; verrijkt
  elke `PatternResult` met `Lifecycle`/`TimesSeen`/`LifecycleReason`. De detector blijft stateless.
- **UI:** confidence ("N× gezien") in tooltips/legenda + een "patroon-updates"-chip per coin (overgangen
  sinds de vorige scan, met reden).
- **Notificatie:** één samengevatte Telegram-alert bij Bevestigd/Geïnvalideerd (via `INotifierService`).
- **Tests:** `PatternFingerprintTests` (10) + `PatternReconcilerTests` (11), beide pure.

**Doc-onderhoud:** ✅ gedaan — handboek is bijgewerkt naar **v2.1** (§2.1 wicks/lookback 5/0,4×ATR;
§2.4 15M; §3.1 R²+aanrakingen; §3.3 ATR+%; §5 drie-staten-bevestiging; §7.2 wicks/regressie; F1/F10/F11/F12).
De Double-Bottom-diepte (§10.4) was al correct in het handboek — de code is daar mee in lijn gebracht.

---

**Fase A + B geïmplementeerd.** P1–P4 gedaan; bevestiging loopt centraal via het drie-staten-model.
Daarna ook gedaan: definitie-fixes (dominante pieken dubbele top/bodem + H&S), Tmax overal (flags, C&H,
reversals), Bull/Bear Pennant, en **Support Bounce / Resistance Rejection** (waren dode enum-waarden,
nu echt geïmplementeerd met geteste-niveau + ommekeer). **Breakout/breakdown-staleness** bleek
redundant — de 0,5–4%-band sluit stale (>4%) al uit. **P5** (driehoek/kanaal bevestiging + invalidatie)
en **P6** (sym-driehoek apex-verval) zijn geïmplementeerd met tests. **P7** (continue invalidatie met
patroon-geheugen: `PatternStates`-tabel + `PatternReconciler` + `PatternStateStore`, UI-chip en
Telegram-notificatie) is nu ook af.

**De volledige werklijst P1–P7 is afgerond.** Daarna ook de resterende systeembrede TA-punten:
**interne-schendingsfilter (§3.2)**, **maximale patroonleeftijd (§4.3)** en **volume-bevestiging bij
breakout (§5.1)** zijn nu geïmplementeerd (zie de tabel in §1). Bovendien berekent
`PatternHistoryCalculator` nu hit/fail-statistiek per patroontype uit de `PatternStates`-historie
(`IPatternStateStore.GetHistoryStatsAsync`) — de basis voor uitkomst-gebaseerde scorekalibratie.

---

*Spec vastgelegd in handboek v2.1; werklijst P1–P7 + systeembrede TA-punten geïmplementeerd met tests.*
