using CollectaMundo.DomainLogic.CardLists.Models;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace CollectaMundo.ViewModels
{
    public class CardImageViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private CardSet? _selectedCard;
        public CardSet? SelectedCard
        {
            get => _selectedCard;
            set
            {
                if (_selectedCard != value)
                {
                    _selectedCard = value;
                    OnPropertyChanged();
                    OnCardSelected(_selectedCard); // Notify image view model
                }
            }
        }

        private static void OnCardSelected(CardSet? selectedCard)
        {
            if (selectedCard is null)
            {
                Debug.WriteLine("No card selected.");
                return;
            }

            if (!string.IsNullOrEmpty(selectedCard.Uuid))
            {
                Debug.WriteLine($"Selected card UUID: {selectedCard.Uuid}");
                // Future: await ShowImage(selectedCard.Uuid);
            }
            else if (!string.IsNullOrEmpty(selectedCard.Name))
            {
                Debug.WriteLine($"Selected card Name: {selectedCard.Name}");
                // Future: await ShowImage(null, selectedCard.Name);
            }
        }




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

    }
}

