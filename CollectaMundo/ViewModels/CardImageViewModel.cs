using CollectaMundo.Utilities;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace CollectaMundo.ViewModels
{
    public class CardImageViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));


        private string? _imageSourceUrl = string.Empty;
        public string? ImageSourceUrl
        {
            get => _imageSourceUrl;
            set
            {
                if (_imageSourceUrl != value)
                {
                    _imageSourceUrl = value;
                    OnPropertyChanged(nameof(ImageSourceUrl));
                }
            }
        }
        private string? _imageSourceUrl2nd = string.Empty;
        public string? ImageSourceUrl2nd
        {
            get => _imageSourceUrl2nd;
            set
            {
                if (_imageSourceUrl2nd != value)
                {
                    _imageSourceUrl2nd = value;
                    OnPropertyChanged(nameof(ImageSourceUrl2nd));
                }
            }
        }


        // Commands
        public ICommand ShowSelectedCardCommand { get; private set; } = null!;

        // Visibility properties

        // Constructor
        public CardImageViewModel()

        {
            ShowSelectedCardCommand = new RelayCommand<object>(async _ => await ShowImage());
        }
        private async Task ShowImage()
        {
            Debug.WriteLine("ShowImage command executed (TBI).");
        }
    }
}

