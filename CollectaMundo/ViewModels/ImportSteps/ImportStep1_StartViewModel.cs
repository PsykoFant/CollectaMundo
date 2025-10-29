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
        public bool IsSecondaryActionEnabled => false;

        [RelayCommand]
        private void PrimaryAction()
        {
            _parent.Step1ToStep2();
            _parent.SetUiBusy(true);
        }

        [RelayCommand]
        private void SecondaryAction()
        {
            // no-op, secondary action is disabled
        }

        [RelayCommand]
        private void Cancel()
        {
            // no-op, cancel is disabled }
        }
    }
}
