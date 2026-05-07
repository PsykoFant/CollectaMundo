using CollectaMundo.ApplicationServices.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows;

namespace CollectaMundo.ViewModels.Import.ImportSteps
{
    public partial class ImportStep11_FinishViewModel(ImportViewModel parent) : ObservableObject, IImportStepViewModel
    {
        private readonly ImportViewModel _parent = parent;

        // --------------------------------------------
        // Initialization (empty for this step)
        // --------------------------------------------


        // --------------------------------------------
        // UI Text & Visibility
        // --------------------------------------------
        public string PrimaryActionButtonText => "   OK   ";
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
        public async Task<OperationResult> OnPrimaryAction() => await _parent.AfterStep11Action();

    }
}
