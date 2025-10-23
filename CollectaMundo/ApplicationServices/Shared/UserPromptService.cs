using System.Diagnostics;

namespace CollectaMundo.ApplicationServices.Shared
{
    public class UserPromptService : IUserPromptService
    {
        private TaskCompletionSource<bool>? _confirmationTcs;
        private CancellationTokenSource? _cts;

        public bool HasPendingPrompt => _confirmationTcs is { Task.IsCompleted: false };

        public void CancelPendingPrompt()
        {
            if (_confirmationTcs is { Task.IsCompleted: false })
            {
                Debug.WriteLine("[PromptService] Cancelled pending prompt.");
                _confirmationTcs.SetResult(false);
            }
        }

        public TaskCompletionSource<bool> CreatePrompt()
        {
            CancelPendingPrompt();
            _confirmationTcs = new TaskCompletionSource<bool>();
            return _confirmationTcs;
        }
        public void ConfirmPrompt()
        {
            if (_confirmationTcs is { Task.IsCompleted: false })
            {
                Debug.WriteLine("[PromptService] Confirmed prompt.");
                _confirmationTcs.SetResult(true);
            }
        }
        public CancellationToken GetNewCancellationToken()
        {
            ClearCancellation();
            _cts = new CancellationTokenSource();
            return _cts.Token;
        }

        public void CancelCurrentOperation()
        {
            if (_cts is { IsCancellationRequested: false })
            {
                Debug.WriteLine("[PromptService] Cancellation requested.");
                _cts.Cancel();
            }
        }

        public void ClearCancellation()
        {
            _cts?.Dispose();
            _cts = null;
        }
    }

}
