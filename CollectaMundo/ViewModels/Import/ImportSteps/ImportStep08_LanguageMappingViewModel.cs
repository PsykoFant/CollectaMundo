using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.Import;
using CollectaMundo.DomainLogic.Import.Models;
using CollectaMundo.ViewModels.Import.ImportSteps;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;

namespace CollectaMundo.ViewModels.ImportSteps
{
    public partial class ImportStep08_LanguageMappingViewModel : ObservableObject, IImportStepViewModel
    {
        private readonly ImportViewModel _parent;

        // --------------------------------------------
        // Constructor
        // --------------------------------------------
        public ImportStep08_LanguageMappingViewModel(ImportViewModel parent)
        {
            _parent = parent;
            Initialize();
        }

        // --------------------------------------------
        // Initialization
        // --------------------------------------------
        private void Initialize()
        {
            if (LanguageMappings.Any())
            {
                return;
            }

            _ = InitializeAsync();
        }
        private async Task InitializeAsync()
        {
            var csvHeader = _parent.AdditionalMappings.FirstOrDefault(m => m.FieldToMap == ImportField.Language)?.SelectedCsvHeader;

            if (string.IsNullOrWhiteSpace(csvHeader))
            {
                return;
            }

            var csvValues = _parent.ImportCardList
                .Select(item =>
                    item.CsvFields.TryGetValue(csvHeader, out var val) ? val?.Trim() : null)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (csvValues.Count == 0)
            {
                return;
            }

            // Lazy, cached, parent-owned async call
            var availableLanguages = await _parent.GetAvailableLanguagesAsync();

            // Ensure default language is present
            var languageOptions = availableLanguages.Append("English").Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(l => l).ToList();

            foreach (var csvValue in csvValues)
            {
                var guessed = ImportValueMatcher.MapImportValue(csvValue!, ImportField.Language, languageOptions) ?? "English"; // Default to "English" if no match found

                LanguageMappings.Add(new CsvValueMapping
                {
                    CsvValue = csvValue!,
                    CardSetValues = [.. languageOptions],
                    SelectedCardSetValue = guessed
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
            return await _parent.AfterStep8Action();
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
        public ObservableCollection<CsvValueMapping> LanguageMappings => _parent.LanguageMappings;

    }
}
