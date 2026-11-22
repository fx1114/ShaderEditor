using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;

namespace ShaderEditor.Windows;

public partial class NewParameterDialog : Window
{
    public JsonObject? ParameterDefinition { get; private set; }

    public NewParameterDialog()
    {
        InitializeComponent();
        UpdateFields();
        NameTextBox.Focus();
    }

    private void TypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsInitialized)
        {
            UpdateFields();
        }
    }

    private void UpdateFields()
    {
        var type = SelectedType;
        var isBool = type == "bool";
        var isVector = type is "point2D" or "color";

        MinLabel.IsEnabled = !isBool;
        MinTextBox.IsEnabled = !isBool;
        MaxLabel.IsEnabled = !isBool;
        MaxTextBox.IsEnabled = !isBool;
        DefaultLabel.Text = isVector ? "Default (Werte):" : "Default:";
        MinLabel.Text = isVector ? "Minimum (Werte):" : "Minimum:";
        MaxLabel.Text = isVector ? "Maximum (Werte):" : "Maximum:";

        if (isBool && string.IsNullOrWhiteSpace(DefaultTextBox.Text))
        {
            DefaultTextBox.Text = "false";
        }
        else if (isVector && type == "point2D" && DefaultTextBox.Text is "0.5" or "false")
        {
            DefaultTextBox.Text = "0.5;0.5";
        }
        else if (isVector && type == "color" && DefaultTextBox.Text is "0.5" or "false")
        {
            DefaultTextBox.Text = "1;1;1;1";
        }
    }

    private string SelectedType => (TypeComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "float";

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorTextBlock.Text = string.Empty;
        var name = NameTextBox.Text.Trim();
        var label = LabelTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowError("Bitte einen Namen eingeben.");
            return;
        }

        try
        {
            var type = SelectedType;
            var definition = new JsonObject
            {
                ["NAME"] = name,
                ["TYPE"] = type,
                ["LABEL"] = label,
                ["DEFAULT"] = ParseValue(DefaultTextBox.Text, type)
            };

            if (type != "bool")
            {
                definition["MIN"] = ParseValue(MinTextBox.Text, type);
                definition["MAX"] = ParseValue(MaxTextBox.Text, type);
            }

            ParameterDefinition = definition;
            DialogResult = true;
        }
        catch (FormatException ex)
        {
            ShowError(ex.Message);
        }
    }

    private static JsonNode ParseValue(string text, string type)
    {
        if (type == "bool")
        {
            if (bool.TryParse(text.Trim(), out var value))
            {
                return JsonValue.Create(value)!;
            }

            throw new FormatException("Der Default-Wert muss true oder false sein.");
        }

        if (type is "point2D" or "color")
        {
            var expectedCount = type == "point2D" ? 2 : 4;
            var values = text.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (values.Length != expectedCount || values.Any(value => !TryParseNumber(value, out _)))
            {
                throw new FormatException($"Für {type} werden {expectedCount} Zahlen mit Semikolon getrennt erwartet.");
            }

            var array = new JsonArray();
            foreach (var value in values)
            {
                TryParseNumber(value, out var number);
                array.Add(number);
            }

            return array;
        }

        if (TryParseNumber(text, out var scalar))
        {
            return JsonValue.Create(scalar)!;
        }

        throw new FormatException("Bitte eine gültige Zahl eingeben.");
    }

    private static bool TryParseNumber(string text, out float value)
        => float.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private void ShowError(string message)
    {
        ErrorTextBlock.Text = message;
    }
}
