using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.Presentation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ListView;

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
        private Visibility overlayVisibility = Visibility.Collapsed;

        [ObservableProperty]
        private Visibility logoVisibility = Visibility.Visible;

        [ObservableProperty]
        private Visibility progressVisibility = Visibility.Collapsed;

        [ObservableProperty]
        private Visibility primaryButtonVisibility = Visibility.Collapsed;

        [ObservableProperty]
        private Visibility secondaryButtonVisibility = Visibility.Collapsed;

        [ObservableProperty]
        private Visibility setupFailVisibility = Visibility.Collapsed;

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
            PrimaryButtonVisibility = Visibility.Visible;
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
            OverlayVisibility = Visibility.Visible;
            ProgressVisibility = showProgress ? Visibility.Visible : Visibility.Collapsed;
        }

        public void Hide()
        {
            OverlayVisibility = Visibility.Collapsed;
            Reset();
        }

        public void Reset()
        {
            LogoVisibility = Visibility.Visible;
            ProgressVisibility = Visibility.Collapsed;

            PrimaryButtonVisibility = Visibility.Collapsed;
            SecondaryButtonVisibility = Visibility.Collapsed;

            SetupFailVisibility = Visibility.Collapsed;

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
