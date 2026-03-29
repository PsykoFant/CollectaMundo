using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ViewModels.Pages.SharedElements;
using System.Diagnostics;

namespace CollectaMundo.ApplicationServices.Navigation
{
    public sealed class NavigationCleanupService(IUserPromptService userPromptService, IOperationOverlayController operationOverlayController, IUtilitiesNavigator utilitiesNavigator) : INavigationCleanupService
    {
        private readonly IUserPromptService _userPromptService = userPromptService;
        private readonly IOperationOverlayController _operationOverlayController = operationOverlayController;
        private readonly IUtilitiesNavigator _utilitiesNavigator = utilitiesNavigator;

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

            _utilitiesNavigator.ShowHome();
            _userPromptService.ResetInteractionState();
            _operationOverlayController.Hide();
        }
    }
}
