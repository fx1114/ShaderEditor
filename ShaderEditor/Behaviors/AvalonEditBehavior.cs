using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Xml;

namespace ShaderEditor.Behaviors;

public static class AvalonEditBehavior
{
    private static readonly Regex NumberLiteralRegex = new(
        @"(?<![A-Za-z0-9_])(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?[fF]?(?![A-Za-z0-9_])",
        RegexOptions.Compiled);

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

    public static readonly DependencyProperty HoverTokenProperty =
        DependencyProperty.RegisterAttached(
            "HoverToken",
            typeof(string),
            typeof(AvalonEditBehavior),
            new FrameworkPropertyMetadata(string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static bool GetIsEnabled(DependencyObject element)
        => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value)
        => element.SetValue(IsEnabledProperty, value);

    public static string GetText(DependencyObject element)
        => (string)element.GetValue(TextProperty);

    public static void SetText(DependencyObject element, string value)
        => element.SetValue(TextProperty, value);

    public static string GetHoverToken(DependencyObject element)
        => (string)element.GetValue(HoverTokenProperty);

    public static void SetHoverToken(DependencyObject element, string value)
        => element.SetValue(HoverTokenProperty, value);

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
            editor.MouseMove += Editor_MouseMove;
            editor.MouseLeave += Editor_MouseLeave;
        }
        else
        {
            editor.TextChanged -= Editor_TextChanged;
            editor.Loaded -= Editor_Loaded;
            editor.MouseMove -= Editor_MouseMove;
            editor.MouseLeave -= Editor_MouseLeave;
        }
    }

    private static void OnTextChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is TextEditor editor && e.NewValue is string text && editor.Text != text)
        {
            editor.Text = text;
        }
    }

    private static void Editor_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is not TextEditor editor || editor.Document == null)
        {
            return;
        }

        var position = editor.GetPositionFromPoint(e.GetPosition(editor));
        if (position == null)
        {
            SetCurrentHoverToken(editor, string.Empty);
            return;
        }

        var offset = editor.Document.GetOffset(position.Value.Location);
        var match = NumberLiteralRegex.Matches(editor.Document.Text)
            .Cast<Match>()
            .FirstOrDefault(candidate => candidate.Index <= offset && offset < candidate.Index + candidate.Length);

        SetCurrentHoverToken(editor, match == null
            ? string.Empty
            : $"{match.Index}:{match.Length}");
    }

    private static void Editor_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is TextEditor editor)
        {
            SetCurrentHoverToken(editor, string.Empty);
        }
    }

    private static void SetCurrentHoverToken(TextEditor editor, string token)
    {
        if (!string.Equals(GetHoverToken(editor), token, StringComparison.Ordinal))
        {
            editor.SetCurrentValue(HoverTokenProperty, token);
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
