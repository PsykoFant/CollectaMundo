using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ViewModels.Pages.SharedElements;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            WriteNavigationDebug(oldPageViewModel, newPageViewModel);

            if (ReferenceEquals(oldPageViewModel, newPageViewModel))
                return;

            if (oldPageViewModel is IClearPageStatus clearPageStatus)
            {
                Debug.WriteLine($"[NavCleanup] Calling ClearPageStatus on {oldPageViewModel!.GetType().FullName}");
                clearPageStatus.ClearPageStatus();
            }
            else
            {
                Debug.WriteLine("[NavCleanup] oldPageViewModel does NOT implement IClearPageStatus");
            }

            _userPromptService.CancelPendingPrompt();
            _userPromptService.CancelCurrentOperation();
            _userPromptService.ClearCancellation();

            _operationOverlayController.Hide();
        }

        private static void WriteNavigationDebug(object? oldPageViewModel, object? newPageViewModel)
        {
            string Describe(object? vm)
            {
                if (vm is null)
                    return "<null>";

                var typeName = vm.GetType().FullName ?? vm.GetType().Name;
                var implementsClear = vm is IClearPageStatus;
                return $"{typeName} | IClearPageStatus={implementsClear} | HashCode={vm.GetHashCode()}";
            }

            Debug.WriteLine("========== Navigation Cleanup ==========");
            Debug.WriteLine($"Old: {Describe(oldPageViewModel)}");
            Debug.WriteLine($"New: {Describe(newPageViewModel)}");
            Debug.WriteLine($"ReferenceEquals: {ReferenceEquals(oldPageViewModel, newPageViewModel)}");
            Debug.WriteLine("=======================================");
        }
    }
}
