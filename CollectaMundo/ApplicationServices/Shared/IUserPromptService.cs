namespace CollectaMundo.ApplicationServices.Shared
{
    public interface IUserPromptService
    {
        // Prompt handling
        TaskCompletionSource<bool> BeginPrompt();
        void ConfirmActivePrompt();
        void CancelActivePrompt();
        bool HasActivePrompt { get; }

        // Cancellation handling
        CancellationToken StartOperationCancellation();
        void CancelActiveOperation();
        void EndOperationCancellation();

        // Total state reset
        void ResetInteractionState();
    }

}
