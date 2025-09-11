using CollectaMundo.ApplicationServices.CardLists;
using CollectaMundo.DomainLogic.CardPrices;
using CollectaMundo.Utilities;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace CollectaMundo.ViewModels
{
    public class PricesViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // Retailer selection
        private readonly Func<string> _getRetailer;
        private readonly Action<string> _setRetailerAndPersist;
        private readonly ICardListService _cardListService;
        private readonly IAppRefresher _appRefresher;

        // Retailer options 
        public sealed record RetailerOption(string Key, string Display);
        public ObservableCollection<RetailerOption> Retailers { get; }

        private RetailerOption? _selectedRetailer;
        public RetailerOption? SelectedRetailer
        {
            get => _selectedRetailer;
            set { if (_selectedRetailer != value) { _selectedRetailer = value; OnPropertyChanged(); } }
        }

        // Price column headers (dynamic based on retailer)
        private string _priceHeader = "Price";
        public string PriceHeader
        {
            get => _priceHeader;
            private set { if (_priceHeader != value) { _priceHeader = value; OnPropertyChanged(); } }
        }

        private string _foilPriceHeader = "Foil Price";
        public string FoilPriceHeader
        {
            get => _foilPriceHeader;
            private set { if (_foilPriceHeader != value) { _foilPriceHeader = value; OnPropertyChanged(); } }
        }

        private string _etchedPriceHeader = "Etched Price";
        public string EtchedPriceHeader
        {
            get => _etchedPriceHeader;
            private set { if (_etchedPriceHeader != value) { _etchedPriceHeader = value; OnPropertyChanged(); } }
        }

        // Command
        public ICommand ChangeRetailerCommand { get; private set; } = null!;

        // Constructor
        public PricesViewModel(Func<string> getRetailer, Action<string> setRetailerAndPersist, ICardListService cardListService, IAppRefresher appRefresher)
        {
            // retailers
            _getRetailer = getRetailer;
            _setRetailerAndPersist = setRetailerAndPersist;
            _cardListService = cardListService;
            _appRefresher = appRefresher;

            // build retailer list (purely static definitions)
            Retailers = new ObservableCollection<RetailerOption>(CardPriceDefinitions.RetailersByFormat["paper"].Select(kv => new RetailerOption(kv.Key, kv.Value)));

            // pick initial from settings via delegate
            var savedKey = _getRetailer();
            SelectedRetailer = Retailers.FirstOrDefault(r => string.Equals(r.Key, savedKey, StringComparison.OrdinalIgnoreCase)) ?? Retailers.First();

            UpdatePriceHeaders();

            ChangeRetailerCommand = new RelayCommand<object>(async _ => await ChangeRetailerAsync());
        }

        // simple currency mapping
        private static string GetCurrencyForRetailer(string key) => string.Equals(key, "cardmarket", StringComparison.OrdinalIgnoreCase) ? "EUR" : "USD";

        // Command action
        private async Task ChangeRetailerAsync()
        {
            if (SelectedRetailer is null)
            {
                return;
            }
            _setRetailerAndPersist(SelectedRetailer.Key);
            await _cardListService.ReloadPriceLookupsAsync(SelectedRetailer.Key);
            _appRefresher.RefreshAllPrices();
            UpdatePriceHeaders();
        }
        private void UpdatePriceHeaders()
        {
            var key = SelectedRetailer?.Key ?? "cardmarket";
            var currency = GetCurrencyForRetailer(key);
            PriceHeader = $"Price ({currency})";
            FoilPriceHeader = $"Foil Price ({currency})";
            EtchedPriceHeader = $"Etched Price ({currency})";
        }

    }
}
