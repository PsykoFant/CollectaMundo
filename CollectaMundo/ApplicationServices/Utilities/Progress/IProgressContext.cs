namespace CollectaMundo.ApplicationServices.Utilities.Progress
{
    public interface IProgressContext
    {
        IProgress<string> Headline { get; }
        IProgress<string> Detail { get; }
        IProgress<string> Step { get; }
        IProgress<int> Percent { get; }
        IProgress<bool> ProgressBarVisible { get; }
    }
}
