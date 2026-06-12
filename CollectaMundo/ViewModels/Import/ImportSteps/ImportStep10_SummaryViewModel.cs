using CollectaMundo.ApplicationServices.Shared.Operation;
using CollectaMundo.ViewModels.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows;

namespace CollectaMundo.ViewModels.Import.ImportSteps
{
    public partial class ImportStep10_SummaryViewModel : ObservableObject, IImportStepViewModel
    {
        private readonly ImportViewModel _parent;

        // --------------------------------------------
        // Constructor
        // --------------------------------------------
        public ImportStep10_SummaryViewModel(ImportViewModel parent)
        {
            _parent = parent;
            Initialize();
        }

        // --------------------------------------------
        // Initialization
        // --------------------------------------------
        private void Initialize()
        {
            _ = InitializeAsync();
        }
        private async Task InitializeAsync()
        {
            await _parent.PrepareSummaryAsync();
        }

        // --------------------------------------------
        // UI Text & Visibility
        // --------------------------------------------
        public string PrimaryActionButtonText => "  Start the import...  \u27A1";
        public string SecondaryActionButtonText => "  Save unrecognized items  \U0001F4BE";

        public bool IsPrimaryActionVisible => true;
        public bool IsSecondaryActionVisible => _parent.Summary.UnableToImportCount == 0 ? false : true;

        [ObservableProperty]
        private bool isStepContentVisible = true;
        // --------------------------------------------
        // Step-level button enablement
        // --------------------------------------------
        public bool CanExecutePrimaryAction => true;
        public bool CanExecuteSecondaryAction => true;

        // --------------------------------------------
        // Actions
        // --------------------------------------------
        public async Task<OperationResult> OnPrimaryAction()
        {
            // Proceed with import
            IsStepContentVisible = false;
            return await _parent.AfterStep10Action();
        }
        public Task<OperationResult> OnSecondaryAction()
        {
            return _parent.SaveUnimportableItemsAsync();
        }


        // --------------------------------------------
        // Mapping Collection
        // --------------------------------------------
        public ImportSummary Summary => _parent.Summary;
    }
}
