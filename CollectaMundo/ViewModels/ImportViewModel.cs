using CollectaMundo.ViewModels.ImportSteps;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows;

namespace CollectaMundo.ViewModels
{
    public partial class ImportViewModel : ObservableObject
    {
        [ObservableProperty]
        private Visibility importOverlayVisibility = Visibility.Collapsed;

        [ObservableProperty]
        private object? currentStepViewModel;

        public void StartImportWizard()
        {
            CurrentStepViewModel = new ImportStartViewModel(this);
        }

        public void GoToNextStep()
        {
            // Example: advance through enum or step logic
            //CurrentStepViewModel = new ImportIdMappingViewModel(this);
        }

        public void CancelImport()
        {
            ImportOverlayVisibility = Visibility.Collapsed;
            CurrentStepViewModel = null;
        }
    }
}
