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
        var screens = System.Windows.Forms.Screen.AllScreens;
        if (screens.Length < 2) return; // Kein 2. Bildschirm vorhanden

        // Den letzten sekundären Bildschirm auswählen
        var secondScreen = screens.LastOrDefault(s => !s.Primary) ?? screens[1];

        var fullscreenWindow = new FullScreen(viewModel)
        {
            Owner = _mainWindow,
            WindowState = WindowState.Normal,
            Left = secondScreen.Bounds.Left,
            Top = secondScreen.Bounds.Top,
            Width = secondScreen.Bounds.Width,
            Height = secondScreen.Bounds.Height,
            Topmost = true // Optional, verdeckt die Taskleiste sicher
        };

        fullscreenWindow.Show();
        fullscreenWindow.WindowState = WindowState.Maximized;
    }

    public void CloseApplication() => _mainWindow?.Close();
}
