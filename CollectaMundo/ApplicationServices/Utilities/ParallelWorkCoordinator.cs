using CollectaMundo.ViewModels;
using System.Collections.Concurrent;

namespace CollectaMundo.ApplicationServices.Utilities
{
    public sealed class ParallelWorkCoordinator<T>(StatusViewModel statusVM, int total, int maxDegreeOfParallelism) : IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
        private readonly ProgressReporter _reporter = new ProgressReporter(statusVM, total);
        public readonly ConcurrentBag<T> Results = [];
        public int Total { get; } = total;

        public async Task DoAsync(Func<Task<T>> work)
        {
            await _semaphore.WaitAsync();
            try
            {
                var result = await work();
                Results.Add(result);
            }
            finally
            {
                _reporter.Increment();
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
            _reporter.Dispose();
            _semaphore.Dispose();
        }
    }

}
