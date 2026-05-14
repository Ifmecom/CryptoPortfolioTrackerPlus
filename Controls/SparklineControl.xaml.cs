using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System.Collections.Generic;
using System.Linq;

namespace CryptoPortfolioTracker.Controls;

/// <summary>
/// Lightweight sparkline UserControl.
/// Bind the <see cref="Values"/> dependency property to an IReadOnlyList&lt;double&gt;.
/// The polyline is drawn green when the linear-regression slope is positive, red otherwise.
/// </summary>
public sealed partial class SparklineControl : UserControl
{
    // ── Dependency property ──────────────────────────────────────────────────

    public static readonly DependencyProperty ValuesProperty =
        DependencyProperty.Register(
            nameof(Values),
            typeof(IReadOnlyList<double>),
            typeof(SparklineControl),
            new PropertyMetadata(null, OnValuesChanged));

    public IReadOnlyList<double> Values
    {
        get => (IReadOnlyList<double>)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    private static void OnValuesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SparklineControl ctrl)
            ctrl.Redraw();
    }

    // ── Constructor ──────────────────────────────────────────────────────────

    public SparklineControl()
    {
        InitializeComponent();
        Loaded += (_, _) => Redraw();
    }

    // ── Drawing ──────────────────────────────────────────────────────────────

    private static readonly Windows.UI.Color ColorUp   = Windows.UI.Color.FromArgb(0xFF, 0x3C, 0xB3, 0x71); // green
    private static readonly Windows.UI.Color ColorDown = Windows.UI.Color.FromArgb(0xFF, 0xCD, 0x5C, 0x5C); // red
    private static readonly Windows.UI.Color ColorFlat = Windows.UI.Color.FromArgb(0xFF, 0xA0, 0xA0, 0xA0); // grey

    private void Redraw()
    {
        sparkCanvas.Children.Clear();

        var values = Values;
        if (values is null || values.Count < 2) return;

        double w = sparkCanvas.Width;
        double h = sparkCanvas.Height;

        double min = values.Min();
        double max = values.Max();
        double range = max - min;
        if (range == 0) range = 1; // avoid division by zero on flat series

        int n = values.Count;

        // Build point collection
        var points = new PointCollection();
        for (int i = 0; i < n; i++)
        {
            double x = i * w / (n - 1);
            double y = h - (values[i] - min) / range * h;
            // Clamp to canvas bounds
            y = System.Math.Clamp(y, 0, h);
            points.Add(new Windows.Foundation.Point(x, y));
        }

        // Determine trend colour from linear regression slope
        double slope = LinearRegressionSlope(values);
        var lineColor = slope > 0.0 ? ColorUp
                      : slope < 0.0 ? ColorDown
                      : ColorFlat;

        var polyline = new Polyline
        {
            Points          = points,
            Stroke          = new SolidColorBrush(lineColor),
            StrokeThickness = 1.5,
            StrokeLineJoin  = PenLineJoin.Round,
        };

        sparkCanvas.Children.Add(polyline);
    }

    // ── Math helper ──────────────────────────────────────────────────────────

    /// <summary>Returns the slope of the OLS regression line (positive = uptrend).</summary>
    private static double LinearRegressionSlope(IReadOnlyList<double> y)
    {
        int n = y.Count;
        double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
        for (int i = 0; i < n; i++)
        {
            sumX  += i;
            sumY  += y[i];
            sumXY += i * y[i];
            sumX2 += i * i;
        }
        double denom = n * sumX2 - sumX * sumX;
        return denom == 0 ? 0 : (n * sumXY - sumX * sumY) / denom;
    }
}
