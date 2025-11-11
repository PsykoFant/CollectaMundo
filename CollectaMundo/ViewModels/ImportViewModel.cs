using CollectaMundo.ApplicationServices.Import;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ApplicationServices.Shared.Progress;
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

        private ProgressSinks? _progressSinks;
        private ProgressSinks Progress => _progressSinks ??= CreateProgressSinks();
        private ProgressSinks CreateProgressSinks() => new()
        {
            Percent = new Progress<int>(v => ProgressValue = v),
            ProgressBarVisible = new Progress<bool>(v => ProgressVisibility = v ? Visibility.Visible : Visibility.Collapsed),
            Headline = new Progress<string>(_ => { }),
            Detail = new Progress<string>(_ => { }),
            Step = new Progress<string>(_ => { }),
            CancelEnabled = new Progress<bool>(v => { true})
        };

        [ObservableProperty]
        private bool isProcessing = false;
        public bool IsActionButtonEnabled => !IsProcessing;

        [ObservableProperty]
        private int progressValue;

        [ObservableProperty]
        private Visibility progressVisibility = Visibility.Collapsed;

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
        public async Task<OperationResult> AfterStep1Action()
        {
            try
            {
                // Let the user pick the CSV file
                var filePath = _importService.PromptForCsvFile();
                if (string.IsNullOrEmpty(filePath))
                    return new(OperationResultCode.CancelledByUser, "User cancelled file selection.");

                // Show the progress bar
                Progress.ProgressBarVisible.Report(true);
                Progress.Percent.Report(0);
                Progress.Step.Report("Parsing CSV file...");

                // Perform parsing (ParseCsvFileAsync now reports progress internally)
                var (parsedItems, mapping) = await _importService.LoadCsvFileAsync(filePath, _progressSinks);

                // Hide progress bar when done
                Progress.ProgressBarVisible.Report(false);

                // Handle results
                if (parsedItems.Count == 0)
                    return new(OperationResultCode.Empty, "The selected CSV file is empty.");

                ImportCardList.Clear();
                foreach (var item in parsedItems)
                    ImportCardList.Add(item);

                Mappings.Clear();
                Mappings.Add(mapping);

                GoToStep(ImportStep.IdColumnMapping);
                return new(OperationResultCode.Success, "CSV parsed successfully.");
            }
            catch (Exception ex)
            {
                // Ensure bar hides even on error
                ProgressVisibility = Visibility.Collapsed;
                return new(OperationResultCode.Error, $"Failed to parse CSV: {ex.Message}");
            }
        }


        public async Task<OperationResult> AfterStep2Action()
        {
            try
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
                return new OperationResult(OperationResultCode.Success, "ID mapping ended successfully.");
            }
            catch (Exception ex)
            {
                return new OperationResult(OperationResultCode.Error, $"Failed during ID mapping: {ex.Message}");
            }
        }
        public async Task<OperationResult> AfterStep3Action()
        {
            try
            {
                GoToStep(ImportStep.AdditionalFieldsMapping);

                return new OperationResult(OperationResultCode.Success, "Name and set mapping ended successfully.");
            }
            catch (Exception ex)
            {
                return new OperationResult(OperationResultCode.Error, $"Failed during name and set mapping: {ex.Message}");
            }
        }
        public async Task<OperationResult> AfterStep4Action()
        {
            try
            {
                GoToStep(ImportStep.AdditionalFieldsMapping);

                return new OperationResult(OperationResultCode.Success, "Multiple uuids mapping ended successfully.");
            }
            catch (Exception ex)
            {
                return new OperationResult(OperationResultCode.Error, $"Failed during Multiple uuids mapping: {ex.Message}");
            }
        }
        public async Task<OperationResult> AfterStep5Action()
        {
            try
            {
                GoToStep(ImportStep.AdditionalFieldsMapping);

                return new OperationResult(OperationResultCode.Success, "Additional fields mapping ended successfully.");
            }
            catch (Exception ex)
            {
                return new OperationResult(OperationResultCode.Error, $"Failed during additional fields mapping: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task PrimaryActionAsync()
        {
            await ExecuteStepAsync(() => CurrentStepViewModel!.OnPrimaryAction(), _currentStep.ToString());
        }

        [RelayCommand]
        private void SecondaryAction()
        {
            CurrentStepViewModel!.OnSecondaryAction();
        }

        private async Task ExecuteStepAsync(Func<Task<OperationResult>> stepFunc, string stepName)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                IsProcessing = true;
                CrunchingDataMessage = $"Processing step '{stepName}'...";

                var result = await stepFunc();

                switch (result.Code)
                {
                    case OperationResultCode.Success:
                        Debug.WriteLine($"{stepName} completed successfully: {result.Message}");
                        break;

                    case OperationResultCode.Empty:
                    case OperationResultCode.Error:
                        MessageBox.Show(result.Message, "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        Debug.WriteLine($"{stepName} failed: {result.Message}");
                        break;

                    case OperationResultCode.CancelledByUser:
                        Debug.WriteLine($"{stepName} cancelled by user.");
                        break;

                    default:
                        Debug.WriteLine($"{stepName} ended with status {result.Code}: {result.Message}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unhandled exception in {stepName}: {ex}");
                MessageBox.Show($"Unexpected error during {stepName}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                IsProcessing = false;
                CrunchingDataMessage = string.Empty;
            }
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
