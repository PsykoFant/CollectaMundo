namespace CollectaMundo.ApplicationServices.Shared
{
    public interface IUserPromptService
    {
        // Prompt handling
        TaskCompletionSource<bool> CreatePrompt();
        void ConfirmActivePrompt();
        void DisposeActivePrompt();
        bool HasActivePrompt { get; }

        // Cancellation handling
        CancellationToken CreateOperationCancellationToken();
        void CancelActiveOperation();
        void DisposeOperationCancellationToken();

        // Total state reset
        void ResetInteractionState();
    }

}
