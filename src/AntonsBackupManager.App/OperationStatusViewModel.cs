namespace AntonsBackupManager.App;

internal sealed class OperationStatusViewModel : ObservableModel
{
    private string key = "Ready";
    private object[] arguments = [];
    private Exception? error;
    public string Text => UiText.Get(key, arguments);
    public string Details => error is null ? "" : ErrorPresentation.Details(error);
    public bool HasError => error is not null;
    public void SetStatus(string statusKey, params object[] args)
    {
        key = statusKey; arguments = args; error = null; Refresh();
    }
    public void ShowError(Exception value)
    {
        key = ErrorPresentation.SummaryKey(value); arguments = []; error = value; Refresh();
    }
    public void Refresh() => Changed(null);
}
