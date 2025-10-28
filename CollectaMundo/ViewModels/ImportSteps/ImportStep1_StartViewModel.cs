using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CollectaMundo.ViewModels.ImportSteps
{
    public partial class ImportStep1_StartViewModel(ImportViewModel parent) : ObservableObject, IImportStepViewModel
    {
        private readonly ImportViewModel _parent = parent;

        public string ActionButtonText => "Let's go!";
        public bool IsCancelEnabled => false; // Disabled on first screen

        [RelayCommand]
        private void Action()
        {
            _parent.GoToNextStep();
        }


        [RelayCommand]
        private void Cancel()
        {
            // no-op, cancel is disabled }
        }

    }
}
