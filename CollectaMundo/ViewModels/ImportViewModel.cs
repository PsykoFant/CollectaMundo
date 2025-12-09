using CollectaMundo.ApplicationServices.Import;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ApplicationServices.Shared.Progress;
using CollectaMundo.DomainLogic.Import.Models;
using CollectaMundo.DomainLogic.Import.Models.Enums;
using CollectaMundo.ViewModels.ImportSteps;
using CollectaMundo.ViewModels.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;

namespace CollectaMundo.ViewModels
{
    public partial class ImportViewModel(IImportService importService, IParentViewModelContext parentContext, IUserPromptService userPromptService, CardImageViewModel cardImageVM) : ObservableObject
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
        public CardImageViewModel CardImageVM { get; } = cardImageVM;

        [ObservableProperty]
        private string? progressHeadline;

        [ObservableProperty]
        private string? progressStep;

        [ObservableProperty]
        private string? progressDetailMessage;

        //  Button Enablement
        // When either of these two change, the computed button properties must refresh
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPrimaryActionButtonEnabled))]
        [NotifyPropertyChangedFor(nameof(IsSecondaryActionButtonEnabled))]
        private bool isProcessing;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPrimaryActionButtonEnabled))]
        [NotifyPropertyChangedFor(nameof(IsSecondaryActionButtonEnabled))]
        private IImportStepViewModel? currentStepViewModel;

        // Computed button states (unchanged logic)
        public bool IsPrimaryActionButtonEnabled => !IsProcessing && (CurrentStepViewModel?.CanExecutePrimaryAction ?? false);
        public bool IsSecondaryActionButtonEnabled => !IsProcessing && (CurrentStepViewModel?.CanExecuteSecondaryAction ?? false);

        // Handle subscriptions for child VM PropertyChanged events
        partial void OnCurrentStepViewModelChanged(IImportStepViewModel? oldValue, IImportStepViewModel? newValue)
        {
            if (oldValue is INotifyPropertyChanged oldNotify)
                oldNotify.PropertyChanged -= CurrentStep_PropertyChanged;

            if (newValue is INotifyPropertyChanged newNotify)
                newNotify.PropertyChanged += CurrentStep_PropertyChanged;
        }

        // Forward relevant child property changes to parent computed properties
        private void CurrentStep_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IImportStepViewModel.CanExecutePrimaryAction))
            {
                OnPropertyChanged(nameof(IsPrimaryActionButtonEnabled));
            }

            if (e.PropertyName == nameof(IImportStepViewModel.CanExecuteSecondaryAction))
            {
                OnPropertyChanged(nameof(IsSecondaryActionButtonEnabled));
            }
        }


        [ObservableProperty]
        private int progressValue;

        [ObservableProperty]
        private Visibility progressVisibility = Visibility.Collapsed;

        [ObservableProperty]
        private Visibility importFailVisibility = Visibility.Collapsed;

        [ObservableProperty]
        private Visibility cancelVisibility = Visibility.Collapsed;


        public static ObservableCollection<TempCardItem> ImportCardList { get; } = [];
        public ObservableCollection<IdColumnMapping> IdMappings { get; } = [];
        public ObservableCollection<CsvFieldMapping> NameSetMappings { get; } = [];
        public ObservableCollection<CsvFieldMapping> AdditionalMappings { get; } = [];


        [ObservableProperty]
        private Visibility importOverlayVisibility = Visibility.Collapsed;

        private ImportStep _currentStep = ImportStep.Start;
        public void GoToStep(ImportStep step)
        {
            _currentStep = step;
            Debug.WriteLine($"ImportViewModel: Navigating to {_currentStep}.");

            CurrentStepViewModel = step switch
            {
                ImportStep.Start => CreateStep(new ImportStep01_StartViewModel(this), string.Empty),
                ImportStep.IdColumnMapping => CreateStep(new ImportStep02_IdMappingViewModel(this), "ID column mapping"),
                ImportStep.NameAndSetMapping => CreateStep(new ImportStep03_NameSetMappingViewModel(this), "Name and set mapping"),
                ImportStep.MultipleUuidsSelection => CreateStep(new ImportStep04_MultipleUuidsViewModel(this), "Resolve multiple UUID matches"),
                ImportStep.AdditionalFieldsMapping => CreateStep(new ImportStep05_AdditionalFieldsMappingViewModel(this), "Additional fields mapping"),
                ImportStep.Finish => CreateStep(new ImportStep10_FinishViewModel(this), string.Empty),
                _ => throw new NotSupportedException($"Unknown import step: {step}")
            };
        }

        // GoToStep helper that also sets progress step text
        private IImportStepViewModel CreateStep(IImportStepViewModel vm, string progressStepText)
        {
            Progress.Step.Report(progressStepText);
            return vm;
        }

        public async Task Begin()
        {
            GoToStep(ImportStep.Start);
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
                Progress.ProgressBarVisible.Report(true);
                Progress.Detail.Report("Parsing CSV file...");

                // Prepare cancel
                CancelVisibility = Visibility.Visible;
                var cancelToken = _userPromptService.GetNewCancellationToken();

                // Perform parsing (ParseCsvFileAsync now reports progress internally)
                var (parsedItems, mapping) = await _importService.LoadCsvFileAsync(filePath, Progress, cancelToken);

                // Handle results
                if (parsedItems.Count == 0)
                {
                    return new(OperationResultCode.Empty, "The selected CSV file is malformed or empty.");
                }

                foreach (var item in parsedItems)
                {
                    cancelToken.ThrowIfCancellationRequested();
                    ImportCardList.Add(item);
                }

                IdMappings.Add(mapping);

                GoToStep(ImportStep.IdColumnMapping);
                return new(OperationResultCode.Success, "CSV parsed successfully.");
            }
        }
        public async Task<OperationResult> AfterStep2Action()
        {
            var mapping = IdMappings.FirstOrDefault() ?? throw new InvalidOperationException("No mapping found after Step 2");

            Progress.ProgressBarVisible.Report(true);
            Progress.Detail.Report("Please wait - attempting to match ids...");

            // Prepare cancel
            var cancelToken = _userPromptService.GetNewCancellationToken();

            var result = await Task.Run(() => _importService.TryResolveUuidsFromMappedIdAsync([.. ImportCardList], mapping, Progress, cancelToken));

            Debug.WriteLine("ImportViewModel: ID Matching done");

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
            Progress.ProgressBarVisible.Report(true);
            Progress.Detail.Report("Please wait - attempting to match by name and set...");

            // Prepare cancel
            var cancelToken = _userPromptService.GetNewCancellationToken();

            var result = await Task.Run(() => _importService.TryResolveUuidsFromNameAndSetAsync(ImportCardList, NameSetMappings, Progress, cancelToken));

            if (result.ItemsWithMultipleUuids > 0)
            {
                _parentViewModelContext.CardViewSectionVisibility = Visibility.Visible;
                GoToStep(ImportStep.MultipleUuidsSelection);
            }
            else
            {
                GoToStep(ImportStep.AdditionalFieldsMapping);
            }

            return new OperationResult(OperationResultCode.Success, "Name and set mapping ended successfully.");
        }
        public async Task<OperationResult> AfterStep4Action()
        {
            _parentViewModelContext.CardViewSectionVisibility = Visibility.Collapsed;

            // Pass user choices to service layer
            var result = await Task.Run(() =>
                _importService.ApplyUserSelectedUuids(ImportCardList, GetStep4Selections(), Progress)
            );

            if (result.ItemsWithMultipleUuids > 0)
            {
                return new OperationResult(OperationResultCode.Error, "Some cards still have multiple UUIDs. Please make a selection for each.");
            }

            GoToStep(ImportStep.AdditionalFieldsMapping);
            return new OperationResult(OperationResultCode.Success, "User selections applied.");

            // Local function to get selections from Step 4 VM
            List<MultipleUuidsItem> GetStep4Selections()
            {
                if (CurrentStepViewModel is ImportStep04_MultipleUuidsViewModel step4)
                {
                    return [.. step4.MultipleChoices];
                }

                return [];
            }
        }
        public Task<OperationResult> AfterStep5Action()
        {
            var mappedFields = AdditionalMappings.Where(m => !string.IsNullOrWhiteSpace(m.SelectedCsvHeader)).Select(m => m.FieldToMap).ToHashSet();

            // Register booleans in parent
            var IsConditionMapped = mappedFields.Contains(ImportField.Condition);
            var IsCardFinishMapped = mappedFields.Contains(ImportField.CardFinish);
            var IsLanguageMapped = mappedFields.Contains(ImportField.Language);

            if (IsConditionMapped)
            {
                Debug.WriteLine("ImportViewModel: Condition field mapped, going to ConditionMapping step.");
                //GoToStep(ImportStep.ConditionMapping);
            }
            else if (IsCardFinishMapped)
            {
                Debug.WriteLine("ImportViewModel: CardFinish field mapped, going to FinishMapping step.");
                //GoToStep(ImportStep.FinishMapping);
            }
            else if (IsLanguageMapped)
            {
                Debug.WriteLine("ImportViewModel: Language field mapped, going to LanguageMapping step.");
                //GoToStep(ImportStep.LanguageMapping);
            }
            else
            {
                Debug.WriteLine("ImportViewModel: No additional fields mapped, going to Summary step.");
                //GoToStep(ImportStep.Summary);
            }

            return Task.FromResult(new OperationResult(OperationResultCode.Success, "Field mappings processed."));
        }

        public async Task<OperationResult> AfterStep10Action()
        {
            ImportCardList.Clear();
            IdMappings.Clear();
            NameSetMappings.Clear();
            AdditionalMappings.Clear();

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
        private async Task SecondaryActionAsync()
        {
            await ExecuteStepAsync(() => CurrentStepViewModel!.OnSecondaryAction(), _currentStep.ToString());
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
                    CancelVisibility = Visibility.Collapsed;
                }

                ClearProgress();
                switch (result.Code)
                {
                    case OperationResultCode.Success:
                        Debug.WriteLine($"{stepName} completed successfully: {result.Message}");
                        break;

                    case OperationResultCode.Empty:
                        Progress.Headline.Report("Import Failed!");
                        Progress.Detail.Report(result.Message);
                        GoToStep(ImportStep.Finish);
                        Debug.WriteLine($"{stepName} resulted in empty data: {result.Message}");
                        break;

                    case OperationResultCode.Error:
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
            CancelVisibility = Visibility.Collapsed;
            _parentViewModelContext.CardViewSectionVisibility = Visibility.Collapsed;
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
        public void DebugNameSetMappings()
        {
            Debug.WriteLine("---- NameSetMappings ----");

            foreach (var mapping in NameSetMappings)
            {
                Debug.WriteLine($"FieldToMap: {mapping.FieldToMap}");
                Debug.WriteLine($"  SelectedCsvHeader: {mapping.SelectedCsvHeader}");

                if (mapping.CsvHeaders is { Count: > 0 })
                {
                    Debug.WriteLine("  CsvHeaders:");
                    foreach (var header in mapping.CsvHeaders)
                    {
                        Debug.WriteLine($"    - {header}");
                    }
                }
                else
                {
                    Debug.WriteLine("  CsvHeaders: (none)");
                }

                Debug.WriteLine("");
            }

            Debug.WriteLine("-------------------------");
        }
    }
}
