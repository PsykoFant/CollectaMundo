namespace CollectaMundo.ApplicationServices.Shared.Progress
{
    public sealed class ProgressReporter : IDisposable
    {
        private readonly IProgress<int> _progress;
        private readonly int _total;
        private int _current = 0;
        private int _lastPercent = -1;
        public ProgressReporter(IProgress<int> progress, int total)
        {
            _progress = progress;
            _total = Math.Max(total, 1);
            _progress.Report(0); // start at 0%
        }
        public void Increment()
        {
            int value = Interlocked.Increment(ref _current);
            ReportProgress(value);
        }

        private void ReportProgress(int done)
        {
            int percent = (int)((double)done / _total * 100);
            if (percent != _lastPercent)
            {
                _lastPercent = percent;
                _progress.Report(percent);
            }
        }

        public void Complete() => ReportProgress(_total);

        public void Dispose()
        {
            Complete();
        }
    }
}
