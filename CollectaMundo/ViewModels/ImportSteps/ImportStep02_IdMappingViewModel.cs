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
    public partial class ImportStep02_IdMappingViewModel: ObservableObject, IImportStepViewModel
    {
        private readonly ImportViewModel _parent;

        // --------------------------------------------
        // Constructor
        // --------------------------------------------
        public ImportStep02_IdMappingViewModel(ImportViewModel parent)
        {
            _parent = parent;
            HookEvents();
        }

        // --------------------------------------------
        // Event wiring
        // --------------------------------------------
        private void HookEvents()
        {
            // Subscribe to existing items
            foreach (var m in Mappings)
                m.PropertyChanged += Mapping_PropertyChanged;

            // And subscribe to collection change for future additions/removals
            Mappings.CollectionChanged += Mappings_CollectionChanged;
        }

        // --------------------------------------------
        // UI Text & Visibility
        // --------------------------------------------
        public string PrimaryActionButtonText => "  Proceed  \u27A1";
        public string SecondaryActionButtonText => "  Skip  \u23ED";
        public Visibility SecondaryActionVisibility => Visibility.Visible;

        // --------------------------------------------
        // Step-level button enablement
        // --------------------------------------------
        public bool CanExecutePrimaryAction =>
            Mappings.All(m =>
                !string.IsNullOrEmpty(m.SelectedCsvHeader) &&
                !string.IsNullOrEmpty(m.SelectedDatabaseField));

        public bool CanExecuteSecondaryAction => true;

        // --------------------------------------------
        // Actions
        // --------------------------------------------
        public async Task<OperationResult> OnPrimaryAction() => await _parent.AfterStep2Action();

        public void OnSecondaryAction() => _parent.GoToStep(ImportStep.NameAndSetMapping);

        // --------------------------------------------
        // Commands
        // --------------------------------------------
        [RelayCommand]
        private static void ClearSelectedMapping(IdColumnMapping mapping)
        {
            mapping.SelectedCsvHeader = null;
            mapping.SelectedDatabaseField = null;
        }

        // --------------------------------------------
        // Mapping Collection
        // --------------------------------------------
        public ObservableCollection<IdColumnMapping> Mappings => _parent.IdMappings;
        private void Mappings_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (IdColumnMapping m in e.NewItems)
                    m.PropertyChanged += Mapping_PropertyChanged;
            }

            if (e.OldItems != null)
            {
                foreach (IdColumnMapping m in e.OldItems)
                    m.PropertyChanged -= Mapping_PropertyChanged;
            }

            OnPropertyChanged(nameof(CanExecutePrimaryAction));
        }
        private void Mapping_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            Debug.WriteLine($"[Step2] Mapping property changed: {e.PropertyName}");

            if (e.PropertyName is nameof(IdColumnMapping.SelectedCsvHeader)
                or nameof(IdColumnMapping.SelectedDatabaseField))
            {
                OnPropertyChanged(nameof(CanExecutePrimaryAction));
            }
        }
    }
}
