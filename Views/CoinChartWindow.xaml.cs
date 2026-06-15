using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using WinRT.Interop;

namespace CryptoPortfolioTracker.Views;

/// <summary>
/// Dedicated window that renders a TradingView Lightweight Charts candlestick chart
/// for a single coin via WebView2.  Supports 1D / 4H / 1H timeframe switching.
///
/// When opened from a pattern badge, <paramref name="highlightPattern"/> supplies
/// chart annotations (markers, necklines, trendlines) that visually overlay the pattern.
/// </summary>
public sealed partial class CoinChartWindow : Window
{
    private readonly PatternCoinAnalysis _analysis;
    private readonly PatternResult?      _highlight;     // null = no annotation
    private string _activeTimeframe = "1D";

    // ── Constructors ─────────────────────────────────────────────────────────

    /// <summary>Open chart without a specific pattern overlay (from the "📈 Grafiek" button).</summary>
    public CoinChartWindow(PatternCoinAnalysis analysis)
        : this(analysis, null) { }

    /// <summary>Open chart with a specific pattern highlighted (from badge click).</summary>
    public CoinChartWindow(PatternCoinAnalysis analysis, PatternResult? highlightPattern)
    {
        _analysis  = analysis;
        _highlight = highlightPattern;
        InitializeComponent();

        string suffix = highlightPattern is not null
            ? $" — {highlightPattern.DisplayName} [{highlightPattern.Timeframe}]"
            : " — Grafiek";
        Title = $"{analysis.Coin.Name} ({analysis.Coin.Symbol?.ToUpperInvariant()}){suffix}";

        SetWindowSize(1280, 760);
    }

    private void SetWindowSize(int width, int height)
    {
        var hwnd    = WindowNative.GetWindowHandle(this);
        var wndId   = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWnd  = AppWindow.GetFromWindowId(wndId);
        var display = DisplayArea.GetFromWindowId(wndId, DisplayAreaFallback.Primary);

        int x = Math.Max(0, (display.WorkArea.Width  - width)  / 2);
        int y = Math.Max(0, (display.WorkArea.Height - height) / 2);

        appWnd.MoveAndResize(new Windows.Graphics.RectInt32(x, y, width, height));
    }

    // ── WebView2 lifecycle ───────────────────────────────────────────────────

    private async void ChartView_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await ChartView.EnsureCoreWebView2Async();

            // Auto-select the timeframe of the highlighted pattern, if any.
            // Map "15M" pattern timeframe label back to chart timeframe key.
            string startTf = _highlight?.Timeframe switch
            {
                "4H"  => "4H",
                "1H"  => "1H",
                "15M" => "15M",
                _     => "1D",
            };
            await LoadChartAsync(startTf);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CoinChartWindow: WebView2 init failed — {ex.Message}");
            BarCountLabel.Text = "Grafiek kon niet worden geïnitialiseerd.";
        }
    }

    // ── Timeframe buttons ────────────────────────────────────────────────────

    private async void Btn1D_Click(object sender, RoutedEventArgs e)  => await LoadChartAsync("1D");
    private async void Btn4H_Click(object sender, RoutedEventArgs e)  => await LoadChartAsync("4H");
    private async void Btn1H_Click(object sender, RoutedEventArgs e)  => await LoadChartAsync("1H");
    private async void Btn15M_Click(object sender, RoutedEventArgs e) => await LoadChartAsync("15M");

    // ── Chart loading ────────────────────────────────────────────────────────

    private async System.Threading.Tasks.Task LoadChartAsync(string timeframe)
    {
        _activeTimeframe = timeframe;
        UpdateButtonStates();

        var bars = timeframe switch
        {
            "4H"  => _analysis.H4Bars,
            "1H"  => _analysis.H1Bars,
            "15M" => _analysis.M15Bars,
            _     => _analysis.DailyBars,
        };

        // Toon precies één patroon-overlay voor het actieve timeframe: het aangeklikte patroon
        // (als de badge is gebruikt en het bij dit timeframe hoort), anders het sterkste
        // geometrische patroon van dit timeframe. Eén patroon tekenen voorkomt overlappende,
        // tegenstrijdige lijnen. Indicator-patronen (RSI/MACD/EMA…) hebben geen annotatie.
        var tfPatterns = _analysis.Patterns
            .Where(p => p.Timeframe == timeframe && p.Annotation is not null && !p.Annotation.IsEmpty)
            .ToList();

        PatternResult? chosen =
            (_highlight is not null && _highlight.Timeframe == timeframe
                && _highlight.Annotation is not null && !_highlight.Annotation.IsEmpty)
            ? _highlight
            : tfPatterns
                .OrderByDescending(p => p.IsConfirmed)
                .ThenByDescending(p => p.Strength)
                .FirstOrDefault();

        PatternAnnotation? annotation = chosen?.Annotation;
        string patternLabel = chosen is not null ? $"  ·  {chosen.DisplayName} ({chosen.StatusLabel})" : string.Empty;

        UpdateLegend(chosen);

        BarCountLabel.Text = bars.Count > 0
            ? $"{bars.Count} candles  ·  Bron: {_analysis.DataSource}{patternLabel}"
            : "Geen data beschikbaar voor dit timeframe";

        var html = BuildChartHtml(bars, _analysis.SupportLevels, _analysis.ResistanceLevels, annotation);
        ChartView.NavigateToString(html);

        await System.Threading.Tasks.Task.CompletedTask;
    }

    /// <summary>Vult de legenda met een uitleg van de getekende symbolen voor het gekozen patroon.</summary>
    private void UpdateLegend(PatternResult? p)
    {
        if (p is null)
        {
            LegendBorder.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            return;
        }

        string body = p.Type switch
        {
            PatternType.CupAndHandle =>
                "🔵 L = cup-linkerrand · B = cup-bodem · R = cup-rechterrand · 🟠 ↑ = handle/breakout.  " +
                "Lijnen: oranje = neklijn (breakout-niveau), groen = T1 (handle-diepte), blauw = Tmax (cup-diepte) — koersdoelen ná uitbraak.",
            PatternType.DoubleBottom =>
                "🔵 B1, B2 = de twee bodems · oranje lijn = neklijn (slotkoers erboven = bevestiging).",
            PatternType.DoubleTop =>
                "🟠 T1, T2 = de twee toppen · rode lijn = neklijn (slotkoers eronder = bevestiging).",
            PatternType.AdamAndEve =>
                "🔵 A = scherpe (Adam) bodem · E = ronde (Eve) bodem · lijn = neklijn (breakout = bevestiging).",
            PatternType.HeadAndShoulders =>
                "🟠 LS = linkerschouder · H = hoofd · RS = rechterschouder · lijn = neklijn (breakdown eronder = bevestiging).",
            PatternType.InverseHeadAndShoulders =>
                "🔵 LS = linkerschouder · H = hoofd · RS = rechterschouder (omgekeerd) · lijn = neklijn (breakout erboven = bevestiging).",
            PatternType.BullFlag =>
                "Groene diagonale lijn = pool (sterke stijging) · oranje vak = vlag (consolidatie) · groene lijn = breakout-niveau · blauwe lijn = Tmax (pool-lengte vanaf de vlag-top).",
            PatternType.BearFlag =>
                "Rode diagonale lijn = pool (sterke daling) · oranje vak = vlag (consolidatie) · rode lijn = breakdown-niveau · blauwe lijn = Tmax (pool-lengte vanaf de vlag-bodem).",
            PatternType.AscendingChannel or PatternType.DescendingChannel =>
                "Twee parallelle trendlijnen = kanaalwanden — rood = weerstand (bovenlijn), groen = steun (onderlijn).",
            PatternType.AscendingTriangle or PatternType.DescendingTriangle or PatternType.SymmetricalTriangle =>
                "Twee convergerende trendlijnen — rood = weerstand (bovenlijn), groen = steun (onderlijn). Uitbraak bepaalt de richting.",
            PatternType.RisingWedge or PatternType.FallingWedge =>
                "Twee samenlopende trendlijnen vormen de wig — rood = bearish, groen = bullish.",
            _ =>
                "🔵 bollen onder de candle = structuurpunten (swings) · 🟠 bollen boven de candle = sleutel-/breakoutpunten · horizontale lijnen = sleutelniveaus.",
        };

        LegendLabel.Text = $"Legenda — {body}";
        LegendBorder.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
    }

    private void UpdateButtonStates()
    {
        Btn1D.IsChecked  = _activeTimeframe == "1D";
        Btn4H.IsChecked  = _activeTimeframe == "4H";
        Btn1H.IsChecked  = _activeTimeframe == "1H";
        Btn15M.IsChecked = _activeTimeframe == "15M";
    }

    // ── HTML generation ──────────────────────────────────────────────────────

    private static string BuildChartHtml(
        List<OhlcvBar>      bars,
        List<PatternLevel>    supportLevels,
        List<PatternLevel>    resistanceLevels,
        PatternAnnotation?  annotation = null)
    {
        string candleJson      = BarsToJson(bars);
        string supportJson     = PatternLevelsToJson(supportLevels);
        string resistJson      = PatternLevelsToJson(resistanceLevels);
        string annotationJson  = AnnotationToJson(annotation);

        return $@"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"">
<style>
*{{margin:0;padding:0;box-sizing:border-box}}
body{{background:#131722;overflow:hidden}}
#chart{{width:100vw;height:100vh}}
#msg{{color:#9598a1;font-family:sans-serif;padding:24px;font-size:13px}}
</style>
</head>
<body>
<div id=""chart""></div>
<script src=""https://unpkg.com/lightweight-charts@4.1.3/dist/lightweight-charts.standalone.production.js""></script>
<script>
try {{
  var chart = LightweightCharts.createChart(document.getElementById('chart'), {{
    layout: {{
      background: {{ type: 'solid', color: '#131722' }},
      textColor: '#c8c8d0'
    }},
    grid: {{
      vertLines: {{ color: '#1e2230' }},
      horzLines: {{ color: '#1e2230' }}
    }},
    crosshair: {{ mode: 1 }},
    rightPriceScale: {{ borderColor: '#2a2e39' }},
    timeScale: {{ borderColor: '#2a2e39', timeVisible: true, secondsVisible: false }},
    handleScroll: true,
    handleScale: true,
    width: window.innerWidth,
    height: window.innerHeight
  }});

  window.addEventListener('resize', function() {{
    chart.applyOptions({{ width: window.innerWidth, height: window.innerHeight }});
  }});

  var series = chart.addCandlestickSeries({{
    upColor:       '#26a69a',
    downColor:     '#ef5350',
    borderVisible: false,
    wickUpColor:   '#26a69a',
    wickDownColor: '#ef5350'
  }});

  var data = {candleJson};
  if (data.length > 0) {{
    series.setData(data);
  }} else {{
    document.getElementById('chart').innerHTML =
      '<div id=""msg"">Geen OHLCV-data beschikbaar voor dit timeframe.</div>';
  }}

  // ── Support / resistance lines ──
  var support = {supportJson};
  support.forEach(function(level) {{
    series.createPriceLine({{
      price: level.price, color: '#26a69a', lineWidth: 1,
      lineStyle: LightweightCharts.LineStyle.Dashed,
      axisLabelVisible: true, title: 'S-' + level.tf
    }});
  }});
  var resistance = {resistJson};
  resistance.forEach(function(level) {{
    series.createPriceLine({{
      price: level.price, color: '#ef5350', lineWidth: 1,
      lineStyle: LightweightCharts.LineStyle.Dashed,
      axisLabelVisible: true, title: 'R-' + level.tf
    }});
  }});

  // ── Pattern annotation overlay ──
  var ann = {annotationJson};
  if (ann) {{

    // Markers (circles above/below bars)
    if (ann.markers && ann.markers.length > 0) {{
      var markers = ann.markers.map(function(m) {{
        return {{
          time:     m.time,
          position: m.aboveBar ? 'aboveBar' : 'belowBar',
          color:    m.aboveBar ? '#f59e0b' : '#60c0c0',
          shape:    'circle',
          text:     m.label,
          size:     1
        }};
      }});
      markers.sort(function(a, b) {{ return a.time - b.time; }});
      series.setMarkers(markers);
    }}

    // Horizontal annotation lines (necklines etc.) — thicker than S/R
    if (ann.hlines) {{
      ann.hlines.forEach(function(hl) {{
        series.createPriceLine({{
          price: hl.price, color: hl.color, lineWidth: 2,
          lineStyle: LightweightCharts.LineStyle.Solid,
          axisLabelVisible: true, title: hl.title
        }});
      }});
    }}

    // Diagonal trendlines via LineSeries
    if (ann.trendlines) {{
      ann.trendlines.forEach(function(tl) {{
        var trendSeries = chart.addLineSeries({{
          color: tl.color, lineWidth: 1,
          lineStyle: LightweightCharts.LineStyle.Dashed,
          lastValueVisible: false,
          priceLineVisible: false,
          crosshairMarkerVisible: false
        }});
        trendSeries.setData([
          {{ time: tl.t1, value: tl.p1 }},
          {{ time: tl.t2, value: tl.p2 }}
        ]);
      }});
    }}
  }}

  chart.timeScale().fitContent();

}} catch(err) {{
  document.getElementById('chart').innerHTML =
    '<div id=""msg"">Chart kon niet worden geladen: ' + err.message + '</div>';
}}
</script>
</body>
</html>";
    }

    // ── Serialisation helpers ────────────────────────────────────────────────

    private static string BarsToJson(List<OhlcvBar> bars)
    {
        if (bars.Count == 0) return "[]";

        var sorted = bars.OrderBy(b => b.Date).ToList();
        var sb = new StringBuilder("[", sorted.Count * 70);

        for (int i = 0; i < sorted.Count; i++)
        {
            var  b  = sorted[i];
            long ts = ToUnixSeconds(b.Date);

            sb.Append('{');
            sb.Append("\"time\":"); sb.Append(ts);
            sb.Append(",\"open\":"); sb.Append(b.Open.ToString("G",  CultureInfo.InvariantCulture));
            sb.Append(",\"high\":"); sb.Append(b.High.ToString("G",  CultureInfo.InvariantCulture));
            sb.Append(",\"low\":");  sb.Append(b.Low.ToString("G",   CultureInfo.InvariantCulture));
            sb.Append(",\"close\":"); sb.Append(b.Close.ToString("G", CultureInfo.InvariantCulture));
            sb.Append('}');

            if (i < sorted.Count - 1) sb.Append(',');
        }

        sb.Append(']');
        return sb.ToString();
    }

    /// <summary>Serialises PatternLevel list to [{price:…,tf:"4H"},…] for use in chart JS.</summary>
    private static string PatternLevelsToJson(List<PatternLevel> levels)
    {
        if (levels.Count == 0) return "[]";
        return "[" + string.Join(",", levels.Select(l =>
            $"{{\"price\":{l.Price.ToString("G", CultureInfo.InvariantCulture)},\"tf\":\"{EscJs(l.Timeframe)}\"}}"
        )) + "]";
    }

    private static string AnnotationToJson(PatternAnnotation? ann)
    {
        if (ann is null || ann.IsEmpty) return "null";

        var sb = new StringBuilder("{");

        // markers
        sb.Append("\"markers\":[");
        for (int i = 0; i < ann.Markers.Count; i++)
        {
            var m = ann.Markers[i];
            sb.Append('{');
            sb.Append($"\"time\":{ToUnixSeconds(m.Time)}");
            sb.Append($",\"price\":{m.Price.ToString("G", CultureInfo.InvariantCulture)}");
            sb.Append($",\"label\":\"{EscJs(m.Label)}\"");
            sb.Append($",\"aboveBar\":{(m.AboveBar ? "true" : "false")}");
            sb.Append('}');
            if (i < ann.Markers.Count - 1) sb.Append(',');
        }
        sb.Append("],");

        // hlines
        sb.Append("\"hlines\":[");
        for (int i = 0; i < ann.HLines.Count; i++)
        {
            var hl = ann.HLines[i];
            sb.Append('{');
            sb.Append($"\"price\":{hl.Price.ToString("G", CultureInfo.InvariantCulture)}");
            sb.Append($",\"color\":\"{EscJs(hl.Color)}\"");
            sb.Append($",\"title\":\"{EscJs(hl.Title)}\"");
            sb.Append('}');
            if (i < ann.HLines.Count - 1) sb.Append(',');
        }
        sb.Append("],");

        // trendlines
        sb.Append("\"trendlines\":[");
        for (int i = 0; i < ann.Trendlines.Count; i++)
        {
            var tl = ann.Trendlines[i];
            sb.Append('{');
            sb.Append($"\"t1\":{ToUnixSeconds(tl.StartTime)}");
            sb.Append($",\"p1\":{tl.StartPrice.ToString("G", CultureInfo.InvariantCulture)}");
            sb.Append($",\"t2\":{ToUnixSeconds(tl.EndTime)}");
            sb.Append($",\"p2\":{tl.EndPrice.ToString("G", CultureInfo.InvariantCulture)}");
            sb.Append($",\"color\":\"{EscJs(tl.Color)}\"");
            sb.Append('}');
            if (i < ann.Trendlines.Count - 1) sb.Append(',');
        }
        sb.Append("]");

        sb.Append('}');
        return sb.ToString();
    }

    private static string EscJs(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    /// <summary>Converts a DateTime to a UTC Unix timestamp (seconds).</summary>
    private static long ToUnixSeconds(DateTime dt)
    {
        var kind = dt.Kind == DateTimeKind.Unspecified ? DateTimeKind.Utc : dt.Kind;
        return new DateTimeOffset(DateTime.SpecifyKind(dt, kind)).ToUnixTimeSeconds();
    }
}
