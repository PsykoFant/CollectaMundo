using CollectaMundo.ApplicationServices.Import;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.Import.Models;
using CollectaMundo.ViewModels.ImportSteps;
using CollectaMundo.ViewModels.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;

namespace CollectaMundo.ViewModels
{
    public partial class ImportViewModel(IImportService importService, IParentViewModelContext parentContext, IUserPromptService userPromptService) : ObservableObject
    {
        private readonly IImportService _importService = importService;
        private readonly IParentViewModelContext _parentViewModelContext = parentContext;
        private readonly IUserPromptService _userPromptService = userPromptService;

        public ObservableCollection<ColumnMapping> Mappings { get; } = [];

        [ObservableProperty]
        private Visibility importOverlayVisibility = Visibility.Collapsed;

        [ObservableProperty]
        private IImportStepViewModel? currentStepViewModel;

        private ImportStep _currentStep = ImportStep.Start;

        public void GoToNextStep()
        {
            _currentStep = _currentStep switch
            {
                ImportStep.Start => ImportStep.IdColumnMapping,
                ImportStep.IdColumnMapping => ImportStep.NameAndSetMapping,
                ImportStep.NameAndSetMapping => ImportStep.MultipleUuidsSelection,
                ImportStep.MultipleUuidsSelection => ImportStep.AdditionalFieldsMapping,
                _ => ImportStep.Finish
            };

            CurrentStepViewModel = _currentStep switch
            {
                ImportStep.Start => new ImportStep1_StartViewModel(this),
                ImportStep.IdColumnMapping => new ImportStep2_IdMappingViewModel(this),
                ImportStep.NameAndSetMapping => new ImportStep3_NameSetMappingViewModel(this),
                ImportStep.MultipleUuidsSelection => new ImportStep4_MultipleUuidsViewModel(this),
                ImportStep.AdditionalFieldsMapping => new ImportStep5_AdditionalFieldsMappingViewModel(this),
                _ => throw new NotSupportedException("Unknown step")
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
                Mappings.Clear();
                var mapping = await _importService.LoadCsvFileAsync(filePath);
                Mappings.Add(mapping);
                GoToNextStep();
            }
        }
        public async Task AfterStep2Action()
        {
            // Placeholder for any actions needed after step 2
        }


        [RelayCommand]
        private void Cancel() => CancelImport();
        private void CancelImport()
        {
            Mappings.Clear();
            _userPromptService.CancelPendingPrompt();
            ImportOverlayVisibility = Visibility.Collapsed;
            CurrentStepViewModel = null;
            _currentStep = ImportStep.Start;
            _parentViewModelContext.SetUiBusy(false);
        }

    }
}
