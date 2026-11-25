using OpenTK.Graphics.OpenGL4;
using ShaderEditor.Services;
using ShaderEditor.Isf;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace ShaderEditor.Windows
{
    public sealed class OpenGlView : IShaderRenderer
    {
        private int _vao;
        private int _timerQuery;
        private int _shaderProgram = 0;
        private bool _initialized;
        private IReadOnlyList<IsfPassDefinition> _passes = Array.Empty<IsfPassDefinition>();
        private readonly Dictionary<string, PassTarget> _passTargets = new(StringComparer.Ordinal);

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

        public void Compile(
            string vertexShaderSource,
            string fragmentSource,
            IReadOnlyList<IsfPassDefinition> passes)
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
            _passes = passes;
            DeletePassTargets();

            //ErrorLog.Text = "ISF-Shader erfolgreich kompiliert.";
            //return _shaderProgram;
            return;
        }

        public void Render(TimeSpan delta, TimeSpan elapsed, int frameIndex, double width, double height, IEnumerable<IsfParameterViewModel> activeParameters)
        {
            if (_shaderProgram == 0) return;

            EnsurePassTargets((int)width, (int)height);

            GL.GetQueryObject(_timerQuery, GetQueryObjectParam.QueryResultAvailable, out int resultAvailable);
            if (resultAvailable != 0)
            {
                GL.GetQueryObject(_timerQuery, GetQueryObjectParam.QueryResult, out long elapsedNanoseconds);
                LastGpuTimeMicroseconds = elapsedNanoseconds / 1000.0;
            }
            
            var passes = GetRenderPasses();

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 1);
            GL.Viewport(0, 0, (int)width, (int)height);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            
            for (var passIndex = 0; passIndex < passes.Count; passIndex++)
            {
                var pass = passes[passIndex];
                var target = !string.IsNullOrWhiteSpace(pass.Target) &&
                             _passTargets.TryGetValue(pass.Target, out var passTarget)
                    ? passTarget
                    : null;
                var passWidth = target?.Width ?? (int)width;
                var passHeight = target?.Height ?? (int)height;

                GL.BindFramebuffer(FramebufferTarget.Framebuffer, target?.Framebuffer ?? 1);
                if (target == null)
                {
                    GL.DrawBuffer(DrawBufferMode.Back);
                }
                GL.Viewport(0, 0, passWidth, passHeight);
                if (target == null || !pass.Persistent)
                {
                    GL.Clear(ClearBufferMask.ColorBufferBit);
                }
            
                GL.UseProgram(_shaderProgram);
                SetUniforms(delta, elapsed, frameIndex, 0, (int)width, (int)height, activeParameters);

                GL.BindVertexArray(_vao);
                GL.BeginQuery(QueryTarget.TimeElapsed, _timerQuery);
                GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
                GL.EndQuery(QueryTarget.TimeElapsed);
            
                SetUniforms(delta, elapsed, frameIndex, passIndex, passWidth, passHeight, activeParameters);
                BindPassTextures();

                GL.BindVertexArray(_vao);
                GL.BeginQuery(QueryTarget.TimeElapsed, _timerQuery);
                GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
                GL.EndQuery(QueryTarget.TimeElapsed);
            }
            
            
            var lastPass = passes[^1];
            if (!string.IsNullOrWhiteSpace(lastPass.Target) &&
                _passTargets.TryGetValue(lastPass.Target, out var lastTarget))
            {
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, lastTarget.Framebuffer);
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 1);
                GL.BlitFramebuffer(
                    0, 0, lastTarget.Width, lastTarget.Height,
                    0, 0, (int)width, (int)height,
                    ClearBufferMask.ColorBufferBit,
                    BlitFramebufferFilter.Linear);
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, 1);
            }
            
            
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 1);
            GL.DrawBuffer(DrawBufferMode.Back);
            GL.Viewport(0, 0, (int)width, (int)height);
            GL.Flush();
        }

        private void SetUniforms(
            TimeSpan delta,
            TimeSpan elapsed,
            int frameIndex,
            int passIndex,
            int width,
            int height,
            IEnumerable<IsfParameterViewModel> activeParameters)
        {
            GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "TIME"), (float)elapsed.TotalSeconds);
            GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "TIMEDELTA"), (float)delta.TotalSeconds);
            GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "FRAMEINDEX"), frameIndex);
            GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "PASSINDEX"), passIndex);

            var now = DateTime.Now;
            GL.Uniform4(
                GL.GetUniformLocation(_shaderProgram, "DATE"),
                (float)now.Year,
                (float)now.Month,
                (float)now.Day,
                (float)now.TimeOfDay.TotalSeconds);
            GL.Uniform2(GL.GetUniformLocation(_shaderProgram, "RENDERSIZE"), (float)width, (float)height);

            foreach (var param in activeParameters)
            {
                var location = GL.GetUniformLocation(_shaderProgram, param.Name);
                if (location == -1) continue;

                switch (param)
                {
                    case FloatParameterVm value:
                        GL.Uniform1(location, value.Value);
                        break;
                    case BoolParameterVm value:
                        GL.Uniform1(location, value.Value ? 1 : 0);
                        break;
                    case Point2DParameterVm value:
                        GL.Uniform2(location, value.X, value.Y);
                        break;
                    case ColorParameterVm value:
                        GL.Uniform4(location, value.R, value.G, value.B, value.A);
                        break;
                }
            }

            foreach( var pass in _passes)
            {
                if (!string.IsNullOrWhiteSpace(pass.Target) &&
                    _passTargets.TryGetValue(pass.Target, out var target))
                {
                    var sizeLocation = GL.GetUniformLocation(_shaderProgram, $"_{pass.Target}_imgSize");
                    if (sizeLocation != -1)
                    {
                        GL.Uniform2(sizeLocation, target.Width, target.Height);
                    }
                    var rectLocation = GL.GetUniformLocation(_shaderProgram, $"_{pass.Target}_imgRect");
                    if (rectLocation != -1)
                    {
                        //GL.Uniform4(rectLocation, 0f, 0f, target.Width, target.Height);
                        GL.Uniform4(rectLocation, 0f, 0f, 1f, 1f);
                    }
                    var flipLocation = GL.GetUniformLocation(_shaderProgram, $"_{pass.Target}_flip");
                    if (flipLocation != -1)
                    {
                        //GL.Uniform1(flipLocation, target.Persistent ? 1 : 0);
                        GL.Uniform1(flipLocation, 1);
                    }
                }
            }
        }

        private IReadOnlyList<IsfPassDefinition> GetRenderPasses()
        {
            var passes = _passes.Count == 0
                ? new List<IsfPassDefinition>()
                : _passes.ToList();

            if (passes.Count == 0 || !string.IsNullOrWhiteSpace(passes[^1].Target))
            {
                passes.Add(new IsfPassDefinition());
            }

            return passes;
        }

        private void BindPassTextures()
        {
            var textureUnit = 0;
            foreach (var target in _passTargets)
            {
                if (textureUnit >= 16) break;

                GL.ActiveTexture(TextureUnit.Texture0 + textureUnit);
                GL.BindTexture(TextureTarget.Texture2D, target.Value.Texture);
                var location = GL.GetUniformLocation(_shaderProgram, target.Key);
                if (location != -1)
                {
                    GL.Uniform1(location, textureUnit);
                }

                textureUnit++;
            }
        }

        private void EnsurePassTargets(int renderWidth, int renderHeight)
        {
            var requiredTargets = _passes
                .Where(pass => !string.IsNullOrWhiteSpace(pass.Target))
                .ToDictionary(
                    pass => pass.Target!,
                    pass => pass,
                    StringComparer.Ordinal);

            if (_passTargets.Count == requiredTargets.Count &&
                requiredTargets.All(required =>
                    _passTargets.TryGetValue(required.Key, out var target) &&
                    target.Width == ResolveDimension(required.Value.Width, renderWidth, renderWidth, renderHeight) &&
                    target.Height == ResolveDimension(required.Value.Height, renderHeight, renderWidth, renderHeight)))
            {
                return;
            }

            DeletePassTargets();
            foreach (var pass in requiredTargets.Values)
            {
                var target = new PassTarget(
                    ResolveDimension(pass.Width, renderWidth, renderWidth, renderHeight),
                    ResolveDimension(pass.Height, renderHeight, renderWidth, renderHeight),
                    pass.Persistent);
                CreatePassTarget(target);
                _passTargets.Add(pass.Target!, target);
            }
        }


        private static int ResolveDimension(
            System.Text.Json.JsonElement? value,
            int defaultValue,
            int renderWidth,
            int renderHeight)
        {
            if (!value.HasValue) return defaultValue;

            var element = value.Value;
            if (element.ValueKind == System.Text.Json.JsonValueKind.Number && element.TryGetDouble(out var number))
            {
                return Math.Max(1, (int)Math.Round(number < 1 ? defaultValue * number : number));
            }

            if (element.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var text = element.GetString();
                if (string.Equals(text, "HEIGHT", StringComparison.OrdinalIgnoreCase)) return renderHeight;
                if (string.Equals(text, "WIDTH", StringComparison.OrdinalIgnoreCase)) return renderWidth;
                if (text?.EndsWith('%') == true &&
                    double.TryParse(text[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
                {
                    return Math.Max(1, (int)Math.Round(defaultValue * percent / 100.0));
                }
            }

            return defaultValue;
        }

        private void CreatePassTarget(PassTarget target)
        {
            // create OpenGL texture buffer
            target.Texture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, target.Texture);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, target.Width, target.Height, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);

            // create OpenGL framebuffer and attach the texture
            target.Framebuffer = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, target.Framebuffer);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                TextureTarget.Texture2D, target.Texture, 0);
            //GL.DrawBuffer(DrawBufferMode.ColorAttachment0);

            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
            {
                DeletePassTarget(target);
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                throw new InvalidOperationException("ISF-Pass-Framebuffer konnte nicht erstellt werden.");
            }

            //GL.BindFramebuffer(FramebufferTarget.Framebuffer, target.Framebuffer);
            //GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        private void DeletePassTargets()
        {
            foreach (var target in _passTargets.Values)
            {
                DeletePassTarget(target);
            }

            _passTargets.Clear();
        }

        private static void DeletePassTarget(PassTarget target)
        {
            if (target.Framebuffer != 0) GL.DeleteFramebuffer(target.Framebuffer);
            if (target.Texture != 0) GL.DeleteTexture(target.Texture);
            target.Framebuffer = 0;
            target.Texture = 0;
        }

        private sealed class PassTarget
        {
            public PassTarget(int width, int height, bool persistent)
            {
                Width = width;
                Height = height;
                Persistent = persistent;
            }

            public int Width { get; }
            public int Height { get; }
            public bool Persistent { get; }
            public int Texture { get; set; }
            public int Framebuffer { get; set; }
        }
    }
}
