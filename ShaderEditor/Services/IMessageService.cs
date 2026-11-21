namespace ShaderEditor.Services;

public interface IMessageService
{
    ConfirmResult ConfirmDiscardChanges();
    void ShowError(string message, string title);
}

public enum ConfirmResult
{
    Save,
    Discard,
    Cancel
}
