using CollectaMundo.ApplicationServices.Shared;

namespace CollectaMundo.Tests.TestUtils
{
    internal sealed class TestPromptService() : IUserPromptService
    {
        public TaskCompletionSource<bool> BeginPrompt()
        {
            var tcs = new TaskCompletionSource<bool>();
            tcs.SetResult(true); // Immediately complete
            return tcs;
        }
        public CancellationToken StartOperationCancellation() => CancellationToken.None;
        public void CancelActiveOperation() { }
        public void EndOperationCancellation() { }
        public void CancelActivePrompt() { }
        public void ConfirmActivePrompt() { }
        public bool HasActivePrompt => false;

    }
}
