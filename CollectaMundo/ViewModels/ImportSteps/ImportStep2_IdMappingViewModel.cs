using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CollectaMundo.ViewModels.ImportSteps
{
    public partial class ImportStep2_IdMappingViewModel(ImportViewModel parent) : ObservableObject, IImportStepViewModel
    {
        private readonly ImportViewModel _parent = parent;
        public string ActionButtonText => "  Proceed  ";
        public bool IsCancelEnabled => true;

        [RelayCommand]
        private void Action()
        {
            _parent.GoToNextStep();
        }

        [RelayCommand]
        private void Cancel()
        {
            // Logic to cancel the import process can be added here if needed
        }

    }

}
