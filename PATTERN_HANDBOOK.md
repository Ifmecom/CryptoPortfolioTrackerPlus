# Patroon Handboek — CryptoPortfolioTracker Plus
**Versie 2.0 · Mei 2026**

Dit document is de **authoritative reference** voor alle patroondetectie in `PatternDetectionService.cs`.
Een patroon mag alleen worden gerapporteerd als **alle** validatiecriteria zijn voldaan.
Twijfelgevallen worden **niet** gerapporteerd — precisie gaat boven kwantiteit.

> **Kernprincipe:** Een vals-positief patroon is schadelijker dan een gemist patroon.

---

## Inhoudsopgave

1. [Institutionele kwaliteitsprincipes](#1-institutionele-kwaliteitsprincipes)
2. [Swing-punt detectie](#2-swing-punt-detectie)
3. [Kwaliteitsfilters](#3-kwaliteitsfilters)
4. [Patroonstart en -einde](#4-patroonstart-en--einde)
5. [Breakout confirmatie](#5-breakout-confirmatie)
6. [Invalidatieregels](#6-invalidatieregels)
7. [Tekenregels](#7-tekenregels)
8. [Marktcontext en multitimeframe](#8-marktcontext-en-multitimeframe)
9. [Level 1 — Indicator-patronen](#9-level-1--indicator-patronen)
10. [Level 2 — Prijsstructuur-patronen](#10-level-2--prijsstructuur-patronen)
11. [Level 3 — Klassieke grafiekpatronen](#11-level-3--klassieke-grafiekpatronen)
12. [Strength-tabel](#12-strength-tabel)
13. [Veelgemaakte fouten](#13-veelgemaakte-fouten)
14. [Roadmap geavanceerde detectie](#14-roadmap-geavanceerde-detectie)

---

## 1. Institutionele kwaliteitsprincipes

Een patroon bestaat **alleen** als voldaan is aan:

- De structuur is duidelijk herkenbaar aan de hand van swing-punten
- De marktcontext ondersteunt het patroon (trend continuatie of reversal na uitgebreide beweging)
- Het volatiliteitsgedrag is logisch (compressie bij wiggen/driehoeken, expansie bij flags)
- Het patroon is nog **niet** geïnvalideerd
- De breakout heeft voldoende bevestiging

**Forceer nooit een patroon.** Als de structuurkwaliteit laag is: verwerp het patroon.

### Wat de detector WEL doet
- Detecteert structureel valide patronen op basis van swing-punten
- Definieert exacte start- en eindpunten
- Definieert breakout-bevestiging en invalidatieniveaus
- Verwerpt zwakke of misvormde patronen

### Wat de detector NIET doet
- Visueel "gokken" op basis van gelijkenis
- Patronen rapporteren met slechts één of twee aanrakingen per trendlijn
- Patronen rapporteren die al volledig uitgespeeld zijn
- Reversalpatronen detecteren in willekeurig zijwaarts koersgedrag

---

## 2. Swing-punt detectie

### 2.1 Definitie

Een **swing high** op bar `i` is geldig wanneer:
- `bars[i].High` strikt hoger is dan alle bars `j` in `[i - lookback … i + lookback]`, `j ≠ i`
- `bars[i].High` is minimaal **0.5%** hoger dan de hoogste naburige bar (`maxNeighbor`)
- Lookback = **3 bars** (minimale isolatie: 3 bars aan elke kant)

Een **swing low** op bar `i` is geldig wanneer:
- `bars[i].Low` strikt lager is dan alle bars `j` in `[i - lookback … i + lookback]`, `j ≠ i`
- `bars[i].Low` is minimaal **0.5%** lager dan de laagste naburige bar (`minNeighbor`)

De 0.5%-significantiedrempel elimineert micro-swings die alleen ruis zijn en nooit een handelbare structuur vormen.

### 2.2 Werkvenster

- Gebruik maximaal de **laatste 120 bars** per timeframe voor detectie
- Level 3 patronen vereisen minimaal **50 bars** aanwezig
- Slopeberekeningen altijd normaliseren door te delen door de gemiddelde prijs → dimensieloze waarde, schaalinvariant

### 2.3 Regressie op werkelijke bar-indices

Voor wedgen en kanalen wordt de helling berekend via **lineaire regressie op werkelijke bar-indices** (niet op array-positie 0,1,2,...). Als swing-punten ongelijk verdeeld zijn in de tijd (e.g. op bar 10, 35, 80), worden ze anders behandeld dan als ze aaneengesloten zouden liggen. De getekende trendlijn wordt altijd bepaald door de geprojecteerde regressiewaarden, nooit door de ruwe swing-high of swing-low prijs.

### 2.4 Tijdframes

| Label | Interval | Primair gebruik |
|-------|----------|-----------------|
| `1D`  | Dagelijks | Swing-structuur, trenddirectie |
| `4H`  | 4 uur     | Intraday momentum, triangle/wedge |
| `1H`  | 1 uur     | Korte-termijn entry-timing |

---

## 3. Kwaliteitsfilters

### 3.1 Verplichte minimum-aanrakingen

| Patroontype | Min. aanrakingen bovenlijn | Min. aanrakingen onderlijn |
|-------------|--------------------------|--------------------------|
| Driehoek (alle types) | 2 | 2 |
| Wig (rising/falling) | 2 | 2 |
| Kanaal (ascending/descending) | 2 | 2 |
| H&S / Inv. H&S | 3 (LS + H + RS) | 2 (beide troughs) |
| Double Bottom / Top | — | 2 bodems / 2 toppen |

### 3.2 Verwerpingscriteria

Verwerp het patroon wanneer:

- Structuur te rommelig: swing-punten overlappen of doorsnijden de trendlijnen **meerdere keren** intern
- Patroon te klein relative aan volatiliteit: patroonbreedte < **1× ATR** van de laatste 14 bars
- Trendlijnen constant intern geschonden: meer dan **30% van de bars** binnen het patroon sluit buiten de trendlijn
- Breakout heeft al plaatsgevonden en prijs is al **>8% voorbij** het sleutelniveau → patroon is verouderd
- Asymmetrie is extreem: bij H&S zijn schouders > 25% van elkaar verwijderd in hoogte
- Patroon is al langer dan **80 bars** oud zonder breakout → patroon vervalt (structuurverval)

### 3.3 ATR-proportionaliteit

Een patroon is pas relevant als de hoogte (afstand van bovenlijn tot onderlijn) proportioneel is aan de recente volatiliteit:
- **Minimum patroonhoogte**: `≥ 0.5 × ATR(14)`
- **Maximum patroonhoogte** (voor wedge/triangle): `≤ 15 × ATR(14)`

Patronen die te klein zijn t.o.v. de ATR zijn ruis. Patronen die veel te groot zijn, zijn geen compressiepatronen.

---

## 4. Patroonstart en -einde

### 4.1 Start van een patroon

Een patroon begint:
- Bij het **eerste structureel relevante swing-punt** dat de patroongrens definieert
- Waar de prijs de patroonbegrenzingen begint te respecteren
- Nooit eerder dan noodzakelijk — sluit gerelateerde koersactie vóór het patroon uit

**Fout:** een dalende wig tekenen vanaf een piek die al 60 bars geleden plaatsvond, terwijl het eigenlijke patroon pas 20 bars geleden begon.

### 4.2 Einde van een patroon

Een patroon eindigt:
- Bij **breakout-bevestiging** (sluiting buiten de structuur)
- Of bij **invalidatie** (zie §6)

Verleng een patroon nooit onbepaald. Na de apex van een driehoek (convergentiepunt) verliest het patroon zijn geldigheid automatisch.

### 4.3 Maximale patroonleeftijd

| Patroontype | Maximale leeftijd (bars) |
|-------------|--------------------------|
| Bull/Bear Flag | 20 |
| Driehoek | 60 |
| Wig | 80 |
| Kanaal | 120 |
| H&S / Double Top/Bottom | 80 |
| Cup & Handle | 80 |

Na deze leeftijd zonder bevestiging: patroon verwijderd uit rapportage.

---

## 5. Breakout confirmatie

### 5.1 Vereisten voor bevestiging

Een breakout is **alleen** bevestigd als:
1. Een **slotkoers** (`Close`, niet een intrabar-wick) buiten de structuur sluit
2. De slotkoers de grens overschrijdt met meer dan normale wick-variatie
3. Bij voorkeur begeleid door volumetoename t.o.v. het 20-periodes gemiddelde
4. Bij voorkeur gevolgd door continuatie (hertest van het niveau houden)

**Gebruik nooit `currentPrice` (live prijs) als bevestiging.** Gebruik `bars[^1].Close`.

### 5.2 Minimale doorbraakmarges per patroon

| Patroon | Vereiste marge boven/onder sleutelniveau |
|---------|------------------------------------------|
| Driehoek | `≥ 1.0%` |
| Wig | `≥ 0.5%` (wig-toppen zijn preciezer) |
| H&S neklijn | `≥ 0.5%` |
| Double Bottom/Top neklijn | `≥ 0.5%` |
| Cup & Handle | `≥ 0.5%` |
| Kanaal | `≥ 1.0%` |

### 5.3 Hertest gedrag (retest)

Na een geldige breakout is een hertest van het doorbroken niveau normaal en gezond:
- Eerdere weerstand wordt **steun** na bullish breakout → prijs bounced op het niveau
- Eerdere steun wordt **weerstand** na bearish breakdown → prijs stuit op het niveau
- Als prijs bij hertest **sluit buiten** het niveau: breakout waarschijnlijk vals → patroon invalide

---

## 6. Invalidatieregels

### 6.1 Algemene invalidatie

Een patroon wordt **onmiddellijk** invalide als:
- Prijs sluit beslissend terug binnen de structuur na een breakout (vals-positieve breakout)
- Structuurgrenzen worden geschonden **vóór** de breakout (interne schending)
- De verwachte compressie/expansie verdwijnt
- De pivotstructuur breekt logisch (bijv. hogere bodems worden lagere bodems in een wig)
- Een breakout in de **tegenovergestelde** richting plaatsvindt

### 6.2 Invalidatie per patroontype

| Patroon | Invalidatieregel |
|---------|-----------------|
| Rising Wedge | Prijs breekt boven bovenlijn (bullish breakout) |
| Falling Wedge | Prijs breekt onder onderlijn (bearish breakdown) |
| Ascending Triangle | Prijs sluit onder steunlijn (loss of higher low structure) |
| Descending Triangle | Prijs breekt boven dalende weerstandslijn |
| Symmetrische Driehoek | Prijs breekt met >2% buiten apex vóór breakout richting |
| Bull Flag | Prijs zakt >50% retrace in de pool |
| Bear Flag | Prijs stijgt >50% retrace in de pool |
| H&S | Prijs stijgt boven het hoofd |
| Double Top | Prijs stijgt >2% boven beide toppen |
| Double Bottom | Prijs zakt >2% onder beide bodems |
| Cup & Handle | Prijs zakt onder de cup-bodem |

---

## 7. Tekenregels

### 7.1 Wat wel tekenen

- Primaire trendlijnen (bovenlijn + onderlijn van het patroon)
- Schone steun/weerstandsgrenzen
- Breakout-zone (horizontale lijn op het sleutelniveau)
- Structuurpunten (swing highs/lows die de trendlijn definiëren)

### 7.2 Wat niet tekenen

- Niet elke wick volgen met een trendlijn — gebruik **body-respecterende** lijnen als wicks inconsistent zijn
- Geen clutter: maximaal 2 trendlijnen per patroon + 1 horizontale lijn
- Geen patronen over elkaar heen tekenen van verschillende timeframes in dezelfde grafiek
- Geen trendlijnen extrapoleren ver voorbij het patroon

### 7.3 Trendlijn starttijdstip

**Beide trendlijnen van een patroon (boven- en onderlijn) beginnen altijd op hetzelfde bar-tijdstip.** Dit is het beginpunt van het overlappende venster van swing highs en swing lows. Op die manier is de wigvorm of driehoek direct zichtbaar als geometrische figuur.

De getekende start- en eindprijzen zijn altijd de **geprojecteerde regressiewaarden** op het gemeenschappelijke start/eindbar — nooit de ruwe swing-punt-prijzen, tenzij die exact op de regressielijn liggen.

---

## 8. Marktcontext en multitimeframe

### 8.1 Continuatiepatronen

Bull Flags, Bear Flags, Driehoeken en Wiggen als voortzetting gelden als **continuatiepatroon** wanneer:
- Ze optreden tijdens een bestaande trend (ADX > 20, hogere highs of lagere lows)
- De consolidatierichting tegengesteld is aan de primaire trend
- Het grotere timeframe de richting ondersteunt

### 8.2 Reversalpatronen

H&S, Double Top, Double Bottom en Inv. H&S zijn alleen geldig als **reversalpatroon** wanneer:
- Ze optreden **na een uitgebreide beweging**: prijs minimaal **15%** bewogen in de primaire richting over de 50 bars vóór het patroon
- Het grotere timeframe tekenen van uitputting toont (RSI divergentie, dalend volume bij hogere toppen)

**Detecteer geen reversalpatronen in willekeurige zijwaartse consolidatie** zonder voorafgaande trend.

### 8.3 Hogere timeframe prioriteit

Hogere timeframe structuur heeft altijd prioriteit:
- Lower timeframe patronen die sterk conflicteren met hogere timeframe trend krijgen lagere Strength (−15)
- Een bullish 1H-patroon tijdens een bearish 1D-trend wordt gerapporteerd met verminderde betrouwbaarheid

### 8.4 Confidence-scoring additief

| Factor | Bonus/Malus |
|--------|-------------|
| Hogere timeframe trend bevestigt richting | +10 |
| ADX > 25 (trending markt) | +5 |
| Hogere timeframe conflicteert | −15 |
| Volume expansion bij breakout | +8 |
| Geen volume data beschikbaar | −5 |
| Patroon al >60 bars oud | −10 |
| Eerste breakout-bar sluit ver buiten patroon | +7 |

---

## 9. Level 1 — Indicator-patronen

Deze patronen vereisen **geen bars** — alleen de voorberekende indicatorwaarden van `TimeframeAnalysis`.

---

### 9.1 RSI Oversold

| | |
|---|---|
| **Definitie** | RSI (14) in oversold-zone |
| **Geldig wanneer** | `RSI > 0` én `RSI < 30` |
| **IsConfirmed** | Altijd `true` |
| **Strength** | `50 + (30 − RSI) × 3.5`, max 85 |
| **Richting** | Bullish |
| **Context** | Sterker signaal als tevens: prijs boven EMA50 op hogere timeframe, of MACD bullish crossover nadert |
| **Invalidatie** | RSI daalt verder onder 20 zonder koersherstel: momentum-capitulatie, signaal tijdelijk minder betrouwbaar |

---

### 9.2 RSI Overbought

| | |
|---|---|
| **Geldig wanneer** | `RSI > 70` |
| **Strength** | `50 + (RSI − 70) × 3.5`, max 85 |
| **Richting** | Bearish |
| **Noot** | In sterke uptrends (ADX > 30) kan RSI lang >70 blijven — score met 10 punten verlagen als ADX > 30 |

---

### 9.3 MACD Bullish / Bearish

| | |
|---|---|
| **Bullish** | `MACD > SignaalLijn` |
| **Bearish** | `MACD < SignaalLijn` |
| **Geldig wanneer** | Beide waarden beschikbaar (niet beide 0) |
| **Strength** | 58 (positie); 68 (verse crossing, detecteerbaar via vorige bar) |
| **Noot** | Detecteert positie, niet de crossing-moment. Een verse crossing is waardevoller |

---

### 9.4 EMA Bull / Bear Cross

| | |
|---|---|
| **Verse crossing** | EMA9 kruist EMA21 → Strength 75 |
| **Aanhoudende positie** | EMA9 boven/onder EMA21 zonder recent kruis → Strength 55 |
| **Geldig wanneer** | Beide EMA > 0, minimaal 22 bars beschikbaar |

---

### 9.5 Prijs boven / onder EMA50

| | |
|---|---|
| **Boven** | `prijs > EMA50 × 1.005` |
| **Onder** | `prijs < EMA50 × 0.995` |
| **Strength** | 55 |
| **Noot** | EMA50 is de scheidslijn tussen structureel bull en bear; altijd meewegen als context |

---

### 9.6 Bollinger Squeeze

| | |
|---|---|
| **Geldig wanneer** | BB-breedte < KC-breedte |
| **Strength** | 70 |
| **Richting** | Neutraal — wacht op richting van eerste uitbraak |
| **Noot** | Hoe langer de squeeze, hoe krachtiger de verwachte uitbraak. Combineer met volume |

---

### 9.7 Trending Markt (ADX)

| | |
|---|---|
| **Geldig wanneer** | `ADX ≥ 25` |
| **Strength** | `ADX × 2`, max 90 |
| **Richting** | Neutraal (ADX geeft kracht, niet richting) |
| **Noot** | ADX 25–35 = matige trend; 35–50 = sterke trend; >50 = extreme trend |

---

## 10. Level 2 — Prijsstructuur-patronen

Vereisen OHLCV-bars. Minimaal **20 bars** aanwezig.

---

### 10.1 Volume Spike

| Criterium | Waarde |
|-----------|--------|
| Geldig wanneer | `lastVolume / avgVolume(20) ≥ 1.8` én `avgVolume > 0` |
| Niet geldig | Volume = 0 (geen data) |
| Strength | `55 + (ratio − 1.8) × 20`, max 90 |
| Richting | Neutraal — bevestigt kracht van beweging, niet richting |

---

### 10.2 Uptrend

| Criterium | Waarde |
|-----------|--------|
| Geldig wanneer | 3 opeenvolgende hogere swing highs **EN** 3 opeenvolgende hogere swing lows |
| Invalidatie | Eerste lagere low doorbreekt de trend |
| Strength | 70 |
| Context | Sterker als ADX > 25 |

---

### 10.3 Downtrend

| Criterium | Waarde |
|-----------|--------|
| Geldig wanneer | 3 opeenvolgende lagere swing highs **EN** 3 opeenvolgende lagere swing lows |
| Invalidatie | Eerste hogere high doorbreekt de trend |
| Strength | 70 |

---

### 10.4 Dubbele Bodem (Double Bottom)

```
    \   /\   /
     \ /  \ /
      V    V   ← twee bodems op ~zelfde niveau
           |
      neklijn (hoogste punt tussen de twee bodems)
```

| Criterium | Waarde |
|-----------|--------|
| Min. bars | 20 |
| Afstand bodems | Minimaal **8 bars** apart |
| Prijsverschil | Maximaal **3%** (`|B1 − B2| / B1`) |
| Minimale diepte | Neklijn naar bodem minimaal **5%** |
| Herstel na B2 | Minimaal **2%** stijging vanuit B2 |
| **Start** | Bij de eerste bodem (B1) |
| **Einde** | Bij breakout boven neklijn of invalidatie |
| **Bevestiging** | Sluiting boven neklijn met ≥ 0.5% |
| **Invalidatie** | Sluiting >2% onder de laagste bodem |
| **Context** | Alleen geldig na voorafgaande neerwaartse trend van ≥ 15% |
| Strength bevestigd | 78 |
| Strength onbevestigd | 60 |

---

### 10.5 Dubbele Top (Double Top)

```
      /\    /\
     /  \  /  \
    /    \/    \
         |
    neklijn (laagste punt tussen de twee toppen)
```

| Criterium | Waarde |
|-----------|--------|
| Afstand toppen | Minimaal **8 bars** apart |
| Prijsverschil | Maximaal **3%** |
| Minimale hoogte | Neklijn naar top minimaal **5%** |
| Daling na T2 | Minimaal **2%** vanuit T2 |
| **Start** | Bij de eerste top (T1) |
| **Einde** | Bij breakdown onder neklijn of invalidatie |
| **Bevestiging** | Sluiting onder neklijn met ≥ 0.5% |
| **Invalidatie** | Sluiting >2% boven de hoogste top |
| **Context** | Alleen geldig na voorafgaande opwaartse trend van ≥ 15% |
| Strength bevestigd | 78 |
| Strength onbevestigd | 60 |

---

### 10.6 Bull Flag

```
      /|
     / | ← pool: ≥8% stijging
    /  | /\  ← vlag: strakke dalende/zijwaartse consolidatie
   /   |/  \
```

| Criterium | Waarde |
|-----------|--------|
| Pool | Stijging ≥ **8%** in bars −14 tot −6 |
| Vlag (laatste 5–7 bars) | Range ≤ **5%** |
| Retrace vlag | Maximaal **50%** van de pool |
| Vlagrichting | Licht dalend of vlak (niet stijgend — dan: pennant) |
| **Start** | Onderaan de pool |
| **Einde** | Breakout boven vlag-hoogste, of prijs >50% retrace in pool |
| **Bevestiging** | Sluiting boven vlag-hoogste met ≥ 0.5% |
| **Invalidatie** | Sluiting >50% retrace in de pool |
| Strength onbevestigd | 72 |
| Strength bevestigd | 80 |

---

### 10.7 Bear Flag

| Criterium | Waarde |
|-----------|--------|
| Pool | Daling ≥ **8%** in bars −14 tot −6 |
| Vlag | Range ≤ **5%**, licht stijgend of vlak |
| Retrace vlag | Maximaal **50%** van de pool |
| **Bevestiging** | Sluiting onder vlag-laagste met ≥ 0.5% |
| **Invalidatie** | Sluiting >50% retrace in pool |
| Strength | 72 (onbevestigd), 80 (bevestigd) |

---

### 10.8 Oplopende Driehoek (Ascending Triangle)

```
‾‾‾‾‾‾‾‾‾‾  ← vlakke weerstand (≥2 aanrakingen)
  /  /  /    ← stijgende bodems (≥2 aanrakingen)
```

| Criterium | Waarde |
|-----------|--------|
| Swing highs | Minimaal **3**, slope vlak `|slope| < 0.0008` |
| Swing lows | Minimaal **3**, slope positief `> 0.0008` |
| Min. aanrakingen | 2 per trendlijn |
| Variatie highs | ≤ 2% (echte vlakke weerstand) |
| **Start** | Eerste hogere bodem |
| **Einde** | Breakout boven weerstand of support-breuk |
| **Bevestiging** | Sluiting boven weerstand + ≥ 1.0% |
| **Invalidatie** | Sluiting onder stijgende steunlijn |
| Strength | 68 |

---

### 10.9 Dalende Driehoek (Descending Triangle)

| Criterium | Waarde |
|-----------|--------|
| Swing lows | Minimaal 3, slope vlak `|slope| < 0.0008` |
| Swing highs | Minimaal 3, slope negatief `< −0.0008` |
| Min. aanrakingen | 2 per trendlijn |
| Variatie lows | ≤ 2% |
| **Bevestiging** | Sluiting onder steun − ≥ 1.0% |
| **Invalidatie** | Sluiting boven dalende weerstandslijn |
| Strength | 68 |

---

### 10.10 Symmetrische Driehoek

| Criterium | Waarde |
|-----------|--------|
| Swing highs | Minimaal 3, slope negatief |
| Swing lows | Minimaal 3, slope positief |
| Min. aanrakingen | 2 per trendlijn |
| Apex | Breakout uiterlijk vóór de laatste **15%** van het apex-punt |
| **Bevestiging** | Directional close buiten de structuur met ≥ 1.0% + volume |
| **Invalidatie** | Prijs bereikt apex zonder breakout (structuurverval) |
| Strength | 60 (richting onbekend) |

---

### 10.11 Consolidatie

| Criterium | Waarde |
|-----------|--------|
| Geldig wanneer | Range ≤ **7%** over laatste 15 bars |
| Niet geldig | Range > 7% (te breed), < 1.5% (noise) |
| Strength | 62 |

---

### 10.12 Breakout boven Weerstand

| Criterium | Waarde |
|-----------|--------|
| Breakout zone | Prijs **0.5%–5%** boven weerstand |
| Bevestiging | Prijs > weerstand + 1.5% |
| Verouderd | Prijs al > 8% boven weerstand → patroon te oud |
| Strength | 80 |

---

### 10.13 Breakdown onder Steun

| Criterium | Waarde |
|-----------|--------|
| Breakdown zone | Prijs **0.5%–5%** onder steun |
| Bevestiging | Prijs < steun − 1.5% |
| Verouderd | Prijs al > 8% onder steun → patroon te oud |
| Strength | 80 |

---

### 10.14 Bijna Breakout (Potential Breakout)

| Criterium | Waarde |
|-----------|--------|
| Geldig wanneer | Prijs binnen 0%–3% onder weerstandsniveau |
| Niet geldig | Al boven weerstand (dan: Breakout), of > 3% eronder |
| Strength | 65 |

---

## 11. Level 3 — Klassieke grafiekpatronen

Vereisen minimaal **50 bars**.

---

### 11.1 Head & Shoulders (H&S) — Bearish Reversal

```
        H
       /|\
      / | \
  LS /  |  \ RS
    /   |   \
___/    |    \___
        neklijn
```

| Criterium | Waarde |
|-----------|--------|
| Swing highs nodig | Minimaal **5** recente swing highs |
| Hoofd (H) | Minimaal **3%** hoger dan zowel LS als RS |
| Schouder-symmetrie | `|LS − RS| / max(LS, RS) ≤ 0.15` |
| Volgorde | LS → H → RS chronologisch |
| Neklijn | Max van (laagste low in LS–H interval, laagste low in H–RS interval) |
| Min. patroonbreedte | **12 bars** van LS tot RS |
| **Start** | Bij de linker schouder (LS) |
| **Einde** | Neklijn-breakdown of invalidatie |
| **Bevestiging** | Sluiting < neklijn × 0.995 |
| **Invalidatie** | Prijs stijgt boven het hoofd (H) |
| **Context** | Alleen geldig na opwaartse trend van ≥ 15% over de 50 bars vóór LS |
| Strength bevestigd | 84 |
| Strength onbevestigd | 70 |

---

### 11.2 Inverse Head & Shoulders (Inv. H&S) — Bullish Reversal

```
        neklijn
___     |     ___
   \    |    /
LS  \   |   / RS
     \  |  /
      \ | /
        H
```

| Criterium | Waarde |
|-----------|--------|
| Swing lows nodig | Minimaal **5** recente swing lows |
| Hoofd (H) | Minimaal **3%** lager dan zowel LS als RS |
| Schouder-symmetrie | ≤ 15% verschil |
| Neklijn | Min van (hoogste high in LS–H interval, hoogste high in H–RS interval) |
| Min. patroonbreedte | **12 bars** |
| **Start** | Bij de linker schouder (LS) |
| **Bevestiging** | Sluiting > neklijn × 1.005 |
| **Invalidatie** | Prijs daalt onder het hoofd (H) |
| **Context** | Alleen geldig na neerwaartse trend van ≥ 15% over de 50 bars vóór LS |
| Strength bevestigd | 84 |
| Strength onbevestigd | 70 |

---

### 11.3 Rising Wedge — Bearish

```
   /‾/‾/‾  ← hogere highs (langzamer stijgend)
  / / /
 /_/_/    ← hogere lows (sneller stijgend → convergentie)
```

#### Definitie

Beide trendlijnen stijgen, maar de **onderlijn** stijgt sneller dan de bovenlijn. Dit zorgt voor geometrische convergentie van boven naar rechts.

#### Vereisten

| Criterium | Waarde |
|-----------|--------|
| Swing highs | Minimaal **3** in overlappend venster, positieve helling |
| Swing lows | Minimaal **3** in overlappend venster, positieve helling |
| Onderlijn stijgt sneller | `lowSlope > highSlope` (na regressie op werkelijke bar-indices) |
| Geometrische convergentie | Gap aan het einde < **70%** van de gap aan het begin (≥ 30% versmalling) |
| Beide lijnen dezelfde startbar | Ja — geprojecteerde regressiewaarden op `winStart` |
| Min. venstergrootte | **15 bars** overlappend |
| Wedge-breedte | Gap-start / gemiddelde prijs: tussen **3% en 35%** |
| Min. helling | Steilste normaliseerde helling ≥ 0.0003 (geen bijna-horizontale lijnen) |
| **Start** | `winStart` — gemeenschappelijk startpunt van swing highs en lows |
| **Einde** | Breakdown onder onderlijn of breakout boven bovenlijn (invalidatie) |
| **Bevestiging** | Sluiting onder geprojecteerde onderlijn op dat moment |
| **Invalidatie** | Sluiting boven de bovenlijn (bullish breakout) |
| Strength | 72 |

---

### 11.4 Falling Wedge — Bullish

```
 \_\_\_   ← lagere highs (sneller dalend → convergentie)
  \ \ \
   \‾\‾\  ← lagere lows (langzamer dalend)
```

#### Definitie

Beide trendlijnen dalen, maar de **bovenlijn** daalt sneller dan de onderlijn. Op de linkerkant staan de lijnen ver uit elkaar; naar rechts convergeren ze — dat is de kenmerkende wigvorm.

#### Vereisten

| Criterium | Waarde |
|-----------|--------|
| Swing highs | Minimaal **3** in overlappend venster, negatieve helling |
| Swing lows | Minimaal **3** in overlappend venster, negatieve helling |
| Bovenlijn daalt sneller | `highSlope < lowSlope` (meer negatief) |
| Geometrische convergentie | Gap aan het einde < **70%** van de gap aan het begin (≥ 30% versmalling) |
| Beide lijnen dezelfde startbar | Ja |
| Min. venstergrootte | **15 bars** overlappend |
| Wedge-breedte | Tussen **3% en 35%** |
| Min. helling | ≥ 0.0003 genormaliseerd |
| **Start** | `winStart` |
| **Einde** | Breakout boven bovenlijn of breakdown onder onderlijn (invalidatie) |
| **Bevestiging** | Sluiting boven geprojecteerde bovenlijn op dat moment |
| **Invalidatie** | Sluiting onder de onderlijn (bearish breakdown) |
| Strength | 72 |

---

### 11.5 Cup & Handle — Bullish Continuation

```
 \           /|
  \         / | ← handle: strakke pullback ≤45% retrace
   \       /  |
    \_____/   ← U-vormige cup, 30–65 bars
```

#### Cup-vereisten

| Criterium | Waarde |
|-----------|--------|
| Cup-lengte | **30–65 bars** |
| Cup-diepte | **10%–40%** van cup-rand naar bodem |
| Symmetrie randen | `|leftRim − rightRim| / max ≤ 0.06` (6%) |
| Minimumpunt | Cup-bodem in **middelste helft** van de cup |

#### Handle-vereisten

| Criterium | Waarde |
|-----------|--------|
| Handle-range | ≤ **7%** |
| Handle-retrace | ≤ **45%** van de cup |
| Handle-richting | Licht dalend of zijwaarts |

#### Bevestiging en invalidatie

| | |
|---|---|
| **Start** | Linker rand van de cup |
| **Einde** | Breakout boven handle-hoogste of prijs zakt onder cup-bodem |
| **Bevestiging** | Sluiting > handleHigh × 1.005 |
| **Invalidatie** | Prijs sluit onder cup-bodem |
| **Context** | Geldig als voortzetting van opwaartse trend of na herstel van significante correctie |
| Strength bevestigd | 82 |
| Strength onbevestigd | 68 |

---

### 11.6 Adam & Eve — Bullish Reversal

| Criterium | Waarde |
|-----------|--------|
| Twee bodems | Prijsverschil ≤ 3% |
| Adam-bodem | Scherpe V-spike: aangrenzende bars (±2) minimaal 2.5% hoger |
| Eve-bodem | Afgeronde basis: ≥3 van 7 gecentreerde bars binnen 2% van het minimum |
| Afstand | Minimaal **8 bars** |
| Minimale diepte | ≥ 5% |
| **Context** | Alleen na neerwaartse trend ≥ 15% |
| Strength bevestigd | 82 |
| Strength onbevestigd | 64 |

---

### 11.7 Kanalen (Ascending / Descending Channel)

#### Vereisten

| Criterium | Waarde |
|-----------|--------|
| Min. aanrakingen | **2 per trendlijn** |
| Hellingsgelijkheid | Slopes binnen **50%** van elkaar (niet convergerende wiggen) |
| Kanaalbreed | **4%–30%** van de prijs |
| Convergentie-uitsluiting | Gap-versmalling < **30%** (anders: wig) |
| Min. patroonlengte | **10 bars** |
| **Bevestiging** | Prijs nadert kanaalwand (koopzone/verkoopzone) |
| **Invalidatie** | Sluiting buiten de kanaalwand met ≥ 1% |
| Strength | 70 (nabij kanaalbodem), 60 (midden kanaal) |

---

## 12. Strength-tabel

| Strength | Label | Interpretatie |
|----------|-------|---------------|
| 80–100 | Sterke setup | Hoge betrouwbaarheid, volume-bevestiging raden aan |
| 60–79 | Mogelijke setup | Goed signaal, wacht op aanvullende bevestiging |
| 50–59 | Zwak signaal | Indicatief; laag gewicht in totaalscore |
| < 50 | Niet rapporteren | Slechts ruis; patroon niet opnemen |

**Richtlijnen per situatie:**

| Situatie | Strength |
|----------|----------|
| Bevestigd Level 3 patroon | 82–84 |
| Onbevestigd Level 3 patroon | 68–70 |
| Level 2 bevestigd | 72–80 |
| Level 2 onbevestigd | 60–68 |
| Level 1 (indicator) | 55–85 afhankelijk van extremiteit |

---

## 13. Veelgemaakte fouten

### F1 — Swing-punten niet significant genoeg
**Probleem:** Lookback van 3 bars zonder significantiedrempel geeft micro-swings van 0.1% die geen handelbare structuur vormen.
**Oplossing:** Swing high/low moet minimaal **0.5%** hogere resp. lager zijn dan de naburige bar in het lookback-venster.

### F2 — Regressie op array-indices in plaats van bar-indices
**Probleem:** Als swing-punten ongelijk verdeeld zijn in de tijd (bars 10, 35, 80), behandelt array-index regressie ze als 0, 1, 2 — een vertekende helling.
**Oplossing:** Altijd regressie op **werkelijke bar-indices** voor trendlijndetectie.

### F3 — Convergentiecheck op slopesverschil i.p.v. geometrische gap
**Probleem:** `highSlope < lowSlope × 1.20` garandeert niet dat de lijnen zichtbaar naar elkaar toelopen. Bij kleine hellingen is het verschil minimaal maar de check slaagt toch.
**Oplossing:** Meet de **geometrische gap** aan het begin en einde van het patroon. Gap-einde < 70% van gap-begin = ≥30% versmalling vereist.

### F4 — Beide trendlijnen beginnen op verschillende bar-posities
**Probleem:** Bovenlijn begint op bar 40, onderlijn op bar 20 → figuur ziet er niet uit als een wig.
**Oplossing:** Beide trendlijnen beginnen altijd op hetzelfde `winStart`-bar. Gebruik geprojecteerde regressiewaarden als startprijzen.

### F5 — Reversalpatronen in willekeurige zijwaartse chop
**Probleem:** H&S en Double Top/Bottom detecteren in een sideways markt zonder voorafgaande trend geeft kwalitatief slechte signalen.
**Oplossing:** Reversalpatronen vereisen een **voorafgaande trend van ≥15%** over de 50 bars vóór het patroon.

### F6 — Patroon rapporteren dat al lang bevestigd is
**Probleem:** Een breakout die al 30 bars en 15% geleden plaatsvond, is geen actief signaal meer.
**Oplossing:** Als `currentPrice` al >8% voorbij het sleutelniveau is terwijl het patroon "confirmed" zou zijn, wordt het niet meer gerapporteerd.

### F7 — Bevestiging op intrabar-wick i.p.v. slotkoers
**Probleem:** Intrabar-pieken triggeren bevestiging die op de slotkoers niet standhoudt.
**Oplossing:** Gebruik altijd `bars[^1].Close` voor bevestigingscheck.

### F8 — Slopeberekening schaalafhankelijk
**Probleem:** Ruwe slope van BTC ($65.000) vs. altcoin ($0.001) zijn niet vergelijkbaar.
**Oplossing:** Normaliseer slope altijd door te delen door de gemiddelde prijs.

### F9 — Neklijn onjuist berekend (H&S)
**Probleem:** Neklijn als eenvoudig gemiddelde van alle lows geeft een onbruikbare waarde.
**Oplossing:** Neklijn = **max** van (laagste low in LS–H interval, laagste low in H–RS interval).

### F10 — Patronen zonder aanrakingsvalidatie
**Probleem:** Regressielijn detecteren is niet hetzelfde als een trendlijn waartegen de prijs daadwerkelijk bounced.
**Oplossing:** Minimaal **2 werkelijke aanrakingen** per trendlijn vereisen (swing-punt op of binnen 1% van de regressielijn).

### F11 — Patroon te klein t.o.v. volatiliteit
**Probleem:** Een wedge van 2% hoogte op een coin met ATR van 5% is geen meaningful patroon.
**Oplossing:** Patroonhoogte ≥ 0.5 × ATR(14).

### F12 — Te tolerante swing-detectie voor Level 3 patronen
**Probleem:** H&S en wig gedetecteerd op micro-swings die alleen ruis zijn op een 4H chart.
**Oplossing:** Level 3 patronen gebruiken dezelfde lookback=3, maar de 0.5% significantiedrempel filtert automatisch ruis weg.

---

## 14. Roadmap geavanceerde detectie

De volgende detectiemethoden zijn buiten scope van de huidige versie maar vormen de volgende stap naar institutionele kwaliteit:

### 14.1 Liquiditeitsevents
- **Liquidity sweeps**: prijs haalt even de highs/lows op (stop hunt) en keert dan onmiddellijk terug
- **Fake breakouts**: wick doorbreekt niveau, slotkoers sluit terug binnen structuur
- **Deviation candles** (Wyckoff): overshoot voorbij range, direct gevolgd door reversal candle met verhoogd volume

### 14.2 Marktstructuur
- **Wyckoff accumulation/distribution**: uitgebreide analyse van volume vs. price action in ranges
- **Impulse vs. correctieve structuur**: impuls = 3+ aanéénsluitende bars in dezelfde richting; correctie = kleiner, trager, tegengesteld
- **Elliott Wave swing-telling**: 5-golf impuls + 3-golf correctie op basis van pivot-sequentie

### 14.3 Volume & Open Interest
- **Volume Profile / Point of Control**: prijsniveaus met hoogste historisch handelsvolume
- **Funding Rate** (Bybit/Binance): extreme positieve/negatieve funding als contra-indicator
- **Open Interest spikes**: grote stijging OI = nieuwe posities opening (trend confirmatie of trap)

### 14.4 Volatiliteitsfilters
- **ATR-proportionaliteit** per patroon: reeds gedeeltelijk geïmplementeerd (zie §3.3)
- **Volatiliteitsregime-detectie**: hoge vs. lage volatiliteitsperiodes scheiden (Bollinger Band width trend)

### 14.5 Fractal nesting
- Patroon op 1H dat overeenkomt met consolidatiefase in een groter patroon op 4H
- Confluence: meerdere patronen op meerdere timeframes wijzen dezelfde richting → hogere Strength

### 14.6 Marktregime-filter
- In bull-regime (BTC ATH-gebied, AltSeason): bearish reversalpatronen kregen lager gewicht
- In bear-regime: bullish continuatiepatronen kregen lager gewicht tenzij bevestigd

---

*Versie 2.0 — Mei 2026 — bijgewerkt na institutionele kwaliteitsreview*
