using ShaderEditor.Isf;

namespace ShaderEditor.Services;

public interface IShaderRenderer
{
    void Initialize();
    void Compile(
        string vertexShaderSource,
        string fragmentShaderSource,
        IReadOnlyList<IsfPassDefinition> passes);
    void Render(TimeSpan delta, TimeSpan elapsed, int frameIndex, double width, double height, IEnumerable<IsfParameterViewModel> parameters);
    double LastGpuTimeMicroseconds { get; }
}
