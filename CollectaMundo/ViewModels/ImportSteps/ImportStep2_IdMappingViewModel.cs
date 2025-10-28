using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CollectaMundo.ViewModels.ImportSteps
{
    public partial class ImportStep2_IdMappingViewModel(ImportViewModel parent) : ObservableObject, IImportStepViewModel
    {
        private readonly ImportViewModel _parent = parent;
        public string PrimaryActionButtonText => "  Proceed  \u27A1";
        public string SecondaryActionButtonText => "  Skip  \u23ED";
        public bool IsCancelEnabled => true;
        public bool IsSecondaryActionEnabled => true;

        [RelayCommand]
        private void PrimaryAction()
        {
            _parent.GoToNextStep();
        }

        [RelayCommand]
        private void SecondaryAction()
        {
            _parent.GoToNextStep();
        }

        [RelayCommand]
        private void Cancel()
        {
            // Logic to handle cancellation of the import process
        }

    }

}
