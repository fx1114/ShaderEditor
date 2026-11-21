using ShaderEditor.ViewModels;
using System.Windows;

namespace ShaderEditor.Windows;

public partial class FullScreen : Window
{
    public FullScreen(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
