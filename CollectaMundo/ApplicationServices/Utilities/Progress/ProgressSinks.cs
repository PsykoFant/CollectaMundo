namespace CollectaMundo.ApplicationServices.Utilities.Progress;
public sealed record ProgressSinks
{
    public required IProgress<string> Headline { get; init; }
    public required IProgress<string> Detail { get; init; }
    public required IProgress<string> Step { get; init; }
    public required IProgress<int> Percent { get; init; }
    public required IProgress<bool> ProgressBarVisible { get; init; }

    public static readonly ProgressSinks NoOp = new()
    {
        Headline = new Progress<string>(_ => { }),
        Detail = new Progress<string>(_ => { }),
        Step = new Progress<string>(_ => { }),
        Percent = new Progress<int>(_ => { }),
        ProgressBarVisible = new Progress<bool>(_ => { })
    };
}
