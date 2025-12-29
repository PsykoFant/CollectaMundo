using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Import.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows;

namespace CollectaMundo.ViewModels.Import.ImportSteps
{
    public partial class ImportStep04_MultipleUuidsViewModel : ObservableObject, IImportStepViewModel
    {
        private readonly ImportViewModel _parent;
        public ObservableCollection<MultipleUuidsItem> MultipleChoices { get; } = [];

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
                .Where(item => item.CsvFields.TryGetValue("uuids", out var uuids) && !string.IsNullOrWhiteSpace(uuids))
                .Select(item =>
                {
                    var selectedCardNameHeader = _parent.NameSetMappings.FirstOrDefault(m => m.FieldToMap == ImportField.CardName)?.SelectedCsvHeader;

                    var name = selectedCardNameHeader is not null && item.CsvFields.TryGetValue(selectedCardNameHeader, out var n) && !string.IsNullOrWhiteSpace(n)
                    ? n
                    : "Unknown";

                    var uuidList = item.CsvFields["uuids"].Split(',');
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
                            // Show image in shared CardImageVM
                            _parent.CardImageVM.SelectedCard = new CardSet { Uuid = uuid };
                        }
                    };
                });

            foreach (var m in items)
            {
                MultipleChoices.Add(m);
            }
        }
        private void HookEvents()
        {
            foreach (var item in MultipleChoices)
            {
                item.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(MultipleUuidsItem.SelectedUuid))
                    {
                        OnPropertyChanged(nameof(CanExecutePrimaryAction));
                    }
                };
            }

            MultipleChoices.CollectionChanged += (_, e) =>
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
        public string PrimaryActionButtonText => "  Continue  \u27A1";
        public string SecondaryActionButtonText => string.Empty;
        public Visibility PrimaryActionVisibility => Visibility.Visible;
        public Visibility SecondaryActionVisibility => Visibility.Collapsed;

        [ObservableProperty]
        private Visibility stepContentVisibility = Visibility.Visible;

        // --------------------------------------------
        // Step-level button enablement
        // --------------------------------------------
        public bool CanExecutePrimaryAction => MultipleChoices.All(c => !string.IsNullOrWhiteSpace(c.SelectedUuid));
        public bool CanExecuteSecondaryAction => false;

        // --------------------------------------------
        // Actions
        // --------------------------------------------
        public async Task<OperationResult> OnPrimaryAction()
        {
            StepContentVisibility = Visibility.Collapsed;
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
