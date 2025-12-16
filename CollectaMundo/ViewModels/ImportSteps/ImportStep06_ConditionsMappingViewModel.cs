using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Import.Models;
using CollectaMundo.DomainLogic.Import.Models.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;

namespace CollectaMundo.ViewModels.ImportSteps
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
            if (ConditionMappings.Any())
            {
                return;
            }

            var csvHeader = _parent.AdditionalMappings
                .FirstOrDefault(m => m.FieldToMap == ImportField.Condition)
                ?.SelectedCsvHeader;

            if (string.IsNullOrWhiteSpace(csvHeader))
            {
                return;
            }

            var csvValues = _parent.ImportCardList
                .Select(item => item.CsvFields.TryGetValue(csvHeader, out var v) ? v?.Trim() : null)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            var allowedValues = new CardSet().Conditions;

            foreach (var csvValue in csvValues)
            {
                ConditionMappings.Add(new CsvValueMapping
                {
                    CsvValue = csvValue!,
                    CardSetValues = [.. allowedValues]
                });
            }
        }

        // --------------------------------------------
        // UI Text & Visibility
        // --------------------------------------------
        public string PrimaryActionButtonText => "  Proceed  \u27A1";
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
