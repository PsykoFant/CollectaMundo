using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.Import.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows;

namespace CollectaMundo.ViewModels.Import.ImportSteps
{
    public partial class ImportStep04_MultipleUuidsViewModel : ObservableObject, IImportStepViewModel
    {
        private readonly ImportViewModel _parent;
        private bool _suppressSelectionImageRequests; // Flag to prevent image requests when programmatically setting SelectedUuid
        public ObservableCollection<MultipleUuidsItem> MultipleUuidItems { get; } = [];

        // --------------------------------------------
        // Constructor
        // --------------------------------------------
        public ImportStep04_MultipleUuidsViewModel(ImportViewModel parent)
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
            var items = _parent.ImportCardList
                .Where(item => item.CsvFields.TryGetValue("collectaMundoUuidsImportField", out var uuids) && !string.IsNullOrWhiteSpace(uuids))
                .Select(item =>
                {
                    var selectedCardNameHeader = _parent.NameSetMappings.FirstOrDefault(m => m.FieldToMap == ImportField.CardName)?.SelectedCsvHeader;

                    var name = selectedCardNameHeader is not null && item.CsvFields.TryGetValue(selectedCardNameHeader, out var n) && !string.IsNullOrWhiteSpace(n)
                    ? n
                    : "Unknown";

                    var uuidList = item.CsvFields["collectaMundoUuidsImportField"].Split(',');
                    var versions = uuidList.Select((uuid, i) => new UuidVersion
                    {
                        Uuid = uuid,
                        DisplayText = $"Version {i + 1}"
                    }).ToList();

                    return new MultipleUuidsItem
                    {
                        Name = name,
                        TempItemImportKey = item.TempItemImportKey,
                        VersionedUuids = versions,
                        SelectedUuid = null,
                        OnSelectionChangedCallback = uuid =>
                        {
                            // If we are not auto-selecting ... 
                            if (_suppressSelectionImageRequests)
                            {
                                return;
                            }
                            // ... show image in shared CardImageVM
                            _parent.RequestCardImage(uuid);
                        }
                    };
                });

            foreach (var m in items)
            {
                MultipleUuidItems.Add(m);
            }
        }
        private void HookEvents()
        {
            foreach (var item in MultipleUuidItems)
            {
                item.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(MultipleUuidsItem.SelectedUuid))
                    {
                        OnPropertyChanged(nameof(CanExecutePrimaryAction));
                    }
                };
            }

            MultipleUuidItems.CollectionChanged += (_, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (MultipleUuidsItem item in e.NewItems)
                    {
                        item.PropertyChanged += (_, e2) =>
                        {
                            if (e2.PropertyName == nameof(MultipleUuidsItem.SelectedUuid))
                            {
                                OnPropertyChanged(nameof(CanExecutePrimaryAction));
                            }
                        };
                    }
                }
            };
        }

        // --------------------------------------------
        // UI Text & Visibility
        // --------------------------------------------
        public string PrimaryActionButtonText => "  Proceed  \u27A1";
        public string SecondaryActionButtonText => "  Don't care - choose a random version  ";
        public bool IsPrimaryActionVisible => true;
        public bool IsSecondaryActionVisible => true;

        [ObservableProperty]
        private bool isStepContentVisible = true;

        // --------------------------------------------
        // Step-level button enablement
        // --------------------------------------------
        public bool CanExecutePrimaryAction => MultipleUuidItems.All(c => !string.IsNullOrWhiteSpace(c.SelectedUuid));
        public bool CanExecuteSecondaryAction => MultipleUuidItems.Any();

        // --------------------------------------------
        // Actions
        // --------------------------------------------
        public async Task<OperationResult> OnPrimaryAction()
        {
            IsStepContentVisible = false;
            return await _parent.AfterStep4Action();
        }
        public async Task<OperationResult> OnSecondaryAction()
        {
            _suppressSelectionImageRequests = true;

            try
            {
                foreach (var item in MultipleUuidItems)
                {
                    if (!string.IsNullOrWhiteSpace(item.SelectedUuid))
                    {
                        continue;
                    }

                    if (item.VersionedUuids.Count == 0)
                    {
                        continue;
                    }

                    var randomIndex = Random.Shared.Next(item.VersionedUuids.Count);
                    item.SelectedUuid = item.VersionedUuids[randomIndex].Uuid;
                }
            }
            finally
            {
                _suppressSelectionImageRequests = false;
            }

            IsStepContentVisible = false;

            return await _parent.AfterStep4Action();
        }

        // --------------------------------------------
        // Commands (none for this step)
        // --------------------------------------------

        // --------------------------------------------
        // Private helper methods (none needed)
        // --------------------------------------------
    }
}
