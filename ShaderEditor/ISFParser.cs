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
        public List<IsfParameterViewModel> Parameters { get; init; } = new();
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
                        ReadCommentHandling = JsonCommentHandling.Skip
                    });
                }
                catch (Exception ex)
                {
                    return new IsfParseResult { Error = $"JSON-Header Fehler: {ex.Message}" };
                }
            }

            // GLSL Header zusammenbauen (Kompatibilitätsschicht für ISF -> GLSL 3.30 Core)
            var sb = new StringBuilder();
            sb.AppendLine("#version 330 core");
            sb.AppendLine("out vec4 _isf_FragColor;");
            sb.AppendLine("#define gl_FragColor _isf_FragColor");
            sb.AppendLine("uniform float TIME;");
            sb.AppendLine("uniform float TIMEDELTA;");
            sb.AppendLine("uniform int FRAMEINDEX;");
            sb.AppendLine("uniform vec2 RENDERSIZE;");
            sb.AppendLine("#define isf_FragNormCoord (gl_FragCoord.xy / RENDERSIZE.xy)");
            sb.AppendLine();

            if (metadata?.Inputs != null)
            {
                foreach (var input in metadata.Inputs)
                {
                    string label = string.IsNullOrWhiteSpace(input.Label) ? input.Name : input.Label;

                    switch (input.Type.ToLowerInvariant())
                    {
                        case "float":
                            sb.AppendLine($"uniform float {input.Name};");
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
                            parameters.Add(new BoolParameterVm
                            {
                                Name = input.Name,
                                DisplayLabel = label,
                                Value = input.Default?.ValueKind == JsonValueKind.True
                            });
                            break;

                        case "point2d":
                            sb.AppendLine($"uniform vec2 {input.Name};");
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
                            var defCol = GetFloatArray(input.Default, new[] { 1f, 1f, 1f, 1f });
                            parameters.Add(new ColorParameterVm
                            {
                                Name = input.Name,
                                DisplayLabel = label,
                                R = defCol.Length > 0 ? defCol[0] : 1f,
                                G = defCol.Length > 1 ? defCol[1] : 1f,
                                B = defCol.Length > 2 ? defCol[2] : 1f,
                                A = defCol.Length > 3 ? defCol[3] : 1f
                            });
                            break;
                    }
                }
            }

            // Falls der User manuell #version geschrieben hat, entfernen wir es, da wir es oben injizieren
            glslBody = Regex.Replace(glslBody, @"^\s*#version\s+.*$", "", RegexOptions.Multiline);
            sb.AppendLine(glslBody);

            return new IsfParseResult
            {
                ProcessedGlslCode = sb.ToString(),
                Parameters = parameters
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
    }
}
