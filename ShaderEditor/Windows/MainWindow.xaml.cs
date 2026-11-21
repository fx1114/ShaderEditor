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

        OpenGlControl.Start(new GLWpfControlSettings
        {
            MajorVersion = 3,
            MinorVersion = 3
        });

        viewModel.InitializeRenderer();
        OpenGlControl.Render += delta =>
            viewModel.Render(delta, OpenGlControl.ActualWidth, OpenGlControl.ActualHeight);
    }
}