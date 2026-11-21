namespace ShaderEditor.Services;

public interface IFileService
{
    string? OpenShader();
    string? SaveShader(string? currentPath);
    string ReadAllText(string path);
    void WriteAllText(string path, string content);
}
