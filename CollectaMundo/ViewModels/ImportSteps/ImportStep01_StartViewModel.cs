using CollectaMundo.ApplicationServices.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows;

namespace CollectaMundo.ViewModels.ImportSteps
{
    public partial class ImportStep01_StartViewModel(ImportViewModel parent) : ObservableObject, IImportStepViewModel
    {
        private readonly ImportViewModel _parent = parent;

        // --------------------------------------------
        // UI Text & Visibility
        // --------------------------------------------
        public string PrimaryActionButtonText => "  Let's go!  \u27A1";
        public string SecondaryActionButtonText => string.Empty;
        public Visibility PrimaryActionVisibility => Visibility.Visible;
        public Visibility SecondaryActionVisibility => Visibility.Collapsed;

        [ObservableProperty]
        private Visibility stepContentVisibility = Visibility.Visible;

        // --------------------------------------------
        // Step-level button enablement
        // --------------------------------------------
        public bool CanExecutePrimaryAction => true;
        public bool CanExecuteSecondaryAction => false;

        // --------------------------------------------
        // Actions
        // --------------------------------------------
        public async Task<OperationResult> OnPrimaryAction()
        {
            StepContentVisibility = Visibility.Collapsed;
            return await _parent.AfterStep1Action();
        }

    }
}
