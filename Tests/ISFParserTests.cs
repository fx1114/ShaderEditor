using Microsoft.VisualStudio.TestTools.UnitTesting;
using ShaderEditor.Isf;

namespace Tests;

[TestClass]
public sealed class ISFParserTests
{
    [TestMethod]
    public void ParseAndTranspile_WithoutMetadata_InjectsFragmentAndVertexHeaders()
    {
        const string source = """
            void main()
            {
                gl_FragColor = vec4(1.0);
            }
            """;

        var result = IsfParser.ParseAndTranspile(source);

        Assert.IsNull(result.Error);
        Assert.AreEqual(0, result.Parameters.Count);
        Assert.AreEqual(0, result.Passes.Count);
        StringAssert.Contains(result.ProcessedGlslCode, "#version 330 core");
        StringAssert.Contains(result.ProcessedGlslCode, "out vec4 _isf_FragColor;");
        StringAssert.Contains(result.ProcessedGlslCode, "#define gl_FragColor _isf_FragColor");
        StringAssert.Contains(result.ProcessedGlslCode, "void main()");
        StringAssert.Contains(result.VertexShaderHeader, "#version 330 core");
        StringAssert.Contains(result.VertexShaderHeader, "void isf_vertShaderInit()");
    }

    [TestMethod]
    public void ParseAndTranspile_RemovesUserDefinedVersionDirective()
    {
        const string source = """
            #version 120
            void main()
            {
                gl_FragColor = vec4(0.5);
            }
            """;

        var result = IsfParser.ParseAndTranspile(source);

        Assert.IsNull(result.Error);
        Assert.AreEqual(1, CountOccurrences(result.ProcessedGlslCode, "#version"));
        StringAssert.Contains(result.ProcessedGlslCode, "#version 330 core");
        Assert.IsFalse(result.ProcessedGlslCode.Contains("#version 120"));
    }

    [TestMethod]
    public void ParseAndTranspile_ParsesAllSupportedInputTypesAndPasses()
    {
        const string source = """
            /*{
                "INPUTS": [
                    {
                        "NAME": "amount",
                        "TYPE": "float",
                        "LABEL": "Amount",
                        "DEFAULT": 0.75,
                        "MIN": 0.25,
                        "MAX": 1.5
                    },
                    {
                        "NAME": "enabled",
                        "TYPE": "bool",
                        "DEFAULT": true
                    },
                    {
                        "NAME": "trigger",
                        "TYPE": "event",
                        "DEFAULT": true
                    },
                    {
                        "NAME": "position",
                        "TYPE": "point2D",
                        "DEFAULT": [0.2, 0.8],
                        "MIN": [0.0, 0.1],
                        "MAX": [1.0, 0.9]
                    },
                    {
                        "NAME": "tint",
                        "TYPE": "color",
                        "DEFAULT": [0.1, 0.2, 0.3, 0.4],
                        "MIN": [0.0, 0.1, 0.2, 0.3],
                        "MAX": [0.7, 0.8, 0.9, 1.0]
                    }
                ],
                "PASSES": [
                    {
                        "TARGET": "bufferA",
                        "PERSISTENT": true
                    }
                ]
            }*/
            void main()
            {
                gl_FragColor = vec4(amount);
            }
            """;

        var result = IsfParser.ParseAndTranspile(source);

        Assert.IsNull(result.Error);
        Assert.AreEqual(5, result.Parameters.Count);
        Assert.AreEqual(1, result.Passes.Count);

        var amount = result.Parameters[0] as FloatParameterVm;
        Assert.IsNotNull(amount);
        Assert.AreEqual("amount", amount.Name);
        Assert.AreEqual("Amount", amount.DisplayLabel);
        Assert.AreEqual(0.75f, amount.Value);
        Assert.AreEqual(0.25f, amount.Min);
        Assert.AreEqual(1.5f, amount.Max);

        var enabled = result.Parameters[1] as BoolParameterVm;
        Assert.IsNotNull(enabled);
        Assert.IsTrue(enabled.Value);

        var trigger = result.Parameters[2] as BoolParameterVm;
        Assert.IsNotNull(trigger);
        Assert.IsTrue(trigger.Value);

        var position = result.Parameters[3] as Point2DParameterVm;
        Assert.IsNotNull(position);
        Assert.AreEqual(0.2f, position.X);
        Assert.AreEqual(0.8f, position.Y);
        Assert.AreEqual(0.1f, position.MinY);
        Assert.AreEqual(0.9f, position.MaxY);

        var tint = result.Parameters[4] as ColorParameterVm;
        Assert.IsNotNull(tint);
        Assert.AreEqual(0.1f, tint.R);
        Assert.AreEqual(0.2f, tint.G);
        Assert.AreEqual(0.3f, tint.B);
        Assert.AreEqual(0.4f, tint.A);
        Assert.AreEqual(0.3f, tint.MinA);
        Assert.AreEqual(1.0f, tint.MaxA);

        Assert.AreEqual("bufferA", result.Passes[0].Target);
        Assert.IsTrue(result.Passes[0].Persistent);

        StringAssert.Contains(result.ProcessedGlslCode, "uniform float amount;");
        StringAssert.Contains(result.ProcessedGlslCode, "uniform bool enabled;");
        StringAssert.Contains(result.ProcessedGlslCode, "uniform bool trigger;");
        StringAssert.Contains(result.ProcessedGlslCode, "uniform vec2 position;");
        StringAssert.Contains(result.ProcessedGlslCode, "uniform vec4 tint;");
        StringAssert.Contains(result.ProcessedGlslCode, "uniform sampler2D bufferA;");
        StringAssert.Contains(result.ProcessedGlslCode, "uniform vec2 _bufferA_imgSize;");
        StringAssert.Contains(result.ProcessedGlslCode, "uniform vec4 _bufferA_imgRect;");
        StringAssert.Contains(result.ProcessedGlslCode, "uniform bool _bufferA_flip;");
    }

    [TestMethod]
    public void ParseAndTranspile_UsesFallbacksForMissingValuesAndLabels()
    {
        const string source = """
            /*{
                "INPUTS": [
                    {
                        "NAME": "amount",
                        "TYPE": "FLOAT"
                    },
                    {
                        "NAME": "position",
                        "TYPE": "point2d",
                        "DEFAULT": [0.25]
                    },
                    {
                        "NAME": "tint",
                        "TYPE": "color",
                        "DEFAULT": [0.1, 0.2]
                    }
                ]
            }*/
            void main() { }
            """;

        var result = IsfParser.ParseAndTranspile(source);

        Assert.IsNull(result.Error);

        var amount = (FloatParameterVm)result.Parameters[0];
        Assert.AreEqual("amount", amount.DisplayLabel);
        Assert.AreEqual(0.5f, amount.Value);
        Assert.AreEqual(0.0f, amount.Min);
        Assert.AreEqual(1.0f, amount.Max);

        var position = (Point2DParameterVm)result.Parameters[1];
        Assert.AreEqual(0.5f, position.X);
        Assert.AreEqual(0.5f, position.Y);

        var tint = (ColorParameterVm)result.Parameters[2];
        Assert.AreEqual(0.1f, tint.R);
        Assert.AreEqual(0.2f, tint.G);
        Assert.AreEqual(1.0f, tint.B);
        Assert.AreEqual(1.0f, tint.A);
    }

    [TestMethod]
    public void ParseAndTranspile_AcceptsCommentsTrailingCommasAndCaseInsensitiveProperties()
    {
        const string source = """
            /*{
                // Metadata comment
                "inputs": [
                    {
                        "name": "amount",
                        "type": "float",
                        "default": 0.25,
                    },
                ],
            }*/
            void main() { }
            """;

        var result = IsfParser.ParseAndTranspile(source);

        Assert.IsNull(result.Error);
        Assert.AreEqual(1, result.Parameters.Count);
        Assert.AreEqual("amount", result.Parameters[0].Name);
    }

    [TestMethod]
    public void ParseAndTranspile_IgnoresPassesWithInvalidTargets()
    {
        const string source = """
            /*{
                "PASSES": [
                    { "TARGET": "valid_target" },
                    { "TARGET": "invalid-target" },
                    { "TARGET": "" },
                    { "TARGET": null }
                ]
            }*/
            void main() { }
            """;

        var result = IsfParser.ParseAndTranspile(source);

        Assert.IsNull(result.Error);
        Assert.AreEqual(4, result.Passes.Count);
        StringAssert.Contains(result.ProcessedGlslCode, "uniform sampler2D valid_target;");
        Assert.IsFalse(result.ProcessedGlslCode.Contains("invalid-target"));
    }

    [TestMethod]
    public void ParseAndTranspile_ReturnsErrorForInvalidJsonHeader()
    {
        const string source = """
            /*{
                "INPUTS": "not-an-array"
            }*/
            void main() { }
            """;

        var result = IsfParser.ParseAndTranspile(source);

        Assert.IsNotNull(result.Error);
        StringAssert.StartsWith(result.Error, "JSON-Header Fehler:");
        Assert.AreEqual(string.Empty, result.ProcessedGlslCode);
        Assert.AreEqual(0, result.Parameters.Count);
    }

    [TestMethod]
    public void ParseAndTranspile_ReturnsErrorForInvalidFloatRange()
    {
        const string source = """
            /*{
                "INPUTS": [
                    {
                        "NAME": "amount",
                        "TYPE": "float",
                        "MIN": 2.0,
                        "MAX": 1.0
                    }
                ]
            }*/
            void main() { }
            """;

        var result = IsfParser.ParseAndTranspile(source);

        Assert.IsNotNull(result.Error);
        StringAssert.Contains(result.Error, "Ungültiger Wertebereich");
        StringAssert.Contains(result.Error, "'amount'");
        StringAssert.Contains(result.Error, "MIN (2");
        StringAssert.Contains(result.Error, "MAX (1");
        Assert.AreEqual(0, result.Parameters.Count);
    }

    [TestMethod]
    public void ParseAndTranspile_ReturnsErrorForInvalidPointRangeComponent()
    {
        const string source = """
            /*{
                "INPUTS": [
                    {
                        "NAME": "position",
                        "TYPE": "point2D",
                        "MIN": [0.0, 0.9],
                        "MAX": [1.0, 0.1]
                    }
                ]
            }*/
            void main() { }
            """;

        var result = IsfParser.ParseAndTranspile(source);

        Assert.IsNotNull(result.Error);
        StringAssert.Contains(result.Error, "'position'");
        StringAssert.Contains(result.Error, "(Y)");
    }

    [TestMethod]
    public void ParseAndTranspile_ReturnsErrorForInvalidColorRangeComponent()
    {
        const string source = """
            /*{
                "INPUTS": [
                    {
                        "NAME": "tint",
                        "TYPE": "color",
                        "MIN": [0.0, 0.1, 0.9, 0.0],
                        "MAX": [1.0, 0.8, 0.1, 1.0]
                    }
                ]
            }*/
            void main() { }
            """;

        var result = IsfParser.ParseAndTranspile(source);

        Assert.IsNotNull(result.Error);
        StringAssert.Contains(result.Error, "'tint'");
        StringAssert.Contains(result.Error, "(B)");
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;

        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}