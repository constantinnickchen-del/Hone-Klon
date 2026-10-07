using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ConstiBoost.UI;

/// <summary>Animated circular score gauge (0–100).</summary>
public sealed class ScoreRing : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(ScoreRing), new PropertyMetadata(0.0, OnValueChanged));

    public static readonly DependencyProperty DisplayValueProperty = DependencyProperty.Register(
        nameof(DisplayValue), typeof(double), typeof(ScoreRing),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RingBrushProperty = DependencyProperty.Register(
        nameof(RingBrush), typeof(Brush), typeof(ScoreRing),
        new FrameworkPropertyMetadata(Brushes.DeepSkyBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush), typeof(Brush), typeof(ScoreRing),
        new FrameworkPropertyMetadata(CreateTrackBrush(), FrameworkPropertyMetadataOptions.AffectsRender));

    // DependencyProperty defaults must be frozen Freezables.
    private static Brush CreateTrackBrush()
    {
        var brush = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF));
        brush.Freeze();
        return brush;
    }

    public static readonly DependencyProperty ThicknessProperty = DependencyProperty.Register(
        nameof(Thickness), typeof(double), typeof(ScoreRing),
        new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double DisplayValue { get => (double)GetValue(DisplayValueProperty); set => SetValue(DisplayValueProperty, value); }
    public Brush RingBrush { get => (Brush)GetValue(RingBrushProperty); set => SetValue(RingBrushProperty, value); }
    public Brush TrackBrush { get => (Brush)GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    public double Thickness { get => (double)GetValue(ThicknessProperty); set => SetValue(ThicknessProperty, value); }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ring = (ScoreRing)d;
        var animation = new DoubleAnimation((double)e.NewValue, TimeSpan.FromMilliseconds(900))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        ring.BeginAnimation(DisplayValueProperty, animation);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0)
        {
            return;
        }

        double radius = (size - Thickness) / 2;
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var trackPen = new Pen(TrackBrush, Thickness);
        dc.DrawEllipse(null, trackPen, center, radius, radius);

        double value = Math.Clamp(DisplayValue, 0, 100);
        if (value <= 0.1)
        {
            return;
        }

        double angle = value / 100 * 359.99;
        double rad = (angle - 90) * Math.PI / 180;
        var start = new Point(center.X, center.Y - radius);
        var end = new Point(center.X + radius * Math.Cos(rad), center.Y + radius * Math.Sin(rad));
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(start, false, false);
            ctx.ArcTo(end, new Size(radius, radius), 0, angle > 180, SweepDirection.Clockwise, true, false);
        }

        geometry.Freeze();
        var pen = new Pen(RingBrush, Thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawGeometry(null, pen, geometry);
    }
}
