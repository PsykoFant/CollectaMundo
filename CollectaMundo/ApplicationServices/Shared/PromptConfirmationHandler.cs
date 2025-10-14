using System.Diagnostics;

namespace CollectaMundo.ApplicationServices.Shared
{
    public class PromptConfirmationHandler
    {
        private TaskCompletionSource<bool>? _tcs;

        public bool IsPromptActive => _tcs is { Task.IsCompleted: false };

        public void CancelPendingPrompt()
        {
            if (_tcs is { Task.IsCompleted: false })
            {
                Debug.WriteLine("[Prompt] Cancelled pending prompt.");
                _tcs.SetResult(false); // false = not confirmed
            }
        }

        public void ConfirmPrompt()
        {
            if (_tcs is { Task.IsCompleted: false })
            {
                Debug.WriteLine("[Prompt] Confirmed prompt.");
                _tcs.SetResult(true); // true = confirmed
            }
        }

        public async Task<bool> WaitForUserConfirmationAsync()
        {
            CancelPendingPrompt(); // ensures only one active at a time
            _tcs = new TaskCompletionSource<bool>();
            return await _tcs.Task;
        }
    }

}
