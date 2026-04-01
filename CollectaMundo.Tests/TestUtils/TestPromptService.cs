using CollectaMundo.ApplicationServices.Shared;

namespace CollectaMundo.Tests.TestUtils
{
    internal sealed class TestPromptService() : IUserPromptService
    {
        public TaskCompletionSource<bool> CreatePrompt()
        {
            var tcs = new TaskCompletionSource<bool>();
            tcs.SetResult(true); // Immediately complete
            return tcs;
        }
        public CancellationToken CreateOperationCancellationToken() => CancellationToken.None;
        public void CancelActiveOperation() { }
        public void DisposeOperationCancellationToken() { }
        public void DisposeActivePrompt() { }
        public void ConfirmActivePrompt() { }
        public bool HasActivePrompt => false;
        public void ResetInteractionState() { }
    }
}
