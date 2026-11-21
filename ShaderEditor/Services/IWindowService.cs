using ShaderEditor.ViewModels;

namespace ShaderEditor.Services;

public interface IWindowService
{
    void ShowMainWindow(MainWindowViewModel viewModel);
    void ShowFullscreen(MainWindowViewModel viewModel);
    void CloseApplication();
}
