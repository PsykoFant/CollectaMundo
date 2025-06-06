namespace CollectaMundo.ViewModels
{
    public interface IStartupProgressReporter
    {
        void Report(string message, bool showProgress = false, string? note = null);
        void Hide();
    }

}
