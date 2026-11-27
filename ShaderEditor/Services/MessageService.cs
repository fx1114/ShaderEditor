using System.Windows;

namespace ShaderEditor.Services;

public sealed class MessageService : IMessageService
{
    public ConfirmResult ConfirmDiscardChanges()
        => MessageBox.Show(
            "Es gibt ungespeicherte Änderungen. Möchtest du diese vorher speichern?",
            "Ungespeicherte Änderungen",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning) switch
        {
            MessageBoxResult.Yes => ConfirmResult.Save,
            MessageBoxResult.No => ConfirmResult.Discard,
            _ => ConfirmResult.Cancel
        };

    public bool ConfirmDelete(string message)
        => MessageBox.Show(
            message,
            "Datei löschen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;

    public bool ConfirmOverwrite(string message)
        => MessageBox.Show(
            message,
            "Datei überschreiben",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;

    public void ShowError(string message, string title)
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
}
