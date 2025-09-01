using CollectaMundo.ApplicationServices.Utilities.Progress;
using System.Collections.Concurrent;

namespace CollectaMundo.ApplicationServices.Utilities
{
    public sealed class ParallelWorkCoordinator<T>(IProgress<int> percentProgress, int total, int maxDegreeOfParallelism) : IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new(maxDegreeOfParallelism);
        private readonly ProgressReporter _reporter = new(percentProgress, total);

        public ConcurrentBag<T> Results { get; } = [];
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
