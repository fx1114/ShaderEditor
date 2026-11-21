using OpenTK.Graphics.OpenGL;
using ShaderEditor.Isf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ShaderEditor.Windows
{
    public class OpenGlView
    {
        private int width = 800;
        private int height = 600;
        private int _vao;
        private int _shaderProgram = 0;

        public void SetupQuad()
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

        public OpenGlView()
        {
            //GL.Viewport(0, 0, width, height);
        }

        public void ChangeSize(int newWidth, int newHeight)
        {
            width = newWidth;
            height = newHeight;
            GL.Viewport(0, 0, width, height);
        }

        public int getVao()
        {
            return _vao;
        }

        public void CompileGlsl(string vertexShaderSource, string fragmentSource)
        {
            int vs = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vs, vertexShaderSource);
            GL.CompileShader(vs);

            int fs = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fs, fragmentSource);
            GL.CompileShader(fs);

            GL.GetShader(fs, ShaderParameter.CompileStatus, out int success);
            if (success == 0)
            {
                //ErrorLog.Text = GL.GetShaderInfoLog(fs);
                GL.DeleteShader(vs);
                GL.DeleteShader(fs);
                //return;
                throw new Exception("Shader compilation failed: " + GL.GetShaderInfoLog(fs));
            }

            int newProgram = GL.CreateProgram();
            GL.AttachShader(newProgram, vs);
            GL.AttachShader(newProgram, fs);
            GL.LinkProgram(newProgram);

            GL.DeleteShader(vs);
            GL.DeleteShader(fs);

            if (_shaderProgram != 0) GL.DeleteProgram(_shaderProgram);
            _shaderProgram = newProgram;
            //ErrorLog.Text = "ISF-Shader erfolgreich kompiliert.";
            //return _shaderProgram;
            return;
        }

        public void Render(TimeSpan delta, Stopwatch stopwatch, int frameIndex, double width, double height, IEnumerable<IsfParameterViewModel> activeParameters)
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
            GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "TIME"), (float)stopwatch.Elapsed.TotalSeconds);
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
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
        }
    }
}
