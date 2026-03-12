using CollectaMundo.Presentation;
using CollectaMundo.ViewModels.Shared;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace CollectaMundo.ApplicationServices.Shared
{
    public sealed class OperationOverlayController(OperationOverlayViewModel vm) : IOperationOverlayController
    {
        private readonly OperationOverlayViewModel _vm = vm;

        public void Show(string headline, bool showProgress = false)
        {
            _vm.Reset();
            _vm.Headline = headline;
            _vm.Show(showProgress);
        }

        public void Hide() => _vm.Hide();

        public void Reset() => _vm.Reset();

        public void SetHeadline(string text) => _vm.Headline = text;

        public void SetDetail(string text) => _vm.Detail = text;

        public void SetStep(string text) => _vm.Step = text;

        public void SetProgress(int value) => _vm.ProgressValue = value;

        public void ShowLogo(bool show)
        {
            _vm.LogoVisibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        public void ShowProgress(bool show)
        {
            _vm.ProgressVisibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        public void ShowPrimaryButton(string text, Action<object?>? action = null)
        {
            _vm.PrimaryButtonText = text;
            _vm.PrimaryButtonVisibility = Visibility.Visible;
            _vm.SetPrimaryAction(action);
        }

        public void HidePrimaryButton()
        {
            _vm.PrimaryButtonVisibility = Visibility.Collapsed;
            _vm.SetPrimaryAction(null);
        }

        public void ShowSecondaryButton(string text, Action<object?>? action = null)
        {
            _vm.SecondaryButtonText = text;
            _vm.SecondaryButtonVisibility = Visibility.Visible;
            _vm.SetSecondaryAction(action);
        }

        public void HideSecondaryButton()
        {
            _vm.SecondaryButtonVisibility = Visibility.Collapsed;
            _vm.SecondaryButtonText = string.Empty;
            _vm.SetSecondaryAction(null);
        }

        public void ShowSetupFailure(bool show)
        {
            _vm.SetupFailVisibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        public CancellationToken PrepareCancelButton(PromptButton button)
            => _vm.PrepareCancelButton(button);

        public Task<bool> WaitForUserConfirmationAsync(PromptButton button, string confirmText)
            => _vm.WaitForUserConfirmationAsync(button, confirmText);
    }
}
