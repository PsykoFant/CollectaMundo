using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.Import;
using CollectaMundo.DomainLogic.Import.Models;
using CollectaMundo.DomainLogic.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;

namespace CollectaMundo.ViewModels.Import.ImportSteps
{
    public partial class ImportStep07_FinishMappingViewModel : ObservableObject, IImportStepViewModel
    {
        private readonly ImportViewModel _parent;

        // --------------------------------------------
        // Constructor
        // --------------------------------------------
        public ImportStep07_FinishMappingViewModel(ImportViewModel parent)
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
            var csvHeader = _parent.AdditionalMappings.First(m => m.FieldToMap == ImportField.CardFinish).SelectedCsvHeader!;
            var csvValues = _parent.ImportCardList.Select(item => item.CsvFields.TryGetValue(csvHeader, out var val)
            ? val?.Trim()
            : null).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var availableFinishes = await _parent.GetAvailableFinishesAsync();

            foreach (var csvValue in csvValues)
            {
                var guessed = ImportValueMatcher.MapImportValue(
                    csvValue!,
                    ImportField.CardFinish,
                    availableFinishes
                ) ?? CollectionCardItemDefaults.GetDefaultString(ImportField.CardFinish);

                FinishMappings.Add(new CsvValueMapping
                {
                    CsvValue = csvValue!,
                    CardSetValues = [.. availableFinishes],
                    SelectedCardSetValue = guessed
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
            return await _parent.AfterStep7Action();
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
        public ObservableCollection<CsvValueMapping> FinishMappings => _parent.FinishMappings;

    }
}
