using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Xml;

namespace ShaderEditor.Behaviors;

public static class AvalonEditBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(AvalonEditBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.RegisterAttached(
            "Text",
            typeof(string),
            typeof(AvalonEditBehavior),
            new FrameworkPropertyMetadata(string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnTextChanged));

    public static bool GetIsEnabled(DependencyObject element)
        => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value)
        => element.SetValue(IsEnabledProperty, value);

    public static string GetText(DependencyObject element)
        => (string)element.GetValue(TextProperty);

    public static void SetText(DependencyObject element, string value)
        => element.SetValue(TextProperty, value);

    private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not TextEditor editor)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            editor.TextChanged += Editor_TextChanged;
            editor.Loaded += Editor_Loaded;
        }
        else
        {
            editor.TextChanged -= Editor_TextChanged;
            editor.Loaded -= Editor_Loaded;
        }
    }

    /*
    private static void TextArea_OnSelectionChanged(object? sender, EventArgs e)
    {
        Selection? selection = (sender as TextArea)?.Selection;
        if (selection != null)
        {
            MessageBox.Show("Selection changed\n Start line: " + selection.StartPosition.Line + " column: " + selection.StartPosition.Column
                + "\n End line: " + selection.EndPosition.Line + " column: " + selection.EndPosition.Column);
        }
    }
    */

    private static void OnTextChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is TextEditor editor && e.NewValue is string text && editor.Text != text)
        {
            editor.Text = text;
        }
    }

    private static void Editor_TextChanged(object? sender, EventArgs e)
    {
        if (sender is TextEditor editor)
        {
            editor.SetCurrentValue(TextProperty, editor.Text);
        }
    }

    private static void Editor_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TextEditor editor)
        {
            return;
        }

        var resourceName = "ShaderEditor.Syntax.IsfGlsl.xshd";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            return;
        }

        using var reader = new XmlTextReader(stream);
        editor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
        editor.LineNumbersForeground = new SolidColorBrush(Color.FromRgb(133, 133, 133));
        editor.TextArea.SelectionBrush = new SolidColorBrush(Color.FromArgb(100, 38, 79, 120));
        editor.TextArea.TextView.CurrentLineBackground = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
        editor.TextArea.TextView.CurrentLineBorder = new Pen(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 1);
        editor.Options.HighlightCurrentLine = true;
    }
}
