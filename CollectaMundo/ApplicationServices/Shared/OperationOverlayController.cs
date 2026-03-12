using CollectaMundo.Presentation;
using CollectaMundo.ViewModels.Shared;

namespace CollectaMundo.ApplicationServices.Shared
{
    public sealed class OperationOverlayController(OperationOverlayViewModel vm) : IOperationOverlayController
    {
        private readonly OperationOverlayViewModel _vm = vm;
        public void Show(string headline, bool showProgress = false)
        {
            _vm.Show(headline, showProgress);
        }
        public void Hide() => _vm.Hide();
        public void Reset() => _vm.Reset();
        public void SetHeadline(string text) => _vm.Headline = text;
        public void SetDetail(string text) => _vm.Detail = text;
        public void SetStep(string text) => _vm.Step = text;
        public void SetProgress(int value) => _vm.ProgressValue = value;
        public void ShowLogo(bool show)
        {
            _vm.IsLogoVisible = show;
        }
        public void ShowProgress(bool show)
        {
            _vm.IsProgressVisible = show;
        }
        public void ShowPrimaryButton(string text, Action<object?>? action = null)
        {
            _vm.PrimaryButtonText = text;
            _vm.IsPrimaryButtonVisible = true;
            _vm.SetPrimaryAction(action);
        }
        public void HidePrimaryButton()
        {
            _vm.IsPrimaryButtonVisible = false;
            _vm.SetPrimaryAction(null);
        }
        public void ShowSecondaryButton(string text, Action<object?>? action = null)
        {
            _vm.SecondaryButtonText = text;
            _vm.IsSecondaryButtonVisible = true;
            _vm.SetSecondaryAction(action);
        }
        public void HideSecondaryButton()
        {
            _vm.IsSecondaryButtonVisible = false;
            _vm.SecondaryButtonText = string.Empty;
            _vm.SetSecondaryAction(null);
        }
        public void ShowSetupFailure(bool show)
        {
            _vm.IsSetupFailVisible = show;
        }
        public CancellationToken PrepareCancelButton(PromptButton button) => _vm.PrepareCancelButton(button);
        public Task<bool> WaitForUserConfirmationAsync(PromptButton button, string confirmText) => _vm.WaitForUserConfirmationAsync(button, confirmText);
    }
}
