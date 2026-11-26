using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

 namespace ShaderEditor.Isf
{
    // --- JSON Deserialisierungs-Klassen ---
    public class IsfMetadata
    {
        [JsonPropertyName("DESCRIPTION")]
        public string? Description { get; set; }

        [JsonPropertyName("CREDIT")]
        public string? Credit { get; set; }

        [JsonPropertyName("INPUTS")]
        public List<IsfInputDefinition> Inputs { get; set; } = new();

        [JsonPropertyName("PASSES")]
        public List<IsfPassDefinition> Passes { get; set; } = new();
    }

    public class IsfPassDefinition
    {
        [JsonPropertyName("TARGET")]
        public string? Target { get; set; }

        [JsonPropertyName("PERSISTENT")]
        public bool Persistent { get; set; }

        [JsonPropertyName("FLOAT")]
        public bool Float { get; set; }

        [JsonPropertyName("WIDTH")]
        public JsonElement? Width { get; set; }

        [JsonPropertyName("HEIGHT")]
        public JsonElement? Height { get; set; }
    }

    public class IsfInputDefinition
    {
        [JsonPropertyName("NAME")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("TYPE")]
        public string Type { get; set; } = "float"; // float, bool, long, event, point2D, color

        [JsonPropertyName("LABEL")]
        public string? Label { get; set; }

        [JsonPropertyName("DEFAULT")]
        public JsonElement? Default { get; set; }

        [JsonPropertyName("MIN")]
        public JsonElement? Min { get; set; }

        [JsonPropertyName("MAX")]
        public JsonElement? Max { get; set; }

        [JsonPropertyName("VALUES")]
        public List<int>? Values { get; set; }

        [JsonPropertyName("LABELS")]
        public List<string>? Labels { get; set; }
    }

    // --- UI-ViewModels für die dynamischen Controls ---
    public abstract class IsfParameterViewModel : INotifyPropertyChanged
    {
        private bool _isVisible = true;
        public string Name { get; init; } = string.Empty;
        public string DisplayLabel { get; init; } = string.Empty;
        public bool IsVisible { get => _isVisible; set { _isVisible = value; OnPropertyChanged(); } }

        public abstract override string ToString();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? prop = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }

    public class FloatParameterVm : IsfParameterViewModel
    {
        private float _value;
        public float Value { get => _value; set { _value = value; OnPropertyChanged(); } }
        public float Min { get; init; } = 0f;
        public float Max { get; init; } = 1f;
        public override string ToString() => Value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    public class BoolParameterVm : IsfParameterViewModel
    {
        private bool _value;
        public bool Value { get => _value; set { _value = value; OnPropertyChanged(); } }
        public override string ToString() => Value ? "true" : "false";
    }

    public class Point2DParameterVm : IsfParameterViewModel
    {
        private float _x;
        private float _y;
        public float X { get => _x; set { _x = value; OnPropertyChanged(); } }
        public float Y { get => _y; set { _y = value; OnPropertyChanged(); } }
        public float MinX { get; init; } = 0f;
        public float MaxX { get; init; } = 1f;
        public float MinY { get; init; } = 0f;
        public float MaxY { get; init; } = 1f;
        public override string ToString() => FormattableString.Invariant($"({X:0.###}, {Y:0.###})");
    }

    public class ColorParameterVm : IsfParameterViewModel
    {
        private float _r = 1f, _g = 1f, _b = 1f, _a = 1f;
        public float R { get => _r; set { _r = value; OnPropertyChanged(); } }
        public float G { get => _g; set { _g = value; OnPropertyChanged(); } }
        public float B { get => _b; set { _b = value; OnPropertyChanged(); } }
        public float A { get => _a; set { _a = value; OnPropertyChanged(); } }
        public float MinR { get; init; } = 0f;
        public float MaxR { get; init; } = 1f;
        public float MinG { get; init; } = 0f;
        public float MaxG { get; init; } = 1f;
        public float MinB { get; init; } = 0f;
        public float MaxB { get; init; } = 1f;
        public float MinA { get; init; } = 0f;
        public float MaxA { get; init; } = 1f;
        public override string ToString() => FormattableString.Invariant($"vec4({R:0.###}, {G:0.###}, {B:0.###}, {A:0.###})");
    }
}
