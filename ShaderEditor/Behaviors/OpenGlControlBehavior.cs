using OpenTK.Wpf;
using ShaderEditor.Services;
using ShaderEditor.ViewModels;
using System.Windows;

namespace ShaderEditor.Behaviors;

public static class OpenGlControlBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(OpenGlControlBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element)
        => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value)
        => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not GLWpfControl control)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            control.Loaded += Control_Loaded;
            control.Unloaded += Control_Unloaded;
        }
        else
        {
            control.Loaded -= Control_Loaded;
            control.Unloaded -= Control_Unloaded;
        }
    }

    private static void Control_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not GLWpfControl control || control.DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        control.Start(new GLWpfControlSettings { MajorVersion = 3, MinorVersion = 3 });
        viewModel.InitializeRenderer();

        Action<TimeSpan> renderHandler = delta =>
            viewModel.Render(delta, control.ActualWidth, control.ActualHeight);
        control.Render += renderHandler;
    }

    private static void Control_Unloaded(object sender, RoutedEventArgs e)
    {
    }
}
