using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardPrices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace CollectaMundo.ViewModels
{
    public partial class PricesViewModel : ObservableObject
    {
        // Settings
        private readonly IAppSettings _appSettings;

        // Retailer selection
        private readonly IAppRefresher _appRefresher;

        // Retailer options 
        public sealed record RetailerOption(string Key, string Display);
        public ObservableCollection<RetailerOption> Retailers { get; }

        [ObservableProperty]
        private RetailerOption? selectedRetailer;

        // Last price update date
        public string LatestPriceUpdateDate => $"Card prices updated: {_appSettings.PriceInfo.PricesUpdatedDate}";

        // Price column headers (dynamic based on retailer)
        [ObservableProperty]
        private string priceHeader = "Price";

        [ObservableProperty]
        private string foilPriceHeader = "Foil Price";

        [ObservableProperty]
        private string etchedPriceHeader = "Etched Price";

        public void RefreshLatestPriceDate()
        {
            OnPropertyChanged(nameof(LatestPriceUpdateDate));
        }

        // Constructor        
        public PricesViewModel(IAppSettings settings, IAppRefresher appRefresher)
        {
            // settings
            _appSettings = settings;

            // retailers
            _appRefresher = appRefresher;

            // build retailer list (purely static definitions)
            Retailers = new ObservableCollection<RetailerOption>(CardPriceDefinitions.RetailersByFormat["paper"].Select(kv => new RetailerOption(kv.Key, kv.Value)));

            // pick initial from settings
            var savedKey = _appSettings.PriceInfo.Retailer;
            SelectedRetailer = Retailers.FirstOrDefault(r => string.Equals(r.Key, savedKey, StringComparison.OrdinalIgnoreCase)) ?? Retailers.First();

            UpdatePriceHeaders();
        }

        // simple currency mapping
        private static string GetCurrencyForRetailer(string key) => string.Equals(key, "cardmarket", StringComparison.OrdinalIgnoreCase) ? "EUR" : "USD";

        // Command and command actions
        [RelayCommand]
        private void ChangeRetailer()
        {
            if (SelectedRetailer is null)
                return;

            _appSettings.PersistPriceInfo(updatedDate: null, retailer: SelectedRetailer.Key);
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
