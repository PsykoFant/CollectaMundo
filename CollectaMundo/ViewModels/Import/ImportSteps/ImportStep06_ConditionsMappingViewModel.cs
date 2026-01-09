using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Import;
using CollectaMundo.DomainLogic.Import.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;

namespace CollectaMundo.ViewModels.Import.ImportSteps
{
    public partial class ImportStep06_ConditionsMappingViewModel : ObservableObject, IImportStepViewModel
    {
        private readonly ImportViewModel _parent;

        // --------------------------------------------
        // Constructor
        // --------------------------------------------
        public ImportStep06_ConditionsMappingViewModel(ImportViewModel parent)
        {
            _parent = parent;
            Initialize();
        }

        // --------------------------------------------
        // Initialization
        // --------------------------------------------
        private void Initialize()
        {

            var csvHeader = _parent.AdditionalMappings.First(m => m.FieldToMap == ImportField.Condition).SelectedCsvHeader!;
            var csvValues = _parent.ImportCardList.Select(item => item.CsvFields.TryGetValue(csvHeader, out var val)
            ? val?.Trim()
            : null).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var allowedValues = new CardSet().Conditions;

            foreach (var csvValue in csvValues)
            {
                var guessed = ImportValueMatcher.MapImportValue(
                    csvValue!,
                    ImportField.Condition,
                    allowedValues
                ) ?? ImportDefaults.GetDefaultString(ImportField.Condition); // Default to "Near Mint" if no match found

                ConditionMappings.Add(new CsvValueMapping
                {
                    CsvValue = csvValue!,
                    CardSetValues = [.. allowedValues],
                    SelectedCardSetValue = guessed
                });
            }
        }

        // --------------------------------------------
        // UI Text & Visibility
        // --------------------------------------------
        public string PrimaryActionButtonText => "  Continue  \u27A1";
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
            return await _parent.AfterStep6Action();
        }

        // --------------------------------------------
        // Commands
        // --------------------------------------------
        [RelayCommand]
        private static void ClearSelectedMapping(CsvValueMapping mapping)
        {
            mapping.SelectedCardSetValue = null;
        }

        // --------------------------------------------
        // Mapping Collection
        // --------------------------------------------
        public ObservableCollection<CsvValueMapping> ConditionMappings => _parent.ConditionMappings;

    }
}
