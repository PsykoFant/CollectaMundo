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

        //  Constructor

        public ImportStep03_NameSetMappingViewModel(ImportViewModel parent)
        {
            _parent = parent;

            EnsureNameSetMappingsInitialized();
            HookMappingEvents();
        }

        //  Step UI Info
        public string PrimaryActionButtonText => "  Proceed  \u27A1";
        public string SecondaryActionButtonText => string.Empty;
        public Visibility SecondaryActionVisibility => Visibility.Collapsed;

        //  Step-level button enablement
        //  Must have Card Name mapped AND (Set Name OR Set Code) mapped
        public bool CanExecutePrimaryAction
        {
            get
            {
                var name = NameSetMappings.FirstOrDefault(m => m.LogicalField == "Card Name");
                var setName = NameSetMappings.FirstOrDefault(m => m.LogicalField == "Set Name");
                var setCode = NameSetMappings.FirstOrDefault(m => m.LogicalField == "Set Code");

                bool hasName = !string.IsNullOrWhiteSpace(name?.SelectedCsvHeader);
                bool hasSetName = !string.IsNullOrWhiteSpace(setName?.SelectedCsvHeader);
                bool hasSetCode = !string.IsNullOrWhiteSpace(setCode?.SelectedCsvHeader);

                return hasName && (hasSetName || hasSetCode);
            }
        }
        public bool CanExecuteSecondaryAction => false;

        //  Actions
        public async Task<OperationResult> OnPrimaryAction()
        {
            return await _parent.AfterStep3Action();
        }

        //  Clear Mapping Command
        [RelayCommand]
        private static void ClearSelectedMapping(NameSetColumnMapping mapping)
        {
            mapping.SelectedCsvHeader = null;
        }

        //  Initialization helpers
        private void EnsureNameSetMappingsInitialized()
        {
            if (NameSetMappings.Any())
            {
                return;
            }

            var firstItem = ImportViewModel.ImportCardList.FirstOrDefault();
            var csvHeaders = firstItem?.Fields.Keys.ToList() ?? new List<string>();

            var logicalFields = new[] { "Card Name", "Set Name", "Set Code" };

            foreach (var field in logicalFields)
            {
                NameSetMappings.Add(new NameSetColumnMapping
                {
                    LogicalField = field,
                    CsvHeaders = csvHeaders,
                    SelectedCsvHeader = GuessCsvHeader(field, csvHeaders)
                });
            }
        }
        private static string? GuessCsvHeader(string logicalField, List<string> csvHeaders)
        {
            // Very simple heuristic – you can refine based on your real data
            if (csvHeaders.Count == 0)
            {
                return null;
            }

            string lowerField = logicalField.ToLowerInvariant();

            // Try exact (case-insensitive) match first
            var exact = csvHeaders.FirstOrDefault(h =>
                string.Equals(h, logicalField, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
            {
                return exact;
            }

            // Then try "contains" match (e.g. "card name" → "Name")
            var contains = csvHeaders.FirstOrDefault(h =>
                h.ToLowerInvariant().Contains("name") && lowerField.Contains("name") ||
                h.ToLowerInvariant().Contains("set") && lowerField.Contains("set"));
            return contains;
        }

        //  Change tracking for button enablement
        private void HookMappingEvents()
        {
            foreach (var m in NameSetMappings)
            {
                m.PropertyChanged += Mapping_PropertyChanged;
            }

            NameSetMappings.CollectionChanged += NameSetMappings_CollectionChanged;
        }

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

            if (e.PropertyName is nameof(NameSetColumnMapping.SelectedCsvHeader))
            {
                OnPropertyChanged(nameof(CanExecutePrimaryAction));
            }
        }

        //  Mapping Collection
        public ObservableCollection<NameSetColumnMapping> NameSetMappings => _parent.NameSetMappings;

    }
}
