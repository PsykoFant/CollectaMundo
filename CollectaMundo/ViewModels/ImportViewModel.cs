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

namespace CollectaMundo.ViewModels
{
    public partial class ImportViewModel(IImportService importService, IParentViewModelContext parentContext, IUserPromptService userPromptService) : ObservableObject
    {
        private readonly IImportService _importService = importService;
        private readonly IParentViewModelContext _parentViewModelContext = parentContext;
        private readonly IUserPromptService _userPromptService = userPromptService;
        private ProgressSinks Progress => CreateProgressSinks();
        private ProgressSinks CreateProgressSinks() => new()
        {
            Percent = new Progress<int>(v => ProgressValue = v),
            ProgressBarVisible = new Progress<bool>(v => ProgressVisibility = v ? Visibility.Visible : Visibility.Collapsed),
            Headline = new Progress<string>(v => ProgressHeadline = v),
            Step = new Progress<string>(v => ProgressStep = v),
            Detail = new Progress<string>(v => ProgressDetailMessage = v),
            CancelEnabled = new Progress<bool>(_ => { })
        };

        [ObservableProperty]
        private string? progressHeadline;

        [ObservableProperty]
        private string? progressStep;


        [ObservableProperty]
        private string? progressDetailMessage;

        [ObservableProperty]
        private bool isProcessing = false;
        public bool IsActionButtonEnabled => !IsProcessing;

        [ObservableProperty]
        private int progressValue;

        [ObservableProperty]
        private Visibility progressVisibility = Visibility.Collapsed;

        [ObservableProperty]
        private Visibility importFailVisibility = Visibility.Collapsed;

        [ObservableProperty]
        private Visibility cancelVisibility = Visibility.Collapsed;
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
                ImportStep.Start => new ImportStep01_StartViewModel(this),
                ImportStep.IdColumnMapping => new ImportStep02_IdMappingViewModel(this),
                ImportStep.NameAndSetMapping => new ImportStep03_NameSetMappingViewModel(this),
                ImportStep.MultipleUuidsSelection => new ImportStep04_MultipleUuidsViewModel(this),
                ImportStep.AdditionalFieldsMapping => new ImportStep05_AdditionalFieldsMappingViewModel(this),
                ImportStep.Finish => new ImportStep10_FinishViewModel(this),
                _ => throw new NotSupportedException($"Unknown import step: {step}")
            };
        }
        public async Task Begin()
        {
            CurrentStepViewModel = new ImportStep01_StartViewModel(this);
            Progress.Headline.Report("The Import Wizard");

            _ = await _userPromptService.CreatePrompt().Task;
        }

        public async Task<OperationResult> AfterStep1Action()
        {
            _parentViewModelContext.SetUiBusy(true);

            // Let the user pick the CSV file
            var filePath = _importService.PromptForCsvFile();
            if (string.IsNullOrEmpty(filePath))
            {
                return new(OperationResultCode.CancelledByUser, "User cancelled file selection.");
            }
            else
            {
                if (CurrentStepViewModel is ImportStep01_StartViewModel step1) { step1.FlowDocumentVisibility = Visibility.Collapsed; } // Hide instructions during processing

                Progress.ProgressBarVisible.Report(true);
                Progress.Headline.Report(string.Empty);
                Progress.Step.Report("CSV PARSING");
                Progress.Detail.Report("Parsing CSV file...");

                // Prepare cancel
                CancelVisibility = Visibility.Visible;
                var cancelToken = _userPromptService.GetNewCancellationToken();

                // Perform parsing (ParseCsvFileAsync now reports progress internally)
                var (parsedItems, mapping) = await _importService.LoadCsvFileAsync(filePath, Progress, cancelToken);

                // Handle results
                if (parsedItems.Count == 0)
                    return new(OperationResultCode.Empty, "The selected CSV file is malformed or empty.");

                foreach (var item in parsedItems)
                {
                    cancelToken.ThrowIfCancellationRequested();
                    ImportCardList.Add(item);
                }

                Mappings.Add(mapping);

                GoToStep(ImportStep.IdColumnMapping);
                return new(OperationResultCode.Success, "CSV parsed successfully.");

            }
        }
        public async Task<OperationResult> AfterStep2Action()
        {
            var mapping = Mappings.FirstOrDefault() ?? throw new InvalidOperationException("No mapping found after Step 2");

            Progress.ProgressBarVisible.Report(true);
            Progress.Step.Report("ID Matching");
            Progress.Detail.Report("Please wait - attempting to match ids...");

            // Prepare cancel
            var cancelToken = _userPromptService.GetNewCancellationToken();

            var result = await Task.Run(() => _importService.TryResolveUuidsFromMappedIdAsync([.. ImportCardList], mapping, Progress, cancelToken));

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

        public async Task<OperationResult> AfterStep10Action()
        {
            ImportCardList.Clear();
            Mappings.Clear();

            _userPromptService.CancelPendingPrompt();
            _userPromptService.ClearCancellation();

            CurrentStepViewModel = null;
            _currentStep = ImportStep.Start;

            _parentViewModelContext.SetUiBusy(false);
            ImportOverlayVisibility = Visibility.Collapsed;
            ImportFailVisibility = Visibility.Collapsed;

            return new(OperationResultCode.Success, "Cleanup completed");

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
                IsProcessing = true;

                OperationResult result;
                try
                {
                    result = await stepFunc();
                }
                catch (OperationCanceledException)
                {
                    result = new OperationResult(OperationResultCode.CancelledByUser, $"{stepName} cancelled by user.");
                }
                catch (Exception ex)
                {
                    result = new OperationResult(OperationResultCode.Error, $"Unexpected error in {stepName}: {ex.Message}");
                }

                if (result.Code != OperationResultCode.Success)
                {
                    ImportFailVisibility = Visibility.Visible;
                }

                switch (result.Code)
                {
                    case OperationResultCode.Success:
                        Debug.WriteLine($"{stepName} completed successfully: {result.Message}");
                        ClearProgress();
                        break;

                    case OperationResultCode.Empty:
                        ClearProgress();
                        CancelVisibility = Visibility.Collapsed;
                        Progress.Headline.Report("Import Failed!");
                        Progress.Detail.Report(result.Message);
                        GoToStep(ImportStep.Finish);
                        Debug.WriteLine($"{stepName} resulted in empty data: {result.Message}");
                        break;

                    case OperationResultCode.Error:
                        ClearProgress();
                        CancelVisibility = Visibility.Collapsed;
                        Progress.Headline.Report("Import Failed!");
                        Progress.Detail.Report(result.Message);
                        GoToStep(ImportStep.Finish);
                        Debug.WriteLine($"{stepName} failed: {result.Message}");
                        break;

                    case OperationResultCode.CancelledByUser:
                        Debug.WriteLine($"{stepName} cancelled by user.");
                        CancelImport();
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
                Progress.ProgressBarVisible.Report(false);
                _userPromptService.ClearCancellation();
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private void Cancel() => CancelImport();
        private void CancelImport()
        {
            Debug.WriteLine("ImportViewModel: Cancelling import operation as per user request.");
            ClearProgress();
            CancelVisibility = Visibility.Collapsed;

            _userPromptService.CancelCurrentOperation();

            ImportFailVisibility = Visibility.Visible;

            Progress.Headline.Report("Import cancelled");
            Progress.Detail.Report("User cancellation - no cards imported to collection.");
            GoToStep(ImportStep.Finish);
        }

        private void ClearProgress()
        {
            Progress.Headline.Report(string.Empty);
            Progress.Step.Report(string.Empty);
            Progress.Detail.Report(string.Empty);
            Progress.Percent.Report(0);
            Progress.ProgressBarVisible.Report(false);
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
