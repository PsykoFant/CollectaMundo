using CollectaMundo.ApplicationServices.KeyedDataProvider.Providers;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.CardLocations.Models;
using CollectaMundo.DomainLogic.KeyedDataProvider;
using CollectaMundo.Infrastructure.KeyedDataProvider;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.KeyedDataProvider
{

    public sealed class KeyedDataProviderService(IUnitOfWorkRunner uowRunner, IKeyedDataProviderRepo repo, Func<string> getRetailer) : IKeyedDataProviderService
    {
        private readonly IUnitOfWorkRunner _uowRunner = uowRunner;
        private readonly IKeyedDataProviderRepo _repo = repo;
        private readonly Func<string> _getRetailer = getRetailer;
        public async Task<KeyedDataProviderPackage> LoadKeyedDataAsync(SQLiteConnection conn, KeyedDataProviderOptions opts)
        {
            IReadOnlyDictionary<string, byte[]> manaIcons = new Dictionary<string, byte[]>();
            IReadOnlyDictionary<string, byte[]> setIcons = new Dictionary<string, byte[]>();
            IReadOnlyDictionary<string, SetDto> sets = new Dictionary<string, SetDto>();
            IReadOnlyDictionary<string, PriceDto> prices = new Dictionary<string, PriceDto>();
            IReadOnlyDictionary<int, CardLocation> locations = new Dictionary<int, CardLocation>();

            if (opts.HasFlag(KeyedDataProviderOptions.Icons))
            {
                manaIcons = await _repo.ReadManaCostImagesAsync(conn);
                setIcons = await _repo.ReadSetIconImagesAsync(conn);
            }

            if (opts.HasFlag(KeyedDataProviderOptions.Sets))
            {
                sets = await _repo.ReadSetsAsync(conn);
            }

            if (opts.HasFlag(KeyedDataProviderOptions.Prices))
            {
                var retailerKey = _getRetailer();
                prices = await _repo.ReadPricesAsync(conn, retailerKey);
            }

            if (opts.HasFlag(KeyedDataProviderOptions.Locations))
            {
                locations = await _repo.ReadLocationsAsync(conn);
            }

            return KeyedDataProviderBuilder.Build(manaIcons, setIcons, sets, prices, locations);
        }
        public async Task ResetPricesMetaProviderAsync(string retailerKey)
        {
            // Load the new map for the requested retailer
            var dict = await _uowRunner.ExecuteReadOnlyAsync(conn => _repo.ReadPricesAsync(conn, retailerKey));

            // Swap the static provider (all CardSet getters read through this)
            CardSet.PriceMetaProvider = new ValueProvider<string, PriceDto>(dict);
        }
        public async Task ResetCardLocationProviderAsync()
        {
            var dict = await _uowRunner.ExecuteReadOnlyAsync(conn => _repo.ReadLocationsAsync(conn));

            CardSet.CardLocationProvider = new ValueProvider<int, CardLocation>(dict);
        }
    }
}
