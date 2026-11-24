using OpenTK.Graphics.OpenGL4;
using ShaderEditor.Services;
using ShaderEditor.Isf;
using System;
using System.Diagnostics;

namespace ShaderEditor.Windows
{
    public sealed class OpenGlView : IShaderRenderer
    {
        private int _vao;
        private int _timerQuery;
        private int _shaderProgram = 0;
        private bool _initialized;

        public double LastGpuTimeMicroseconds { get; private set; }

        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            float[] quadVertices = {
                -1f,  1f,  -1f, -1f,   1f, -1f,
                -1f,  1f,   1f, -1f,   1f,  1f
            };
            _vao = GL.GenVertexArray();
            _timerQuery = GL.GenQuery();
            int vbo = GL.GenBuffer();
            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, quadVertices.Length * sizeof(float), quadVertices, BufferUsageHint.StaticDraw);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
            _initialized = true;
        }

        public void Compile(string vertexShaderSource, string fragmentSource)
        {
            int vs = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vs, vertexShaderSource);
            GL.CompileShader(vs);

            int fs = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fs, fragmentSource);
            GL.CompileShader(fs);

            var errors = new List<string>();

            GL.GetShader(vs, ShaderParameter.CompileStatus, out int vertexSuccess);
            if (vertexSuccess == 0)
            {
                errors.Add($"Vertex-Shader-Kompilierung fehlgeschlagen:\n{GL.GetShaderInfoLog(vs)}");
            }

            GL.GetShader(fs, ShaderParameter.CompileStatus, out int fragmentSuccess);
            if (fragmentSuccess == 0)
            {
                errors.Add($"Fragment-Shader-Kompilierung fehlgeschlagen:\n{GL.GetShaderInfoLog(fs)}");
            }

            if (errors.Count > 0)
            {
                GL.DeleteShader(vs);
                GL.DeleteShader(fs);
                throw new Exception(string.Join("\n\n", errors));
            }

            int newProgram = GL.CreateProgram();
            GL.AttachShader(newProgram, vs);
            GL.AttachShader(newProgram, fs);
            GL.LinkProgram(newProgram);

            GL.GetProgram(newProgram, GetProgramParameterName.LinkStatus, out int linkSuccess);
            if (linkSuccess == 0)
            {
                var linkError = GL.GetProgramInfoLog(newProgram);
                GL.DeleteProgram(newProgram);
                GL.DeleteShader(vs);
                GL.DeleteShader(fs);
                throw new Exception($"Shader-Linking fehlgeschlagen:\n{linkError}");
            }

            GL.DeleteShader(vs);
            GL.DeleteShader(fs);

            if (_shaderProgram != 0) GL.DeleteProgram(_shaderProgram);
            _shaderProgram = newProgram;
            //ErrorLog.Text = "ISF-Shader erfolgreich kompiliert.";
            //return _shaderProgram;
            return;
        }

        public void Render(TimeSpan delta, TimeSpan elapsed, int frameIndex, double width, double height, IEnumerable<IsfParameterViewModel> activeParameters)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit);
            if (_shaderProgram == 0) return;

            GL.GetQueryObject(_timerQuery, GetQueryObjectParam.QueryResultAvailable, out int resultAvailable);
            if (resultAvailable != 0)
            {
                GL.GetQueryObject(_timerQuery, GetQueryObjectParam.QueryResult, out long elapsedNanoseconds);
                LastGpuTimeMicroseconds = elapsedNanoseconds / 1000.0;
            }

            /*
            int width = OpenGlControl.FrameBufferWidth;
            int height = OpenGlControl.FrameBufferHeight;

            GL.Viewport(0, 0, width, height);
            */

            GL.UseProgram(_shaderProgram);

            // 1. Standard ISF Uniforms übergeben
            GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "TIME"), (float)elapsed.TotalSeconds);
            GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "TIMEDELTA"), (float)delta.TotalSeconds);
            GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "FRAMEINDEX"), frameIndex);
            GL.Uniform2(GL.GetUniformLocation(_shaderProgram, "RENDERSIZE"), (float)width, (float)height);

            // 2. Dynamische ISF INPUTS übergeben
            foreach (var param in activeParameters)
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
            GL.BeginQuery(QueryTarget.TimeElapsed, _timerQuery);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
            GL.EndQuery(QueryTarget.TimeElapsed);
        }
    }
}
