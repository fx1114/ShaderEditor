using System.Windows;
using ShaderEditor.Services;
using ShaderEditor.ViewModels;
using ShaderEditor.Windows;

namespace ShaderEditor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var windowService = new WindowService();
            var viewModel = new MainWindowViewModel(
                new FileService(),
                new MessageService(),
                windowService,
                new OpenGlView());

            windowService.ShowMainWindow(viewModel);
        }
    }

}
