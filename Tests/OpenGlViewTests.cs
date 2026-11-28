using System.Reflection;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using ShaderEditor.Windows;

namespace Tests;

[TestClass]
[DoNotParallelize]
public sealed class OpenGlViewTests
{
    private NativeWindow? _window;

    [TestInitialize]
    public void InitializeOpenGlContext()
    {
        _window = new NativeWindow(new NativeWindowSettings
        {
            API = ContextAPI.OpenGL,
            APIVersion = new Version(3, 3),
            Profile = ContextProfile.Core,
            ClientSize = new Vector2i(1, 1),
            StartVisible = false,
            Title = nameof(OpenGlViewTests)
        });
    }

    [TestCleanup]
    public void DisposeOpenGlContext()
    {
        _window?.Close();
        _window?.Dispose();
        _window = null;
    }

    [TestMethod]
    public void Initialize_CreatesVertexArrayAndConfiguresQuadVertexBuffer()
    {
        var view = new OpenGlView();

        view.Initialize();

        Assert.AreNotEqual(0, GetPrivateField<int>(view, "_vao"));
        Assert.AreNotEqual(0, GetPrivateField<int>(view, "_timerQuery"));

        GL.GetInteger(GetPName.VertexArrayBinding, out var boundVertexArray);
        GL.GetInteger(GetPName.ArrayBufferBinding, out var boundBuffer);

        Assert.AreEqual(GetPrivateField<int>(view, "_vao"), boundVertexArray);
        Assert.AreNotEqual(0, boundBuffer);

        GL.GetBufferParameter(
            BufferTarget.ArrayBuffer,
            BufferParameterName.BufferSize,
            out long bufferSize);

        Assert.AreEqual(6 * 2 * sizeof(float), bufferSize);

        GL.GetVertexAttrib(
            0,
            VertexAttribParameter.ArrayEnabled,
            out float attributeEnabled);

        Assert.AreEqual(1, attributeEnabled);

        GL.GetVertexAttrib(
            0,
            VertexAttribParameter.ArraySize,
            out float attributeSize);

        Assert.AreEqual(2, attributeSize);

        GL.GetVertexAttrib(
            0,
            VertexAttribParameter.ArrayStride,
            out float attributeStride);

        Assert.AreEqual(2 * sizeof(float), attributeStride);

        GL.GetVertexAttrib(
            0,
            VertexAttribParameter.ArrayType,
            out float attributeType);

        Assert.AreEqual((int)VertexAttribPointerType.Float, attributeType);
    }

    [TestMethod]
    public void Initialize_WhenCalledMoreThanOnce_DoesNotRecreateOpenGlResources()
    {
        var view = new OpenGlView();

        view.Initialize();

        var firstVertexArray = GetPrivateField<int>(view, "_vao");
        var firstTimerQuery = GetPrivateField<int>(view, "_timerQuery");

        view.Initialize();

        var secondVertexArray = GetPrivateField<int>(view, "_vao");
        var secondTimerQuery = GetPrivateField<int>(view, "_timerQuery");

        Assert.AreEqual(firstVertexArray, secondVertexArray);
        Assert.AreEqual(firstTimerQuery, secondTimerQuery);

        GL.GetInteger(GetPName.VertexArrayBinding, out var boundVertexArray);
        Assert.AreEqual(firstVertexArray, boundVertexArray);
    }

    private static T GetPrivateField<T>(OpenGlView view, string fieldName)
    {
        var field = typeof(OpenGlView).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(field);

        return (T)field!.GetValue(view)!;
    }
}