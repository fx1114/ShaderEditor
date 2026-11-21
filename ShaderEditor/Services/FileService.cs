using Microsoft.Win32;
using System.IO;

namespace ShaderEditor.Services;

public sealed class FileService : IFileService
{
    private const string Filter = "ISF-Shader (*.fs)|*.fs|GLSL-Shader (*.glsl;*.frag)|*.glsl;*.frag|Alle Dateien (*.*)|*.*";

    public string? OpenShader()
    {
        var dialog = new OpenFileDialog
        {
            Title = "ISF-Shader öffnen",
            Filter = Filter
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? SaveShader(string? currentPath)
    {
        var dialog = new SaveFileDialog
        {
            Title = "ISF-Shader speichern unter",
            Filter = Filter,
            FileName = string.IsNullOrWhiteSpace(currentPath) ? "shader.fs" : System.IO.Path.GetFileName(currentPath)
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string ReadAllText(string path) => System.IO.File.ReadAllText(path);

    public void WriteAllText(string path, string content) => System.IO.File.WriteAllText(path, content);
}
