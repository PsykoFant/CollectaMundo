using CollectaMundo.ApplicationServices.Shared;
using System.Windows;

namespace CollectaMundo.ViewModels.ImportSteps
{
    public interface IImportStepViewModel
    {
        // Actions
        Task<OperationResult> OnPrimaryAction();
        void OnSecondaryAction() { }

        // Button texts
        string PrimaryActionButtonText { get; }
        string SecondaryActionButtonText { get; }

        // Button enabled state
        bool CanExecutePrimaryAction { get; }
        bool CanExecuteSecondaryAction { get; }

        // Visibilites of action buttons
        Visibility PrimaryActionVisibility { get; }
        Visibility SecondaryActionVisibility { get; }

        // NEW: Visibility of main interactive UI for the step
        Visibility StepContentVisibility { get; set; }
    }

}
