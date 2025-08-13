namespace CollectaMundo.ApplicationServices.Utilities.Progress;

public sealed record ProgressContext(IProgress<string> Headline, IProgress<string> Detail, IProgress<string> Step, IProgress<int> Percent, IProgress<bool> ProgressBarVisible) : IProgressContext
{
    public static readonly IProgressContext NoOp = new ProgressContext(
        new Progress<string>(_ => { }),
        new Progress<string>(_ => { }),
        new Progress<string>(_ => { }),
        new Progress<int>(_ => { }),
        new Progress<bool>(_ => { })
    );
}
