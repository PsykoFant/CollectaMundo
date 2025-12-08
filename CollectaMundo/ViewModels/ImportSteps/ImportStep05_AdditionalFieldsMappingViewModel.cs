using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.Import;
using CollectaMundo.DomainLogic.Import.Models;
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
            HookEvents();
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

            var firstItem = ImportViewModel.ImportCardList.FirstOrDefault();
            var csvHeaders = firstItem?.Fields.Keys.ToList() ?? [];

            var fieldsToMap = new[]
            {
                //new { Field = "Condition", Guesses = new[] { "condition", "state" } },
                new { Field = "Condition", Guesses = new[] { "fisk", "hund" } },
                new { Field = "Card Finish", Guesses = new[] { "finish", "foiling", "card finish" } },
                new { Field = "Cards Owned", Guesses = new[] { "quantity", "count", "owned", "qty" } },
                new { Field = "Cards For Trade/Selling", Guesses = new[] { "trade", "for trade", "sell", "forsale", "selling" } },
                new { Field = "Language", Guesses = new[] { "lang", "language" } }
    };

            foreach (var field in fieldsToMap)
            {
                AdditionalMappings.Add(new AdditionalFieldMapping
                {
                    CardSetField = field.Field,
                    CsvHeaders = [.. csvHeaders],
                    SelectedCsvHeader = CsvHeaderMatcher.GuessCsvHeader(field.Field, field.Guesses, csvHeaders)
                });
            }
        }


        private void HookEvents()
        {
            // Step 1 has no dynamic collections or item-level events.
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
        public async Task<OperationResult> OnPrimaryAction() => await _parent.AfterStep5Action();

        public void OnSecondaryAction()
        {
            // Not used in this step (and SecondaryActionVisibility is Collapsed).
        }

        // --------------------------------------------
        // Commands (none for this step)
        // --------------------------------------------
        [RelayCommand]
        private static void ClearSelectedMapping(AdditionalFieldMapping mapping)
        {
            mapping.SelectedCsvHeader = null;
        }


        // --------------------------------------------
        // Mapping Collection
        // --------------------------------------------
        public ObservableCollection<AdditionalFieldMapping> AdditionalMappings => _parent.AdditionalMappings;

        // --------------------------------------------
        // Private helper methods (none needed)
        // --------------------------------------------
    }
}
