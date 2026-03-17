using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ViewModels.Pages.SharedElements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollectaMundo.ApplicationServices.Shell
{
    public sealed class NavigationCleanupService(IUserPromptService userPromptService,IOperationOverlayController operationOverlayController) : INavigationCleanupService
    {
        private readonly IUserPromptService _userPromptService = userPromptService;
        private readonly IOperationOverlayController _operationOverlayController = operationOverlayController;

        public void CleanupBeforePageChange(object? oldPageViewModel, object? newPageViewModel)
        {
            if (ReferenceEquals(oldPageViewModel, newPageViewModel))
                return;

            if (oldPageViewModel is IClearPageStatus clearPageStatus)
            {
                clearPageStatus.ClearPageStatus();
            }

            _userPromptService.CancelPendingPrompt();
            _userPromptService.CancelCurrentOperation();
            _userPromptService.ClearCancellation();

            _operationOverlayController.Hide();
            _operationOverlayController.Hide();
        }
    }
}
