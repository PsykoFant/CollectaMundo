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

        public void Begin()
        {
            CurrentStepViewModel = new ImportStartViewModel(this);
        }
    }
}
