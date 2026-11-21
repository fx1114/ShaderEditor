using ShaderEditor.ViewModels;
using ShaderEditor.Windows;
using System.Windows;

namespace ShaderEditor.Services;

public sealed class WindowService : IWindowService
{
    private MainWindow? _mainWindow;

    public void ShowMainWindow(MainWindowViewModel viewModel)
    {
        _mainWindow = new MainWindow(viewModel);
        _mainWindow.Show();
    }

    public void ShowFullscreen(MainWindowViewModel viewModel)
    {
        var fullscreenWindow = new FullScreen(viewModel)
        {
            Owner = _mainWindow,
            WindowState = WindowState.Maximized
        };
        fullscreenWindow.Show();
    }

    public void CloseApplication() => _mainWindow?.Close();
}
