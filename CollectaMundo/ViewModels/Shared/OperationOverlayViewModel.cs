using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.Presentation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;

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

        public bool IsOverlayVisible = false;
        public bool IsLogoVisible = true;
        public bool IsProgressVisible = false;
        public bool IsPrimaryButtonVisible = false;
        public bool IsSecondaryButtonVisible = false;
        public bool IsSetupFailVisible = false;

        [ObservableProperty]
        private string headline = string.Empty;

        [ObservableProperty]
        private string detail = string.Empty;

        [ObservableProperty]
        private string step = string.Empty;

        [ObservableProperty]
        private int progressValue;

        [ObservableProperty]
        private string primaryButtonText = "  OK  ";

        [ObservableProperty]
        private string secondaryButtonText = string.Empty;

        [RelayCommand]
        private void PrimaryAction(object? parameter) => _primaryAction(parameter);

        [RelayCommand]
        private void SecondaryAction(object? parameter) => _secondaryAction(parameter);

        public void SetPrimaryAction(Action<object?>? action)
        {
            _primaryAction = action ?? (_ => Hide());
        }

        public void SetSecondaryAction(Action<object?>? action)
        {
            _secondaryAction = action ?? (_ => { });
        }

        public CancellationToken PrepareCancelButton(PromptButton button)
        {
            IsPrimaryButtonVisible = true;
            PrimaryButtonText = "   Cancel   ";
            return _userPromptService.Prepare(button);
        }

        public async Task<bool> WaitForUserConfirmationAsync(
            PromptButton button,
            string confirmText)
        {
            PrimaryButtonVisibility = Visibility.Visible;
            PrimaryButtonText = confirmText;
            return await _userPromptService.WaitForUserConfirmationAsync(button);
        }

        public void Show(bool showProgress = false)
        {
            IsOverlayVisible = true;
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
