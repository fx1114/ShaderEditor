using ShaderEditor.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ShaderEditor.Windows;

public partial class PaletteWindow : Window
{
    private bool _isReady;

    public PaletteParameters Parameters { get; }

    public PaletteWindow()
        : this(new PaletteParameters())
    {
    }

    public PaletteWindow(PaletteParameters parameters)
    {
        InitializeComponent();
        Parameters = DesignerProperties.GetIsInDesignMode(this)
            ? new PaletteParameters()
            : parameters.Clone();
        DataContext = Parameters;
        UpdatePalettePreview();
        _isReady = true;
        UpdatePaletteGraph();
    }

    private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isReady)
        {
            return;
        }

        UpdatePalettePreview();
        UpdatePaletteGraph();
    }

    private void PaletteGraph_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isReady)
        {
            UpdatePaletteGraph();
        }
    }

    private void UpdatePalettePreview()
    {
        const int stopCount = 101;
        const double twoPi = 6.28318;
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0)
        };

        for (var index = 0; index < stopCount; index++)
        {
            var t = (double)index / (stopCount - 1);
            var red = Parameters.AR + Parameters.BR * Math.Cos(twoPi * (Parameters.CR * t + Parameters.DR));
            var green = Parameters.AG + Parameters.BG * Math.Cos(twoPi * (Parameters.CG * t + Parameters.DG));
            var blue = Parameters.AB + Parameters.BB * Math.Cos(twoPi * (Parameters.CB * t + Parameters.DB));

            brush.GradientStops.Add(new GradientStop(
                Color.FromRgb(ToByte(red), ToByte(green), ToByte(blue)),
                t));
        }

        PalettePreview.Background = brush;
    }

    private void UpdatePaletteGraph()
    {
        const double left = 32;
        const double right = 8;
        const double top = 8;
        const double bottom = 22;

        var width = PaletteGraph.ActualWidth - left - right;
        var height = PaletteGraph.ActualHeight - top - bottom;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        PaletteGraph.Children.Clear();

        for (var value = 0.0; value <= 1.0; value += 0.5)
        {
            var y = top + (1 - value) * height;
            AddLine(new Point(left, y), new Point(left + width, y), Color.FromRgb(63, 63, 70), 1);
            AddLabel(value.ToString("0.0"), 2, y - 8);
        }

        for (var value = 0.0; value <= 1.0; value += 0.5)
        {
            var x = left + value * width;
            AddLine(new Point(x, top), new Point(x, top + height), Color.FromRgb(63, 63, 70), 1);
            AddLabel(value.ToString("0.0"), x - 8, top + height + 4);
        }

        AddLine(new Point(left, top), new Point(left, top + height), Colors.LightGray, 1);
        AddLine(new Point(left, top + height), new Point(left + width, top + height), Colors.LightGray, 1);

        AddChannelLine(Parameters.AR, Parameters.BR, Parameters.CR, Parameters.DR, Colors.Red);
        AddChannelLine(Parameters.AG, Parameters.BG, Parameters.CG, Parameters.DG, Colors.LimeGreen);
        AddChannelLine(Parameters.AB, Parameters.BB, Parameters.CB, Parameters.DB, Colors.DeepSkyBlue);

        AddLabel("R", left + width - 46, top + 2, Colors.Red);
        AddLabel("G", left + width - 30, top + 2, Colors.LimeGreen);
        AddLabel("B", left + width - 14, top + 2, Colors.DeepSkyBlue);
    }

    private void AddChannelLine(double offset, double amplitude, double frequency, double phase, Color color)
    {
        const int pointCount = 101;
        const double left = 32;
        const double right = 8;
        const double top = 8;
        const double bottom = 22;
        var width = PaletteGraph.ActualWidth - left - right;
        var height = PaletteGraph.ActualHeight - top - bottom;
        var points = new PointCollection(pointCount);

        for (var index = 0; index < pointCount; index++)
        {
            var t = (double)index / (pointCount - 1);
            var value = offset + amplitude * Math.Cos(6.28318 * (frequency * t + phase));
            var x = left + t * width;
            var y = top + (1 - Math.Clamp(value, 0, 1)) * height;
            points.Add(new Point(x, y));
        }

        PaletteGraph.Children.Add(new Polyline
        {
            Points = points,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 1.5,
            SnapsToDevicePixels = true
        });
    }

    private void AddLine(Point start, Point end, Color color, double thickness)
    {
        PaletteGraph.Children.Add(new Line
        {
            X1 = start.X,
            Y1 = start.Y,
            X2 = end.X,
            Y2 = end.Y,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = thickness,
            SnapsToDevicePixels = true
        });
    }

    private void AddLabel(string text, double left, double top, Color? color = null)
    {
        var label = new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(color ?? Colors.LightGray),
            FontSize = 10
        };
        Canvas.SetLeft(label, left);
        Canvas.SetTop(label, top);
        PaletteGraph.Children.Add(label);
    }

    private static byte ToByte(double value)
    {
        return (byte)Math.Round(Math.Clamp(value, 0, 1) * byte.MaxValue);
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
