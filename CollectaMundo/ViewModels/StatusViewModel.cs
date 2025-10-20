using CollectaMundo.Presentation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.Windows;

namespace CollectaMundo.ViewModels
{
    public partial class StatusViewModel : ObservableObject
    {
        private Action<object?> _primaryAction;
        private Action<object?> _secondaryAction;
        private TaskCompletionSource<bool>? _confirmationTcs;
        private CancellationTokenSource? _cts;

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
        public StatusViewModel()
        {
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
        public void CancelPendingPrompt()
        {
            if (_confirmationTcs is { Task.IsCompleted: false })
            {
                Debug.WriteLine("[Prompt] Cancelled pending prompt.");
                _confirmationTcs.SetResult(false); // false = not confirmed
            }
        }
        public void ConfirmPrompt()
        {
            if (_confirmationTcs is { Task.IsCompleted: false })
            {
                Debug.WriteLine("[Prompt] Confirmed prompt.");
                _confirmationTcs.SetResult(true); // true = confirmed
            }
        }
        public async Task<bool> WaitForUserConfirmationAsync(PromptButton button)
        {
            CancelPendingPrompt(); // ensures only one active at a time
            _confirmationTcs = new TaskCompletionSource<bool>();

            switch (button)
            {
                case PromptButton.Primary:
                    SetPrimaryAction(_ => ConfirmPrompt());
                    break;
                case PromptButton.Secondary:
                    SetSecondaryAction(_ => ConfirmPrompt());
                    break;
            }

            return await _confirmationTcs.Task;
        }

        #endregion



        #region Status Overlay control methods
        public void ShowStatusOverlay(string message, bool showProgress = false)
        {

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
