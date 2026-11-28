using ShaderEditor.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace ShaderEditor.Windows;

public partial class PaletteWindow : Window
{
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
    }
}
