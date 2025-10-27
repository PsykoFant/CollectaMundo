using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ViewModels.ImportSteps;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows;

namespace CollectaMundo.ViewModels
{
    public partial class ImportViewModel(IUserPromptService userPromptService) : ObservableObject
    {
        private readonly IUserPromptService _userPromptService = userPromptService;

        [ObservableProperty]
        private Visibility importOverlayVisibility = Visibility.Collapsed;

        [ObservableProperty]
        private object? currentStepViewModel;

        public async Task Begin()
        {
            // Can use _userPromptService here if needed later
            CurrentStepViewModel = new ImportStartViewModel(this);

            var tcs = _userPromptService.CreatePrompt();
            var confirmed = await tcs.Task;

            if (confirmed)
            {
                // User finished import successfully
            }
            else
            {
                // Wizard was cancelled
            }

        }
    }
}
