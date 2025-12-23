using CollectaMundo.ApplicationServices.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows;

namespace CollectaMundo.ViewModels.Import.ImportSteps
{
    public partial class ImportStep10_FinishViewModel : ObservableObject, IImportStepViewModel
    {
        private readonly ImportViewModel _parent;

        // --------------------------------------------
        // Constructor
        // --------------------------------------------
        public ImportStep10_FinishViewModel(ImportViewModel parent)
        {
            _parent = parent;

            Initialize();
            HookEvents();
        }

        // --------------------------------------------
        // Initialization (empty for this step)
        // --------------------------------------------
        private void Initialize()
        {
            // Step 1 has no per-item mappings or dynamic data to initialize.
            // FlowDocumentVisibility is already defaulted via ObservableProperty.
        }

        private void HookEvents()
        {
            // Step 1 has no dynamic collections or item-level events.
        }

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
        public async Task<OperationResult> OnPrimaryAction() => await _parent.AfterStep10Action();

        public void OnSecondaryAction()
        {
            // Not used in this step (and SecondaryActionVisibility is Collapsed).
        }

        // --------------------------------------------
        // Commands (none for this step)
        // --------------------------------------------

        // --------------------------------------------
        // Private helper methods (none needed)
        // --------------------------------------------
    }
}
