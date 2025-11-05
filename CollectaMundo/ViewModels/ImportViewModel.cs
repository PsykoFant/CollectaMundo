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

namespace CollectaMundo.ViewModels
{
    public partial class ImportViewModel(IImportService importService, IParentViewModelContext parentContext, IUserPromptService userPromptService) : ObservableObject
    {
        private readonly IImportService _importService = importService;
        private readonly IParentViewModelContext _parentViewModelContext = parentContext;
        private readonly IUserPromptService _userPromptService = userPromptService;

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
                var (parsedItems, mapping) = await _importService.LoadCsvFileAsync(filePath);

                foreach (var item in parsedItems)
                {
                    ImportCardList.Add(item);
                }

                Mappings.Add(mapping);
                GoToStep(ImportStep.IdColumnMapping);
                //DebugAllItems();
            }
        }
        public async Task AfterStep2Action()
        {
            var result = await _importService.TryResolveUuidsFromMappedIdAsync([.. ImportCardList], Mappings.FirstOrDefault());

            DebugImportProcess();

            Debug.WriteLine($"Import UUID Resolution Summary: TotalItems={result.TotalItems}, ItemsWithUuid={result.ItemsWithUuid}, ItemsWithMultipleUuids={result.ItemsWithMultipleUuids}");
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
        private void Cancel() => CancelImport();
        private void CancelImport()
        {
            ImportCardList.Clear();
            Mappings.Clear();
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
