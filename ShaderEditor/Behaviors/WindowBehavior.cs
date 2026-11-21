using ShaderEditor.ViewModels;
using System.Windows;

namespace ShaderEditor.Behaviors;

public static class WindowBehavior
{
    public static readonly DependencyProperty ConfirmCloseProperty =
        DependencyProperty.RegisterAttached(
            "ConfirmClose",
            typeof(bool),
            typeof(WindowBehavior),
            new PropertyMetadata(false, OnConfirmCloseChanged));

    public static bool GetConfirmClose(DependencyObject element)
        => (bool)element.GetValue(ConfirmCloseProperty);

    public static void SetConfirmClose(DependencyObject element, bool value)
        => element.SetValue(ConfirmCloseProperty, value);

    private static void OnConfirmCloseChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not Window window)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            window.Closing += Window_Closing;
        }
        else
        {
            window.Closing -= Window_Closing;
        }
    }

    private static void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (sender is Window { DataContext: MainWindowViewModel viewModel } && !viewModel.CanClose())
        {
            e.Cancel = true;
        }
    }
}
