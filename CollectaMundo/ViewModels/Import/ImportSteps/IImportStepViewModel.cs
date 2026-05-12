using CollectaMundo.ApplicationServices.Shared;
using System.Windows;

namespace CollectaMundo.ViewModels.Import.ImportSteps
{
    public interface IImportStepViewModel
    {
        // Actions
        Task<OperationResult> OnPrimaryAction();
        Task<OperationResult> OnSecondaryAction() => Task.FromResult(new OperationResult(OperationResultCode.NoOp, string.Empty));

        // Button texts
        string PrimaryActionButtonText { get; }
        string SecondaryActionButtonText { get; }

        // Button enabled state
        bool CanExecutePrimaryAction { get; }
        bool CanExecuteSecondaryAction { get; }

        // Visibilites of action buttons
        bool IsPrimaryActionVisible { get; }
        bool IsSecondaryActionVisible { get; }

        // Visibility of main interactive UI for the step
        bool IsStepContentVisible { get; set; }
    }

}
