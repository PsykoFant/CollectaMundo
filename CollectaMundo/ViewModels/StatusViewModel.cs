using CollectaMundo.ApplicationServices.Utilities;
using CollectaMundo.Utilities;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace CollectaMundo.ViewModels
{
    public class StatusViewModel : INotifyPropertyChanged
    {
        private Visibility _statusOverlayVisibility;
        private Visibility _logoVisibility = Visibility.Visible;
        private Visibility _setupFailVisibility = Visibility.Collapsed;
        private Visibility _progressVisibility = Visibility.Collapsed;
        private int _progressValue;
        private string _statusLabel1 = string.Empty;
        private string _statusLabel2 = string.Empty;
        private string _statusLabel3 = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private Action<object?> _primaryAction;
        public ICommand PrimaryButtonCommand { get; }

        private void SetField<T>(ref T field, T value, [CallerMemberName] string? propName = null)
        {
            if (!Equals(field, value))
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
            }
        }
        public Visibility StatusOverlayVisibilitiy
        {
            get => _statusOverlayVisibility;
            set => SetField(ref _statusOverlayVisibility, value);
        }
        public Visibility LogoVisibility
        {
            get => _logoVisibility;
            set => SetField(ref _logoVisibility, value);
        }
        public Visibility SetupFailVisibility
        {
            get => _setupFailVisibility;
            set => SetField(ref _setupFailVisibility, value);
        }
        public Visibility ProgressVisibility
        {
            get => _progressVisibility;
            set => SetField(ref _progressVisibility, value);
        }
        public int ProgressValue
        {
            get => _progressValue;
            set => SetField(ref _progressValue, value);
        }

        private Visibility _primaryButtonVisibility = Visibility.Collapsed;
        public Visibility PrimaryButtonVisibility
        {
            get => _primaryButtonVisibility;
            set { if (_primaryButtonVisibility != value) { _primaryButtonVisibility = value; OnPropertyChanged(nameof(PrimaryButtonVisibility)); } }
        }

        private string _primaryButtonText = "OK";
        public string PrimaryButtonText
        {
            get => _primaryButtonText;
            set { if (_primaryButtonText != value) { _primaryButtonText = value; OnPropertyChanged(nameof(PrimaryButtonText)); } }
        }
        public string StatusLabel1
        {
            get => _statusLabel1;
            set => SetField(ref _statusLabel1, value);
        }
        public string StatusLabel2
        {
            get => _statusLabel2;
            set => SetField(ref _statusLabel2, value);
        }
        public string StatusLabel3
        {
            get => _statusLabel3;
            set => SetField(ref _statusLabel3, value);
        }
        public StatusViewModel()
        {
            _primaryAction = _ => HideStatusOverlay();
            PrimaryButtonCommand = new RelayCommand<object>(o => _primaryAction(o));
        }
        public void SetPrimaryAction(Action<object?>? action) => _primaryAction = action ?? (_ => HideStatusOverlay());

        public void ShowStatusOverlay(string message, bool showProgress = false)
        {

            StatusOverlayVisibilitiy = Visibility.Visible;
            StatusLabel1 = message;
            ProgressVisibility = showProgress ? Visibility.Visible : Visibility.Collapsed;
        }
        public void HideStatusOverlay()
        {
            StatusOverlayVisibilitiy = Visibility.Collapsed;

            ResetStatusOverlay();
        }

        public void ResetStatusOverlay()
        {
            LogoVisibility = Visibility.Visible;
            ProgressVisibility = Visibility.Collapsed;
            PrimaryButtonVisibility = Visibility.Collapsed;
            SetupFailVisibility = Visibility.Collapsed;
            PrimaryButtonVisibility = Visibility.Collapsed;

            StatusLabel1 = string.Empty;
            StatusLabel2 = string.Empty;
            StatusLabel3 = string.Empty;

            ProgressValue = 0;
            PrimaryButtonText = "  OK  ";
        }
        public void ShowBackupResult(OperationResult result)
        {
            PrimaryButtonVisibility = Visibility.Visible;

            switch (result.Code)
            {
                case OperationResultCode.Success:
                    PrimaryButtonText = "Awesome!";
                    ShowStatusOverlay(result.Message);
                    break;

                case OperationResultCode.Error:
                    PrimaryButtonText = "Ok :-/";
                    ShowStatusOverlay($"Error: {result.Message}");
                    break;

                case OperationResultCode.Empty:
                    PrimaryButtonText = "Oh ... I guess that makes sense...";
                    ShowStatusOverlay(result.Message);
                    break;
            }
        }

    }
}
