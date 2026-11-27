using ShaderEditor.Isf;
using ShaderEditor.Mvvm;
using ShaderEditor.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ShaderEditor.Windows;

namespace ShaderEditor.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private const int GpuGraphSampleCount = 100;
    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ShaderEditor",
        "settings.json");

    private const string DefaultVertexShaderSource = @"
void main() {
    isf_vertShaderInit();
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
    private string? _shaderFolderPath;
    private FavoriteFolderItem? _selectedFavoriteFolder;
    private string? _copiedShaderPath;
    private string? _copiedVertexShaderPath;
    private ShaderFileItem? _selectedShader;
    private string _editorText = string.Empty;
    private string _vertexEditorText = string.Empty;
    private string _fragmentHoverToken = string.Empty;
    private string _vertexHoverToken = string.Empty;
    private string _hoverExpression = "2*sin(TIME)";
    private double _gpuRenderTimeMicroseconds;
    private TimeSpan _lastGpuTimeUiUpdate;
    private string _errorLog = string.Empty;
    private string _title = "Unbenannt.fs - ISF Shader Studio";
    private string? _processedShaderSource;
    private string _vertexShaderHeader = string.Empty;
    private IReadOnlyList<IsfPassDefinition> _passes = Array.Empty<IsfPassDefinition>();
    private int _shaderRevision;
    private readonly Dictionary<IShaderRenderer, int> _rendererRevisions = new();
    private readonly List<IsfParameterViewModel> _hiddenParameters = new();
    private bool _isDirty;
    private bool _createVertexShader;
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
        ShaderFiles = new ObservableCollection<ShaderFileItem>();
        FavoriteFolders = new ObservableCollection<FavoriteFolderItem>();
        GpuRenderTimes = new ObservableCollection<double>();

        NewCommand = new RelayCommand(_ => NewShader());
        OpenCommand = new RelayCommand(_ => OpenShader());
        DeleteShaderCommand = new RelayCommand(parameter => DeleteShader(parameter as ShaderFileItem));
        CopyShaderCommand = new RelayCommand(parameter => CopyShader(parameter as ShaderFileItem));
        PasteShaderCommand = new RelayCommand(parameter => PasteShader(parameter as FavoriteFolderItem));
        SetDefaultCommand = new RelayCommand(parameter => SetParameterDefault(parameter as IsfParameterViewModel));
        RemoveParameterCommand = new RelayCommand(parameter => RemoveParameter(parameter as IsfParameterViewModel));
        NewParameterCommand = new RelayCommand(_ => AddParameter());
        SelectFolderCommand = new RelayCommand(_ => SelectShaderFolder());
        AddFavoriteFolderCommand = new RelayCommand(_ => AddFavoriteFolder());
        RemoveFavoriteFolderCommand = new RelayCommand(parameter => RemoveFavoriteFolder(parameter as FavoriteFolderItem));
        SaveCommand = new RelayCommand(_ => SaveShader());
        SaveAsCommand = new RelayCommand(_ => SaveShaderAs());
        ExitCommand = new RelayCommand(_ => _windowService.CloseApplication());
        FullscreenCommand = new RelayCommand(_ => _windowService.ShowFullscreen(this));

        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _debounceTimer.Tick += DebounceTimer_Tick;

        SetInitialText(SampleIsfShader, DefaultVertexShaderSource);
        LoadSettings();
        _stopwatch.Start();
    }

    public string VertexEditorText
    {
        get => _vertexEditorText;
        set
        {
            if (!SetProperty(ref _vertexEditorText, value))
            {
                return;
            }

            IsDirty = true;
            UpdateWindowTitle();
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }
    }

    public string HoverExpression
    {
        get => _hoverExpression;
        set
        {
            if (!SetProperty(ref _hoverExpression, value))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(FragmentHoverToken))
            {
                ReloadShader(EditorText);
            }
            else if (!string.IsNullOrWhiteSpace(VertexHoverToken))
            {
                CompileRenderer(_renderer);
            }
        }
    }

    public double GpuRenderTimeMicroseconds
    {
        get => _gpuRenderTimeMicroseconds;
        private set => SetProperty(ref _gpuRenderTimeMicroseconds, value);
    }

    public bool CreateVertexShader
    {
        get => _createVertexShader;
        set => SetProperty(ref _createVertexShader, value);
    }

    public string FragmentHoverToken
    {
        get => _fragmentHoverToken;
        set
        {
            if (!SetProperty(ref _fragmentHoverToken, value))
            {
                return;
            }

            ReloadShader(EditorText);
        }
    }

    public string VertexHoverToken
    {
        get => _vertexHoverToken;
        set
        {
            if (!SetProperty(ref _vertexHoverToken, value))
            {
                return;
            }

            CompileRenderer(_renderer);
        }
    }

    public ObservableCollection<IsfParameterViewModel> Parameters { get; }
    public ObservableCollection<ShaderFileItem> ShaderFiles { get; }
    public ObservableCollection<FavoriteFolderItem> FavoriteFolders { get; }
    public ObservableCollection<double> GpuRenderTimes { get; }
    public IShaderRenderer Renderer => _renderer;
    public ICommand NewCommand { get; }
    public ICommand OpenCommand { get; }
    public ICommand DeleteShaderCommand { get; }
    public ICommand CopyShaderCommand { get; }
    public ICommand PasteShaderCommand { get; }
    public ICommand SetDefaultCommand { get; }
    public ICommand RemoveParameterCommand { get; }
    public ICommand NewParameterCommand { get; }
    public ICommand SelectFolderCommand { get; }
    public ICommand AddFavoriteFolderCommand { get; }
    public ICommand RemoveFavoriteFolderCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAsCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand FullscreenCommand { get; }
    public TimeSpan Elapsed => _stopwatch.Elapsed;

    public string? ShaderFolderPath
    {
        get => _shaderFolderPath;
        private set
        {
            if (!SetProperty(ref _shaderFolderPath, value))
            {
                return;
            }

            RefreshShaderFiles();
        }
    }

    private void CopyShader(ShaderFileItem? shader)
    {
        if (shader == null || !File.Exists(shader.FilePath))
        {
            return;
        }

        _copiedShaderPath = shader.FilePath;
        var vertexPath = Path.ChangeExtension(shader.FilePath, ".vs");
        _copiedVertexShaderPath = File.Exists(vertexPath) ? vertexPath : null;
        ErrorLog = _copiedVertexShaderPath == null
            ? $"Shader zum Einfügen vorgemerkt: {shader.FileName}"
            : $"Shader einschließlich Vertex-Shader zum Einfügen vorgemerkt: {shader.FileName}";
    }

    private void PasteShader(FavoriteFolderItem? favorite)
    {
        if (favorite == null || string.IsNullOrWhiteSpace(_copiedShaderPath) ||
            !File.Exists(_copiedShaderPath) || !Directory.Exists(favorite.FolderPath))
        {
            return;
        }

        var targetShaderPath = Path.Combine(favorite.FolderPath, Path.GetFileName(_copiedShaderPath));
        var targetVertexPath = _copiedVertexShaderPath == null
            ? null
            : Path.Combine(favorite.FolderPath, Path.GetFileName(_copiedVertexShaderPath));
        var overwriteRequired = File.Exists(targetShaderPath) ||
            (targetVertexPath != null && File.Exists(targetVertexPath));

        if (overwriteRequired && !_messageService.ConfirmOverwrite(
                $"Der Shader '{Path.GetFileName(targetShaderPath)}' ist im Favorite-Ordner bereits vorhanden. Soll er einschließlich der Vertex-Datei überschrieben werden?"))
        {
            return;
        }

        try
        {
            File.Copy(_copiedShaderPath, targetShaderPath, true);
            if (_copiedVertexShaderPath != null && targetVertexPath != null)
            {
                File.Copy(_copiedVertexShaderPath, targetVertexPath, true);
            }

            if (string.Equals(ShaderFolderPath, favorite.FolderPath, StringComparison.OrdinalIgnoreCase))
            {
                RefreshShaderFiles();
            }

            ErrorLog = $"Shader eingefügt: {targetShaderPath}";
        }
        catch (Exception ex)
        {
            _messageService.ShowError($"Fehler beim Einfügen des Shaders:\n{ex.Message}", "Fehler");
        }
    }

    public FavoriteFolderItem? SelectedFavoriteFolder
    {
        get => _selectedFavoriteFolder;
        set
        {
            if (!SetProperty(ref _selectedFavoriteFolder, value) || value == null)
            {
                return;
            }

            ShaderFolderPath = value.FolderPath;
            SaveSettings();
        }
    }

    private void DeleteShader(ShaderFileItem? shader)
    {
        if (shader == null || !File.Exists(shader.FilePath))
        {
            RefreshShaderFiles();
            return;
        }

        var vertexPath = Path.ChangeExtension(shader.FilePath, ".vs");
        var hasVertexShader = File.Exists(vertexPath);
        var message = hasVertexShader
            ? $"Soll '{shader.FileName}' einschließlich der zugehörigen .vs-Datei wirklich gelöscht werden?"
            : $"Soll '{shader.FileName}' wirklich gelöscht werden?";

        if (!_messageService.ConfirmDelete(message))
        {
            return;
        }

        try
        {
            _fileService.DeleteFile(shader.FilePath);
            if (hasVertexShader)
            {
                _fileService.DeleteFile(vertexPath);
            }

            if (string.Equals(_currentFilePath, shader.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                _currentFilePath = null;
                SelectedShader = null;
                CreateVertexShader = false;
                SetInitialText(SampleIsfShader, DefaultVertexShaderSource);
                ReloadShader(EditorText);
            }

            RefreshShaderFiles();
        }
        catch (Exception ex)
        {
            _messageService.ShowError($"Fehler beim Löschen der Datei:\n{ex.Message}", "Fehler");
            RefreshShaderFiles();
        }
    }

    private void AddFavoriteFolder()
    {
        var path = _fileService.SelectFolder();
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path) ||
            FavoriteFolders.Any(favorite => string.Equals(favorite.FolderPath, path, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var favorite = new FavoriteFolderItem(path);
        FavoriteFolders.Add(favorite);
        SelectedFavoriteFolder = favorite;
        SaveSettings();
    }

    private void RemoveFavoriteFolder(FavoriteFolderItem? favorite)
    {
        if (favorite == null || !FavoriteFolders.Remove(favorite))
        {
            return;
        }

        if (ReferenceEquals(SelectedFavoriteFolder, favorite))
        {
            SelectedFavoriteFolder = null;
        }

        SaveSettings();
    }

    public ShaderFileItem? SelectedShader
    {
        get => _selectedShader;
        set
        {
            if (ReferenceEquals(_selectedShader, value))
            {
                return;
            }

            if (value != null && !LoadExplorerShader(value.FilePath))
            {
                OnPropertyChanged(nameof(SelectedShader));
                return;
            }

            _selectedShader = value;
            OnPropertyChanged(nameof(SelectedShader));
        }
    }

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

    private void SelectShaderFolder()
    {
        var path = _fileService.SelectFolder();
        if (path != null)
        {
            ShaderFolderPath = path;
            SaveSettings();
        }
    }

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                return;
            }

            var settings = JsonSerializer.Deserialize<UserSettings>(
                File.ReadAllText(SettingsFilePath));
            foreach (var path in settings?.FavoriteFolderPaths ?? Enumerable.Empty<string>())
            {
                if (Directory.Exists(path) &&
                    FavoriteFolders.All(favorite => !string.Equals(favorite.FolderPath, path, StringComparison.OrdinalIgnoreCase)))
                {
                    FavoriteFolders.Add(new FavoriteFolderItem(path));
                }
            }

            if (!string.IsNullOrWhiteSpace(settings?.ShaderFolderPath) &&
                Directory.Exists(settings.ShaderFolderPath))
            {
                ShaderFolderPath = settings.ShaderFolderPath;
            }
        }
        catch
        {
            // Beschädigte oder nicht lesbare Einstellungen werden ignoriert.
        }
    }

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);
            var settings = new UserSettings
            {
                ShaderFolderPath = ShaderFolderPath,
                FavoriteFolderPaths = FavoriteFolders.Select(favorite => favorite.FolderPath).ToList()
            };
            File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(settings));
        }
        catch
        {
            // Die Anwendung bleibt auch ohne persistente Einstellungen funktionsfähig.
        }
    }

    private void RefreshShaderFiles()
    {
        ShaderFiles.Clear();

        if (string.IsNullOrWhiteSpace(ShaderFolderPath) || !Directory.Exists(ShaderFolderPath))
        {
            return;
        }

        try
        {
            var extensions = new[] { ".fs", ".glsl", ".frag" };
            foreach (var path in Directory.EnumerateFiles(ShaderFolderPath)
                         .Where(path => extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                         .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
            {
                ShaderFiles.Add(new ShaderFileItem(path));
            }

            var current = ShaderFiles.FirstOrDefault(shader =>
                string.Equals(shader.FilePath, _currentFilePath, StringComparison.OrdinalIgnoreCase));
            if (current != null)
            {
                _selectedShader = current;
                OnPropertyChanged(nameof(SelectedShader));
            }
        }
        catch (Exception ex)
        {
            _messageService.ShowError($"Fehler beim Einlesen des Shaderordners:\n{ex.Message}", "Fehler");
        }
    }

    private bool LoadExplorerShader(string path)
    {
        if (!ConfirmDiscardChanges())
        {
            return false;
        }

        try
        {
            _currentFilePath = path;
            var vertexPath = Path.ChangeExtension(path, ".vs");
            CreateVertexShader = File.Exists(vertexPath);
            var vertexSource = CreateVertexShader
                ? _fileService.ReadAllText(vertexPath)
                : DefaultVertexShaderSource;
            SetInitialText(_fileService.ReadAllText(path), vertexSource);
            ReloadShader(EditorText, _renderer, resetParametersToDefaults: true);
            return true;
        }
        catch (Exception ex)
        {
            _messageService.ShowError($"Fehler beim Laden der Datei:\n{ex.Message}", "Fehler");
            return false;
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
        InitializeRenderer(_renderer);
    }

    public void InitializeRenderer(IShaderRenderer renderer)
    {
        renderer.Initialize();

        if (_processedShaderSource == null)
        {
            ReloadShader(EditorText, renderer);
        }
        else
        {
            CompileRenderer(renderer);
        }
    }

    public void Render(TimeSpan delta, double width, double height)
        => Render(_renderer, delta, width, height);

    public void Render(IShaderRenderer renderer, TimeSpan delta, double width, double height)
    {
        EnsureRendererCurrent(renderer);
        renderer.Render(delta, _stopwatch.Elapsed, _frameIndex++, width, height, Parameters);

        var now = _stopwatch.Elapsed;
        if (_lastGpuTimeUiUpdate == TimeSpan.Zero ||
            now - _lastGpuTimeUiUpdate >= TimeSpan.FromMilliseconds(10))
        {
            _lastGpuTimeUiUpdate = now;
            AddGpuTimeSample(renderer.LastGpuTimeMicroseconds);
        }
    }

    private void AddGpuTimeSample(double sample)
    {
        void Update()
        {
            GpuRenderTimeMicroseconds = sample;
            GpuRenderTimes.Add(sample);
            while (GpuRenderTimes.Count > GpuGraphSampleCount)
            {
                GpuRenderTimes.RemoveAt(0);
            }
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            Update();
        }
        else
        {
            dispatcher.BeginInvoke(Update);
        }
    }

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

    private void SetInitialText(string fragmentText, string vertexText)
    {
        _editorText = fragmentText;
        _vertexEditorText = vertexText;
        OnPropertyChanged(nameof(EditorText));
        OnPropertyChanged(nameof(VertexEditorText));
        IsDirty = false;
        UpdateWindowTitle();
    }

    private void ReloadShader(string rawSource)
        => ReloadShader(rawSource, _renderer);

    private void ReloadShader(
        string rawSource,
        IShaderRenderer renderer,
        bool resetParametersToDefaults = false)
    {
        var hiddenParameters = _hiddenParameters.ToArray();
        var parseResult = IsfParser.ParseAndTranspile(
            ApplyHoveredLiteral(rawSource, _fragmentHoverToken, HoverExpression));
        if (parseResult.Error != null)
        {
            ErrorLog = parseResult.Error;
            return;
        }

        if (!resetParametersToDefaults)
        {
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
                        newColor.R = Math.Clamp(oldColor.R, newColor.MinR, newColor.MaxR);
                        newColor.G = Math.Clamp(oldColor.G, newColor.MinG, newColor.MaxG);
                        newColor.B = Math.Clamp(oldColor.B, newColor.MinB, newColor.MaxB);
                        newColor.A = Math.Clamp(oldColor.A, newColor.MinA, newColor.MaxA);
                        break;
                }
            }
        }

        Parameters.Clear();
        foreach (var parameter in parseResult.Parameters)
        {
            Parameters.Add(parameter);
        }

        foreach (var parameter in hiddenParameters)
        {
            if (Parameters.All(existing => existing.Name != parameter.Name))
            {
                parameter.IsVisible = false;
                Parameters.Add(parameter);
            }
        }

        _processedShaderSource = parseResult.ProcessedGlslCode;
        _vertexShaderHeader = parseResult.VertexShaderHeader;
        _passes = parseResult.Passes;
        _shaderRevision++;
        CompileRenderer(renderer);
    }

    private void SetParameterDefault(IsfParameterViewModel? parameter)
    {
        if (parameter == null || !TryUpdateIsfHeader(root =>
            {
                var inputs = GetInputs(root);
                var input = inputs.FirstOrDefault(node =>
                    string.Equals(node?["NAME"]?.GetValue<string>(), parameter.Name, StringComparison.Ordinal));
                if (input == null)
                {
                    return false;
                }

                input["DEFAULT"] = CreateJsonValue(parameter);
                return true;
            }))
        {
            return;
        }

        ErrorLog = $"Default-Wert übernommen: {parameter.DisplayLabel}";
    }

    private void RemoveParameter(IsfParameterViewModel? parameter)
    {
        if (parameter == null || _hiddenParameters.Any(hidden => hidden.Name == parameter.Name))
        {
            return;
        }

        if (!TryUpdateIsfHeader(root =>
            {
                var inputs = GetInputs(root);
                var input = inputs.FirstOrDefault(node =>
                    string.Equals(node?["NAME"]?.GetValue<string>(), parameter.Name, StringComparison.Ordinal));
                if (input == null)
                {
                    return false;
                }

                inputs.Remove(input);
                return true;
            }))
        {
            return;
        }

        var parameterType = parameter switch
        {
            FloatParameterVm => "float",
            BoolParameterVm => "bool",
            Point2DParameterVm => "vec2",
            ColorParameterVm => "vec4",
            _ => null
        };
        if (parameterType == null)
        {
            return;
        }

        var uniformDeclaration = $"{parameterType} {parameter.Name} = {parameter.ToString()};";
        if (!Regex.IsMatch(EditorText, $@"\buniform\s+\w+\s+{Regex.Escape(parameter.Name)}\s*;"))
        {
            var header = Regex.Match(EditorText, @"/\*\s*(\{[\s\S]*?\})\s*\*/");
            if (header.Success)
            {
                EditorText = EditorText.Insert(header.Index + header.Length,
                    $"\n\n{uniformDeclaration}");
            }
        }

        parameter.IsVisible = false;
        _hiddenParameters.Add(parameter);
        ErrorLog = $"Parameter entfernt, Uniform beibehalten: {parameter.DisplayLabel}";
    }

    private void AddParameter()
    {
        var dialog = new NewParameterDialog
        {
            Owner = Application.Current?.MainWindow
        };
        if (dialog.ShowDialog() != true || dialog.ParameterDefinition == null)
        {
            return;
        }

        var definition = dialog.ParameterDefinition;
        var name = definition["NAME"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(name) || !Regex.IsMatch(name, @"^[A-Za-z_][A-Za-z0-9_]*$"))
        {
            ErrorLog = "Der Parametername muss ein gültiger GLSL-Bezeichner sein.";
            return;
        }

        if (!TryUpdateIsfHeader(root =>
            {
                var inputs = GetInputs(root);
                if (inputs.Any(input => string.Equals(
                        input?["NAME"]?.GetValue<string>(), name, StringComparison.Ordinal)))
                {
                    ErrorLog = $"Der Parametername ist bereits vorhanden: {name}";
                    return false;
                }

                inputs.Add(definition);
                return true;
            }))
        {
            return;
        }

        ReloadShader(EditorText);
        ErrorLog = $"Parameter hinzugefügt: {name}";
    }

    private bool TryUpdateIsfHeader(Func<JsonObject, bool> update)
    {
        var header = Regex.Match(EditorText, @"/\*\s*(\{[\s\S]*?\})\s*\*/");
        if (!header.Success)
        {
            ErrorLog = "Der Shader enthält keinen gültigen ISF-JSON-Header.";
            return false;
        }

        try
        {
            var root = JsonNode.Parse(header.Groups[1].Value) as JsonObject;
            if (root == null || !update(root))
            {
                ErrorLog = "Der ISF-Parameter wurde im JSON-Header nicht gefunden.";
                return false;
            }

            var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            EditorText = EditorText[..header.Index] + $"/*{json}*/" +
                         EditorText[(header.Index + header.Length)..];
            return true;
        }
        catch (Exception ex)
        {
            ErrorLog = $"JSON-Header konnte nicht aktualisiert werden: {ex.Message}";
            return false;
        }
    }

    private static JsonArray GetInputs(JsonObject root)
    {
        if (root["INPUTS"] is not JsonArray inputs)
        {
            inputs = new JsonArray();
            root["INPUTS"] = inputs;
        }

        return inputs;
    }

    private static JsonNode CreateJsonValue(IsfParameterViewModel parameter)
    {
        switch (parameter)
        {
            case FloatParameterVm value:
                return JsonValue.Create(value.Value)!;
            case BoolParameterVm value:
                return JsonValue.Create(value.Value)!;
            case Point2DParameterVm value:
                return new JsonArray(value.X, value.Y);
            case ColorParameterVm value:
                return new JsonArray(value.R, value.G, value.B, value.A);
            default:
                throw new NotSupportedException($"Nicht unterstützter ISF-Parameter: {parameter.Name}");
        }
    }

    private void EnsureRendererCurrent(IShaderRenderer renderer)
    {
        if (_rendererRevisions.TryGetValue(renderer, out var revision) &&
            revision == _shaderRevision)
        {
            return;
        }

        CompileRenderer(renderer);
    }

    private void CompileRenderer(IShaderRenderer renderer)
    {
        if (_processedShaderSource == null)
        {
            return;
        }

        try
        {
            renderer.Compile(
                BuildVertexShaderSource(
                    _vertexShaderHeader,
                    ApplyHoveredLiteral(VertexEditorText, _vertexHoverToken, HoverExpression)),
                _processedShaderSource,
                _passes);
            _rendererRevisions[renderer] = _shaderRevision;
            ErrorLog = "ISF-Shader erfolgreich kompiliert.";
        }
        catch (Exception ex)
        {
            // Auch bei einem Fehler wird die Version markiert, damit nicht
            // bei jedem Frame erneut im OpenGL-Kontext kompiliert wird.
            _rendererRevisions[renderer] = _shaderRevision;
            ErrorLog = $"Fehler beim Kompilieren des Shaders:\n{ex.Message}";
        }
    }

    private static string ApplyHoveredLiteral(string source, string hoverToken, string expression)
    {
        var separator = hoverToken.IndexOf(':');
        if (separator <= 0 ||
            !int.TryParse(hoverToken[..separator], out var offset) ||
            !int.TryParse(hoverToken[(separator + 1)..], out var length) ||
            offset < 0 || length <= 0 || offset + length > source.Length)
        {
            return source;
        }

        return source[..offset] + expression + source[(offset + length)..];
    }

    private static string BuildVertexShaderSource(string header, string source)
    {
        var sourceWithoutVersion = Regex.Replace(
            source,
            @"^[ \t]*#version[^\r\n]*(?:\r?\n|$)",
            string.Empty,
            RegexOptions.Multiline);

        return header + sourceWithoutVersion;
    }

    private void NewShader()
    {
        if (!ConfirmDiscardChanges()) return;
        _currentFilePath = null;
        SelectedShader = null;
        CreateVertexShader = false;
        SetInitialText(SampleIsfShader, DefaultVertexShaderSource);
        ReloadShader(EditorText, _renderer, resetParametersToDefaults: true);
    }

    private void OpenShader()
    {
        if (!ConfirmDiscardChanges()) return;
        var path = _fileService.OpenShader();
        if (path == null) return;

        try
        {
            _currentFilePath = path;
            var vertexPath = Path.ChangeExtension(path, ".vs");
            CreateVertexShader = File.Exists(vertexPath);
            var vertexSource = CreateVertexShader
                ? _fileService.ReadAllText(vertexPath)
                : DefaultVertexShaderSource;
            SetInitialText(_fileService.ReadAllText(path), vertexSource);
            ReloadShader(EditorText, _renderer, resetParametersToDefaults: true);
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
            if (CreateVertexShader)
            {
                _fileService.WriteAllText(Path.ChangeExtension(path, ".vs"), VertexEditorText);
            }
            _currentFilePath = path;
            RefreshShaderFiles();
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

file sealed class UserSettings
{
    public string? ShaderFolderPath { get; set; }
    public List<string> FavoriteFolderPaths { get; set; } = new();
}

public sealed class FavoriteFolderItem
{
    public FavoriteFolderItem(string folderPath)
    {
        FolderPath = folderPath;
        FolderName = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(FolderName))
        {
            FolderName = folderPath;
        }
    }

    public string FolderPath { get; }
    public string FolderName { get; }
}

public sealed class ShaderFileItem
{
    public ShaderFileItem(string filePath)
    {
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
    }

    public string FilePath { get; }
    public string FileName { get; }
}
