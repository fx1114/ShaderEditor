using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ShaderEditor.Windows;

public sealed class GpuTimeGraph : FrameworkElement
{
    public static readonly DependencyProperty SamplesProperty =
        DependencyProperty.Register(
            nameof(Samples),
            typeof(ObservableCollection<double>),
            typeof(GpuTimeGraph),
            new FrameworkPropertyMetadata(null, OnSamplesChanged));

    public ObservableCollection<double>? Samples
    {
        get => (ObservableCollection<double>?)GetValue(SamplesProperty);
        set => SetValue(SamplesProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var background = new SolidColorBrush(Color.FromRgb(37, 37, 38));
        var gridBrush = new SolidColorBrush(Color.FromRgb(63, 63, 70));
        var lineBrush = new SolidColorBrush(Color.FromRgb(156, 220, 254));
        drawingContext.DrawRectangle(background, null, new Rect(0, 0, ActualWidth, ActualHeight));

        if (ActualWidth <= 0 || ActualHeight <= 0 || Samples is not { Count: > 0 })
        {
            return;
        }

        var maxValue = Math.Max(1.0, Samples.Max());
        var plotTop = 8.0;
        var plotBottom = Math.Max(plotTop, ActualHeight - 8.0);
        var plotHeight = plotBottom - plotTop;

        drawingContext.DrawLine(new Pen(gridBrush, 1), new Point(0, plotTop), new Point(ActualWidth, plotTop));
        drawingContext.DrawLine(new Pen(gridBrush, 1), new Point(0, plotBottom), new Point(ActualWidth, plotBottom));

        var points = new PointCollection();
        for (var index = 0; index < Samples.Count; index++)
        {
            var x = Samples.Count == 1
                ? 0
                : index * ActualWidth / (Samples.Count - 1);
            var y = plotBottom - Samples[index] / maxValue * plotHeight;
            points.Add(new Point(x, y));
        }

        if (points.Count > 1)
        {
            drawingContext.DrawGeometry(null, new Pen(lineBrush, 1.5), new StreamGeometryBuilder(points).Build());
        }

        var latest = Samples[^1];
        var label = new FormattedText(
            $"GPU: {latest:F2} µs",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            12,
            lineBrush,
            1.0);
        drawingContext.DrawText(label, new Point(6, this.ActualHeight - 30));
    }

    protected override Size MeasureOverride(Size availableSize)
        => new(availableSize.Width, Math.Max(40, double.IsInfinity(availableSize.Height) ? 60 : availableSize.Height));

    private static void OnSamplesChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        var graph = (GpuTimeGraph)dependencyObject;
        if (e.OldValue is ObservableCollection<double> oldSamples)
        {
            oldSamples.CollectionChanged -= graph.Samples_CollectionChanged;
        }

        if (e.NewValue is ObservableCollection<double> newSamples)
        {
            newSamples.CollectionChanged += graph.Samples_CollectionChanged;
        }

        graph.InvalidateVisual();
    }

    private void Samples_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => InvalidateVisual();

    private sealed class StreamGeometryBuilder
    {
        private readonly PointCollection _points;

        public StreamGeometryBuilder(PointCollection points)
        {
            _points = points;
        }

        public StreamGeometry Build()
        {
            var geometry = new StreamGeometry();
            using var context = geometry.Open();
            context.BeginFigure(_points[0], false, false);
            context.PolyLineTo(_points.Cast<Point>().Skip(1).ToList(), true, true);
            geometry.Freeze();
            return geometry;
        }
    }
}
