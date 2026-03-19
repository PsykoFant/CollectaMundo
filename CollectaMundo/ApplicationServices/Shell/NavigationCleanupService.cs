using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ViewModels.Pages.SharedElements;
using System.Diagnostics;

namespace CollectaMundo.ApplicationServices.Shell
{
    public sealed class NavigationCleanupService(IUserPromptService userPromptService, IOperationOverlayController operationOverlayController, IImportOverlayController importOverlayController) : INavigationCleanupService
    {
        private readonly IUserPromptService _userPromptService = userPromptService;
        private readonly IOperationOverlayController _operationOverlayController = operationOverlayController;
        private readonly IImportOverlayController _importOverlayController = importOverlayController;

        public void CleanupBeforePageChange(object? oldPageViewModel, object? newPageViewModel)
        {
            if (ReferenceEquals(oldPageViewModel, newPageViewModel))
            {
                return;
            }

            if (oldPageViewModel is IClearPageStatus clearPageStatus)
            {
                Debug.WriteLine($"[NavCleanup] Calling ClearPageStatus on {oldPageViewModel!.GetType().FullName}");
                clearPageStatus.ClearPageStatus();
            }

            _userPromptService.ResetInteractionState();
            _importOverlayController.HideImportOverlayAndEndImport();
            _operationOverlayController.Hide();
        }
    }
}
