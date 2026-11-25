using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ShaderEditor.Isf
{
    public class IsfParseResult
    {
        public string ProcessedGlslCode { get; init; } = string.Empty;
        public string VertexShaderHeader { get; init; } = string.Empty;
        public List<IsfParameterViewModel> Parameters { get; init; } = new();
        public List<IsfPassDefinition> Passes { get; init; } = new();
        public string? Error { get; init; }
    }

    public static class IsfParser
    {
        // Sucht nach dem ersten /*{ ... }*/ Block im Shader
        private static readonly Regex HeaderRegex = new(@"/\*\s*(\{[\s\S]*?\})\s*\*/", RegexOptions.Compiled);

        public static IsfParseResult ParseAndTranspile(string rawIsfSource)
        {
            var parameters = new List<IsfParameterViewModel>();
            string glslBody = rawIsfSource;
            IsfMetadata? metadata = null;

            var match = HeaderRegex.Match(rawIsfSource);
            if (match.Success)
            {
                string jsonText = match.Groups[1].Value;
                glslBody = rawIsfSource.Remove(match.Index, match.Length);

                try
                {
                    metadata = JsonSerializer.Deserialize<IsfMetadata>(jsonText, new JsonSerializerOptions
                    {
                        AllowTrailingCommas = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch (Exception ex)
                {
                    return new IsfParseResult { Error = $"JSON-Header Fehler: {ex.Message}" };
                }
            }

            // GLSL Header zusammenbauen (Kompatibilitätsschicht für ISF -> GLSL 3.30 Core)
            var sb = new StringBuilder();
            var vertexHeader = new StringBuilder();
            sb.AppendLine("#version 330 core");
            sb.AppendLine("out vec4 _isf_FragColor;");
            sb.AppendLine("#define gl_FragColor _isf_FragColor");
            sb.AppendLine("uniform float TIME;");
            sb.AppendLine("uniform float TIMEDELTA;");
            sb.AppendLine("uniform int FRAMEINDEX;");
            sb.AppendLine("uniform int PASSINDEX;");
            sb.AppendLine("uniform vec4 DATE;");
            sb.AppendLine("uniform vec2 RENDERSIZE;");
            sb.AppendLine("#define isf_FragNormCoord (gl_FragCoord.xy / RENDERSIZE.xy)");
            sb.AppendLine("#define IMG_PIXEL(inputImage, pixelCoord) (texture((inputImage), (pixelCoord) / vec2(textureSize((inputImage), 0))))");


            sb.AppendLine("#define IMG_NORM_PIXEL(inputImage, normCoord) (texture((inputImage), (normCoord)))");
            sb.AppendLine("#define IMG_THIS_PIXEL(inputImage) (IMG_PIXEL((inputImage), (gl_FragCoord.xy)))");
            sb.AppendLine("#define IMG_THIS_NORM_PIXEL(inputImage) (IMG_NORM_PIXEL((inputImage), (isf_FragNormCoord)))");
            sb.AppendLine();

            vertexHeader.AppendLine("#version 330 core");
            vertexHeader.AppendLine("layout (location = 0) in vec2 aPos;");
            vertexHeader.AppendLine("uniform float TIME;");
            vertexHeader.AppendLine("uniform float TIMEDELTA;");
            vertexHeader.AppendLine("uniform int FRAMEINDEX;");
            vertexHeader.AppendLine("uniform int PASSINDEX;");
            vertexHeader.AppendLine("uniform vec4 DATE;");
            vertexHeader.AppendLine("uniform vec2 RENDERSIZE;");
            vertexHeader.AppendLine("void isf_vertShaderInit() {" +
                "   gl_Position = vec4(aPos.x, aPos.y, 1.0, 1.0);" +
                "}");
            vertexHeader.AppendLine();

            if (metadata?.Inputs != null)
            {
                foreach (var input in metadata.Inputs)
                {
                    string label = string.IsNullOrWhiteSpace(input.Label) ? input.Name : input.Label;

                    switch (input.Type.ToLowerInvariant())
                    {
                        case "float":
                            sb.AppendLine($"uniform float {input.Name};");
                            vertexHeader.AppendLine($"uniform float {input.Name};");
                            parameters.Add(new FloatParameterVm
                            {
                                Name = input.Name,
                                DisplayLabel = label,
                                Value = GetFloat(input.Default, 0.5f),
                                Min = GetFloat(input.Min, 0.0f),
                                Max = GetFloat(input.Max, 1.0f)
                            });
                            break;

                        case "bool":
                        case "event":
                            sb.AppendLine($"uniform bool {input.Name};");
                            vertexHeader.AppendLine($"uniform bool {input.Name};");
                            parameters.Add(new BoolParameterVm
                            {
                                Name = input.Name,
                                DisplayLabel = label,
                                Value = input.Default?.ValueKind == JsonValueKind.True
                            });
                            break;

                        case "point2d":
                            sb.AppendLine($"uniform vec2 {input.Name};");
                            vertexHeader.AppendLine($"uniform vec2 {input.Name};");
                            var defPt = GetFloatArray(input.Default, new[] { 0.5f, 0.5f });
                            var minPt = GetFloatArray(input.Min, new[] { 0.0f, 0.0f });
                            var maxPt = GetFloatArray(input.Max, new[] { 1.0f, 1.0f });
                            parameters.Add(new Point2DParameterVm
                            {
                                Name = input.Name,
                                DisplayLabel = label,
                                X = defPt[0],
                                Y = defPt[1],
                                MinX = minPt[0],
                                MaxX = maxPt[0],
                                MinY = minPt[1],
                                MaxY = maxPt[1]
                            });
                            break;

                        case "color":
                            sb.AppendLine($"uniform vec4 {input.Name};");
                            vertexHeader.AppendLine($"uniform vec4 {input.Name};");
                            var defCol = GetFloatArray(input.Default, new[] { 1f, 1f, 1f, 1f });
                            var minCol = GetFloatArray(input.Min, new[] { 0f, 0f, 0f, 0f });
                            var maxCol = GetFloatArray(input.Max, new[] { 1f, 1f, 1f, 1f });
                            parameters.Add(new ColorParameterVm
                            {
                                Name = input.Name,
                                DisplayLabel = label,
                                R = GetArrayValue(defCol, 0, 1f),
                                G = GetArrayValue(defCol, 1, 1f),
                                B = GetArrayValue(defCol, 2, 1f),
                                A = GetArrayValue(defCol, 3, 1f),
                                MinR = GetArrayValue(minCol, 0, 0f),
                                MaxR = GetArrayValue(maxCol, 0, 1f),
                                MinG = GetArrayValue(minCol, 1, 0f),
                                MaxG = GetArrayValue(maxCol, 1, 1f),
                                MinB = GetArrayValue(minCol, 2, 0f),
                                MaxB = GetArrayValue(maxCol, 2, 1f),
                                MinA = GetArrayValue(minCol, 3, 0f),
                                MaxA = GetArrayValue(maxCol, 3, 1f)
                            });
                            break;
                    }
                }

            if (metadata?.Passes != null)
            {
                foreach (var pass in metadata.Passes)
                {
                    if (string.IsNullOrWhiteSpace(pass.Target) ||
                        !Regex.IsMatch(pass.Target, @"^[A-Za-z_][A-Za-z0-9_]*$"))
                    {
                        continue;
                    }

                    sb.AppendLine($"uniform sampler2D {pass.Target};");
                    sb.AppendLine($"uniform vec2 _{pass.Target}_imgSize;");
                    sb.AppendLine($"uniform vec4 _{pass.Target}_imgRect;");
                    sb.AppendLine($"uniform bool _{pass.Target}_flip;");
                    vertexHeader.AppendLine($"uniform sampler2D {pass.Target};");
                }
            }
            }

            // Falls der User manuell #version geschrieben hat, entfernen wir es, da wir es oben injizieren
            glslBody = Regex.Replace(glslBody, @"^\s*#version\s+.*$", "", RegexOptions.Multiline);
            sb.AppendLine(glslBody);

            return new IsfParseResult
            {
                ProcessedGlslCode = sb.ToString(),
                VertexShaderHeader = vertexHeader.ToString(),
                Parameters = parameters,
                Passes = metadata?.Passes ?? new List<IsfPassDefinition>()
            };
        }

        private static float GetFloat(JsonElement? el, float fallback)
            => el.HasValue && el.Value.ValueKind == JsonValueKind.Number ? el.Value.GetSingle() : fallback;

        private static float[] GetFloatArray(JsonElement? el, float[] fallback)
        {
            if (!el.HasValue || el.Value.ValueKind != JsonValueKind.Array) return fallback;
            var list = new List<float>();
            foreach (var item in el.Value.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Number) list.Add(item.GetSingle());
            }
            return list.Count >= 2 ? list.ToArray() : fallback;
        }

        private static float GetArrayValue(float[] values, int index, float fallback)
            => index < values.Length ? values[index] : fallback;
    }
}
