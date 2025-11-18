using CollectaMundo.ApplicationServices.Shared;
using System.Windows;

namespace CollectaMundo.ViewModels.ImportSteps
{
    public interface IImportStepViewModel
    {
        // Actions to be performed when the primary and secondary buttons are clicked
        Task<OperationResult> OnPrimaryAction();
        void OnSecondaryAction() { } // <-- default no-op

        // Properties for button texts
        string PrimaryActionButtonText { get; }
        string SecondaryActionButtonText { get; }

        // Properties to determine if actions can be executed
        bool CanExecutePrimaryAction { get; }
        bool CanExecuteSecondaryAction { get; }

        // Property for secondary action visibility
        Visibility SecondaryActionVisibility { get; }

        //// Command to clear selected mapping
        //IRelayCommand<IdColumnMapping> ClearSelectedMappingCommand { get; }

    }
}
