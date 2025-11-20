using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.Import.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
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
                return;

            var firstItem = ImportViewModel.ImportCardList.FirstOrDefault();
            var csvHeaders = firstItem?.Fields.Keys.ToList() ?? [];

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

        // --------------------------------------------
        // Event Wiring
        // --------------------------------------------
        private void HookEvents()
        {
            foreach (var m in NameSetMappings)
                m.PropertyChanged += Mapping_PropertyChanged;

            NameSetMappings.CollectionChanged += NameSetMappings_CollectionChanged;
        }

        // --------------------------------------------
        // UI Text & Visibility
        // --------------------------------------------
        public string PrimaryActionButtonText => "  Proceed  \u27A1";
        public string SecondaryActionButtonText => string.Empty;
        public Visibility SecondaryActionVisibility => Visibility.Collapsed;

        // --------------------------------------------
        // Step-level button enablement
        // --------------------------------------------
        public bool CanExecutePrimaryAction
        {
            get
            {
                var name = NameSetMappings.FirstOrDefault(m => m.LogicalField == "Card Name");
                var setNm = NameSetMappings.FirstOrDefault(m => m.LogicalField == "Set Name");
                var setCd = NameSetMappings.FirstOrDefault(m => m.LogicalField == "Set Code");

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
            => await _parent.AfterStep3Action();

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
                    m.PropertyChanged += Mapping_PropertyChanged;
            }

            if (e.OldItems != null)
            {
                foreach (NameSetColumnMapping m in e.OldItems)
                    m.PropertyChanged -= Mapping_PropertyChanged;
            }

            OnPropertyChanged(nameof(CanExecutePrimaryAction));
        }
        private void Mapping_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            Debug.WriteLine($"[Step3] Mapping property changed: {e.PropertyName}");

            if (e.PropertyName == nameof(NameSetColumnMapping.SelectedCsvHeader))
                OnPropertyChanged(nameof(CanExecutePrimaryAction));
        }

        // --------------------------------------------
        // Helpers
        // --------------------------------------------
        private static string? GuessCsvHeader(string logicalField, List<string> csvHeaders)
        {
            if (csvHeaders.Count == 0)
                return null;

            string lowerField = logicalField.ToLowerInvariant();

            var exact = csvHeaders.FirstOrDefault(h =>
                string.Equals(h, logicalField, StringComparison.OrdinalIgnoreCase));

            if (exact != null)
                return exact;

            return csvHeaders.FirstOrDefault(h =>
                h.Contains("name", StringComparison.InvariantCultureIgnoreCase) && lowerField.Contains("name") ||
                h.Contains("set", StringComparison.InvariantCultureIgnoreCase) && lowerField.Contains("set"));
        }
    }
}
