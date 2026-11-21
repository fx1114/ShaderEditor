using OpenTK.Wpf;
using ShaderEditor.ViewModels;
using System.Windows;

namespace ShaderEditor.Windows;

public partial class FullScreen : Window
{
    public FullScreen(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Jeder GLWpfControl benötigt eigene OpenGL-Ressourcen und daher
        // einen eigenen Renderer/GL-Kontext.
        var renderer = new OpenGlView();

        OpenGlControl.Start(new GLWpfControlSettings
        {
            MajorVersion = 3,
            MinorVersion = 3
        });

        viewModel.InitializeRenderer(renderer);
        OpenGlControl.Render += delta =>
            viewModel.Render(renderer, delta, OpenGlControl.ActualWidth, OpenGlControl.ActualHeight);
    }
}
