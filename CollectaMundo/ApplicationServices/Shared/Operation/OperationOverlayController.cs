using CollectaMundo.Presentation;
using CollectaMundo.ViewModels.Shared;

namespace CollectaMundo.ApplicationServices.Shared.Operation
{
    public sealed class OperationOverlayController(OperationOverlayViewModel operationOverlayVm) : IOperationOverlayController
    {
        private readonly OperationOverlayViewModel _operationOverlayVm = operationOverlayVm;
        public void Show(string headline, bool showProgress = false)
        {
            _operationOverlayVm.Show(headline, showProgress);
        }
        public void Hide() => _operationOverlayVm.Hide();
        public void Reset() => _operationOverlayVm.Reset();
        public void SetHeadline(string text) => _operationOverlayVm.Headline = text;
        public void SetDetail(string text) => _operationOverlayVm.Detail = text;
        public void SetStep(string text) => _operationOverlayVm.Step = text;
        public void SetProgress(int value) => _operationOverlayVm.ProgressValue = value;
        public void ShowLogo(bool show)
        {
            _operationOverlayVm.IsLogoVisible = show;
        }
        public void ShowProgress(bool show)
        {
            _operationOverlayVm.IsProgressVisible = show;
        }
        public void ShowPrimaryButton(string text, Action<object?>? action = null)
        {
            _operationOverlayVm.PrimaryButtonText = text;
            _operationOverlayVm.IsPrimaryButtonVisible = true;
            _operationOverlayVm.SetPrimaryAction(action);
        }
        public void SetPrimaryButtonText(string text)
        {
            _operationOverlayVm.IsPrimaryButtonVisible = true;
            _operationOverlayVm.PrimaryButtonText = text;
        }
        public void HidePrimaryButton()
        {
            _operationOverlayVm.IsPrimaryButtonVisible = false;
            _operationOverlayVm.SetPrimaryAction(null);
        }
        public void ShowSecondaryButton(string text, Action<object?>? action = null)
        {
            _operationOverlayVm.SecondaryButtonText = text;
            _operationOverlayVm.IsSecondaryButtonVisible = true;
            _operationOverlayVm.SetSecondaryAction(action);
        }
        public void SetSecondaryButtonText(string text)
        {
            _operationOverlayVm.IsSecondaryButtonVisible= true;
            _operationOverlayVm.SecondaryButtonText = text;
        }
        public void HideSecondaryButton()
        {
            _operationOverlayVm.IsSecondaryButtonVisible = false;
            _operationOverlayVm.SecondaryButtonText = string.Empty;
            _operationOverlayVm.SetSecondaryAction(null);
        }
        public void ShowSetupFailure(bool show)
        {
            _operationOverlayVm.IsSetupFailVisible = show;
        }
        public CancellationToken PrepareCancelButton(PromptButtonEnum button) => _operationOverlayVm.PrepareCancelButton(button);
        public Task<bool> WaitForUserConfirmationAsync(PromptButtonEnum button, string confirmText) => _operationOverlayVm.WaitForUserConfirmationAsync(button, confirmText);
    }
}
