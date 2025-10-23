using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.Presentation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;

namespace CollectaMundo.ViewModels
{
    public partial class StatusViewModel : ObservableObject
    {
        private readonly IUserPromptService _userPromptService;
        private Action<object?> _primaryAction;
        private Action<object?> _secondaryAction;

        #region Observable Properties
        [ObservableProperty]
        private Visibility statusOverlayVisibility;

        [ObservableProperty]
        private Visibility logoVisibility = Visibility.Visible;

        [ObservableProperty]
        private Visibility setupFailVisibility = Visibility.Collapsed;

        [ObservableProperty]
        private Visibility progressVisibility = Visibility.Collapsed;

        [ObservableProperty]
        private int progressValue;

        [ObservableProperty]
        private Visibility primaryButtonVisibility = Visibility.Collapsed;

        [ObservableProperty]
        private string primaryButtonText = "OK";

        [ObservableProperty]
        private Visibility secondaryButtonVisibility = Visibility.Collapsed;

        [ObservableProperty]
        private string secondaryButtonText = string.Empty;

        [ObservableProperty]
        private string statusLabel1 = string.Empty;

        [ObservableProperty]
        private string statusLabel2 = string.Empty;

        [ObservableProperty]
        private string statusLabel3 = string.Empty;

        #endregion

        // Constructor
        public StatusViewModel(IUserPromptService userPromptService)
        {
            _userPromptService = userPromptService;
            _primaryAction = _ => HideStatusOverlay();
            _secondaryAction = _ => { };
        }

        #region Commands and wiring
        [RelayCommand]
        private void PrimaryButton(object? param)
        {
            _primaryAction(param);
        }

        [RelayCommand]
        private void SecondaryButton(object? param)
        {
            _secondaryAction(param);
        }
        public void SetPrimaryAction(Action<object?>? action) => _primaryAction = action ?? (_ => HideStatusOverlay());
        public void SetSecondaryAction(Action<object?>? action) => _secondaryAction = action ?? (_ => { });

        #endregion

        #region Confirmation Prompt handling

        public async Task<bool> WaitForUserConfirmationAsync(PromptButton button, string buttonText)
        {
            _userPromptService.CancelPendingPrompt(); // ensures only one active at a time
            var tcs = _userPromptService.CreatePrompt();

            switch (button)
            {
                case PromptButton.Primary:
                    PrimaryButtonVisibility = Visibility.Visible;
                    PrimaryButtonText = buttonText;
                    SetPrimaryAction(_ => _userPromptService.ConfirmPrompt());
                    break;
                case PromptButton.Secondary:
                    SecondaryButtonVisibility = Visibility.Visible;
                    SecondaryButtonText = buttonText;
                    SetSecondaryAction(_ => _userPromptService.ConfirmPrompt());
                    break;
            }

            return await tcs.Task;
        }

        #endregion

        #region Cancellation Token handling
        public CancellationToken PrepareCancelButton(PromptButton button)
        {
            var cancelMessage = "Cancelling…";
            var primaryButtonText = "   Cancel   ";

            var token = _userPromptService.GetNewCancellationToken();

            switch (button)
            {
                case PromptButton.Primary:
                    PrimaryButtonVisibility = Visibility.Visible;
                    PrimaryButtonText = primaryButtonText;

                    SetPrimaryAction(_ =>
                    {
                        StatusLabel2 = cancelMessage;
                        _userPromptService.CancelCurrentOperation();
                    });
                    break;

                case PromptButton.Secondary:
                    SecondaryButtonVisibility = Visibility.Visible;
                    SecondaryButtonText = primaryButtonText;

                    SetSecondaryAction(_ =>
                    {
                        StatusLabel2 = cancelMessage;
                        _userPromptService.CancelCurrentOperation();
                    });
                    break;
            }

            return token;
        }

        #endregion

        #region Status Overlay control methods
        public void ShowStatusOverlay(string message, bool showProgress = false)
        {
            ResetStatusOverlay();
            StatusOverlayVisibility = Visibility.Visible;
            StatusLabel1 = message;
            ProgressVisibility = showProgress ? Visibility.Visible : Visibility.Collapsed;
        }
        public void HideStatusOverlay()
        {
            StatusOverlayVisibility = Visibility.Collapsed;
            ResetStatusOverlay();
        }
        public void ResetStatusOverlay()
        {
            LogoVisibility = Visibility.Visible;
            ProgressVisibility = Visibility.Collapsed;

            PrimaryButtonVisibility = Visibility.Collapsed;
            SecondaryButtonVisibility = Visibility.Collapsed;

            SetupFailVisibility = Visibility.Collapsed;

            StatusLabel1 = string.Empty;
            StatusLabel2 = string.Empty;
            StatusLabel3 = string.Empty;

            ProgressValue = 0;
            PrimaryButtonText = "  OK  ";
            SecondaryButtonText = string.Empty;

            _primaryAction = _ => HideStatusOverlay();
            _secondaryAction = _ => { };
        }

        #endregion
    }
}
