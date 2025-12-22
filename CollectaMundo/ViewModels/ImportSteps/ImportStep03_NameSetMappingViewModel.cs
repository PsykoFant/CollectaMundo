using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.Import;
using CollectaMundo.DomainLogic.Import.Models;
using CollectaMundo.DomainLogic.Import.Models.Enums;
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

            var firstItem = _parent.ImportCardList.FirstOrDefault();
            var csvHeaders = firstItem?.CsvFields.Keys.ToList() ?? [];

            foreach (var field in new[] { ImportField.CardName, ImportField.SetName, ImportField.SetCode })
            {
                NameSetMappings.Add(new CsvFieldMapping
                {
                    FieldToMap = field,
                    CsvHeaders = [.. csvHeaders],
                    SelectedCsvHeader = ImportValueMatcher.GuessCsvHeader(field, csvHeaders)
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
        public Visibility PrimaryActionVisibility => Visibility.Visible;
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
                var name = NameSetMappings.FirstOrDefault(m => m.FieldToMap == ImportField.CardName);
                var setNm = NameSetMappings.FirstOrDefault(m => m.FieldToMap == ImportField.SetName);
                var setCd = NameSetMappings.FirstOrDefault(m => m.FieldToMap == ImportField.SetCode);

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
        private static void ClearSelectedMapping(CsvFieldMapping mapping)
        {
            mapping.SelectedCsvHeader = null;
        }

        // --------------------------------------------
        // Mapping Collection
        // --------------------------------------------
        public ObservableCollection<CsvFieldMapping> NameSetMappings => _parent.NameSetMappings;
        private void NameSetMappings_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (CsvFieldMapping m in e.NewItems)
                {
                    m.PropertyChanged += Mapping_PropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (CsvFieldMapping m in e.OldItems)
                {
                    m.PropertyChanged -= Mapping_PropertyChanged;
                }
            }

            OnPropertyChanged(nameof(CanExecutePrimaryAction));
        }
        private void Mapping_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            Debug.WriteLine($"[Step3] Mapping property changed: {e.PropertyName}");

            if (e.PropertyName == nameof(CsvFieldMapping.SelectedCsvHeader))
            {
                OnPropertyChanged(nameof(CanExecutePrimaryAction));
            }
        }

    }
}
