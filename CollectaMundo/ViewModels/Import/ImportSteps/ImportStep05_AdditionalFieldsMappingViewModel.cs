using CollectaMundo.ApplicationServices.Shared.Operation;
using CollectaMundo.DomainLogic.Import;
using CollectaMundo.DomainLogic.Import.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;

namespace CollectaMundo.ViewModels.Import.ImportSteps
{
    public partial class ImportStep05_AdditionalFieldsMappingViewModel : ObservableObject, IImportStepViewModel
    {
        private readonly ImportViewModel _parent;

        // --------------------------------------------
        // Constructor
        // --------------------------------------------
        public ImportStep05_AdditionalFieldsMappingViewModel(ImportViewModel parent)
        {
            _parent = parent;
            Initialize();
        }

        // --------------------------------------------
        // Initialization
        // --------------------------------------------
        private void Initialize()
        {
            if (AdditionalMappings.Any())
            {
                return;
            }

            foreach (var field in new[] { ImportField.Condition, ImportField.CardFinish, ImportField.Language, ImportField.Location, ImportField.Comment, ImportField.CardsOwned, ImportField.CardsForTrade })
            {
                AdditionalMappings.Add(new CsvFieldMapping
                {
                    FieldToMap = field,
                    CsvHeaders = [.. _parent.CsvHeaders],
                    SelectedCsvHeader = ImportValueMatcher.GuessCsvHeader(field, _parent.CsvHeaders)
                });
            }

        }

        // --------------------------------------------
        // UI Text & Visibility
        // --------------------------------------------
        public string PrimaryActionButtonText => "  Proceed  \u27A1";
        public string SecondaryActionButtonText => string.Empty;
        public bool IsPrimaryActionVisible => true;
        public bool IsSecondaryActionVisible => false;

        [ObservableProperty]
        private bool isStepContentVisible = true;
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
            IsStepContentVisible = false;
            return await _parent.AfterStep5Action();
        }

        // --------------------------------------------
        // Commands (none for this step)
        // --------------------------------------------
        [RelayCommand]
        private static void ClearSelectedMapping(CsvFieldMapping mapping)
        {
            mapping.SelectedCsvHeader = null;
        }

        // --------------------------------------------
        // Mapping Collection
        // --------------------------------------------
        public ObservableCollection<CsvFieldMapping> AdditionalMappings => _parent.AdditionalMappings;

    }
}
