namespace CollectaMundo.ApplicationServices.Utilities.Progress
{
    public sealed class ProgressContext(IProgress<SetupProgress> p) : IProgressContext
    {
        public static readonly IProgressContext NoOp = new ProgressContext(new Progress<SetupProgress>(_ => { }));
        public IProgress<string> Headline { get; } = new Progress<string>(s => p.Report(new SetupProgress(Headline: s)));
        public IProgress<string> Detail { get; } = new Progress<string>(s => p.Report(new SetupProgress(Detail: s)));
        public IProgress<string> Step { get; } = new Progress<string>(s => p.Report(new SetupProgress(Step: s)));
        public IProgress<int> Percent { get; } = new Progress<int>(v => p.Report(new SetupProgress(Percent: v)));
        public IProgress<bool> ProgressBarVisible { get; } = new Progress<bool>(v => p.Report(new SetupProgress(IsProgressVisible: v)));
    }
}
