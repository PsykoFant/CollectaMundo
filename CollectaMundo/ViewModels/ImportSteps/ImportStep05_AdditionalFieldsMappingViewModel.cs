using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.Import;
using CollectaMundo.DomainLogic.Import.Models;
using CollectaMundo.DomainLogic.Import.Models.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;

namespace CollectaMundo.ViewModels.ImportSteps
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
        // Initialization (empty for this step)
        // --------------------------------------------
        private void Initialize()
        {
            if (AdditionalMappings.Any())
            {
                return;
            }

            var firstItem = _parent.ImportCardList.FirstOrDefault();
            var csvHeaders = firstItem?.Fields.Keys.ToList() ?? [];

            var fieldsToMap = new[]
            {
                new { Field = ImportField.Condition,      Guesses = new[] { "condition", "state", "card condition" } },
                new { Field = ImportField.CardFinish,     Guesses = new[] { "finish", "foiling", "card finish", "foil" } },
                new { Field = ImportField.Language,       Guesses = new[] { "lang", "language" } },
                new { Field = ImportField.CardsOwned,     Guesses = new[] { "quantity", "count", "owned", "qty" } },
                new { Field = ImportField.CardsForTrade,  Guesses = new[] { "trade", "for trade", "sell", "forsale", "selling" } }
            };

            foreach (var field in fieldsToMap)
            {
                AdditionalMappings.Add(new CsvFieldMapping
                {
                    FieldToMap = field.Field,
                    CsvHeaders = [.. csvHeaders],
                    SelectedCsvHeader = CsvHeaderMatcher.GuessCsvHeader(field.Field, field.Guesses, csvHeaders)
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
