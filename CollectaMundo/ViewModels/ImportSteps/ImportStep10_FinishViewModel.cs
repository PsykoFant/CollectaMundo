using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.Import.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;

namespace CollectaMundo.ViewModels.ImportSteps
{
    public partial class ImportStep10_FinishViewModel(ImportViewModel parent) : ObservableObject, IImportStepViewModel
    {
        private readonly ImportViewModel _parent = parent;
        public string PrimaryActionButtonText => "  OK  ";
        public string SecondaryActionButtonText => string.Empty;
        public Visibility SecondaryActionVisibility => Visibility.Collapsed;
        public bool CanExecutePrimaryAction => true;
        public bool CanExecuteSecondaryAction => false;
        public async Task<OperationResult> OnPrimaryAction()
        {
            return await _parent.AfterStep10Action();
        }
        public void OnSecondaryAction()
        {
            // No secondary action
        }

        [RelayCommand]
        private static void ClearSelectedMapping(ColumnMapping mapping)
        {
            // No mapping to clear in the finish step
        }
    }
}
