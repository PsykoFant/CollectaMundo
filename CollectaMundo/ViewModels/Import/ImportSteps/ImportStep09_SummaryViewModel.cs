using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ViewModels.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows;

namespace CollectaMundo.ViewModels.Import.ImportSteps
{
    public partial class ImportStep09_SummaryViewModel : ObservableObject, IImportStepViewModel
    {
        private readonly ImportViewModel _parent;

        // --------------------------------------------
        // Constructor
        // --------------------------------------------
        public ImportStep09_SummaryViewModel(ImportViewModel parent)
        {
            _parent = parent;
        }

        // --------------------------------------------
        // UI Text & Visibility
        // --------------------------------------------
        public string PrimaryActionButtonText => "  Start the import...  \u27A1";
        public string SecondaryActionButtonText => "  Save unrecognized items  \U0001F4BE";

        public Visibility PrimaryActionVisibility => Visibility.Visible;
        public Visibility SecondaryActionVisibility => _parent.Summary.UnableToImportCount == 0 ? Visibility.Collapsed : Visibility.Visible;

        [ObservableProperty]
        private Visibility stepContentVisibility = Visibility.Visible;

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
            return await _parent.AfterStep9Action();
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
