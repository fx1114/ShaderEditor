namespace ShaderEditor.Services;

public interface IMessageService
{
    ConfirmResult ConfirmDiscardChanges();
    bool ConfirmDelete(string message);
    bool ConfirmOverwrite(string message);
    void ShowError(string message, string title);
}

public enum ConfirmResult
{
    Save,
    Discard,
    Cancel
}
