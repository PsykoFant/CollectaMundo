using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CollectaMundo.ViewModels
{
    public class StatusViewModel : INotifyPropertyChanged
    {
        private bool _isVisible;
        private bool _isProgressVisible;
        private int _progressValue;
        private string _statusMessage = string.Empty;
        private string _firstTimeSetupText = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool IsVisible
        {
            get => _isVisible;
            set => SetField(ref _isVisible, value);
        }
        public bool IsProgressVisible
        {
            get => _isProgressVisible;
            set => SetField(ref _isProgressVisible, value);
        }
        public int ProgressValue
        {
            get => _progressValue;
            set => SetField(ref _progressValue, value);
        }


        public string StatusMessage
        {
            get => _statusMessage;
            set => SetField(ref _statusMessage, value);
        }

        public string FirstTimeSetupText
        {
            get => _firstTimeSetupText;
            set => SetField(ref _firstTimeSetupText, value);
        }

        public void Show(string message, bool showProgress = false, string? firstTimeNote = null)
        {
            StatusMessage = message;
            IsProgressVisible = showProgress;
            FirstTimeSetupText = firstTimeNote ?? string.Empty;
            IsVisible = true;
        }

        public void Hide()
        {
            IsVisible = false;
            IsProgressVisible = false;
            StatusMessage = string.Empty;
            FirstTimeSetupText = string.Empty;
        }

        private void SetField<T>(ref T field, T value, [CallerMemberName] string? propName = null)
        {
            if (!Equals(field, value))
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
            }
        }
    }
}
