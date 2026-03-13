using CollectaMundo.Presentation;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace CollectaMundo.ApplicationServices.Shared
{
    public interface IOperationOverlayController
    {
        void Show(string headline, bool showProgress = false);
        void Hide();
        void Reset();

        void SetHeadline(string text);
        void SetDetail(string text);
        void SetStep(string text);
        void SetProgress(int value);

        void ShowLogo(bool show);
        void ShowProgress(bool show);

        void ShowPrimaryButton(string text, Action<object?>? action = null);
        void SetPrimaryButtonText(string text);        
        void HidePrimaryButton();

        void ShowSecondaryButton(string text, Action<object?>? action = null);
        void SetSecondaryButtonText(string text);
        void HideSecondaryButton();

        void ShowSetupFailure(bool show);

        CancellationToken PrepareCancelButton(PromptButton button);
        Task<bool> WaitForUserConfirmationAsync(PromptButton button, string confirmText);
    }
}
