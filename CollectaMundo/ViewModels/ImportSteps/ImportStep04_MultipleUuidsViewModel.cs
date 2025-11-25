using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.Import.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows;

namespace CollectaMundo.ViewModels.ImportSteps
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
        // Initialization (empty for this step)
        // --------------------------------------------
        private void Initialize()
        {
            var items = ImportViewModel.ImportCardList
                .Where(item => item.Fields.TryGetValue("uuids", out var uuids) && !string.IsNullOrWhiteSpace(uuids))
                .Select(item =>
                {
                    var name = item.Fields.TryGetValue("Card Name", out var n) ? n : "Unknown";
                    var uuidList = item.Fields["uuids"].Split(',');
                    var versions = uuidList.Select((uuid, i) => new UuidVersion
                    {
                        Uuid = uuid,
                        DisplayText = $"Version {i + 1}"
                    }).ToList();

                    return new MultipleUuidsItem
                    {
                        Name = name,
                        CMImportKey = item.Fields["CMImportKey"],
                        VersionedUuids = versions,
                        SelectedUuid = null
                    };
                });

            foreach (var m in items)
            {
                MultipleChoices.Add(m);
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
        public async Task<OperationResult> OnPrimaryAction() => await _parent.AfterStep4Action();

        public void OnSecondaryAction()
        {
            // Not used in this step (and SecondaryActionVisibility is Collapsed).
        }

        // --------------------------------------------
        // Commands (none for this step)
        // --------------------------------------------

        // --------------------------------------------
        // Private helper methods (none needed)
        // --------------------------------------------
    }
}
