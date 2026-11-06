using CollectaMundo.Presentation.Parsing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using System.Windows.Input;

namespace CollectaMundo.ViewModels.ModifyCollection.BindinViewModels
{
    public sealed class SellPriceBindingViewModel : ObservableObject
    {
        private string _displayCurrency;
        private readonly Action<decimal?, string?> _setPrice;
        private string _inputText;
        private bool _isDirty;

        // Constructor takes an initial display price, the currency to display, and a callback to set the price
        public SellPriceBindingViewModel(decimal? initialDisplayPrice, string displayCurrency, Action<decimal?, string?> setPrice)
        {
            _displayCurrency = displayCurrency;
            _setPrice = setPrice;

            _inputText = initialDisplayPrice?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty;

            CommitCommand = new RelayCommand(() => TryCommit());
        }
        public ICommand CommitCommand { get; }
        public string Currency => _displayCurrency;
        public string InputText
        {
            get => _inputText;
            set
            {
                if (_inputText == value)
                {
                    return;
                }

                _inputText = value;
                _isDirty = true;

                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDirty));
                OnPropertyChanged(nameof(IsValid));
            }
        }
        public bool IsDirty => _isDirty;
        public bool IsValid => SellPriceInputParser.TryParse(InputText, out _);
        public bool TryCommit()
        {
            if (!_isDirty)
            {
                return true;
            }

            if (!SellPriceInputParser.TryParse(InputText, out var price))
            {
                return false;
            }

            _setPrice(price, price.HasValue ? _displayCurrency : null);

            _isDirty = false;
            OnPropertyChanged(nameof(IsDirty));

            return true;
        }
        public void RefreshDisplay(decimal? displayPrice, string displayCurrency)
        {
            if (_isDirty)
            {
                return;
            }

            var formattedPrice = displayPrice?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty;

            if (_inputText != formattedPrice)
            {
                _inputText = formattedPrice;
                OnPropertyChanged(nameof(InputText));
            }

            if (_displayCurrency != displayCurrency)
            {
                _displayCurrency = displayCurrency;
                OnPropertyChanged(nameof(Currency));
            }

            OnPropertyChanged(nameof(IsValid));
        }
    }
}
