using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.Import.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;

namespace CollectaMundo.ViewModels.ImportSteps
{
    public partial class ImportStep03_NameSetMappingViewModel : ObservableObject, IImportStepViewModel
    {
        private readonly ImportViewModel _parent;

        // --------------------------------------------
        // Constructor
        // --------------------------------------------
        public ImportStep03_NameSetMappingViewModel(ImportViewModel parent)
        {
            _parent = parent;

            Initialize();
            HookEvents();
        }

        // --------------------------------------------
        // Initialization
        // --------------------------------------------
        private void Initialize()
        {
            if (NameSetMappings.Any())
            {
                return;
            }

            var firstItem = ImportViewModel.ImportCardList.FirstOrDefault();
            var csvHeaders = firstItem?.Fields.Keys.ToList() ?? [];

            var fieldsToMap = new[]
            {
                new { Field = "Card Name", Guesses = new[] { "name", "card name", "card_name" } },
                new { Field = "Set Name",  Guesses = new[] { "set name", "setname", "set", "edition" } },
                new { Field = "Set Code",  Guesses = new[] { "set code", "setcode", "code", "edition code" } }
            };

            foreach (var field in fieldsToMap)
            {
                NameSetMappings.Add(new NameSetColumnMapping
                {
                    FieldToMap = field.Field,
                    CsvHeaders = [.. csvHeaders],
                    SelectedCsvHeader = GuessCsvHeader(field.Field, field.Guesses, csvHeaders)
                });
            }
        }

        // --------------------------------------------
        // Event Wiring
        // --------------------------------------------
        private void HookEvents()
        {
            foreach (var m in NameSetMappings)
            {
                m.PropertyChanged += Mapping_PropertyChanged;
            }

            NameSetMappings.CollectionChanged += NameSetMappings_CollectionChanged;
        }

        // --------------------------------------------
        // UI Text & Visibility
        // --------------------------------------------
        public string PrimaryActionButtonText => "  Proceed  \u27A1";
        public string SecondaryActionButtonText => string.Empty;
        public Visibility SecondaryActionVisibility => Visibility.Collapsed;

        [ObservableProperty]
        private Visibility stepContentVisibility = Visibility.Visible;

        // --------------------------------------------
        // Step-level button enablement
        // --------------------------------------------
        public bool CanExecutePrimaryAction
        {
            get
            {
                var name = NameSetMappings.FirstOrDefault(m => m.FieldToMap == "Card Name");
                var setNm = NameSetMappings.FirstOrDefault(m => m.FieldToMap == "Set Name");
                var setCd = NameSetMappings.FirstOrDefault(m => m.FieldToMap == "Set Code");

                bool hasName = !string.IsNullOrWhiteSpace(name?.SelectedCsvHeader);
                bool hasSetName = !string.IsNullOrWhiteSpace(setNm?.SelectedCsvHeader);
                bool hasSetCode = !string.IsNullOrWhiteSpace(setCd?.SelectedCsvHeader);

                return hasName && (hasSetName || hasSetCode);
            }
        }
        public bool CanExecuteSecondaryAction => false;

        // --------------------------------------------
        // Actions
        // --------------------------------------------
        public async Task<OperationResult> OnPrimaryAction()
        {
            StepContentVisibility = Visibility.Collapsed;
            return await _parent.AfterStep3Action();
        }

        // --------------------------------------------
        // Commands
        // --------------------------------------------
        [RelayCommand]
        private static void ClearSelectedMapping(NameSetColumnMapping mapping)
        {
            mapping.SelectedCsvHeader = null;
        }

        // --------------------------------------------
        // Mapping Collection
        // --------------------------------------------
        public ObservableCollection<NameSetColumnMapping> NameSetMappings => _parent.NameSetMappings;
        private void NameSetMappings_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (NameSetColumnMapping m in e.NewItems)
                {
                    m.PropertyChanged += Mapping_PropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (NameSetColumnMapping m in e.OldItems)
                {
                    m.PropertyChanged -= Mapping_PropertyChanged;
                }
            }

            OnPropertyChanged(nameof(CanExecutePrimaryAction));
        }
        private void Mapping_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            Debug.WriteLine($"[Step3] Mapping property changed: {e.PropertyName}");

            if (e.PropertyName == nameof(NameSetColumnMapping.SelectedCsvHeader))
            {
                OnPropertyChanged(nameof(CanExecutePrimaryAction));
            }
        }

        // --------------------------------------------
        // Helpers
        // --------------------------------------------
        private static string? GuessCsvHeader(string fieldToMap, IReadOnlyList<string> guesses, IReadOnlyList<string> csvHeaders)
        {
            if (csvHeaders == null || csvHeaders.Count == 0)
            {
                return null;
            }

            // Normalize: field + guesses → candidate patterns
            var candidates = new List<string> { fieldToMap };
            if (guesses != null)
            {
                candidates.AddRange(guesses);
            }

            candidates = [.. candidates
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Select(g => g.Trim())];

            if (candidates.Count == 0)
            {
                return null;
            }

            // 1) Exact match on any candidate (case-insensitive)
            foreach (var header in csvHeaders)
            {
                foreach (var candidate in candidates)
                {
                    if (string.Equals(header, candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        return header;
                    }
                }
            }

            // 2) "Contains" match on any candidate (case-insensitive)
            foreach (var header in csvHeaders)
            {
                string headerLower = header.ToLowerInvariant();

                foreach (var candidate in candidates)
                {
                    string candidateLower = candidate.ToLowerInvariant();

                    // Symmetric-ish contains: header contains candidate or candidate contains header
                    if (headerLower.Contains(candidateLower) || candidateLower.Contains(headerLower))
                    {
                        return header;
                    }
                }
            }

            // No match
            return null;
        }

    }
}
