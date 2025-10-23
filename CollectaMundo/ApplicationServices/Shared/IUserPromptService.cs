namespace CollectaMundo.ApplicationServices.Shared
{
    public interface IUserPromptService
    {
        // Prompt handling
        TaskCompletionSource<bool> CreatePrompt();
        void ConfirmPrompt();
        void CancelPendingPrompt();
        bool HasPendingPrompt { get; }

        // Cancellation handling
        CancellationToken GetNewCancellationToken();
        void CancelCurrentOperation();
        void ClearCancellation();
    }

}
