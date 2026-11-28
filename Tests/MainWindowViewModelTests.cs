using Microsoft.VisualStudio.TestTools.UnitTesting;
using ShaderEditor.Isf;
using ShaderEditor.Services;
using ShaderEditor.ViewModels;
using System.Windows.Input;

namespace Tests;

[TestClass]
public sealed class MainWindowViewModelTests
{
    [TestMethod]
    public void Constructor_InitializesViewModelState()
    {
        var fileService = new FakeFileService();
        var messageService = new FakeMessageService();
        var windowService = new FakeWindowService();
        var renderer = new FakeShaderRenderer();

        using var viewModel = new MainWindowViewModel(
            fileService,
            messageService,
            windowService,
            renderer);

        Assert.AreSame(renderer, viewModel.Renderer);
        Assert.IsNotNull(viewModel.Parameters);
        Assert.IsNotNull(viewModel.ShaderFiles);
        Assert.IsNotNull(viewModel.FavoriteFolders);
        Assert.IsNotNull(viewModel.GpuRenderTimes);

        Assert.AreEqual(0, viewModel.Parameters.Count);
        Assert.AreEqual(0, viewModel.GpuRenderTimes.Count);
        Assert.IsFalse(viewModel.IsDirty);
        Assert.IsFalse(viewModel.CreateVertexShader);
        Assert.AreEqual("Unbenannt.fs - ISF Shader Studio", viewModel.Title);
        Assert.IsTrue(viewModel.EditorText.Contains("Interaktiver ISF Test-Shader"));
        Assert.IsTrue(viewModel.VertexEditorText.Contains("isf_vertShaderInit"));
    }

    [TestMethod]
    public void Constructor_InitializesAllCommands()
    {
        using var viewModel = CreateViewModel();

        var commands = new ICommand[]
        {
            viewModel.NewCommand,
            viewModel.OpenCommand,
            viewModel.DeleteShaderCommand,
            viewModel.CopyShaderCommand,
            viewModel.PasteShaderCommand,
            viewModel.SetDefaultCommand,
            viewModel.ResetParameterCommand,
            viewModel.RemoveParameterCommand,
            viewModel.NewParameterCommand,
            viewModel.SelectFolderCommand,
            viewModel.AddFavoriteFolderCommand,
            viewModel.RemoveFavoriteFolderCommand,
            viewModel.SaveCommand,
            viewModel.SaveAsCommand,
            viewModel.ExitCommand,
            viewModel.FullscreenCommand
        };

        Assert.IsTrue(commands.All(command => command is not null));
        Assert.IsTrue(commands.All(command => command.CanExecute(null)));
    }

    [TestMethod]
    public void ExitCommand_ClosesApplication()
    {
        var windowService = new FakeWindowService();

        using var viewModel = CreateViewModel(windowService: windowService);

        viewModel.ExitCommand.Execute(null);

        Assert.AreEqual(1, windowService.CloseApplicationCallCount);
    }

    [TestMethod]
    public void FullscreenCommand_ShowsCurrentViewModel()
    {
        var windowService = new FakeWindowService();

        using var viewModel = CreateViewModel(windowService: windowService);

        viewModel.FullscreenCommand.Execute(null);

        Assert.AreSame(viewModel, windowService.LastFullscreenViewModel);
    }

    private static MainWindowViewModel CreateViewModel(
        IWindowService? windowService = null)
    {
        return new MainWindowViewModel(
            new FakeFileService(),
            new FakeMessageService(),
            windowService ?? new FakeWindowService(),
            new FakeShaderRenderer());
    }

    private sealed class FakeFileService : IFileService
    {
        public string? SelectFolder() => null;

        public string? OpenShader() => null;

        public string? SaveShader(string? currentPath) => null;

        public string ReadAllText(string path) => string.Empty;

        public void WriteAllText(string path, string content)
        {
        }

        public void DeleteFile(string path)
        {
        }
    }

    private sealed class FakeMessageService : IMessageService
    {
        public ConfirmResult ConfirmDiscardChanges() => ConfirmResult.Discard;

        public bool ConfirmDelete(string message) => true;

        public bool ConfirmOverwrite(string message) => true;

        public void ShowError(string message, string title)
        {
        }
    }

    private sealed class FakeWindowService : IWindowService
    {
        public int CloseApplicationCallCount { get; private set; }

        public MainWindowViewModel? LastFullscreenViewModel { get; private set; }

        public void ShowMainWindow(MainWindowViewModel viewModel)
        {
        }

        public void ShowFullscreen(MainWindowViewModel viewModel)
        {
            LastFullscreenViewModel = viewModel;
        }

        public void CloseApplication()
        {
            CloseApplicationCallCount++;
        }
    }

    private sealed class FakeShaderRenderer : IShaderRenderer
    {
        public double LastGpuTimeMicroseconds => 0;

        public void Initialize()
        {
        }

        public void Compile(
            string vertexShaderSource,
            string fragmentShaderSource,
            IReadOnlyList<IsfPassDefinition> passes)
        {
        }

        public void Render(
            TimeSpan delta,
            TimeSpan elapsed,
            int frameIndex,
            double width,
            double height,
            IEnumerable<IsfParameterViewModel> parameters)
        {
        }
    }
}