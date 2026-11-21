using ShaderEditor.Isf;
using ShaderEditor.Mvvm;
using ShaderEditor.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;

namespace ShaderEditor.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private const string VertexShaderSource = @"
        #version 330 core
        layout (location = 0) in vec2 aPos;
        void main() {
            gl_Position = vec4(aPos.x, aPos.y, 0.0, 1.0);
        }";

    private const string SampleIsfShader = @"/*{
    ""DESCRIPTION"": ""Interaktiver ISF Test-Shader"",
    ""CREDIT"": ""PsyFX"",
    ""INPUTS"": [
        { ""NAME"": ""speed"", ""TYPE"": ""float"", ""LABEL"": ""Geschwindigkeit"", ""DEFAULT"": 1.5, ""MIN"": 0.0, ""MAX"": 5.0 },
        { ""NAME"": ""zoom"", ""TYPE"": ""float"", ""LABEL"": ""Zoom-Faktor"", ""DEFAULT"": 4.0, ""MIN"": 1.0, ""MAX"": 20.0 },
        { ""NAME"": ""center"", ""TYPE"": ""point2D"", ""LABEL"": ""Zentrum"", ""DEFAULT"": [0.5, 0.5], ""MIN"": [0.0, 0.0], ""MAX"": [1.0, 1.0] },
        { ""NAME"": ""tintColor"", ""TYPE"": ""color"", ""LABEL"": ""Farbton"", ""DEFAULT"": [0.2, 0.8, 1.0, 1.0] },
        { ""NAME"": ""invert"", ""TYPE"": ""bool"", ""LABEL"": ""Farben invertieren"", ""DEFAULT"": false }
    ]
}*/

void main() {
    vec2 uv = isf_FragNormCoord - center;
    uv.x *= RENDERSIZE.x / RENDERSIZE.y;
    float d = length(uv) * zoom;
    float pattern = sin(d - TIME * speed) * 0.5 + 0.5;
    vec3 col = pattern * tintColor.rgb;
    if (invert) {
        col = 1.0 - col;
    }

    gl_FragColor = vec4(col, tintColor.a);
}";

    private readonly IFileService _fileService;
    private readonly IMessageService _messageService;
    private readonly IWindowService _windowService;
    private readonly IShaderRenderer _renderer;
    private readonly DispatcherTimer _debounceTimer;
    private readonly Stopwatch _stopwatch = new();
    private string? _currentFilePath;
    private string _editorText = string.Empty;
    private string _errorLog = string.Empty;
    private string _title = "Unbenannt.fs - ISF Shader Studio";
    private bool _isDirty;
    private int _frameIndex;

    public MainWindowViewModel(
        IFileService fileService,
        IMessageService messageService,
        IWindowService windowService,
        IShaderRenderer renderer)
    {
        _fileService = fileService;
        _messageService = messageService;
        _windowService = windowService;
        _renderer = renderer;
        Parameters = new ObservableCollection<IsfParameterViewModel>();

        NewCommand = new RelayCommand(_ => NewShader());
        OpenCommand = new RelayCommand(_ => OpenShader());
        SaveCommand = new RelayCommand(_ => SaveShader());
        SaveAsCommand = new RelayCommand(_ => SaveShaderAs());
        ExitCommand = new RelayCommand(_ => _windowService.CloseApplication());
        FullscreenCommand = new RelayCommand(_ => _windowService.ShowFullscreen(this));

        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _debounceTimer.Tick += DebounceTimer_Tick;

        SetInitialText(SampleIsfShader);
        _stopwatch.Start();
    }

    public ObservableCollection<IsfParameterViewModel> Parameters { get; }
    public IShaderRenderer Renderer => _renderer;
    public ICommand NewCommand { get; }
    public ICommand OpenCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAsCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand FullscreenCommand { get; }
    public TimeSpan Elapsed => _stopwatch.Elapsed;

    public string EditorText
    {
        get => _editorText;
        set
        {
            if (!SetProperty(ref _editorText, value))
            {
                return;
            }

            _isDirty = true;
            UpdateWindowTitle();
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }
    }

    public string ErrorLog
    {
        get => _errorLog;
        private set => SetProperty(ref _errorLog, value);
    }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set => SetProperty(ref _isDirty, value);
    }

    public void InitializeRenderer()
    {
        _renderer.Initialize();
        ReloadShader(EditorText);
    }

    public void Render(TimeSpan delta, double width, double height)
        => _renderer.Render(delta, _stopwatch.Elapsed, _frameIndex++, width, height, Parameters);

    public void Dispose()
    {
        _debounceTimer.Stop();
        _debounceTimer.Tick -= DebounceTimer_Tick;
    }

    public bool CanClose() => ConfirmDiscardChanges();

    private void DebounceTimer_Tick(object? sender, EventArgs e)
    {
        _debounceTimer.Stop();
        ReloadShader(EditorText);
    }

    private void SetInitialText(string text)
    {
        _editorText = text;
        OnPropertyChanged(nameof(EditorText));
        IsDirty = false;
        UpdateWindowTitle();
    }

    private void ReloadShader(string rawSource)
    {
        var parseResult = IsfParser.ParseAndTranspile(rawSource);
        if (parseResult.Error != null)
        {
            ErrorLog = parseResult.Error;
            return;
        }

        foreach (var newParameter in parseResult.Parameters)
        {
            var existing = Parameters.FirstOrDefault(parameter =>
                parameter.Name == newParameter.Name && parameter.GetType() == newParameter.GetType());

            switch (existing, newParameter)
            {
                case (FloatParameterVm oldFloat, FloatParameterVm newFloat):
                    newFloat.Value = Math.Clamp(oldFloat.Value, newFloat.Min, newFloat.Max);
                    break;
                case (BoolParameterVm oldBool, BoolParameterVm newBool):
                    newBool.Value = oldBool.Value;
                    break;
                case (Point2DParameterVm oldPoint, Point2DParameterVm newPoint):
                    newPoint.X = Math.Clamp(oldPoint.X, newPoint.MinX, newPoint.MaxX);
                    newPoint.Y = Math.Clamp(oldPoint.Y, newPoint.MinY, newPoint.MaxY);
                    break;
                case (ColorParameterVm oldColor, ColorParameterVm newColor):
                    newColor.R = oldColor.R;
                    newColor.G = oldColor.G;
                    newColor.B = oldColor.B;
                    newColor.A = oldColor.A;
                    break;
            }
        }

        Parameters.Clear();
        foreach (var parameter in parseResult.Parameters)
        {
            Parameters.Add(parameter);
        }

        try
        {
            _renderer.Compile(VertexShaderSource, parseResult.ProcessedGlslCode);
            ErrorLog = "ISF-Shader erfolgreich kompiliert.";
        }
        catch (Exception ex)
        {
            ErrorLog = $"Fehler beim Kompilieren des Shaders:\n{ex.Message}";
        }
    }

    private void NewShader()
    {
        if (!ConfirmDiscardChanges()) return;
        _currentFilePath = null;
        SetInitialText(SampleIsfShader);
        ReloadShader(EditorText);
    }

    private void OpenShader()
    {
        if (!ConfirmDiscardChanges()) return;
        var path = _fileService.OpenShader();
        if (path == null) return;

        try
        {
            _currentFilePath = path;
            SetInitialText(_fileService.ReadAllText(path));
            ReloadShader(EditorText);
        }
        catch (Exception ex)
        {
            _messageService.ShowError($"Fehler beim Laden der Datei:\n{ex.Message}", "Fehler");
        }
    }

    private bool SaveShader()
    {
        if (string.IsNullOrWhiteSpace(_currentFilePath))
        {
            return SaveShaderAs();
        }

        return SaveToPath(_currentFilePath);
    }

    private bool SaveShaderAs()
    {
        var path = _fileService.SaveShader(_currentFilePath);
        return path != null && SaveToPath(path);
    }

    private bool SaveToPath(string path)
    {
        try
        {
            _fileService.WriteAllText(path, EditorText);
            _currentFilePath = path;
            IsDirty = false;
            UpdateWindowTitle();
            ErrorLog = $"Gespeichert: {path}";
            return true;
        }
        catch (Exception ex)
        {
            _messageService.ShowError($"Fehler beim Speichern:\n{ex.Message}", "Fehler");
            return false;
        }
    }

    private bool ConfirmDiscardChanges()
    {
        if (!IsDirty) return true;

        return _messageService.ConfirmDiscardChanges() switch
        {
            ConfirmResult.Save => SaveShader(),
            ConfirmResult.Discard => true,
            _ => false
        };
    }

    private void UpdateWindowTitle()
    {
        var fileName = string.IsNullOrWhiteSpace(_currentFilePath)
            ? "Unbenannt.fs"
            : System.IO.Path.GetFileName(_currentFilePath);
        Title = $"{fileName}{(IsDirty ? "*" : string.Empty)} - ISF Shader Studio";
    }
}
