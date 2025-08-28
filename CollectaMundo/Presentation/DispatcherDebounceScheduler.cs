using CollectaMundo.ApplicationServices.Filtering.CollectaMundo.ApplicationServices.Filtering;
using System.Windows;
using System.Windows.Threading;

namespace CollectaMundo.Presentation
{
    public sealed class DispatcherDebounceScheduler : IFacetUpdateScheduler
    {
        private readonly DispatcherTimer _timer;
        private Action? _pending;

        public DispatcherDebounceScheduler(TimeSpan interval)
        {
            var disp = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            _timer = new DispatcherTimer(DispatcherPriority.Background, disp)
            {
                Interval = interval
            };
            _timer.Tick += (_, __) =>
            {
                _timer.Stop();
                var run = _pending;
                _pending = null;
                run?.Invoke();
            };
        }

        public void Schedule(Action run)
        {
            _pending = run;
            _timer.Stop();
            _timer.Start();
        }

        public void Cancel()
        {
            _pending = null;
            _timer.Stop();
        }
    }
}
