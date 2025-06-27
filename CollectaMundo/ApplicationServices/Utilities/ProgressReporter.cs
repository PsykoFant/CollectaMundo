using CollectaMundo.ViewModels;
using System.Windows;
using System.Windows.Threading;

namespace CollectaMundo.ApplicationServices.Utilities
{
    public sealed class ProgressReporter : IDisposable
    {
        private readonly StatusViewModel _statusVM;
        private readonly int _total;
        private int _current = 0;
        private int _lastPercent = -1;

        public ProgressReporter(StatusViewModel statusVM, int total)
        {
            _statusVM = statusVM;
            _total = Math.Max(total, 1); // prevent divide by zero
            _statusVM.ProgressVisibility = Visibility.Visible;
            _statusVM.ProgressValue = 0;
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
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _statusVM.ProgressValue = percent;
                });
                Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            }
        }

        public void Complete() => ReportProgress(_total);

        public void Dispose()
        {
            Complete();
            _statusVM.ProgressVisibility = Visibility.Collapsed;
        }

    }


}
