using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.Presentation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CollectaMundo.ViewModels.Shared
{
    public partial class OperationOverlayViewModel : ObservableObject
    {
        private readonly IUserPromptService _userPromptService;

        private Action<object?> _primaryAction;
        private Action<object?> _secondaryAction;

        public OperationOverlayViewModel(IUserPromptService userPromptService)
        {
            _userPromptService = userPromptService;
            _primaryAction = _ => Hide();
            _secondaryAction = _ => { };
        }

        [ObservableProperty]
        private bool isOverlayVisible;

        [ObservableProperty]
        private bool isLogoVisible = true;

        [ObservableProperty]
        private bool isProgressVisible;

        [ObservableProperty]
        private bool isPrimaryButtonVisible;

        [ObservableProperty]
        private bool isSecondaryButtonVisible;

        [ObservableProperty]
        private bool isSetupFailVisible;

        [ObservableProperty]
        private string headline = string.Empty;

        [ObservableProperty]
        private string detail = string.Empty;

        [ObservableProperty]
        private string step = string.Empty;

        [ObservableProperty]
        private int progressValue;

        [ObservableProperty]
        private string primaryButtonText = "OK!";

        [ObservableProperty]
        private string secondaryButtonText = string.Empty;

        [RelayCommand]
        private void PrimaryAction(object? parameter) => _primaryAction(parameter);

        [RelayCommand]
        private void SecondaryAction(object? parameter) => _secondaryAction(parameter);

        public void SetPrimaryAction(Action<object?>? action) { _primaryAction = action ?? (_ => Hide()); }
        public void SetSecondaryAction(Action<object?>? action) { _secondaryAction = action ?? (_ => { }); }

        public CancellationToken PrepareCancelButton(PromptButtonEnum button)
        {
            var cancelMessage = "Cancelling…";
            var buttonText = "   Cancel   ";

            var token = _userPromptService.CreateOperationCancellationToken();

            switch (button)
            {
                case PromptButtonEnum.Primary:
                    IsPrimaryButtonVisible = true;
                    PrimaryButtonText = buttonText;

                    SetPrimaryAction(_ =>
                    {
                        Step = cancelMessage;
                        _userPromptService.CancelActiveOperation();
                    });
                    break;

                case PromptButtonEnum.Secondary:
                    IsSecondaryButtonVisible = true;
                    SecondaryButtonText = buttonText;

                    SetSecondaryAction(_ =>
                    {
                        Step = cancelMessage;
                        _userPromptService.CancelActiveOperation();
                    });
                    break;
            }

            return token;
        }
        public async Task<bool> WaitForUserConfirmationAsync(PromptButtonEnum button, string buttonText)
        {
            _userPromptService.DisposeActivePrompt(); // ensures only one active at a time
            var tcs = _userPromptService.CreatePrompt();

            switch (button)
            {
                case PromptButtonEnum.Primary:
                    IsPrimaryButtonVisible = true;
                    PrimaryButtonText = buttonText;
                    SetPrimaryAction(_ => _userPromptService.ConfirmActivePrompt());
                    break;
                case PromptButtonEnum.Secondary:
                    IsSecondaryButtonVisible = true;
                    SecondaryButtonText = buttonText;
                    SetSecondaryAction(_ => _userPromptService.ConfirmActivePrompt());
                    break;
            }

            return await tcs.Task;
        }
        public void Show(string message, bool showProgress = false)
        {
            Reset();
            IsOverlayVisible = true;
            IsProgressVisible = showProgress;
            Headline = message;
            IsProgressVisible = showProgress;
        }
        public void Hide()
        {
            IsOverlayVisible = false;
            Reset();
        }
        public void Reset()
        {
            IsLogoVisible = true;
            IsProgressVisible = false;

            IsPrimaryButtonVisible = false;
            IsSecondaryButtonVisible = false;

            IsSetupFailVisible = false;

            Headline = string.Empty;
            Detail = string.Empty;
            Step = string.Empty;

            ProgressValue = 0;
            PrimaryButtonText = "  OK  ";
            SecondaryButtonText = string.Empty;

            _primaryAction = _ => Hide();
            _secondaryAction = _ => { };
        }
    }
}
