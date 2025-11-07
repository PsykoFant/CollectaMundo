using CollectaMundo.ApplicationServices.Import;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.Import.Models;
using CollectaMundo.ViewModels.ImportSteps;
using CollectaMundo.ViewModels.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace CollectaMundo.ViewModels
{
    public partial class ImportViewModel(IImportService importService, IParentViewModelContext parentContext, IUserPromptService userPromptService) : ObservableObject
    {
        private readonly IImportService _importService = importService;
        private readonly IParentViewModelContext _parentViewModelContext = parentContext;
        private readonly IUserPromptService _userPromptService = userPromptService;

        [ObservableProperty]
        private bool isProcessing = false;
        public bool IsActionButtonEnabled => !IsProcessing;

        [ObservableProperty]
        private string? crunchingDataMessage = string.Empty;

        partial void OnIsProcessingChanged(bool oldValue, bool newValue)
        {
            // When isProcessing changes, tell WPF that IsActionButtonEnabled changed too
            OnPropertyChanged(nameof(IsActionButtonEnabled));
        }


        public static ObservableCollection<TempCardItem> ImportCardList { get; } = [];
        public ObservableCollection<ColumnMapping> Mappings { get; } = [];

        [ObservableProperty]
        private Visibility importOverlayVisibility = Visibility.Collapsed;

        [ObservableProperty]
        private IImportStepViewModel? currentStepViewModel;

        private ImportStep _currentStep = ImportStep.Start;
        public void GoToStep(ImportStep step)
        {
            _currentStep = step;
            Debug.WriteLine($"ImportViewModel: Navigating from {_currentStep}.");

            CurrentStepViewModel = step switch
            {
                ImportStep.Start => new ImportStep1_StartViewModel(this),
                ImportStep.IdColumnMapping => new ImportStep2_IdMappingViewModel(this),
                ImportStep.NameAndSetMapping => new ImportStep3_NameSetMappingViewModel(this),
                ImportStep.MultipleUuidsSelection => new ImportStep4_MultipleUuidsViewModel(this),
                ImportStep.AdditionalFieldsMapping => new ImportStep5_AdditionalFieldsMappingViewModel(this),
                ImportStep.Finish => throw new InvalidOperationException("You must not navigate to Finish directly"),
                _ => throw new NotSupportedException($"Unknown import step: {step}")
            };
        }
        public async Task Begin()
        {
            CurrentStepViewModel = new ImportStep1_StartViewModel(this);

            var tcs = _userPromptService.CreatePrompt();
            var confirmed = await tcs.Task;

            if (confirmed)
            {
                // User finished import successfully
            }
        }

        public async Task AfterStep1Action()
        {
            var filePath = _importService.PromptForCsvFile();

            if (!string.IsNullOrEmpty(filePath))
            {
                _parentViewModelContext.SetUiBusy(true);
                CrunchingDataMessage = "Please wait - gobbling up and parsing CSV file...";
                var (parsedItems, mapping) = await Task.Run(() => _importService.LoadCsvFileAsync(filePath));

                foreach (var item in parsedItems)
                    ImportCardList.Add(item);
                Mappings.Add(mapping);
                GoToStep(ImportStep.IdColumnMapping);
                //DebugAllItems();
            }
        }
        public async Task AfterStep2Action()
        {
            var mapping = Mappings.FirstOrDefault() ?? throw new InvalidOperationException("No mapping found after Step 2");
            CrunchingDataMessage = "Please wait - attempting to match ids...";
            var result = await Task.Run(() => _importService.TryResolveUuidsFromMappedIdAsync([.. ImportCardList], mapping));

            //DebugImportProcess();

            if (result.TotalItems == result.ItemsWithUuid)
            {
                if (result.ItemsWithMultipleUuids > 0)
                {
                    GoToStep(ImportStep.MultipleUuidsSelection);
                }
                else
                {
                    GoToStep(ImportStep.AdditionalFieldsMapping);
                }
            }
            else
            {
                GoToStep(ImportStep.NameAndSetMapping);
            }
        }

        [RelayCommand]
        private async Task PrimaryActionAsync()
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait; // set spinner
                IsProcessing = true;
                await CurrentStepViewModel!.OnPrimaryAction();
            }
            finally
            {
                CrunchingDataMessage = string.Empty;
                IsProcessing = false;
                Mouse.OverrideCursor = null; // reset to default
            }
        }

        [RelayCommand]
        private void SecondaryAction()
        {
            CurrentStepViewModel!.OnSecondaryAction();
        }


        [RelayCommand]
        private void Cancel() => CancelImport();
        private void CancelImport()
        {
            ImportCardList.Clear();
            Mappings.Clear();
            CrunchingDataMessage = string.Empty;
            _userPromptService.CancelPendingPrompt();
            ImportOverlayVisibility = Visibility.Collapsed;
            CurrentStepViewModel = null;
            _currentStep = ImportStep.Start;
            _parentViewModelContext.SetUiBusy(false);
        }

        private static void DebugAllItems()
        {
            Debug.WriteLine("\n");
            Debug.WriteLine("Debugging TempImport items:");
            foreach (TempCardItem tempItem in ImportCardList)
            {
                Debug.WriteLine("TempItem:");
                foreach (KeyValuePair<string, string> field in tempItem.Fields)
                {
                    Debug.WriteLine($"{field.Key}: {field.Value}");
                }
                Debug.WriteLine("\n");
            }
        }
        private static void DebugImportProcess()
        {
            // Total number of items in TempImport
            int totalTempImportItems = ImportCardList.Count;

            // Number of TempImport items with a single uuid
            int singleUuidItems = ImportCardList.Count(item => item.Fields.ContainsKey("uuid") && !item.Fields.ContainsKey("uuids"));

            // Number of TempImport items with multiple uuids
            int multipleUuidItems = ImportCardList.Count(item => item.Fields.ContainsKey("uuids"));

            int noUuidItems = ImportCardList.Count(item => !item.Fields.ContainsKey("uuid") && !item.Fields.ContainsKey("uuids"));

            // Debug write lines
            Debug.WriteLine($"Total number of items in TempImport: {totalTempImportItems}");
            Debug.WriteLine($"Number of TempImport items with single uuid: {singleUuidItems}");
            Debug.WriteLine($"Number of TempImport items with multiple uuids: {multipleUuidItems}");
            Debug.WriteLine($"Number of TempImport items with no uuid or uuids: {noUuidItems}");
        }

    }
}
