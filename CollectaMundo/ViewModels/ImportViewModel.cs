using CollectaMundo.ApplicationServices.Import;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.Import;
using CollectaMundo.ViewModels.ImportSteps;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;

namespace CollectaMundo.ViewModels
{
    public partial class ImportViewModel(IImportService importService, IUserPromptService userPromptService) : ObservableObject
    {
        private readonly IImportService _importService = importService;
        private readonly IUserPromptService _userPromptService = userPromptService;
        public event Action<bool>? UiBusyChanged;

        public void SetUiBusy(bool isBusy)
        {
            UiBusyChanged?.Invoke(isBusy);
        }


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
                ImportStep.NameAndSetMapping => ImportStep.AdditionalFieldsMapping,
                _ => ImportStep.Finish
            };

            CurrentStepViewModel = _currentStep switch
            {
                ImportStep.Start => new ImportStep1_StartViewModel(this),
                ImportStep.IdColumnMapping => new ImportStep2_IdMappingViewModel(this),
                _ => throw new NotSupportedException("Unknown step")
            };
        }
        public async Task Begin()
        {
            // Can use _userPromptService here if needed later
            CurrentStepViewModel = new ImportStep1_StartViewModel(this);

            var tcs = _userPromptService.CreatePrompt();
            var confirmed = await tcs.Task;

            if (confirmed)
            {
                // User finished import successfully
            }
        }

        public void Step1ToStep2()
        {
            var filePath = _importService.PromptForCsvFile();

            if (!string.IsNullOrEmpty(filePath))
            {
                GoToNextStep(); // This will create ImportStep2_IdMappingViewModel
            }
        }

        [RelayCommand]
        private void Cancel() => CancelImport();
        private void CancelImport()
        {
            _userPromptService.CancelPendingPrompt();
            ImportOverlayVisibility = Visibility.Collapsed;
            SetUiBusy(false);
            CurrentStepViewModel = null;
            _currentStep = ImportStep.Start;
        }

    }
}
