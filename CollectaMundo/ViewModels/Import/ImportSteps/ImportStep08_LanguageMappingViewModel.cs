using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.Import;
using CollectaMundo.DomainLogic.Import.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;

namespace CollectaMundo.ViewModels.Import.ImportSteps
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
            _ = InitializeAsync();
        }
        private async Task InitializeAsync()
        {
            var csvHeader = _parent.AdditionalMappings.First(m => m.FieldToMap == ImportField.Language).SelectedCsvHeader!;
            var csvValues = _parent.ImportCardList.Select(item => item.CsvFields.TryGetValue(csvHeader, out var val)
            ? val?.Trim()
            : null)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Lazy, cached, parent-owned async call
            var availableLanguages = await _parent.GetAvailableLanguagesAsync();
            var defaultLanguage = ImportDefaults.GetDefaultString(ImportField.Language);

            // Ensure default language is present
            var languageOptions = availableLanguages.Append(defaultLanguage).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(l => l).ToList();

            foreach (var csvValue in csvValues)
            {
                var guessed = ImportValueMatcher.MapImportValue(csvValue!, ImportField.Language, languageOptions) ?? defaultLanguage; // Default to "English" if no match found

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
