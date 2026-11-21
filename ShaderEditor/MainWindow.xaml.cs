using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Wpf;
using ShaderEditor.Isf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml;
using Microsoft.Win32;

namespace ShaderEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private int _shaderProgram;
        private int _vao;
        private int _frameIndex;
        private readonly Stopwatch _stopwatch = new();
        private readonly DispatcherTimer _debounceTimer;
        private List<IsfParameterViewModel> _activeParameters = new();

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

        public MainWindow()
        {
            InitializeComponent();
            SetupEditorHighlighting();

            _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            _debounceTimer.Tick += (_, _) =>
            {
                _debounceTimer.Stop();
                ReloadIsfShader(CodeEditor.Text);
            };

            OpenGlControl.Start(new GLWpfControlSettings { MajorVersion = 3, MinorVersion = 3 });
            SetupQuad();

            CodeEditor.Text = SampleIsfShader;
            ReloadIsfShader(SampleIsfShader);
            _stopwatch.Start();

            _isDirty = false;
            UpdateWindowTitle();
        }

        private void SetupEditorHighlighting()
        {
            var assembly = Assembly.GetExecutingAssembly();
            // Der Ressourcen-Name setzt sich zusammen aus: <RootNamespace>.<Ordner>.<Dateiname>
            const string resourceName = "ShaderEditor.Syntax.IsfGlsl.xshd";

            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using var reader = new XmlTextReader(stream);
                CodeEditor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
            }

            // Optische Feinabstimmung für Dark-Mode (Zeilennummern & Auswahl)
            CodeEditor.LineNumbersForeground = new SolidColorBrush(Color.FromRgb(133, 133, 133));
            CodeEditor.TextArea.SelectionBrush = new SolidColorBrush(Color.FromArgb(100, 38, 79, 120));
            CodeEditor.TextArea.TextView.CurrentLineBackground = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
            CodeEditor.TextArea.TextView.CurrentLineBorder = new Pen(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 1);
            CodeEditor.Options.HighlightCurrentLine = true;
        }

        private void SetupQuad()
        {
            float[] quadVertices = {
                -1f,  1f,  -1f, -1f,   1f, -1f,
                -1f,  1f,   1f, -1f,   1f,  1f
            };
            _vao = GL.GenVertexArray();
            int vbo = GL.GenBuffer();
            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, quadVertices.Length * sizeof(float), quadVertices, BufferUsageHint.StaticDraw);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
        }

        private void ReloadIsfShader(string rawSource)
        {
            var parseResult = IsfParser.ParseAndTranspile(rawSource);
            if (parseResult.Error != null)
            {
                ErrorLog.Text = parseResult.Error;
                return;
            }

            // Aktuelle Slider-Werte beibehalten, wenn der Parameter-Name gleich bleibt
            foreach (var newParam in parseResult.Parameters)
            {
                var existing = _activeParameters.FirstOrDefault(p => p.Name == newParam.Name && p.GetType() == newParam.GetType());
                if (existing is FloatParameterVm oldF && newParam is FloatParameterVm newF)
                    newF.Value = Math.Clamp(oldF.Value, newF.Min, newF.Max);
                else if (existing is BoolParameterVm oldB && newParam is BoolParameterVm newB)
                    newB.Value = oldB.Value;
                else if (existing is Point2DParameterVm oldP && newParam is Point2DParameterVm newP)
                {
                    newP.X = Math.Clamp(oldP.X, newP.MinX, newP.MaxX);
                    newP.Y = Math.Clamp(oldP.Y, newP.MinY, newP.MaxY);
                }
                else if (existing is ColorParameterVm oldC && newParam is ColorParameterVm newC)
                {
                    newC.R = oldC.R; newC.G = oldC.G; newC.B = oldC.B; newC.A = oldC.A;
                }
            }

            _activeParameters = parseResult.Parameters;
            ParametersPanel.ItemsSource = _activeParameters;

            CompileGlsl(parseResult.ProcessedGlslCode);
        }

        private void CompileGlsl(string fragmentSource)
        {
            int vs = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vs, VertexShaderSource);
            GL.CompileShader(vs);

            int fs = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fs, fragmentSource);
            GL.CompileShader(fs);

            GL.GetShader(fs, ShaderParameter.CompileStatus, out int success);
            if (success == 0)
            {
                ErrorLog.Text = GL.GetShaderInfoLog(fs);
                GL.DeleteShader(vs);
                GL.DeleteShader(fs);
                return;
            }

            int newProgram = GL.CreateProgram();
            GL.AttachShader(newProgram, vs);
            GL.AttachShader(newProgram, fs);
            GL.LinkProgram(newProgram);

            GL.DeleteShader(vs);
            GL.DeleteShader(fs);

            if (_shaderProgram != 0) GL.DeleteProgram(_shaderProgram);
            _shaderProgram = newProgram;
            ErrorLog.Text = "ISF-Shader erfolgreich kompiliert.";
        }

        private void OpenGlControl_Render(TimeSpan delta)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit);
            if (_shaderProgram == 0) return;

            /*
            int width = OpenGlControl.FrameBufferWidth;
            int height = OpenGlControl.FrameBufferHeight;

            GL.Viewport(0, 0, width, height);
            */

            GL.UseProgram(_shaderProgram);

            // 1. Standard ISF Uniforms übergeben
            GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "TIME"), (float)_stopwatch.Elapsed.TotalSeconds);
            GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "TIMEDELTA"), (float)delta.TotalSeconds);
            GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "FRAMEINDEX"), _frameIndex++);
            GL.Uniform2(GL.GetUniformLocation(_shaderProgram, "RENDERSIZE"), (float)OpenGlControl.ActualWidth, (float)OpenGlControl.ActualHeight);

            // 2. Dynamische ISF INPUTS übergeben
            foreach (var param in _activeParameters)
            {
                int loc = GL.GetUniformLocation(_shaderProgram, param.Name);
                if (loc == -1) continue;

                switch (param)
                {
                    case FloatParameterVm f:
                        GL.Uniform1(loc, f.Value);
                        break;
                    case BoolParameterVm b:
                        GL.Uniform1(loc, b.Value ? 1 : 0);
                        break;
                    case Point2DParameterVm p:
                        GL.Uniform2(loc, p.X, p.Y);
                        break;
                    case ColorParameterVm c:
                        GL.Uniform4(loc, c.R, c.G, c.B, c.A);
                        break;
                }
            }

            GL.BindVertexArray(_vao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
        }

        private void CodeEditor_TextChanged(object sender, EventArgs e)
        {
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }


        private string? _currentFilePath;
        private bool _isDirty;

        // Commands für die Tastenkombinationen (Strg+N / Strg+O / Strg+S)
        public ICommand NewCommand => new RelayCommand(_ => NewShader());
        public ICommand OpenCommand => new RelayCommand(_ => OpenShader());
        public ICommand SaveCommand => new RelayCommand(_ => SaveShader());
        public ICommand SaveAsCommand => new RelayCommand(_ => SaveShaderAs());

        // --- In deinem bestehenden Konstruktor ergänzen: ---
        // CodeEditor.Text = SampleIsfShader;
        // _isDirty = false;
        // UpdateWindowTitle();

        // --- In CodeEditor_TextChanged ergänzen: ---
        // _isDirty = true;
        // UpdateWindowTitle();

        private void UpdateWindowTitle()
        {
            string fileName = string.IsNullOrEmpty(_currentFilePath)
                ? "Unbenannt.fs"
                : Path.GetFileName(_currentFilePath);
            string dirtyMarker = _isDirty ? "*" : "";
            Title = $"{fileName}{dirtyMarker} - ISF Shader Studio";
        }

        private bool ConfirmDiscardChanges()
        {
            if (!_isDirty) return true;

            var result = MessageBox.Show(
                "Es gibt ungespeicherte Änderungen. Möchtest du diese vorab speichern?",
                "Ungespeicherte Änderungen",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes) return SaveShader();
            return result == MessageBoxResult.No;
        }

        private void NewShader()
        {
            if (!ConfirmDiscardChanges()) return;

            _currentFilePath = null;
            CodeEditor.Text = SampleIsfShader;
            _isDirty = false;
            UpdateWindowTitle();
            ReloadIsfShader(CodeEditor.Text);
        }

        private void OpenShader()
        {
            if (!ConfirmDiscardChanges()) return;

            var dialog = new OpenFileDialog
            {
                Title = "ISF-Shader öffnen",
                Filter = "ISF Fragment Shader (*.fs)|*.fs|GLSL Shader (*.glsl;*.frag)|*.glsl;*.frag|Alle Dateien (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _currentFilePath = dialog.FileName;
                    CodeEditor.Text = File.ReadAllText(_currentFilePath);
                    _isDirty = false;
                    UpdateWindowTitle();
                    ReloadIsfShader(CodeEditor.Text);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Laden der Datei:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private bool SaveShader()
        {
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                return SaveShaderAs();
            }

            try
            {
                File.WriteAllText(_currentFilePath, CodeEditor.Text);
                _isDirty = false;
                UpdateWindowTitle();
                ErrorLog.Text = $"Gespeichert: {_currentFilePath}";
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private bool SaveShaderAs()
        {
            var dialog = new SaveFileDialog
            {
                Title = "ISF-Shader speichern unter",
                Filter = "ISF Fragment Shader (*.fs)|*.fs|GLSL Shader (*.glsl;*.frag)|*.glsl;*.frag|Alle Dateien (*.*)|*.*",
                DefaultExt = ".fs",
                FileName = string.IsNullOrEmpty(_currentFilePath) ? "shader.fs" : Path.GetFileName(_currentFilePath)
            };

            if (dialog.ShowDialog() == true)
            {
                _currentFilePath = dialog.FileName;
                return SaveShader();
            }
            return false;
        }

        // --- Menü Click-Handler ---
        private void MenuNew_Click(object sender, RoutedEventArgs e) => NewShader();
        private void MenuOpen_Click(object sender, RoutedEventArgs e) => OpenShader();
        private void MenuSave_Click(object sender, RoutedEventArgs e) => SaveShader();
        private void MenuSaveAs_Click(object sender, RoutedEventArgs e) => SaveShaderAs();
        private void MenuExit_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!ConfirmDiscardChanges())
            {
                e.Cancel = true;
            }
        }

        private void MenuFullScreen_Click(object sender, RoutedEventArgs e)
        {
            OpenFullscreenOnSecondScreen(OpenGlControl);
        }

        public void OpenFullscreenOnSecondScreen(GLWpfControl mainControl)
        {
            var screens = System.Windows.Forms.Screen.AllScreens;
            if (screens.Length < 2) return; // Kein 2. Bildschirm vorhanden

            // Den ersten sekundären Bildschirm auswählen
            var secondScreen = screens.LastOrDefault(s => !s.Primary) ?? screens[1];

            var fullscreenWindow = new FullScreen();
            fullscreenWindow.Owner = this;
            fullscreenWindow.Left = secondScreen.Bounds.Left;
            fullscreenWindow.Top = secondScreen.Bounds.Top;
            fullscreenWindow.Width = secondScreen.Bounds.Width;
            fullscreenWindow.Height = secondScreen.Bounds.Height;
            fullscreenWindow.Topmost = true;

            var secondGlControl = new GLWpfControl();
            var settings = new GLWpfControlSettings
            {
                MajorVersion = 3,
                MinorVersion = 3,
                ContextToUse = mainControl.Context // Teilt Ressourcen (Texturen, Shader, Buffer)
            };
            secondGlControl.Start(settings);

            // Render-Event verknüpfen
            secondGlControl.Render += (delta) =>
            {
                // Hier deine Render-Methode aufrufen
                //RenderScene();
                OpenGlControl_Render(delta);
            };

            fullscreenWindow.Content = secondGlControl;
            fullscreenWindow.Show();

            // Wichtig: Maximieren erst NACH dem Show() auf der richtigen Position
            fullscreenWindow.WindowState = WindowState.Maximized;
        }
    }

    // Kleiner Helper für die KeyBindings ohne zusätzliches MVVM-Paket
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        public RelayCommand(Action<object?> execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute(parameter);
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}