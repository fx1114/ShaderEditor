using ShaderEditor.ViewModels;
using System.Windows;

namespace ShaderEditor.Windows;

public partial class PaletteWindow : Window
{
    public PaletteParameters Parameters { get; }

    public PaletteWindow(PaletteParameters parameters)
    {
        InitializeComponent();
        Parameters = parameters.Clone();
        DataContext = Parameters;
    }
}
