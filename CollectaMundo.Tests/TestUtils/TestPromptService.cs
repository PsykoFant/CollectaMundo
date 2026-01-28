using CollectaMundo.ApplicationServices.Shared;

namespace CollectaMundo.Tests.TestUtils
{
    internal sealed class TestPromptService : IUserPromptService
    {
        private readonly string _fakeCsvPath;

        public TestPromptService(string fakeCsvPath)
        {
            _fakeCsvPath = fakeCsvPath;
        }

        public TaskCompletionSource<bool> CreatePrompt()
        {
            var tcs = new TaskCompletionSource<bool>();
            tcs.SetResult(true); // Immediately complete
            return tcs;
        }

        public string PromptForCsvFile() => _fakeCsvPath;

        public CancellationToken GetNewCancellationToken() => CancellationToken.None;

        public void CancelCurrentOperation() { }
        public void ClearCancellation() { }
        public void CancelPendingPrompt() { }
        public void ConfirmPrompt() { }
        public bool HasPendingPrompt => false;

    }
}
