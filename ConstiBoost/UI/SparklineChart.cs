using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace ConstiBoost.UI;

/// <summary>Lightweight line chart (no external chart library) for live CPU/GPU/RAM history.</summary>
public sealed class SparklineChart : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<double>), typeof(SparklineChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(SparklineChart),
        new FrameworkPropertyMetadata(Brushes.DeepSkyBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(SparklineChart),
        new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CapacityProperty = DependencyProperty.Register(
        nameof(Capacity), typeof(int), typeof(SparklineChart),
        new FrameworkPropertyMetadata(60, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<double>? Values
    {
        get => (IReadOnlyList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public int Capacity
    {
        get => (int)GetValue(CapacityProperty);
        set => SetValue(CapacityProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 0 || h <= 0)
        {
            return;
        }

        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)), 1);
        gridPen.Freeze();
        for (int i = 1; i < 4; i++)
        {
            double y = Math.Round(h * i / 4) + 0.5;
            dc.DrawLine(gridPen, new Point(0, y), new Point(w, y));
        }

        var values = Values;
        if (values is null || values.Count < 2)
        {
            return;
        }

        int capacity = Math.Max(Capacity, values.Count);
        double step = w / (capacity - 1);
        double max = Maximum <= 0 ? Math.Max(1, values.Max()) : Maximum;
        double startX = w - (values.Count - 1) * step;

        var points = values.Select((v, i) => new Point(startX + i * step, h - Math.Clamp(v / max, 0, 1) * (h - 2) - 1)).ToList();

        var line = new StreamGeometry();
        using (var ctx = line.Open())
        {
            ctx.BeginFigure(points[0], false, false);
            ctx.PolyLineTo(points.Skip(1).ToList(), true, true);
        }

        line.Freeze();

        var area = new StreamGeometry();
        using (var ctx = area.Open())
        {
            ctx.BeginFigure(new Point(points[0].X, h), true, true);
            ctx.PolyLineTo(points.Append(new Point(points[^1].X, h)).ToList(), false, true);
        }

        area.Freeze();

        Color color = Stroke is SolidColorBrush solid ? solid.Color : Colors.DeepSkyBlue;
        var fill = new LinearGradientBrush(Color.FromArgb(0x55, color.R, color.G, color.B), Color.FromArgb(0x00, color.R, color.G, color.B), 90);
        fill.Freeze();
        dc.DrawGeometry(fill, null, area);

        var pen = new Pen(Stroke, 2) { LineJoin = PenLineJoin.Round };
        pen.Freeze();
        dc.DrawGeometry(null, pen, line);
    }
}
