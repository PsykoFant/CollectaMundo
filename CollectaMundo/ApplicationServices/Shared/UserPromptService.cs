using System.Diagnostics;

namespace CollectaMundo.ApplicationServices.Shared
{
    public class UserPromptService : IUserPromptService
    {
        private TaskCompletionSource<bool>? _activePromptCompletion;
        private CancellationTokenSource? _activeOperationCancellation;

        public bool HasActivePrompt => _activePromptCompletion is { Task.IsCompleted: false };

        // User confirmation prompt lifecycle
        public TaskCompletionSource<bool> CreatePrompt()
        {
            DisposeActivePrompt();
            _activePromptCompletion = new TaskCompletionSource<bool>();
            return _activePromptCompletion;
        }
        public void ConfirmActivePrompt()
        {
            if (_activePromptCompletion is { Task.IsCompleted: false })
            {
                Debug.WriteLine("[PromptService] Confirmed prompt.");
                _activePromptCompletion.SetResult(true);
            }
        }
        public void DisposeActivePrompt()
        {
            if (_activePromptCompletion == null)
                return;

            if (!_activePromptCompletion.Task.IsCompleted)
            {
                try
                {
                    _activePromptCompletion.SetResult(false); // Mark task as completed
                    Debug.WriteLine($"[PromptService] Prompt cancelled. Completed: {_activePromptCompletion?.Task.IsCompleted}");
                }
                catch (InvalidOperationException)
                {
                    // Fail-safe: likely already completed
                }
            }

            _activePromptCompletion = null; // Always reset to ensure clean state
        }

        // Operation cancellation lifecycle
        public CancellationToken CreateOperationCancellationToken()
        {
            DisposeOperationCancellationToken();
            _activeOperationCancellation = new CancellationTokenSource();
            return _activeOperationCancellation.Token;
        }
        public void CancelActiveOperation()
        {
            if (_activeOperationCancellation is { IsCancellationRequested: false })
            {
                Debug.WriteLine("[PromptService] Cancellation requested.");
                _activeOperationCancellation.Cancel();
            }
        }
        public void DisposeOperationCancellationToken()
        {
            _activeOperationCancellation?.Dispose();
            _activeOperationCancellation = null;
        }

        // Comprehensive reset for all interaction states
        public void ResetInteractionState()
        {
            DisposeActivePrompt();
            CancelActiveOperation();
            DisposeOperationCancellationToken();
        }
    }

}
