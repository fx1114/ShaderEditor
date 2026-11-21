using OpenTK.Wpf;
using ShaderEditor.ViewModels;
using System.Windows;

namespace ShaderEditor.Windows;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        //OpenGlControl.Start(new GLWpfControlSettings { MajorVersion = 3, MinorVersion = 3 });
    }
}