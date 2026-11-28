using ShaderEditor.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

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
    }

    private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isReady)
        {
            return;
        }

        UpdatePalettePreview();
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

    private static byte ToByte(double value)
    {
        return (byte)Math.Round(Math.Clamp(value, 0, 1) * byte.MaxValue);
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
