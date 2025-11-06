using CollectaMundo.DomainLogic.Import.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CollectaMundo.ViewModels.ImportSteps
{
    public partial class ImportStep1_StartViewModel(ImportViewModel parent) : ObservableObject, IImportStepViewModel
    {
        private readonly ImportViewModel _parent = parent;
        public string PrimaryActionButtonText => "  Let's go!  \u27A1";
        public string SecondaryActionButtonText => string.Empty; // No secondary action on first screen
        public bool IsCancelEnabled => false;

        [ObservableProperty]
        private bool isSecondaryActionEnabled = false;

        [RelayCommand]
        private async Task PrimaryAction()
        {
            await _parent.AfterStep1Action();
        }
        public void OnSecondaryAction()
        {
            // no-op, no secondary action on first step

        }

        [RelayCommand]
        private void ClearSelectedMapping(ColumnMapping mapping)
        {
            // no-op, no mappings to clear on first step
        }

        [RelayCommand]
        private void Cancel()
        {
            // no-op, cancel is disabled }
        }
    }
}
